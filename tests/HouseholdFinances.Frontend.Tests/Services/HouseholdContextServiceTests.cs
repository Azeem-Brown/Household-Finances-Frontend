using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using HouseholdFinances.Frontend.Tests.Components;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Verifies the household context: it loads the current user's households once per circuit, selects
/// the first by default, keeps a selection across a refresh, and refuses an unknown household.
/// </summary>
public class HouseholdContextServiceTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task EnsureLoadedAsync_SelectsTheFirstHouseholdAndOnlyLoadsOnce()
    {
        StubApiClient api = new StubApiClient();
        api.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" },
            new Household { Id = SecondId, Name = "Green Household" }
        };
        HouseholdContextService service = new HouseholdContextService(api);

        await service.EnsureLoadedAsync();
        await service.EnsureLoadedAsync();

        Assert.Equal(2, service.Households.Count);
        Assert.Equal(FirstId, service.SelectedHousehold!.Id);
        Assert.Single(api.Gets);
    }

    [Fact]
    public async Task RefreshAsync_KeepsTheSelectedHouseholdWhenItStillExists()
    {
        StubApiClient api = new StubApiClient();
        api.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" },
            new Household { Id = SecondId, Name = "Green Household" }
        };
        HouseholdContextService service = new HouseholdContextService(api);
        await service.EnsureLoadedAsync();

        service.Select(SecondId);
        await service.RefreshAsync();

        Assert.Equal(SecondId, service.SelectedHousehold!.Id);
    }

    [Fact]
    public async Task RefreshAsync_FallsBackToTheFirstHouseholdWhenTheSelectionIsGone()
    {
        StubApiClient api = new StubApiClient();
        api.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" },
            new Household { Id = SecondId, Name = "Green Household" }
        };
        HouseholdContextService service = new HouseholdContextService(api);
        await service.EnsureLoadedAsync();
        service.Select(SecondId);

        api.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" }
        };
        await service.RefreshAsync();

        Assert.Equal(FirstId, service.SelectedHousehold!.Id);
    }

    [Fact]
    public async Task Select_RejectsAHouseholdTheUserCannotSee()
    {
        StubApiClient api = new StubApiClient();
        api.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" }
        };
        HouseholdContextService service = new HouseholdContextService(api);
        await service.EnsureLoadedAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Select(SecondId));
        Assert.Equal(FirstId, service.SelectedHousehold!.Id);
    }
}
