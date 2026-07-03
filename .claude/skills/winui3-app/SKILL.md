---
name: winui3-app
description: dotnet CLI のみで WinUI3 デスクトップアプリを作成・起動する手順。
---

# WinUI3 アプリ作成スキル

## 概要
WinUI3 アプリを作成するスキル。
Windows App SDK の Bootstrapper API を使うことで、MSIX パッケージ不要で動作する。

## 前提条件

- .NET 9 SDK（`dotnet --version` で確認）
- Windows 10 22H2 以降

## ステップ 1: プロジェクト生成

```bash
dotnet new winui -n MyApp
cd MyApp
```

## ステップ 2: csproj を修正

生成された csproj から不要な設定を削除し、以下の最小構成にする。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>MyApp</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="Assets\*.png" />
    <Content Include="Assets\*.ico" />
  </ItemGroup>

  <ItemGroup>
    <Manifest Include="$(ApplicationManifest)" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.1839" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.1.3" />
  </ItemGroup>
</Project>
```

**重要なポイント:**
- `RuntimeIdentifier=win-x64` を明示する（AnyCPU だとビルドエラー）
- `WindowsAppSDKSelfContained=true` + `SelfContained=true` が核心。これにより Windows App SDK ランタイムが exe に同梱され、COM 登録不要・Program.cs 不要で動作する

## ステップ 3: App.xaml.cs を簡潔にする

テンプレートが生成する不要な using を削除する。Program.cs は不要。

```csharp
// App.xaml.cs
using Microsoft.UI.Xaml;

namespace MyApp;

public partial class App : Application
{
    Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
```

## ステップ 4: Grid の行列定義（ショートハンド）

`<Grid.RowDefinitions>` ブロックの代わりに、属性形式で簡潔に書ける。

```xml
<!-- 従来の書き方 -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    ...
</Grid>

<!-- ショートハンド -->
<Grid RowDefinitions="Auto,*">
    ...
</Grid>
```

列定義も同様。

```xml
<!-- ショートハンド -->
<Grid ColumnDefinitions="170,Auto,*,Auto">
    ...
</Grid>
```

値には `Auto`、`*`、`2*`、固定値（`170` など）が使える。

## ステップ 5: カスタムタイトルバー（任意）

`ExtendsContentIntoTitleBar = true` + `SetTitleBar()` でカスタムタイトルバーを実装する。
Mica 背景がタイトルバーまで拡張されてモダンな外観になる。

**MainWindow.xaml:**

```xml
<Window ...>
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- タイトルバー領域: SetTitleBar() に渡す要素 -->
        <Grid x:Name="TitleBarGrid" Grid.Row="0" Height="48" Background="Transparent">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center"
                        Margin="16,0,0,0" Spacing="8">
                <FontIcon Glyph="&#xE8B7;" FontSize="16" VerticalAlignment="Center" />
                <TextBlock Text="My App" Style="{ThemeResource CaptionTextBlockStyle}"
                           VerticalAlignment="Center" />
            </StackPanel>
        </Grid>

        <!-- コンテンツ -->
        <StackPanel Grid.Row="1" Padding="20" Spacing="15">
            <!-- UI をここに書く -->
        </StackPanel>
    </Grid>
</Window>
```

**MainWindow.xaml.cs:**

```csharp
public MainWindow()
{
    InitializeComponent();

    AppWindow.SetIcon("Assets/AppIcon.ico");
    ExtendsContentIntoTitleBar = true;
    SetTitleBar(TitleBarGrid);
}
```

## ステップ 5: ビルドと起動

```bash
dotnet build -c Debug
./bin/Debug/net9.0-windows10.0.26100.0/win-x64/MyApp.exe
```

## トラブルシューティング

| エラー | 原因 | 対処 |
|---|---|---|
| `REGDB_E_CLASSNOTREG (0x80040154)` | `Bootstrap.Initialize` を呼んでいない | Program.cs を上記の通り作成する |
| `CS0017: 複数のエントリポイント` | `DISABLE_XAML_GENERATED_MAIN` が未定義 | csproj に `<DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>` を追加 |
| `AnyCPU` ビルドエラー | RuntimeIdentifier 未指定 | csproj に `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` を追加 |
| `SetTitleBar` が `FrameworkElement` を要求 | Window 直下で `x:Bind` を使っている | Window 直下の要素に `x:Bind` を使わない。DataContext はコードビハインドで設定する |

## MVVM パターンのバインディング注意点

WinUI3 の `x:Bind` は Window クラスに対しては `SetConverterLookupRoot` で `FrameworkElement` を要求するためコンパイルエラーになる。
Window 直下では `x:Bind` を避け、コードビハインドで `ItemsSource` などを直接設定する。

```csharp
// NG: MainWindow.xaml で <ListBox ItemsSource="{x:Bind ViewModel.Items}" />
// OK: コードビハインドで設定
MyList.ItemsSource = ViewModel.Items;
```

DataTemplate 内は `x:Bind` ではなく `{Binding}` を使う（DataType 指定は可）。

## UI スレッドのブロックを防ぐ（async/await の注意点）

WinUI3 で `async/await` を使っても、UI がフリーズする場合がある。

### 原因

`await` はデフォルトで **現在のスレッド（UI スレッド）に処理を戻す**。  
サービス層のメソッド内で `ConfigureAwait(false)` を使わないと、HTTP 応答後の JSON パース・ファイル検索などの同期処理がすべて UI スレッドで実行されフリーズする。

### 対策 1: サービス層では `ConfigureAwait(false)` を使う

UI を持たないサービスクラスでは、すべての `await` に `.ConfigureAwait(false)` を付ける。  
これにより継続処理がスレッドプールで実行され、UI スレッドを解放する。

```csharp
// NG: 継続が UI スレッドに戻り、後続の同期処理がフリーズを引き起こす
HttpResponseMessage response = await _http.PostAsync(url, content, ct);

// OK: スレッドプールで継続される
HttpResponseMessage response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
```

### 対策 2: CPU バウンドな処理は `Task.Run` に移す

ファイル I/O や正規表現など重い同期処理は `Task.Run` でスレッドプールに渡す。

```csharp
var result = await Task.Run(() => GrepService.Execute(pattern, path), ct).ConfigureAwait(false);
```

### 対策 3: UI 更新は `DispatcherQueue.TryEnqueue` を使う

バックグラウンドスレッドから UI コントロールを直接操作すると例外になる。  
`IProgress<T>` のコールバック内で `DispatcherQueue.TryEnqueue` を使って UI スレッドにマーシャルする。

```csharp
// Window や Page (DependencyObject) では this.DispatcherQueue プロパティを使う
// GetForCurrentThread() は UI スレッドから呼ぶ必要があるが、
// this.DispatcherQueue はどこからでも安全に参照できる
DispatcherQueue queue = this.DispatcherQueue;

Progress<string> progress = new(msg =>
    queue.TryEnqueue(() => StatusText.Text = msg));

// バックグラウンド処理から progress.Report() を呼ぶと自動的に UI スレッドで反映される
await _service.RunAsync(progress, ct);
```

### ルールまとめ

| 場所 | ルール |
|---|---|
| サービス・ライブラリ層 | `await` に必ず `ConfigureAwait(false)` |
| CPU バウンド処理 | `await Task.Run(() => ...)` でスレッドプールへ |
| UI 更新（バックグラウンドから） | `DispatcherQueue.TryEnqueue` 経由 |
| UI 更新（コードビハインド内） | `ConfigureAwait(false)` を付けない（UI スレッドに戻る） |

## C# コーディングスタイル

### 型は var を使わず明示的に書く

```csharp
// NG
var models = new List<string>();
var response = await _http.GetStringAsync(url, ct);

// OK
List<string> models = new();
string response = await _http.GetStringAsync(url, ct);
```

### 宣言は `型 変数 = new();` を原則とする

```csharp
// NG
var picker = new FolderPicker();
var dialog = new ContentDialog { Title = "Error" };

// OK
FolderPicker picker = new();
ContentDialog dialog = new() { Title = "Error" };
```

### using はファイル先頭の宣言形式のみ使用し、不要なものは削除する

```csharp
// NG: ブロック using（ネスト）
using (var stream = File.OpenRead(path))
{
    // ...
}

// OK: 宣言形式
using FileStream stream = File.OpenRead(path);
```

### リスト・配列はコレクション式を使う

```csharp
// NG
var list = new List<string>();
return new JsonArray { ... };

// OK
List<string> list = [];
return [item1, item2];
```

### `private` は省略する（デフォルトが private のため）

```csharp
// NG
private readonly OllamaService _ollama = new();
private CancellationTokenSource? _cts;
private void OnClick(object sender, RoutedEventArgs e) { ... }

// OK
readonly OllamaService _ollama = new();
CancellationTokenSource? _cts;
void OnClick(object sender, RoutedEventArgs e) { ... }
```

### 単純な関数・ゲッター・セッターはラムダ式で簡潔に書く

```csharp
// NG
void RefreshButton_Click(object sender, RoutedEventArgs e)
{
    _ = LoadAsync();
}

// OK
void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
```

### 型の比較はパターンマッチングを使う

```csharp
// NG
var model = ComboBox.SelectedItem as string;
if (model == null) return;

// OK
if (ComboBox.SelectedItem is not string model) return;
```

### null チェックは `?` を使う

```csharp
// NG
if (_cts != null) _cts.Cancel();

// OK
_cts?.Cancel();
```

### switch は switch 式で簡潔に書く

```csharp
// NG
string result;
switch (outputMode)
{
    case "count": result = "件数"; break;
    case "content": result = "内容"; break;
    default: result = "ファイル"; break;
}

// OK
string result = outputMode switch
{
    "count" => "件数",
    "content" => "内容",
    _ => "ファイル"
};
```

### Nullable を有効にする（csproj）

```xml
<Nullable>enable</Nullable>
```