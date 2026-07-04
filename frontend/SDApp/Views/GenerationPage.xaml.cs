using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using SDApp.Services;
using SDApp.ViewModels;

namespace SDApp.Views;

public sealed partial class GenerationPage : Page
{
    const string BrailleSpinnerFrames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    static readonly ResourceLoader ResourceLoader = new();

    readonly DispatcherQueueTimer _backendStartingSpinnerTimer;
    readonly GenerationViewModel _viewModel;

    int _backendStartingSpinnerIndex;

    public event EventHandler<string>? StatusChanged;

    public event EventHandler<string>? DeviceInfoChanged;

    public GenerationPage()
    {
        InitializeComponent();

        _viewModel = new GenerationViewModel(DispatcherQueue);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        GenerateButton.IsEnabled = false;

        string backendStartingText = ResourceLoader.GetString("Generation_BackendStarting");
        _backendStartingSpinnerTimer = DispatcherQueue.CreateTimer();
        _backendStartingSpinnerTimer.Interval = TimeSpan.FromMilliseconds(80);
        _backendStartingSpinnerTimer.Tick += (_, _) =>
        {
            _backendStartingSpinnerIndex = (_backendStartingSpinnerIndex + 1) % BrailleSpinnerFrames.Length;
            GenerateButton.Content = $"{BrailleSpinnerFrames[_backendStartingSpinnerIndex]} {backendStartingText}";
        };
        _backendStartingSpinnerTimer.Start();
    }

    /// <summary>現在の状態でイベントを再発火する。購読側がページ生成後に接続した場合の初期同期用。</summary>
    internal void NotifyCurrentStatus()
    {
        StatusChanged?.Invoke(this, _viewModel.StatusText);
        DeviceInfoChanged?.Invoke(this, _viewModel.DeviceInfo);
    }

    internal void AttachBackend(BackendApiClient apiClient) =>
        _ = _viewModel.AttachBackendAsync(apiClient, CancellationToken.None);

    internal Task RefreshDeviceInfoAsync(BackendApiClient apiClient) =>
        _viewModel.RefreshDeviceInfoAsync(apiClient, CancellationToken.None);

    void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GenerationViewModel.StatusText):
                StatusChanged?.Invoke(this, _viewModel.StatusText);
                break;
            case nameof(GenerationViewModel.ResultImage):
                ResultImageControl.Source = _viewModel.ResultImage;
                break;
            case nameof(GenerationViewModel.IsBackendReady):
                if (_viewModel.IsBackendReady)
                {
                    _backendStartingSpinnerTimer.Stop();
                    GenerateButton.Content = ResourceLoader.GetString("Generation_GenerateButtonLabel");
                }

                GenerateButton.IsEnabled = _viewModel.IsBackendReady && !_viewModel.IsGenerating;
                break;
            case nameof(GenerationViewModel.IsGenerating):
                GenerateButton.IsEnabled = _viewModel.IsBackendReady && !_viewModel.IsGenerating;
                GenerationProgressRing.IsActive = _viewModel.IsGenerating;
                break;
            case nameof(GenerationViewModel.Samplers):
                SamplerComboBox.ItemsSource = _viewModel.Samplers;
                if (_viewModel.Samplers.Count > 0)
                {
                    SamplerComboBox.SelectedIndex = 0;
                }

                break;
            case nameof(GenerationViewModel.DeviceInfo):
                DeviceInfoChanged?.Invoke(this, _viewModel.DeviceInfo);
                break;
        }
    }

    async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Prompt = PromptTextBox.Text;
        _viewModel.NegativePrompt = NegativePromptTextBox.Text;
        _viewModel.Steps = (int)StepsSlider.Value;
        _viewModel.CfgScale = CfgScaleSlider.Value;
        _viewModel.Width = (int)WidthNumberBox.Value;
        _viewModel.Height = (int)HeightNumberBox.Value;
        _viewModel.Sampler = SamplerComboBox.SelectedItem as string ?? _viewModel.Sampler;
        _viewModel.SeedText = SeedTextBox.Text;

        await _viewModel.GenerateAsync(CancellationToken.None);
    }

    void StepsSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (StepsValueTextBlock is not null)
        {
            StepsValueTextBlock.Text = ((int)e.NewValue).ToString();
        }
    }

    void CfgScaleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (CfgScaleValueTextBlock is not null)
        {
            CfgScaleValueTextBlock.Text = e.NewValue.ToString("F1");
        }
    }
}
