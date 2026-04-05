using Microsoft.Data.Sqlite;
using Polly;
using Polly.Retry;

namespace YomiYa.Core.Resilience;

public static class ResiliencePolicies
{
    // Política de reintentos para peticiones HTTP.
    public static AsyncRetryPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine(
                        $"[HTTP Retry] Retrying request... Attempt {retryAttempt}. Waiting {timespan.TotalSeconds}s. Reason: {outcome.Exception?.Message ?? outcome.Result.ReasonPhrase}");
                });
    }

    // Nueva política de reintentos para la descarga de imágenes.
    public static AsyncRetryPolicy<HttpResponseMessage> GetImageRetryPolicy()
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine(
                        $"[Image Download Retry] Retrying... Attempt {retryAttempt}. Waiting {timespan.TotalSeconds}s. Reason: {outcome.Exception?.Message ?? outcome.Result.ReasonPhrase}");
                });
    }

    // Política de reintentos para operaciones de base de datos.
    public static AsyncRetryPolicy GetDatabaseRetryPolicy()
    {
        return Policy
            .Handle<SqliteException>(e => e.SqliteErrorCode == 5) // SQLITE_BUSY
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt),
                onRetry: (exception, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine(
                        $"[DB Retry] Retrying operation... Attempt {retryAttempt}. Waiting {timespan.TotalMilliseconds}ms. Reason: {exception.Message}");
                });
    }
}