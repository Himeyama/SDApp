using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Sodalite.Views;

/// <summary>画像をウィンドウいっぱいに表示するだけのプレビューダイアログ。</summary>
sealed partial class GalleryImagePreviewDialog : ContentDialog
{
    const double SizeMargin = 80;

    public GalleryImagePreviewDialog(BitmapImage image)
    {
        InitializeComponent();
        PreviewImage.Source = image;
    }

    // ContentDialog はコンテンツの必要サイズで測ってレイアウトするため、Loaded の時点で
    // XamlRoot のサイズを基に最大幅・高さを明示しないとウィンドウいっぱいに広がらない。
    void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not XamlRoot xamlRoot)
        {
            return;
        }

        RootGrid.MaxWidth = Math.Max(200, xamlRoot.Size.Width - SizeMargin);
        RootGrid.MaxHeight = Math.Max(200, xamlRoot.Size.Height - SizeMargin);
    }
}
