using MudBlazor;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Frontend-only client service that owns the MudBlazor theme applied to the shell.
/// Keeps theme state out of the layout so it can be reused and tested independently.
/// </summary>
public class ThemeService
{
    /// <summary>The theme applied by <c>MudThemeProvider</c> in the root layout.</summary>
    public MudTheme Theme { get; } = new();

    /// <summary>Whether the light or dark palette is currently active.</summary>
    public bool IsDarkMode { get; set; }

    /// <summary>Switches between the light and dark palettes.</summary>
    public void ToggleDarkMode() => IsDarkMode = !IsDarkMode;
}
