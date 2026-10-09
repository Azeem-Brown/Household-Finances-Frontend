using System.Net;
using System.Text;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// A recorded view of an outgoing request. The underlying <see cref="HttpRequestMessage"/> is
/// disposed by the caller, so only the values of interest are captured.
/// </summary>
internal sealed record RecordedRequest(HttpMethod Method, string? Path, string? Authorization, string? Body);

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that records every request and replays queued
/// responses in order, letting the API client be tested without a live server.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    /// <summary>Gets the requests that have been sent, in order.</summary>
    public List<RecordedRequest> Requests { get; } = new();

    /// <summary>Gets the number of refresh requests that were sent.</summary>
    public int RefreshRequestCount =>
        Requests.Count(request => request.Method == HttpMethod.Post && request.Path == "/api/v1/auth/refresh");

    /// <summary>Queues a response to be returned by the next send.</summary>
    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    /// <summary>Queues a response with the supplied status, body, and media type.</summary>
    public void EnqueueJson(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri?.PathAndQuery,
            request.Headers.Authorization?.ToString(),
            body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No stub response was queued for {request.Method} {request.RequestUri}.");
        }

        return _responses.Dequeue();
    }
}
