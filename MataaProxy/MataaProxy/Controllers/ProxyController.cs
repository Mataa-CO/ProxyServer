using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using MataaProxy.Controllers;
[Route("proxy")]
[ApiController]
public class ProxyController : ControllerBase
{
    private static readonly HashSet<string> ExcludedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Proxy-Connection", "Keep-Alive", "Transfer-Encoding", "TE", "Trailer", "Upgrade",
        "Proxy-Authorization", "Proxy-Authenticate", "Host", "Content-Length"
    };

    private static readonly HashSet<string> ExcludedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Proxy-Connection", "Keep-Alive", "Transfer-Encoding", "TE", "Trailer", "Upgrade",
        "Proxy-Authorization", "Proxy-Authenticate", "Content-Length", "Content-Type"
    };

    private readonly HttpClient _httpClient;

    public ProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("UnsafeProxy");
    }

    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch, HttpOptions, HttpHead]
    public async Task<IActionResult> Forward()
    {
        var targetUrl = Request.Query["url"].ToString();

        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.IsWellFormedUriString(targetUrl, UriKind.Absolute))
            return BadRequest("Missing or invalid 'url' query parameter.");

        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(Request.Method),
            RequestUri = new Uri(targetUrl)
        };

        if (Request.ContentLength.HasValue && Request.ContentLength.Value > 0)
        {
            requestMessage.Content = new StreamContent(Request.Body);

            // Set Content-Type to 'application/json' exactly if the incoming request is JSON
            if (!string.IsNullOrEmpty(Request.ContentType) &&
                Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content.Headers.Remove("Content-Type");
                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            }
            else if (!string.IsNullOrEmpty(Request.ContentType))
            {
                requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(Request.ContentType);
            }

            foreach (var header in Request.Headers)
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        foreach (var header in Request.Headers)
        {
            if (ExcludedRequestHeaders.Contains(header.Key))
                continue;

            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                continue;

            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        using var responseMessage = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            HttpContext.RequestAborted);

        Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            if (!ExcludedResponseHeaders.Contains(header.Key))
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            if (!ExcludedResponseHeaders.Contains(header.Key))
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        var contentType = responseMessage.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            Response.ContentType = contentType;
        }

        await responseMessage.Content.CopyToAsync(Response.Body, HttpContext.RequestAborted);

        return new EmptyResult();
    }
}
