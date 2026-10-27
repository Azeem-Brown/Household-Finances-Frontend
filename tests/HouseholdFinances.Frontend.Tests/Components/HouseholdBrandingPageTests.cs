using Bunit;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using HouseholdFinances.Frontend.Tests.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit tests for the household branding page: it shows the stored logo, rejects an oversized file
/// before calling the API, uploads the chosen image, and removes the logo on request.
/// </summary>
/// <remarks>
/// The browser's file picker cannot run in unit tests, so uploads are simulated with bUnit's
/// <see cref="InputFileContent"/>. These tests prove the page's validation and the calls it makes;
/// they do not prove a real browser round trip or the API's own validation.
/// </remarks>
public class HouseholdBrandingPageTests : BunitContext, IAsyncLifetime
{
    private static readonly Guid HouseholdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly StubApiClient _apiClient = new();

    public HouseholdBrandingPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
        Services.AddScoped<HouseholdContextService>();

        _apiClient.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = HouseholdId, Name = "Brown Household" }
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    private static string LogoPath => $"/api/v1/households/{HouseholdId}/logo";

    [Fact]
    public void Branding_RendersTheStoredLogo()
    {
        _apiClient.FileResponses[LogoPath] = new FileContent("image/png", new byte[] { 1, 2, 3 });

        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-logo']")));
        Assert.Empty(cut.FindAll("[data-testid='branding-placeholder']"));
    }

    [Fact]
    public void Branding_ShowsThePlaceholderWhenTheHouseholdHasNoLogo()
    {
        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-placeholder']")));
        Assert.Empty(cut.FindAll("[data-testid='branding-logo']"));
    }

    [Fact]
    public void Branding_RejectsAnOversizedFileWithoutCallingTheApi()
    {
        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-upload']")));

        string oversized = new string('a', HouseholdBranding.MaxLogoBytes + 1);
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(oversized, "logo.png"));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                HouseholdBranding.TooLargeMessage,
                cut.Find("[data-testid='branding-upload-error']").TextContent.Trim()));
        Assert.Empty(_apiClient.FileUploads);
    }

    [Fact]
    public void Branding_RejectsAnUnsupportedFileTypeWithoutCallingTheApi()
    {
        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-upload']")));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("not an image", "notes.txt"));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                HouseholdBranding.UnsupportedTypeMessage,
                cut.Find("[data-testid='branding-upload-error']").TextContent.Trim()));
        Assert.Empty(_apiClient.FileUploads);
    }

    [Fact]
    public void Branding_UploadsTheChosenImage()
    {
        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-upload']")));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("image-bytes", "logo.png"));

        cut.WaitForAssertion(() => Assert.Single(_apiClient.FileUploads));

        (string path, string fileName, string contentType, byte[] content) = _apiClient.FileUploads[0];
        Assert.Equal(LogoPath, path);
        Assert.Equal("logo.png", fileName);
        Assert.Equal("image/png", contentType);
        Assert.NotEmpty(content);
    }

    [Fact]
    public void Branding_RemovesTheLogoAndClearsThePreview()
    {
        _apiClient.FileResponses[LogoPath] = new FileContent("image/png", new byte[] { 1, 2, 3 });

        IRenderedComponent<HouseholdBranding> cut = Render<HouseholdBranding>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='branding-remove']")));

        // The reload after removal must not return the deleted image again.
        _apiClient.FileResponses[LogoPath] = null;
        cut.Find("[data-testid='branding-remove']").Click();

        cut.WaitForAssertion(() => Assert.Contains(LogoPath, _apiClient.Deletes));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='branding-logo']")));
    }
}
