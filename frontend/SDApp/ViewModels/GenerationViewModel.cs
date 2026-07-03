using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using SDApp.Models;
using SDApp.Services;
using Windows.Storage.Streams;

namespace SDApp.ViewModels;

sealed class GenerationViewModel : INotifyPropertyChanged
{
    readonly BackendApiClient _apiClient;
    readonly DispatcherQueue _dispatcherQueue;

    string _prompt = "";
    string _statusText = "Ready";
    bool _isGenerating;
    BitmapImage? _resultImage;

    public GenerationViewModel(BackendApiClient apiClient, DispatcherQueue dispatcherQueue)
    {
        _apiClient = apiClient;
        _dispatcherQueue = dispatcherQueue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Prompt
    {
        get => _prompt;
        set => SetField(ref _prompt, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set => SetField(ref _isGenerating, value);
    }

    public BitmapImage? ResultImage
    {
        get => _resultImage;
        set => SetField(ref _resultImage, value);
    }

    public async Task GenerateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Prompt) || IsGenerating)
        {
            return;
        }

        IsGenerating = true;
        StatusText = "Generating...";

        try
        {
            GenerationRequest request = new(Prompt);
            GenerationResult result = await _apiClient.GenerateTextToImageAsync(request, ct).ConfigureAwait(false);

            if (result.Error is not null)
            {
                _dispatcherQueue.TryEnqueue(() => StatusText = $"Error: {result.Error}");
                return;
            }

            if (result.ImageUrl is not string imageUrl)
            {
                _dispatcherQueue.TryEnqueue(() => StatusText = "No image returned.");
                return;
            }

            byte[] imageBytes = await _apiClient.DownloadImageAsync(imageUrl, ct).ConfigureAwait(false);

            _dispatcherQueue.TryEnqueue(async void () =>
            {
                InMemoryRandomAccessStream stream = new();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                BitmapImage bitmap = new();
                await bitmap.SetSourceAsync(stream);
                ResultImage = bitmap;
                StatusText = "Done";
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => StatusText = $"Error: {ex.Message}");
        }
        finally
        {
            _dispatcherQueue.TryEnqueue(() => IsGenerating = false);
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
