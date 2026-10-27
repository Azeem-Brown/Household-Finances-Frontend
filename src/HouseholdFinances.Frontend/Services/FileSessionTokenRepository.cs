using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Stores token pairs as one encrypted file per browser session, so a signed-in session survives a
/// browser reload and an app restart while the refresh token is still valid (issue #28).
/// </summary>
/// <remarks>
/// <para>
/// The file name is the SHA-256 hash of the session id, so a directory listing does not disclose
/// cookie values. The contents are protected with ASP.NET Core Data Protection under the purpose
/// <see cref="ProtectionPurpose"/>, so no encryption key is committed or configured by hand; the
/// key ring is a deployment concern and must be persisted for sessions to survive a restart.
/// </para>
/// <para>
/// A record is only as long-lived as the refresh token it holds: a read that finds an expired,
/// unreadable, or corrupt record deletes it and reports no pair, so the caller fails closed rather
/// than resuming with a value the API will reject.
/// </para>
/// </remarks>
public sealed class FileSessionTokenRepository : ISessionTokenRepository
{
    /// <summary>The Data Protection purpose used to encrypt every session record.</summary>
    public const string ProtectionPurpose = "HouseholdFinances.Frontend.SessionTokenStore.v1";

    private const string FileExtension = ".session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PersistentSessionOptions _options;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FileSessionTokenRepository> _logger;

    // Writes replace a record atomically, so this only serializes writers; readers always see a
    // complete file (either the previous record or the replacement).
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Creates the repository and ensures its directory exists.</summary>
    /// <param name="options">The resolved session options, including the record directory.</param>
    /// <param name="dataProtectionProvider">The provider whose protector encrypts every record.</param>
    /// <param name="timeProvider">The clock used to decide whether a record has expired.</param>
    /// <param name="logger">The logger used to report discarded records.</param>
    public FileSessionTokenRepository(
        PersistentSessionOptions options,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        ILogger<FileSessionTokenRepository> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protector = (dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider)))
            .CreateProtector(ProtectionPurpose);

        Directory.CreateDirectory(_options.Directory);
    }

    /// <inheritdoc />
    public async Task<TokenPair?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        string path = GetRecordPath(sessionId);

        byte[] protectedRecord;
        try
        {
            protectedRecord = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        TokenPair? tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<TokenPair>(_protector.Unprotect(protectedRecord), JsonOptions);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            // Unreadable: a rotated key ring, a truncated write, or a tampered file. Fail closed.
            _logger.LogWarning("A persisted session record could not be read and was discarded.");
            DeleteRecord(path);
            return null;
        }

        if (tokens is null)
        {
            DeleteRecord(path);
            return null;
        }

        if (IsExpired(tokens))
        {
            _logger.LogDebug("A persisted session record had expired and was discarded.");
            DeleteRecord(path);
            return null;
        }

        return tokens;
    }

    /// <inheritdoc />
    public async Task SetAsync(string sessionId, TokenPair tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);

        string path = GetRecordPath(sessionId);
        byte[] protectedRecord = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions));

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_options.Directory);

            // Write beside the record and move it into place, so a concurrent read never sees a
            // half-written file and a crash cannot leave a corrupt record behind.
            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, protectedRecord, cancellationToken);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        string path = GetRecordPath(sessionId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            DeleteRecord(path);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Deletes records that are older than the session cookie lifetime, so a browser that never
    /// returns cannot leave unusable files behind. Runs once at startup and is best effort.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the sweep.</param>
    /// <returns>The number of records that were removed.</returns>
    public Task<int> PruneExpiredRecordsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_options.Directory))
        {
            return Task.FromResult(0);
        }

        DateTimeOffset cutoff = _timeProvider.GetUtcNow() - _options.CookieMaxAge;
        int removed = 0;

        foreach (FileInfo record in new DirectoryInfo(_options.Directory).EnumerateFiles("*" + FileExtension))
        {
            if (record.LastWriteTimeUtc >= cutoff.UtcDateTime)
            {
                continue;
            }

            try
            {
                record.Delete();
                removed++;
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "An expired session record could not be removed.");
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(exception, "An expired session record could not be removed.");
            }
        }

        return Task.FromResult(removed);
    }

    private bool IsExpired(TokenPair tokens) =>
        DateTime.SpecifyKind(tokens.RefreshTokenExpiresAtUtc, DateTimeKind.Utc) <= _timeProvider.GetUtcNow().UtcDateTime;

    private string GetRecordPath(string sessionId) =>
        Path.Combine(_options.Directory, HashSessionId(sessionId) + FileExtension);

    private static string HashSessionId(string sessionId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId))).ToLowerInvariant();

    private static void DeleteRecord(string path) => TryDelete(path);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A failed cleanup leaves an unusable record behind; the next read discards it.
        }
        catch (UnauthorizedAccessException)
        {
            // As above: the record stays, but it can never be resumed.
        }
    }
}
