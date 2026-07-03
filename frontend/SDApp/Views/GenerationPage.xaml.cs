using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SDApp.Services;
using SDApp.ViewModels;

namespace SDApp.Views;

public sealed partial class GenerationPage : Page
{
    GenerationViewModel? _viewModel;

    public GenerationPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not BackendApiClient apiClient)
        {
            return;
        }

        _viewModel = new GenerationViewModel(apiClient, DispatcherQueue);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is not GenerationViewModel viewModel)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(GenerationViewModel.StatusText):
                StatusTextBlock.Text = viewModel.StatusText;
                break;
            case nameof(GenerationViewModel.ResultImage):
                ResultImageControl.Source = viewModel.ResultImage;
                break;
            case nameof(GenerationViewModel.IsGenerating):
                GenerateButton.IsEnabled = !viewModel.IsGenerating;
                break;
        }
    }

    async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not GenerationViewModel viewModel)
        {
            return;
        }

        viewModel.Prompt = PromptTextBox.Text;
        await viewModel.GenerateAsync(CancellationToken.None);
    }
}
