using System.Text.Json;
using Bunit;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ProfilePage = HouseholdFinances.Frontend.Components.Pages.Profile;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Profile page (issue #15): the loaded profile renders the current
/// user's name, email, and household; the display name saves through PATCH and the saved value is
/// reflected afterwards; a blank name is rejected before any request; and failures surface through
/// the shared client error convention. No password or hash is ever rendered.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page reads <c>GET /api/v1/users/me</c>, saves with <c>PATCH /api/v1/users/me</c>
/// using only the name, and shows guidance or an error. They do not prove the real backend returns
/// or persists the profile, nor that a browser renders the page.
/// </remarks>
public class ProfilePageTests : BunitContext, IAsyncLifetime
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly StubApiClient _apiClient = new();

    public ProfilePageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
    }

    // The editable name renders a MudBlazor input that resolves services which only implement
    // IAsyncDisposable, so the context must be disposed asynchronously.
    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    [Fact]
    public void Profile_Populated_RendersNameEmailAndHousehold()
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Alex Doe", cut.Find("[data-testid='profile-name']").TextContent.Trim());
            Assert.Equal("alex@example.com", cut.Find("[data-testid='profile-email']").TextContent.Trim());
            Assert.Equal("Family", cut.Find("[data-testid='profile-household']").TextContent.Trim());
        });

        // The profile is read from the current-user endpoint, which resolves the user from the token.
        Assert.Equal(ProfilePage.ProfilePath, Assert.Single(_apiClient.Gets));
    }

    [Fact]
    public void Profile_NoHousehold_RendersNoHouseholdGuidance()
    {
        ArrangeProfile("Alex Doe", "alex@example.com");

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() =>
        {
            var guidance = cut.Find("[data-testid='profile-no-household']");
            Assert.Contains(ProfilePage.NoHouseholdMessage, guidance.TextContent);
        });

        // Guidance replaces the household name, so no bare household value is shown.
        Assert.Empty(cut.FindAll("[data-testid='profile-household']"));
    }

    [Fact]
    public void Profile_Populated_DoesNotRenderAnEditableEmailField()
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='profile-email']")));

        // The email is shown as read-only text, so there is no input bound to it.
        Assert.Empty(cut.FindAll("[data-testid='profile-email-input']"));
    }

    [Fact]
    public void Profile_LoadFailure_ShowsConventionMessageAndNoDetails()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='profile-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='profile-details']"));
    }

    [Fact]
    public void Profile_UnexpectedLoadFailure_ShowsConventionMessage()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='profile-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
        });
    }

    [Fact]
    public void Profile_Save_SendsTrimmedNameAndReflectsTheSavedValue()
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");

        // The backend trims and returns the persisted profile, which is what the page shows.
        _apiClient.PatchResponsesByPath[ProfilePage.ProfilePath] =
            SampleProfile("Alexandria", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='profile-save']")));

        cut.Find("[data-testid='profile-name-input']").Input("  Alexandria  ");
        cut.Find("[data-testid='profile-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Alexandria", cut.Find("[data-testid='profile-name']").TextContent.Trim());
            Assert.Contains(ProfilePage.SavedMessage, cut.Markup);
        });

        var patch = Assert.Single(_apiClient.Patches);
        Assert.Equal(ProfilePage.ProfilePath, patch.Path);

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(patch.Body, WebJson));
        Assert.Equal("Alexandria", body.RootElement.GetProperty("name").GetString());

        // The profile is read once and saved once; no extra GET is issued after the save.
        Assert.Equal(ProfilePage.ProfilePath, Assert.Single(_apiClient.Gets));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Profile_Save_BlankName_ShowsRequiredMessageAndDoesNotCallTheApi(string name)
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='profile-save']")));

        cut.Find("[data-testid='profile-name-input']").Input(name);
        cut.Find("[data-testid='profile-save']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(ProfilePage.NameRequiredMessage, cut.Markup));

        Assert.Empty(_apiClient.Patches);
    }

    [Fact]
    public void Profile_SaveFailure_ShowsConventionMessageAndKeepsTheForm()
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='profile-save']")));

        cut.Find("[data-testid='profile-name-input']").Input("Alexandria");

        _apiClient.ThrowForPatch = new ClientErrorException(ClientErrorCode.InvalidInput, "Name");

        cut.Find("[data-testid='profile-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='profile-save-error']");
            Assert.Equal("InvalidInput Name", alert.TextContent.Trim());
        });

        // The form stays available so the user can correct the input and retry.
        Assert.Single(cut.FindAll("[data-testid='profile-form']"));
    }

    [Fact]
    public void Profile_NeverRendersOrSendsAPasswordField()
    {
        ArrangeProfile("Alex Doe", "alex@example.com", "Family");
        _apiClient.PatchResponsesByPath[ProfilePage.ProfilePath] =
            SampleProfile("Alexandria", "alex@example.com", "Family");

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='profile-save']")));

        // The read model carries no password member, so neither the page nor the loaded data can
        // render a password or hash.
        Assert.DoesNotContain("password", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", cut.Markup, StringComparison.OrdinalIgnoreCase);

        cut.Find("[data-testid='profile-name-input']").Input("Alexandria");
        cut.Find("[data-testid='profile-save']").Click();
        cut.WaitForAssertion(() => Assert.Single(_apiClient.Patches));

        using var body = JsonDocument.Parse(
            JsonSerializer.Serialize(Assert.Single(_apiClient.Patches).Body, WebJson));

        // The update payload carries exactly one property, the display name.
        Assert.Equal(new[] { "name" }, body.RootElement.EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// Registers the current-user profile as the API response, so the page's load has a
    /// deterministic result.
    /// </summary>
    /// <param name="name">The user's display name.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="householdName">The household name, or <see langword="null"/> for no membership.</param>
    private void ArrangeProfile(string name, string email, string? householdName = null) =>
        _apiClient.GetResponsesByPath[ProfilePage.ProfilePath] =
            SampleProfile(name, email, householdName);

    /// <summary>Creates a user profile with representative values for a test.</summary>
    /// <param name="name">The user's display name.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="householdName">The household name, or <see langword="null"/> for no membership.</param>
    /// <returns>The user profile.</returns>
    private static UserProfile SampleProfile(string name, string email, string? householdName) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Households = householdName is null
                ? new List<Household>()
                : new List<Household> { new() { Id = Guid.NewGuid(), Name = householdName } }
        };
}
