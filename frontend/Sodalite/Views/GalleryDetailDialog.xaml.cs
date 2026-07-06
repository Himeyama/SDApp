using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Sodalite.Models;

namespace Sodalite.Views;

sealed partial class GalleryDetailDialog : ContentDialog
{
    readonly GalleryImageInfo _image;
    readonly BitmapImage _thumbnail;

    /// <summary>「パラメータを再利用」が押されたときに発火する。</summary>
    public event EventHandler? ReuseRequested;

    /// <summary>削除が確定したときに発火する。ダイアログ自体は呼び出し元が閉じる。</summary>
    public event EventHandler? DeleteConfirmed;

    public GalleryDetailDialog(GalleryImageInfo image, BitmapImage thumbnail)
    {
        _image = image;
        _thumbnail = thumbnail;
        InitializeComponent();
    }

    // ContentDialog はコンストラクタ実行時点でまだ visual tree が構築されておらず、
    // ここで子要素を操作すると例外がダイアログ表示側で握り潰され「無反応」に見える。
    // そのため初期化は Loaded まで遅らせる。
    void GalleryDetailDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PreviewImage.Source = _thumbnail;

        GalleryParameters? parameters = _image.Parameters;
        bool hasParameters = parameters is not null;

        NoMetadataTextBlock.Visibility = hasParameters ? Visibility.Collapsed : Visibility.Visible;
        PromptPanel.Visibility = hasParameters ? Visibility.Visible : Visibility.Collapsed;
        NegativePromptPanel.Visibility = hasParameters ? Visibility.Visible : Visibility.Collapsed;
        SettingsTextBlock.Visibility = hasParameters ? Visibility.Visible : Visibility.Collapsed;
        ReuseButton.IsEnabled = hasParameters;

        if (parameters is not null)
        {
            PromptTextBlock.Text = parameters.Prompt;
            NegativePromptTextBlock.Text = parameters.NegativePrompt;
            SettingsTextBlock.Text = BuildSettingsSummary(parameters);
        }
    }

    static string BuildSettingsSummary(GalleryParameters parameters)
    {
        List<string> parts = [];
        if (parameters.Steps is int steps)
        {
            parts.Add($"Steps: {steps}");
        }

        if (parameters.Sampler is string sampler)
        {
            parts.Add($"Sampler: {sampler}");
        }

        if (parameters.CfgScale is double cfgScale)
        {
            parts.Add($"CFG scale: {cfgScale:F1}");
        }

        if (parameters.Seed is long seed)
        {
            parts.Add($"Seed: {seed}");
        }

        if (parameters.Width is int width && parameters.Height is int height)
        {
            parts.Add($"Size: {width}x{height}");
        }

        if (parameters.Loras.Count > 0)
        {
            string loraText = string.Join(", ", parameters.Loras.Select(lora => $"{lora.ModelId}:{lora.Weight}"));
            parts.Add($"LoRAs: {loraText}");
        }

        return string.Join(", ", parts);
    }

    void ReuseButton_Click(object sender, RoutedEventArgs e) => ReuseRequested?.Invoke(this, EventArgs.Empty);

    async void PreviewImageButton_Click(object sender, RoutedEventArgs e)
    {
        // ContentDialog は同じ XamlRoot 上に同時に1つしか表示できず、表示中のまま
        // 別の ContentDialog を ShowAsync すると例外でアプリごと落ちる。
        // そのため一旦自分を閉じてからプレビューを表示し、閉じられたら自分を開き直す。
        Hide();

        GalleryImagePreviewDialog previewDialog = new(_thumbnail) { XamlRoot = XamlRoot };
        await previewDialog.ShowAsync();

        await ShowAsync();
    }

    void OpenImageButton_Click(object sender, RoutedEventArgs e)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = _image.ImagePath,
            UseShellExecute = true,
        });
    }

    void OpenContainingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { $"/select,{_image.ImagePath}" },
            UseShellExecute = false,
        });
    }

    void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteButton.Visibility = Visibility.Collapsed;
        DeleteConfirmPanel.Visibility = Visibility.Visible;
    }

    void CancelDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteConfirmPanel.Visibility = Visibility.Collapsed;
        DeleteButton.Visibility = Visibility.Visible;
    }

    void ConfirmDeleteButton_Click(object sender, RoutedEventArgs e) => DeleteConfirmed?.Invoke(this, EventArgs.Empty);
}
