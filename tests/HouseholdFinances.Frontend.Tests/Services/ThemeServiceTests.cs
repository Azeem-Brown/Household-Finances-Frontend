using HouseholdFinances.Frontend.Services;
using MudBlazor.Utilities;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Unit tests for the green theme applied through <c>MudThemeProvider</c>: the light and dark
/// palettes are both green-based, differ from each other, and follow the dark-mode toggle. These
/// prove the configured palette values; they do not prove how a browser renders them.
/// </summary>
public class ThemeServiceTests
{
    [Fact]
    public void Theme_DefinesDistinctLightAndDarkPrimaryColors()
    {
        var service = new ThemeService();

        Assert.NotEqual(service.Theme.PaletteLight.Primary, service.Theme.PaletteDark.Primary);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Theme_PrimaryColorIsGreenInBothModes(bool dark)
    {
        var theme = new ThemeService().Theme;
        var primary = dark ? theme.PaletteDark.Primary : theme.PaletteLight.Primary;

        Assert.True(IsGreen(primary), $"Expected a green primary but was '{primary}'.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Theme_AppbarBackgroundIsGreenInBothModes(bool dark)
    {
        var theme = new ThemeService().Theme;
        var appbar = dark ? theme.PaletteDark.AppbarBackground : theme.PaletteLight.AppbarBackground;

        Assert.True(IsGreen(appbar), $"Expected a green appbar background but was '{appbar}'.");
    }

    [Fact]
    public void ToggleDarkMode_SwitchesBetweenLightAndDark()
    {
        var service = new ThemeService();

        Assert.False(service.IsDarkMode);

        service.ToggleDarkMode();
        Assert.True(service.IsDarkMode);

        service.ToggleDarkMode();
        Assert.False(service.IsDarkMode);
    }

    // A colour counts as green when its green channel is the strongest of the three channels.
    private static bool IsGreen(MudColor color) => color.G > color.R && color.G > color.B;
}
