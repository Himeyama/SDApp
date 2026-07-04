using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using SDApp.Models;
using SDApp.Services;

namespace SDApp.Views;

sealed partial class ModelSelectionDialog : ContentDialog
{
    static readonly ResourceLoader ResourceLoader = new();

    readonly BackendApiClient _apiClient;

    public string? SelectedModelId { get; private set; }

    public ModelSelectionDialog(BackendApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        Loaded += ModelSelectionDialog_Loaded;
    }

    async void ModelSelectionDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadModelsAsync();
    }

    async Task LoadModelsAsync()
    {
        ErrorInfoBar.IsOpen = false;
        LoadingProgressRing.IsActive = true;
        ModelListView.IsEnabled = false;

        try
        {
            List<ModelInfo> models = await _apiClient.GetModelsAsync(CancellationToken.None).ConfigureAwait(true);
            ModelListView.ItemsSource = models
                .Select(model => new ModelListItem(model.ModelId, model.IsActive))
                .ToList();
        }
        catch (Exception ex)
        {
            ModelListView.ItemsSource = new List<ModelListItem>();
            ShowError(ResourceLoader.GetString("ModelSelectionDialog_LoadErrorTitle"), ex.Message);
        }
        finally
        {
            LoadingProgressRing.IsActive = false;
            ModelListView.IsEnabled = true;
        }
    }

    async void ModelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelListView.SelectedItem is not ModelListItem item || item.IsActive)
        {
            return;
        }

        ErrorInfoBar.IsOpen = false;
        ModelListView.IsEnabled = false;
        LoadingProgressRing.IsActive = true;

        try
        {
            ModelInfo result = await _apiClient.SetActiveModelAsync(item.ModelId, CancellationToken.None).ConfigureAwait(true);
            SelectedModelId = result.ModelId;
            Hide();
        }
        catch (Exception ex)
        {
            ModelListView.SelectedItem = null;
            ShowError(ResourceLoader.GetString("ModelSelectionDialog_SwitchErrorTitle"), ex.Message);
        }
        finally
        {
            LoadingProgressRing.IsActive = false;
            ModelListView.IsEnabled = true;
        }
    }

    void ShowError(string title, string message)
    {
        ErrorInfoBar.Title = title;
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }
}

sealed record ModelListItem(string ModelId, bool IsActive)
{
    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
}
