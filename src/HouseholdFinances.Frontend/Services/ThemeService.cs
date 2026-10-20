using MudBlazor;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Frontend-only client service that owns the MudBlazor theme applied to the shell.
/// Keeps theme state out of the layout so it can be reused and tested independently.
/// </summary>
public class ThemeService
{
    /// <summary>
    /// The cohesive green theme applied by <c>MudThemeProvider</c> in the root layout. Both the
    /// light and dark palettes are defined together so the green branding stays consistent when the
    /// user switches modes; only palette values change and no page behavior is affected.
    /// </summary>
    public MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2E7D32",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#558B2F",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#00695C",
            AppbarBackground = "#1B5E20",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#F4F9F4"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#81C784",
            PrimaryContrastText = "#0B1F0E",
            Secondary = "#AED581",
            SecondaryContrastText = "#16260C",
            Tertiary = "#4DB6AC",
            AppbarBackground = "#0E1F14",
            AppbarText = "#E8F5E9",
            DrawerBackground = "#0B1710"
        }
    };

    /// <summary>Whether the light or dark palette is currently active.</summary>
    public bool IsDarkMode { get; set; }

    /// <summary>Switches between the light and dark palettes.</summary>
    public void ToggleDarkMode() => IsDarkMode = !IsDarkMode;
}
