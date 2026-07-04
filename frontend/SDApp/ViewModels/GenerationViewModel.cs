using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using SDApp.Models;
using SDApp.Services;
using Windows.Storage.Streams;

namespace SDApp.ViewModels;

sealed class GenerationViewModel : INotifyPropertyChanged
{
    static readonly ResourceLoader ResourceLoader = new();

    readonly DispatcherQueue _dispatcherQueue;

    BackendApiClient? _apiClient;
    string _prompt = "";
    string _negativePrompt = "";
    int _steps = 20;
    double _cfgScale = 7.0;
    int _width = 1024;
    int _height = 1024;
    string _sampler = "euler_a";
    string _seedText = "";
    string _statusText = ResourceLoader.GetString("Generation_BackendStarting");
    bool _isGenerating;
    bool _isBackendReady;
    BitmapImage? _resultImage;
    List<string> _samplers = [];
    string _deviceInfo = "";

    public GenerationViewModel(DispatcherQueue dispatcherQueue) => _dispatcherQueue = dispatcherQueue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Prompt
    {
        get => _prompt;
        set => SetField(ref _prompt, value);
    }

    public string NegativePrompt
    {
        get => _negativePrompt;
        set => SetField(ref _negativePrompt, value);
    }

    public int Steps
    {
        get => _steps;
        set => SetField(ref _steps, value);
    }

    public double CfgScale
    {
        get => _cfgScale;
        set => SetField(ref _cfgScale, value);
    }

    public int Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }

    public int Height
    {
        get => _height;
        set => SetField(ref _height, value);
    }

    public string Sampler
    {
        get => _sampler;
        set => SetField(ref _sampler, value);
    }

    /// <summary>空欄ならランダムシード(null送信)として扱う。</summary>
    public string SeedText
    {
        get => _seedText;
        set => SetField(ref _seedText, value);
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

    public bool IsBackendReady
    {
        get => _isBackendReady;
        set => SetField(ref _isBackendReady, value);
    }

    public BitmapImage? ResultImage
    {
        get => _resultImage;
        set => SetField(ref _resultImage, value);
    }

    public List<string> Samplers
    {
        get => _samplers;
        set => SetField(ref _samplers, value);
    }

    public string DeviceInfo
    {
        get => _deviceInfo;
        set => SetField(ref _deviceInfo, value);
    }

    public async Task AttachBackendAsync(BackendApiClient apiClient, CancellationToken ct)
    {
        _apiClient = apiClient;

        try
        {
            List<string> samplers = await apiClient.GetSamplersAsync(ct).ConfigureAwait(false);
            HealthInfo health = await apiClient.GetHealthAsync(ct).ConfigureAwait(false);

            _dispatcherQueue.TryEnqueue(() =>
            {
                Samplers = samplers;
                if (samplers.Count > 0 && !samplers.Contains(Sampler))
                {
                    Sampler = samplers[0];
                }

                DeviceInfo = $"{health.Device} / {DisplayNameFor(health.LoadedModel)}";
                IsBackendReady = true;
                StatusText = ResourceLoader.GetString("Generation_Ready");
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => StatusText = string.Format(ResourceLoader.GetString("Generation_Error"), ex.Message));
        }
    }

    public async Task RefreshDeviceInfoAsync(BackendApiClient apiClient, CancellationToken ct)
    {
        HealthInfo health = await apiClient.GetHealthAsync(ct).ConfigureAwait(false);
        _dispatcherQueue.TryEnqueue(() => DeviceInfo = $"{health.Device} / {DisplayNameFor(health.LoadedModel)}");
    }

    static string DisplayNameFor(string modelId) =>
        Path.Exists(modelId) ? Path.GetFileNameWithoutExtension(modelId) : modelId;

    public async Task GenerateAsync(CancellationToken ct)
    {
        if (_apiClient is not BackendApiClient apiClient || string.IsNullOrWhiteSpace(Prompt) || IsGenerating)
        {
            return;
        }

        if (!TryParseSeed(SeedText, out long? seed))
        {
            StatusText = ResourceLoader.GetString("Generation_SeedMustBeInteger");
            return;
        }

        IsGenerating = true;
        StatusText = ResourceLoader.GetString("Generation_Generating");

        try
        {
            GenerationRequest request = new(
                Prompt,
                NegativePrompt,
                Steps,
                CfgScale,
                Width,
                Height,
                Sampler,
                seed);

            GenerationResult result = await apiClient.GenerateTextToImageAsync(request, ct).ConfigureAwait(false);

            if (result.Error is not null)
            {
                _dispatcherQueue.TryEnqueue(() => StatusText = string.Format(ResourceLoader.GetString("Generation_Error"), result.Error));
                return;
            }

            if (result.ImageUrl is not string imageUrl)
            {
                _dispatcherQueue.TryEnqueue(() => StatusText = ResourceLoader.GetString("Generation_NoImageReturned"));
                return;
            }

            byte[] imageBytes = await apiClient.DownloadImageAsync(imageUrl, ct).ConfigureAwait(false);

            _dispatcherQueue.TryEnqueue(async void () =>
            {
                InMemoryRandomAccessStream stream = new();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                BitmapImage bitmap = new();
                await bitmap.SetSourceAsync(stream);
                ResultImage = bitmap;
                StatusText = ResourceLoader.GetString("Generation_Done");
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => StatusText = string.Format(ResourceLoader.GetString("Generation_Error"), ex.Message));
        }
        finally
        {
            _dispatcherQueue.TryEnqueue(() => IsGenerating = false);
        }
    }

    static bool TryParseSeed(string seedText, out long? seed)
    {
        if (string.IsNullOrWhiteSpace(seedText))
        {
            seed = null;
            return true;
        }

        if (long.TryParse(seedText, out long parsed))
        {
            seed = parsed;
            return true;
        }

        seed = null;
        return false;
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
