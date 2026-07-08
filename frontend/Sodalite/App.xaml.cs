using Microsoft.UI.Xaml;

namespace Sodalite;

public partial class App : Application
{
    const string SingleInstanceMutexName = "Sodalite-SingleInstance-9F3C6F1E-9C4A-4B7E-8B8B-6B7B6C3B7A5B";

    Window? _window;
    Mutex? _singleInstanceMutex;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);

        if (!createdNew)
        {
            AlreadyRunningWindow alreadyRunningWindow = new();
            alreadyRunningWindow.Closed += (_, _) => Environment.Exit(0);
            alreadyRunningWindow.Activate();
            _window = alreadyRunningWindow;
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
