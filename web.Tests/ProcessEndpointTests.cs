using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebTests;

public class ProcessEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProcessEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Process_WithoutFileOrUrl_ReturnsBadRequest()
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("both"), "mode" },
        };

        var response = await _client.PostAsync("/api/process", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Add a file or a URL", body);
    }

    [Theory]
    [InlineData("ftp://example.com/video")]
    [InlineData("not-a-url")]
    public async Task Process_WithInvalidUrlScheme_ReturnsBadRequest(string url)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(url), "url" },
        };

        var response = await _client.PostAsync("/api/process", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("http:// or https://", body);
    }

    [Fact]
    public async Task Results_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/results");

        response.EnsureSuccessStatusCode();
    }
}
