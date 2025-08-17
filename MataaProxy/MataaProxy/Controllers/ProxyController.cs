using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

[Route("proxy")]
[ApiController]
public class ProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public ProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch]
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

        foreach (var header in Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        if (Request.ContentLength > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            requestMessage.Content = new StringContent(body);

            if (Request.ContentType != null)
                requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
        }

        var responseMessage = await _httpClient.SendAsync(requestMessage);

        var content = await responseMessage.Content.ReadAsByteArrayAsync();

        foreach (var header in responseMessage.Headers)
            Response.Headers[header.Key] = header.Value.ToArray();

        foreach (var header in responseMessage.Content.Headers)
            Response.Headers[header.Key] = header.Value.ToArray();

        Response.StatusCode = (int)responseMessage.StatusCode;

        return File(content, responseMessage.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
    }
}
