namespace SDApp.Services;

/// <summary>
/// Python バックエンド (backend/) の配置場所を解決する。
/// 配布(インストール)構成と開発(リポジトリ内 Debug ビルド)構成の両方に対応する。
/// </summary>
static class BackendLocator
{
    /// <summary>
    /// backend プロジェクトディレクトリ(pyproject.toml を含む)の絶対パス。
    /// インストール構成 (SDApp.exe と同じルート直下に app\ と backend\ が並ぶ) を優先し、
    /// 見つからなければ開発時のリポジトリ相対パスにフォールバックする。
    /// </summary>
    public static string BackendProjectPath
    {
        get
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
            string devLayout = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "backend"));
            return devLayout;
        }
    }

    static bool IsBackendProject(string directory) =>
        File.Exists(Path.Combine(directory, "pyproject.toml"));
}
