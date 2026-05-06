using Avalonia;
using Avalonia.Markup.Xaml;
using AApplication = Avalonia.Application;

namespace Turbophrase.Avalonia;

public sealed class TurbophraseAvaloniaApp : AApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<TurbophraseAvaloniaApp>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
