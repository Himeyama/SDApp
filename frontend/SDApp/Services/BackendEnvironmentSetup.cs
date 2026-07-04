using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SDApp.Services;

/// <summary>
/// バックエンドの Python 仮想環境(.venv)を初回起動時に用意する。
/// <c>uv sync</c> を実行して依存をインストールし、成功を <c>%LOCALAPPDATA%\SDApp\.venv-ready</c>
/// マーカーに記録する。マーカーには uv.lock のハッシュを書き込み、依存が変わったら再セットアップする。
/// セットアップが失敗した場合はマーカーを書かないため、次回起動時に自動的に再試行される。
/// </summary>
/// <remarks>
/// uv 本体(uv.exe)はユーザーがインストール済みである前提。Python 3.13 は uv sync が
/// (uv python 管理経由で)必要に応じて自動取得する。
/// </remarks>
sealed class UvNotFoundException(string message) : Exception(message);

sealed class BackendEnvironmentSetup(string backendProjectPath)
{
    static readonly string MarkerFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SDApp",
        ".venv-ready");

    readonly string _backendProjectPath = backendProjectPath;

    /// <summary>
    /// 必要であれば <c>uv sync</c> を実行する。既にセットアップ済み(マーカーが現在の uv.lock と一致)
    /// なら何もしない。uv が見つからない場合は <see cref="UvNotFoundException"/> を投げる(マーカーは書かない)。
    /// </summary>
    /// <param name="onSettingUp">実際に uv sync を開始する直前に一度だけ呼ばれる(スキップ時は呼ばれない)。</param>
    public async Task EnsureAsync(Action? onSettingUp = null, CancellationToken ct = default)
    {
        string currentLockHash = ComputeLockHash();

        if (IsUpToDate(currentLockHash))
        {
            return;
        }

        onSettingUp?.Invoke();

        await RunUvSyncAsync(ct).ConfigureAwait(false);

        // uv sync が終了コード 0 で完了した場合のみここに到達する。マーカーを書いて完了を記録する。
        WriteMarker(currentLockHash);
    }

    bool IsUpToDate(string currentLockHash)
    {
        try
        {
            return File.ReadAllText(MarkerFilePath).Trim() == currentLockHash;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    string ComputeLockHash()
    {
        string lockPath = Path.Combine(_backendProjectPath, "uv.lock");
        try
        {
            byte[] bytes = File.ReadAllBytes(lockPath);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // uv.lock が読めない場合はハッシュを固定せず、毎回セットアップを試みる方に倒す。
            return string.Empty;
        }
    }

    async Task RunUvSyncAsync(CancellationToken ct)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "uv",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // 起動時の環境と揃える。hf_xet 経由のダウンロードはこの環境でハングすることがあるため無効化する。
        startInfo.Environment["HF_HUB_DISABLE_XET"] = "1";

        startInfo.ArgumentList.Add("sync");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(_backendProjectPath);

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start uv.");
        }
        catch (Win32Exception ex)
        {
            // uv.exe が PATH に無い場合。ユーザーに uv のインストールを促す。
            throw new UvNotFoundException("uv command was not found on PATH.") { Data = { ["inner"] = ex } };
        }

        using (process)
        {
            // uv/pip は大量に stdout/stderr へ出力する。読み出さないと OS のパイプバッファが埋まって
            // uv 側の書き込みがブロックし、uv sync が永久に終わらなくなる(BackendProcessManager と同じ理由)。
            process.OutputDataReceived += DiscardProcessOutput;
            process.ErrorDataReceived += DiscardProcessOutput;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // アプリ終了などでキャンセルされたら、途中の uv/子プロセスを確実に終了させる。
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"uv sync failed with exit code {process.ExitCode}.");
            }
        }
    }

    static void WriteMarker(string lockHash)
    {
        string? directory = Path.GetDirectoryName(MarkerFilePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(MarkerFilePath, lockHash);
    }

    // リダイレクトしたパイプを空にし続けるためだけのハンドラ。ログ内容は破棄する。
    static void DiscardProcessOutput(object sender, DataReceivedEventArgs e)
    {
    }
}
