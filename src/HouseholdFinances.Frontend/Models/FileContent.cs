namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// An image returned by the API together with the media type the API stored for it.
/// </summary>
/// <param name="ContentType">The media type, for example <c>image/png</c>.</param>
/// <param name="Content">The image bytes exactly as the API stored them.</param>
public sealed record FileContent(string ContentType, byte[] Content);
