using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Shares the current household across pages in the Blazor circuit, so the shell and the branding
/// page agree on which household's logo is shown and changed.
/// </summary>
/// <remarks>
/// The household list comes from the authenticated households endpoint, which only returns the
/// current user's households, so selection never exposes another user's household. The list is
/// loaded once per circuit and refreshed on demand.
/// </remarks>
public sealed class HouseholdContextService
{
    /// <summary>The authenticated endpoint returning the current user's households.</summary>
    public const string HouseholdsPath = "/api/v1/households";

    private readonly IApiClient _apiClient;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>Creates the service.</summary>
    /// <param name="apiClient">The shared API client used to load households.</param>
    public HouseholdContextService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>Gets the households available to the current user.</summary>
    public IReadOnlyList<Household> Households { get; private set; } = Array.Empty<Household>();

    /// <summary>Gets the household the shell and branding page currently act on.</summary>
    public Household? SelectedHousehold { get; private set; }

    /// <summary>Gets whether the household list has been loaded for this circuit.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Raised when the list or the selection changes.</summary>
    public event Action? Changed;

    /// <summary>Loads the households once per circuit.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>Reloads the households, keeping the current selection when it still exists.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            Guid? previousId = SelectedHousehold?.Id;

            IReadOnlyList<Household>? loaded = await _apiClient.GetAsync<IReadOnlyList<Household>?>(
                HouseholdsPath,
                cancellationToken);

            IReadOnlyList<Household> households = loaded ?? Array.Empty<Household>();

            Households = households;
            SelectedHousehold = households.FirstOrDefault(household => household.Id == previousId)
                ?? households.FirstOrDefault();
            IsLoaded = true;

            Changed?.Invoke();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>Selects one of the loaded households.</summary>
    /// <param name="householdId">The identifier of a household in <see cref="Households"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The household is not available to the user.</exception>
    public void Select(Guid householdId)
    {
        Household? household = Households.FirstOrDefault(candidate => candidate.Id == householdId)
            ?? throw new ArgumentOutOfRangeException(
                nameof(householdId),
                "The household is not available to the current user.");

        if (SelectedHousehold?.Id == household.Id)
        {
            return;
        }

        SelectedHousehold = household;
        Changed?.Invoke();
    }
}
