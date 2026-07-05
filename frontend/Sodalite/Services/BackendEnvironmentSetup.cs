using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Sodalite.Services;

/// <summary>
/// バックエンドの Python 仮想環境(.venv)を初回起動時に用意する。
/// <c>uv sync</c> を実行して依存をインストールし、成功を <c>%LOCALAPPDATA%\Sodalite\.venv-ready</c>
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
        "Sodalite",
        ".venv-ready");

    readonly string _backendProjectPath = backendProjectPath;

    /// <summary>
    /// 必要であれば <c>uv sync</c> を実行する。既にセットアップ済み(マーカーが現在の uv.lock と一致)
    /// なら何もしない。uv が見つからない場合は <see cref="UvNotFoundException"/> を投げる(マーカーは書かない)。
    /// </summary>
    /// <param name="onProgress">
    /// 実際に uv sync を開始する直前に空文字で一度、その後 uv sync のログ行が出るたびに呼ばれる
    /// (スキップ時は一度も呼ばれない)。UI に進捗を実況表示するために使う。
    /// </param>
    public async Task EnsureAsync(IProgress<string>? onProgress = null, CancellationToken ct = default)
    {
        string currentLockHash = ComputeLockHash();

        if (IsUpToDate(currentLockHash))
        {
            return;
        }

        // セットアップ開始の合図(まだログ行が無いので空文字)。UI 側でオーバーレイを表示する契機になる。
        onProgress?.Report(string.Empty);

        await RunUvSyncAsync(onProgress, ct).ConfigureAwait(false);

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

    async Task RunUvSyncAsync(IProgress<string>? onProgress, CancellationToken ct)
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

        // torch (数 GiB) のダウンロードは既定の 30 秒タイムアウトだと回線次第で切れることがあるため延長する。
        startInfo.Environment["UV_HTTP_TIMEOUT"] = "3600";

        // インストール構成では仮想環境を書き込み権限のあるユーザー領域に作る(インストール先の
        // Program Files は書き込み不可)。開発構成では null なので設定せず backend\.venv を使う。
        // uv run 側 (BackendProcessManager) と必ず同じ場所を指すこと。
        if (BackendLocator.VenvPath is string venvPath)
        {
            startInfo.Environment["UV_PROJECT_ENVIRONMENT"] = venvPath;

            // uv が .venv を作れるよう親ディレクトリ (%LOCALAPPDATA%\Sodalite) を先に用意しておく。
            string? venvParent = Path.GetDirectoryName(venvPath);
            if (venvParent is not null)
            {
                Directory.CreateDirectory(venvParent);
            }
        }

        startInfo.ArgumentList.Add("sync");
        startInfo.ArgumentList.Add("--no-dev");
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
            // 読み出した行は onProgress へ転送して UI に実況表示する(uv の進捗はほとんど stderr に出る)。
            void ForwardLine(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    onProgress?.Report(e.Data);
                }
            }

            process.OutputDataReceived += ForwardLine;
            process.ErrorDataReceived += ForwardLine;
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
}
