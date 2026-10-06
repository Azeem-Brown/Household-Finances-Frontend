using System.Globalization;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Formats client-side errors using the convention from the specification's Error Handling
/// section: the error enum name followed by the identifier or number of significance that was
/// being reached (for example, <c>NotFound 42</c>).
/// </summary>
public static class ClientErrorFormatter
{
    /// <summary>
    /// Builds the display string for an error as <c>&lt;enum&gt; &lt;identifier&gt;</c>.
    /// </summary>
    /// <param name="errorCode">The client-side error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    /// <returns>The formatted display string, with the enum placed before the identifier.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identifier"/> is <see langword="null"/>.
    /// </exception>
    public static string Format(ClientErrorCode errorCode, object identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var identifierText = Convert.ToString(identifier, CultureInfo.InvariantCulture);

        return $"{errorCode} {identifierText}";
    }
}
