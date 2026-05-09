using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using AApplication = Avalonia.Application;
using AColor = Avalonia.Media.Color;
using AColors = Avalonia.Media.Colors;
using ASolidColorBrush = Avalonia.Media.SolidColorBrush;

namespace Turbophrase.Avalonia;

public sealed class TurbophraseAvaloniaApp : AApplication
{
    private IPlatformSettings? _platformSettings;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        ApplyPlatformSettings();
    }

    public void ApplyPlatformSettings()
    {
        var settings = PlatformSettings;
        if (ReferenceEquals(_platformSettings, settings))
        {
            ApplyWindowsColors(settings?.GetColorValues());
            return;
        }

        if (_platformSettings != null)
        {
            _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
        }

        _platformSettings = settings;
        if (_platformSettings == null)
        {
            ApplyWindowsColors(null);
            return;
        }

        ApplyWindowsColors(_platformSettings.GetColorValues());
        _platformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
    }

    private void OnPlatformColorValuesChanged(object? sender, PlatformColorValues values) => ApplyWindowsColors(values);

    private void ApplyWindowsColors(PlatformColorValues? values)
    {
        var accent = values?.AccentColor1 ?? AColor.FromRgb(0, 120, 212);
        RequestedThemeVariant = values?.ThemeVariant == PlatformThemeVariant.Light
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        Resources["TpAccentColor"] = accent;
        Resources["TpAccentHoverColor"] = AdjustLightness(accent, RequestedThemeVariant == ThemeVariant.Light ? -0.08 : 0.10);
        Resources["TpAccentPressedColor"] = AdjustLightness(accent, RequestedThemeVariant == ThemeVariant.Light ? -0.16 : 0.18);
        Resources["TpAccentSoftColor"] = AColor.FromArgb(0x33, accent.R, accent.G, accent.B);
        Resources["TpAccentSelectionColor"] = AColor.FromArgb(0x55, accent.R, accent.G, accent.B);
        Resources["TpAccentTextColor"] = UseDarkText(accent) ? AColors.Black : AColors.White;

        UpdateBrush("TpAccentBrush", accent);
        UpdateBrush("TpAccent2Brush", accent);
        UpdateBrush("TpAccentHoverBrush", (AColor)Resources["TpAccentHoverColor"]!);
        UpdateBrush("TpAccentPressedBrush", (AColor)Resources["TpAccentPressedColor"]!);
        UpdateBrush("TpAccentSoftBrush", (AColor)Resources["TpAccentSoftColor"]!);
        UpdateBrush("TpAccentSelectionBrush", (AColor)Resources["TpAccentSelectionColor"]!);
        UpdateBrush("TpAccentTextBrush", (AColor)Resources["TpAccentTextColor"]!);

        UpdateAccentSurfaces(accent);
    }

    private void UpdateAccentSurfaces(AColor accent)
    {
        UpdateBrush("TpAppBackground", Blend(AColor.FromRgb(26, 26, 26), accent, 0.18, 0xCC));
        UpdateBrush("TpHeroGradient", Blend(AColor.FromRgb(36, 36, 36), accent, 0.16, 0xC2));
        UpdateBrush("TpAcrylicTintBrush", Blend(AColor.FromRgb(26, 26, 26), accent, 0.20, 0xCC));
        UpdateBrush("TpAcrylicHeaderBrush", Blend(AColor.FromRgb(36, 36, 36), accent, 0.18, 0xC7));
        UpdateBrush("TpAcrylicCardBrush", Blend(AColor.FromRgb(43, 43, 43), accent, 0.10, 0xD9));
        UpdateBrush("TpAcrylicRaisedBrush", Blend(AColor.FromRgb(51, 51, 51), accent, 0.09, 0xDE));
        UpdateBrush("TpAcrylicControlBrush", Blend(AColor.FromRgb(69, 69, 69), accent, 0.07, 0xCC));
        UpdateBrush("TpAcrylicControlHoverBrush", Blend(AColor.FromRgb(80, 80, 80), accent, 0.09, 0xD9));
        UpdateBrush("TpAcrylicControlPressedBrush", Blend(AColor.FromRgb(56, 56, 56), accent, 0.06, 0xD9));
    }

    private void UpdateBrush(string key, AColor color)
    {
        if (TryGetResource(key, ActualThemeVariant, out var resource) && resource is ASolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            Resources[key] = new ASolidColorBrush(color);
        }
    }

    private static AColor AdjustLightness(AColor color, double amount)
    {
        byte Adjust(byte channel)
        {
            var target = amount >= 0 ? 255 : 0;
            return (byte)Math.Clamp(channel + (target - channel) * Math.Abs(amount), 0, 255);
        }

        return AColor.FromArgb(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
    }

    private static AColor Blend(AColor baseColor, AColor overlayColor, double overlayWeight, byte alpha)
    {
        byte Mix(byte baseChannel, byte overlayChannel) =>
            (byte)Math.Clamp(baseChannel + (overlayChannel - baseChannel) * overlayWeight, 0, 255);

        return AColor.FromArgb(
            alpha,
            Mix(baseColor.R, overlayColor.R),
            Mix(baseColor.G, overlayColor.G),
            Mix(baseColor.B, overlayColor.B));
    }

    private static bool UseDarkText(AColor color)
    {
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        return luminance > 0.62;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<TurbophraseAvaloniaApp>()
        .UsePlatformDetect()
        .LogToTrace();
}
