namespace Sodalite.Services;

/// <summary>
/// Python バックエンド (backend/) の配置場所を解決する。
/// 配布(インストール)構成と開発(リポジトリ内 Debug ビルド)構成の両方に対応する。
/// </summary>
static class BackendLocator
{
    /// <summary>
    /// backend プロジェクトディレクトリ(pyproject.toml を含む)の絶対パス。
    /// インストール構成 (Sodalite.exe と同じルート直下に app\ と backend\ が並ぶ) を優先し、
    /// 見つからなければ開発時のリポジトリ相対パスにフォールバックする。
    /// </summary>
    public static string BackendProjectPath { get; } = ResolveBackendProjectPath();

    /// <summary>
    /// backend の隣 (<c>app\</c> の親直下) に backend\ が並ぶ配布(インストール)構成かどうか。
    /// この場合インストール先 (<c>C:\Program Files\Sodalite</c> 等) は書き込みに管理者権限が要る。
    /// </summary>
    static bool IsInstalledLayout { get; } = IsBackendProject(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "backend")));

    /// <summary>
    /// uv に <c>UV_PROJECT_ENVIRONMENT</c> で渡す仮想環境(.venv)の絶対パス。設定不要なら <c>null</c>。
    /// インストール構成では書き込み権限のあるユーザー領域 <c>%LOCALAPPDATA%\Sodalite\.venv</c> に置く。
    /// 開発構成では <c>null</c> を返し、uv 既定のプロジェクト直下 (<c>backend\.venv</c>) をそのまま使う。
    /// uv sync / uv run の両方に同じ値を設定すること。
    /// </summary>
    public static string? VenvPath { get; } = IsInstalledLayout
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sodalite",
            ".venv")
        : null;

    static string ResolveBackendProjectPath()
    {
        // インストール構成: <install root>\app\ に exe があり、<install root>\backend\ にソースがある。
        // AppContext.BaseDirectory は app\ を指すため、その親直下の backend\ を探す。
        string installLayout = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "backend"));
        if (IsBackendProject(installLayout))
        {
            return installLayout;
        }

        // 開発構成: bin\Debug\net9.0-windows...\win-x64\ から .. 6 回でリポジトリルート、その直下の backend\。
        // pyproject.toml の有無で判定するため、ビルド出力階層の深さが変わっても壊れにくい。
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "backend"));
    }

    static bool IsBackendProject(string directory) =>
        File.Exists(Path.Combine(directory, "pyproject.toml"));
}
