namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Client-side failure categories used to identify what went wrong before a message is shown to
/// the user. These are frontend categories only: they are not the backend's wire codes, and the
/// code that calls the API maps backend responses onto them. Values are explicit so the set can
/// grow without renumbering existing members.
/// </summary>
public enum ClientErrorCode
{
    /// <summary>An unexpected or otherwise unclassified client-side failure.</summary>
    Unknown = 0,

    /// <summary>The requested entity or record could not be located.</summary>
    NotFound = 1,

    /// <summary>The supplied input failed validation.</summary>
    InvalidInput = 2,

    /// <summary>The current user is not authenticated for the requested action.</summary>
    Unauthorized = 3,

    /// <summary>The requested change conflicts with the current state.</summary>
    Conflict = 4
}
