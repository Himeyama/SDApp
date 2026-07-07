using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Sodalite.Services;

/// <summary>CPU・メモリ・GPU・VRAM の使用状況スナップショット。GPU 系は取得できない環境では null。</summary>
readonly record struct SystemStats(
    double CpuUsagePercent,
    double MemoryUsedGiB,
    double MemoryTotalGiB,
    double? GpuUsagePercent,
    double? VramUsedGiB,
    double? VramTotalGiB);

/// <summary>
/// CPU/メモリは Win32 API から直接取得する。GPU/VRAM は NVIDIA 専用の nvidia-smi を都度起動して
/// 取得し、コマンドが存在しない・失敗する環境では該当値を null にして呼び出し側で非表示にする。
/// </summary>
sealed class SystemMonitorService
{
    (ulong Idle, ulong Kernel, ulong User)? _lastCpuTimes;
    bool _nvidiaSmiUnavailable;

    public async Task<SystemStats> GetStatsAsync(CancellationToken ct = default)
    {
        double cpuUsagePercent = GetCpuUsagePercent();
        (double usedGiB, double totalGiB) = GetMemoryUsage();
        (double? gpuPercent, double? vramUsedGiB, double? vramTotalGiB) =
            await GetGpuStatsAsync(ct).ConfigureAwait(false);

        return new SystemStats(cpuUsagePercent, usedGiB, totalGiB, gpuPercent, vramUsedGiB, vramTotalGiB);
    }

    double GetCpuUsagePercent()
    {
        if (!GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
        {
            return 0;
        }

        ulong idle = ToUInt64(idleTime);
        ulong kernel = ToUInt64(kernelTime);
        ulong user = ToUInt64(userTime);

        if (_lastCpuTimes is not { } last)
        {
            _lastCpuTimes = (idle, kernel, user);
            return 0;
        }

        _lastCpuTimes = (idle, kernel, user);

        ulong idleDiff = idle - last.Idle;
        ulong totalDiff = (kernel - last.Kernel) + (user - last.User);

        return totalDiff == 0 ? 0 : (1.0 - (double)idleDiff / totalDiff) * 100.0;
    }

    static (double UsedGiB, double TotalGiB) GetMemoryUsage()
    {
        MEMORYSTATUSEX status = new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        const double BytesPerGiB = 1024.0 * 1024.0 * 1024.0;
        double totalGiB = status.ullTotalPhys / BytesPerGiB;
        double usedGiB = (status.ullTotalPhys - status.ullAvailPhys) / BytesPerGiB;
        return (usedGiB, totalGiB);
    }

    async Task<(double? GpuPercent, double? VramUsedGiB, double? VramTotalGiB)> GetGpuStatsAsync(CancellationToken ct)
    {
        if (_nvidiaSmiUnavailable)
        {
            return (null, null, null);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "nvidia-smi",
            Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start nvidia-smi.");

            string output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _nvidiaSmiUnavailable = true;
                return (null, null, null);
            }

            // 複数 GPU 構成では先頭行(1台目)のみを表示対象とする。
            string firstLine = output.AsSpan().TrimStart().ToString().Split('\n')[0];
            string[] parts = firstLine.Split(',');
            if (parts.Length != 3)
            {
                _nvidiaSmiUnavailable = true;
                return (null, null, null);
            }

            double gpuPercent = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double vramUsedMiB = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double vramTotalMiB = double.Parse(parts[2], CultureInfo.InvariantCulture);

            const double MiBPerGiB = 1024.0;
            return (gpuPercent, vramUsedMiB / MiBPerGiB, vramTotalMiB / MiBPerGiB);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // nvidia-smi が存在しない(非 NVIDIA 環境)場合はここに来る。以後は再試行しない。
            _nvidiaSmiUnavailable = true;
            return (null, null, null);
        }
    }

    static ulong ToUInt64(FILETIME fileTime) =>
        ((ulong)(uint)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
