using Polly;

namespace YomiYa.Core.Resilience.Handlers;

public class RateLimiterHandler(IAsyncPolicy rateLimitPolicy) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return rateLimitPolicy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
    }
}