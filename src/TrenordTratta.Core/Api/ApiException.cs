using System.Net;

namespace TrenordTratta.Core.Api;

public sealed class ApiException(string source, string message, HttpStatusCode? statusCode = null, Exception? inner = null)
    : Exception($"[{source}] {message}", inner)
{
    public string ApiName { get; } = source;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

internal static class ApiResponse
{
    /// <summary>
    /// Controllo su due fronti: status diverso da 200 e presenza di "Error" nel body
    /// (le API rispondono a volte 200 con un messaggio d'errore).
    /// </summary>
    public static async Task<string> ReadCheckedAsync(HttpResponseMessage response, string source, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK)
            throw new ApiException(source, $"HTTP {(int)response.StatusCode}", response.StatusCode);

        if (body.Contains("Error", StringComparison.Ordinal))
            throw new ApiException(source, "La risposta contiene un errore", response.StatusCode);

        return body;
    }
}
