namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The access and refresh token pair issued by the backend, each with its UTC expiry.
/// </summary>
/// <remarks>
/// A refresh always returns and replaces the pair as a whole, so a backend that rotates refresh
/// tokens on use (backend issue #29) cannot leave a stale refresh token behind.
/// </remarks>
/// <param name="AccessToken">The API-issued JWT to send on protected requests.</param>
/// <param name="AccessTokenExpiresAtUtc">The access token's expiry in UTC.</param>
/// <param name="RefreshToken">The token to exchange for a new pair.</param>
/// <param name="RefreshTokenExpiresAtUtc">The refresh token's expiry in UTC.</param>
public sealed record TokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
