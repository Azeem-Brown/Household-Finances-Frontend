using System.Net;
using System.Text;
using System.Text.Json;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the persistent session store (issue #28): persistence across a new store instance
/// (a simulated app restart), replacement on rotation, clearing on sign-out and on refresh
/// failure, and rejection of expired or unreadable records. Every test works in its own temporary
/// directory with an ephemeral Data Protection key ring, so nothing touches the real user profile.
/// </summary>
public sealed class SessionPersistenceTests : IDisposable
{
    private const string SessionId = "session-id-for-tests";
    private const string OtherSessionId = "another-session-id-for-tests";

    private static readonly DateTimeOffset PinnedNow = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hf-session-tests", Guid.NewGuid().ToString("N"));
    private readonly IDataProtectionProvider _keyRing = new EphemeralDataProtectionProvider();
    private readonly FixedTimeProvider _clock = new(PinnedNow);

    private PersistentSessionOptions Options => new(
        _directory,
        Path.Combine(_directory, "keys"),
        TimeSpan.FromDays(PersistentSessionOptions.DefaultCookieMaxAgeDays));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory is harmless.
        }
    }

    [Fact]
    public async Task SetAsync_InANewRepositoryInstanceWithTheSameKeyRing_ReturnsTheStoredPair()
    {
        await CreateRepository().SetAsync(SessionId, NewPair("access-1", "refresh-1"));

        // A new instance over the same directory and key ring stands in for a restarted app.
        var restarted = await CreateRepository().GetAsync(SessionId);

        Assert.NotNull(restarted);
        Assert.Equal("access-1", restarted!.AccessToken);
        Assert.Equal("refresh-1", restarted.RefreshToken);
    }

    [Fact]
    public async Task SetAsync_EncryptsTheRecordAtRest()
    {
        await CreateRepository().SetAsync(SessionId, NewPair("access-secret", "refresh-secret"));

        var record = Assert.Single(Directory.GetFiles(_directory, "*.session"));
        var contents = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(record));

        Assert.DoesNotContain("access-secret", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secret", contents, StringComparison.Ordinal);

        // The file name is a hash of the session id, so a directory listing does not leak the cookie value.
        Assert.DoesNotContain(SessionId, record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetAsync_OnRotation_ReplacesTheStoredPairSoTheOldRefreshTokenCannotBeResumed()
    {
        var repository = CreateRepository();
        await repository.SetAsync(SessionId, NewPair("access-1", "refresh-1"));
        await repository.SetAsync(SessionId, NewPair("access-2", "refresh-2"));

        var stored = await CreateRepository().GetAsync(SessionId);

        Assert.NotNull(stored);
        Assert.Equal("access-2", stored!.AccessToken);
        Assert.Equal("refresh-2", stored.RefreshToken);
    }

    [Fact]
    public async Task ClearAsync_OnSignOut_RemovesTheRecordForANewInstanceToo()
    {
        var repository = CreateRepository();
        await repository.SetAsync(SessionId, NewPair("access-1", "refresh-1"));

        await repository.ClearAsync(SessionId);

        Assert.Null(await repository.GetAsync(SessionId));
        Assert.Null(await CreateRepository().GetAsync(SessionId));
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task ClearAsync_LeavesOtherSessionsAlone()
    {
        var repository = CreateRepository();
        await repository.SetAsync(SessionId, NewPair("access-1", "refresh-1"));
        await repository.SetAsync(OtherSessionId, NewPair("access-2", "refresh-2"));

        await repository.ClearAsync(SessionId);

        Assert.Null(await repository.GetAsync(SessionId));
        Assert.NotNull(await repository.GetAsync(OtherSessionId));
    }

    [Fact]
    public async Task GetAsync_WithAnExpiredRecord_ReturnsNullAndRemovesTheRecord()
    {
        var repository = CreateRepository();
        var expiresAt = PinnedNow.AddMinutes(30);
        await repository.SetAsync(SessionId, NewPair("access-1", "refresh-1", expiresAt));

        _clock.UtcNow = expiresAt.AddSeconds(1);

        Assert.Null(await repository.GetAsync(SessionId));

        // The stale record is removed, not merely ignored.
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task GetAsync_WithARecordFromAnotherKeyRing_ReturnsNullAndRemovesIt()
    {
        await CreateRepository().SetAsync(SessionId, NewPair("access-1", "refresh-1"));

        // A lost or rotated key ring makes the record unreadable; that must fail closed.
        var unreadable = CreateRepository(new EphemeralDataProtectionProvider());

        Assert.Null(await unreadable.GetAsync(SessionId));
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task GetAsync_WithATamperedRecord_ReturnsNullAndRemovesIt()
    {
        await CreateRepository().SetAsync(SessionId, NewPair("access-1", "refresh-1"));

        var record = Assert.Single(Directory.GetFiles(_directory, "*.session"));
        await File.WriteAllBytesAsync(record, "not a protected payload"u8.ToArray());

        Assert.Null(await CreateRepository().GetAsync(SessionId));
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task GetAsync_ForAnUnknownSession_ReturnsNullAndWritesNothing()
    {
        Assert.Null(await CreateRepository().GetAsync(SessionId));
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task PruneExpiredRecordsAsync_RemovesRecordsOlderThanTheCookieLifetime()
    {
        var repository = CreateRepository();
        await repository.SetAsync(SessionId, NewPair("access-1", "refresh-1"));

        var record = Assert.Single(Directory.GetFiles(_directory, "*.session"));
        File.SetLastWriteTimeUtc(record, PinnedNow.AddDays(-(PersistentSessionOptions.DefaultCookieMaxAgeDays + 1)).UtcDateTime);

        Assert.Equal(1, await repository.PruneExpiredRecordsAsync());
        Assert.Empty(Directory.GetFiles(_directory, "*.session"));
    }

    [Fact]
    public async Task PersistentTokenStore_WithoutASessionId_ThrowsInsteadOfFallingBackToMemory()
    {
        var store = new PersistentTokenStore(CreateRepository(), new SessionIdAccessor(new ActiveSessionRegistry()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetAsync(NewPair("access-1", "refresh-1")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ClearAsync());
    }

    [Fact]
    public async Task PersistentTokenStore_ReadsAndWritesTheRecordOfItsOwnSessionOnly()
    {
        var repository = CreateRepository();
        await repository.SetAsync(OtherSessionId, NewPair("other-access", "other-refresh"));

        var store = new PersistentTokenStore(repository, CreateAccessor(SessionId));

        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var stored = await store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("access-1", stored!.AccessToken);
        Assert.Equal("other-access", (await repository.GetAsync(OtherSessionId))!.AccessToken);
    }

    [Fact]
    public async Task ApiClient_WhenTheRefreshIsRejected_ClearsThePersistedRecordSoTheSessionFailsClosed()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "1"));
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "refresh"));

        var store = new PersistentTokenStore(CreateRepository(), CreateAccessor(SessionId));
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateApiClient(handler, store);

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/users/me"));

        Assert.Equal(ClientErrorCode.Unauthorized, exception.ErrorCode);

        // The persisted record is gone, so a reload cannot resume the rejected session.
        Assert.Null(await CreateRepository().GetAsync(SessionId));
    }

    [Fact]
    public async Task ApiClient_AfterARotation_PersistsTheReplacementPairImmediately()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "1"));
        handler.EnqueueJson(HttpStatusCode.OK, TokenJson("access-2", "refresh-2"));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var store = new PersistentTokenStore(CreateRepository(), CreateAccessor(SessionId));
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateApiClient(handler, store);

        await client.GetAsync<JsonElement>("/api/v1/users/me");

        // A new repository instance sees the rotated refresh token, which the backend invalidated
        // the presented one in favour of.
        var persisted = await CreateRepository().GetAsync(SessionId);
        Assert.NotNull(persisted);
        Assert.Equal("access-2", persisted!.AccessToken);
        Assert.Equal("refresh-2", persisted.RefreshToken);
    }

    private FileSessionTokenRepository CreateRepository(IDataProtectionProvider? keyRing = null) =>
        new(Options, keyRing ?? _keyRing, _clock, NullLogger<FileSessionTokenRepository>.Instance);

    private static SessionIdAccessor CreateAccessor(string sessionId)
    {
        var accessor = new SessionIdAccessor(new ActiveSessionRegistry());
        accessor.SetSessionId(sessionId);
        return accessor;
    }

    private static TokenPair NewPair(string accessToken, string refreshToken, DateTimeOffset? refreshExpiresAt = null) =>
        new(
            accessToken,
            PinnedNow.AddMinutes(10).UtcDateTime,
            refreshToken,
            (refreshExpiresAt ?? PinnedNow.AddDays(1)).UtcDateTime);

    private static ApiClient CreateApiClient(HttpMessageHandler handler, ITokenStore store) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5252") },
            store,
            NullLogger<ApiClient>.Instance);

    private static string ProblemJson(int errorCode, string identifier) =>
        $"{{\"errorCode\":{errorCode},\"identifier\":\"{identifier}\"}}";

    private static string TokenJson(string accessToken, string refreshToken) =>
        JsonSerializer.Serialize(
            new
            {
                accessToken,
                accessTokenExpiresAtUtc = PinnedNow.AddMinutes(10),
                refreshToken,
                refreshTokenExpiresAtUtc = PinnedNow.AddDays(1)
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
