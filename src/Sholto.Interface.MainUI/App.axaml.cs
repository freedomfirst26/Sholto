using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Sholto.Interface.MainUI;

public partial class App : Application
{
    private readonly IApplicationStartup _startup;

    // Avalonia-forced: the designer and AppBuilder.Configure<App>() (the Harness's
    // BenchHeadlessApp) both need a parameterless constructor. It starts nothing; the
    // host's Program builds the real one through App(IApplicationStartup).
    public App() : this(new NullApplicationStartup()) { }

    public App(IApplicationStartup startup)
    {
        _startup = startup;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) _startup.Start(d);
        base.OnFrameworkInitializationCompleted();
    }
}
