namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// A client-side failure that carries a <see cref="ClientErrorCode"/> and the identifier or
/// number of significance that was being reached. Its message follows the convention produced by
/// <see cref="ClientErrorFormatter"/>: <c>&lt;enum&gt; &lt;identifier&gt;</c>.
/// </summary>
public sealed class ClientErrorException : Exception
{
    /// <summary>Gets the client-side error category.</summary>
    public ClientErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets the identifier or number of significance that was being reached when the failure
    /// occurred. This may be an entity id or a numeric value.
    /// </summary>
    public object Identifier { get; }

    /// <summary>
    /// Creates the exception and derives its message from the error code and identifier.
    /// </summary>
    /// <param name="errorCode">The client-side error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    public ClientErrorException(ClientErrorCode errorCode, object identifier)
        : this(errorCode, identifier, innerException: null)
    {
    }

    /// <summary>
    /// Creates the exception and derives its message from the error code and identifier.
    /// </summary>
    /// <param name="errorCode">The client-side error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    /// <param name="innerException">The exception that caused this failure, if any.</param>
    public ClientErrorException(ClientErrorCode errorCode, object identifier, Exception? innerException)
        : base(ClientErrorFormatter.Format(errorCode, identifier), innerException)
    {
        ErrorCode = errorCode;
        Identifier = identifier;
    }
}
