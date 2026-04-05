using Polly;

namespace YomiYa.Core.Resilience.Handlers;

public class PolicyHandler(IAsyncPolicy<HttpResponseMessage> policy, HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
    }
}