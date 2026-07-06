using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Sodalite.Models;
using Sodalite.Services;

namespace Sodalite.ViewModels;

sealed class GalleryViewModel : INotifyPropertyChanged
{
    readonly DispatcherQueue _dispatcherQueue;

    BackendApiClient? _apiClient;
    bool _isLoading;
    string? _errorMessage;

    public ObservableCollection<GalleryImageInfo> Images { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public GalleryViewModel(DispatcherQueue dispatcherQueue) => _dispatcherQueue = dispatcherQueue;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public async Task LoadAsync(BackendApiClient apiClient, CancellationToken ct)
    {
        _apiClient = apiClient;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            List<GalleryImageInfo> images = await apiClient.GetGalleryImagesAsync(ct).ConfigureAwait(false);
            _dispatcherQueue.TryEnqueue(() =>
            {
                Images.Clear();
                foreach (GalleryImageInfo image in images)
                {
                    Images.Add(image);
                }
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => ErrorMessage = ex.Message);
        }
        finally
        {
            _dispatcherQueue.TryEnqueue(() => IsLoading = false);
        }
    }

    public async Task DeleteAsync(GalleryImageInfo image, CancellationToken ct)
    {
        if (_apiClient is not BackendApiClient apiClient)
        {
            return;
        }

        try
        {
            await apiClient.DeleteGalleryImageAsync(image.ImageId, ct).ConfigureAwait(false);
            _dispatcherQueue.TryEnqueue(() => Images.Remove(image));
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => ErrorMessage = ex.Message);
        }
    }

    void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
