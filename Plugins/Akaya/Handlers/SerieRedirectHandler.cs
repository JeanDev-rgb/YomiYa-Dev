using YomiYa.Core.Exceptions;

namespace YomiYa.Extensions.Es.Handlers;

internal class SerieRedirectHandler(string baseUrl, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && !request.RequestUri.ToString().StartsWith($"{_baseUrl}/serie"))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.RequestMessage?.RequestUri?.ToString().TrimEnd('/') == _baseUrl)
        {
            throw new SerieUnavailableException();
        }

        return response;
    }
}