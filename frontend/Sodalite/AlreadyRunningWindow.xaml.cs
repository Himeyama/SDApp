using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Sodalite;

public sealed partial class AlreadyRunningWindow : Window
{
    static readonly ResourceLoader ResourceLoader = new();

    public AlreadyRunningWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon("Assets/AppIcon.ico");
        RootGrid.Loaded += RootGrid_Loaded;
    }

    async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = ResourceLoader.GetString("App_AlreadyRunningTitle"),
            Content = ResourceLoader.GetString("App_AlreadyRunningMessage"),
            CloseButtonText = ResourceLoader.GetString("App_AlreadyRunningCloseButton"),
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();

        Close();
    }
}
