using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Gurka.Tests.Integration;

// Origin-kontroll på /hub (B12) och HSTS/CSP bakom https-proxy (B14), via en riktig testserver.
[Collection("Server")]
public class SecurityTests : IAsyncLifetime
{
    WebApplicationFactory<Program> f = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await f.DisposeAsync();

    // skickar en request med valfri anslutnings-IP och headers; TestServer har annars ingen RemoteIpAddress
    async Task<HttpContext> Send(string method, string path, string? remote = null, params (string k, string v)[] headers) =>
        await f.Server.SendAsync(c =>
        {
            c.Request.Method = method;
            var q = path.IndexOf('?');
            c.Request.Path = q < 0 ? path : path[..q];
            if (q >= 0) c.Request.QueryString = new QueryString(path[q..]);
            if (remote != null) c.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            foreach (var (k, v) in headers) c.Request.Headers[k] = v;
        });

    static async Task<string> Body(HttpContext c) => await new StreamReader(c.Response.Body).ReadToEndAsync();

    const string Negotiate = "/hub/negotiate?negotiateVersion=1";

    [Fact]
    public async Task Främmande_Origin_ger_403_utan_innehåll()
    {
        var c = await Send("POST", Negotiate, null, ("Origin", "https://evil.example"));
        Assert.Equal(403, c.Response.StatusCode);
        Assert.Equal("", await Body(c));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("inte en url")]
    [InlineData("http://localhost.evil.example")]
    [InlineData("http://localhost:81")]
    public async Task Ogiltig_eller_annan_Origin_ger_403(string origin)
    {
        var c = await Send("POST", Negotiate, null, ("Origin", origin));
        Assert.Equal(403, c.Response.StatusCode);
    }

    [Fact]
    public async Task WebSocket_uppgradering_med_främmande_Origin_ger_403()
    {
        var c = await Send("GET", "/hub?id=x", null, ("Origin", "https://evil.example"), ("Connection", "Upgrade"), ("Upgrade", "websocket"));
        Assert.Equal(403, c.Response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://localhost")]  // TestServers värd
    [InlineData("HTTP://LOCALHOST")]
    public async Task Utan_Origin_eller_med_samma_Origin_fungerar_negotiate(string? origin)
    {
        var c = origin == null ? await Send("POST", Negotiate) : await Send("POST", Negotiate, null, ("Origin", origin));
        Assert.Equal(200, c.Response.StatusCode);
        Assert.Contains("connectionToken", await Body(c));
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/build.js")]
    [InlineData("/app.js")]
    [InlineData("/")]
    public async Task Origin_påverkar_inte_annat_än_hubben(string path)
    {
        var c = await Send("GET", path, null, ("Origin", "https://evil.example"));
        Assert.Equal(200, c.Response.StatusCode);
    }

    static string Csp(HttpContext c) => c.Response.Headers.ContentSecurityPolicy.ToString();

    [Fact]
    public async Task Över_http_ingen_HSTS_och_både_ws_och_wss()
    {
        var c = await Send("GET", "/healthz", "127.0.0.1");
        Assert.False(c.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Contains("ws://localhost", Csp(c));
        Assert.Contains("wss://localhost", Csp(c));
    }

    [Fact]
    public async Task Https_från_betrodd_proxy_ger_HSTS_och_bara_wss()
    {
        var c = await Send("GET", "/healthz", "127.0.0.1", ("X-Forwarded-Proto", "https"));
        Assert.Equal("max-age=31536000", c.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Contains("wss://localhost", Csp(c));
        Assert.DoesNotContain("ws://", Csp(c));
    }

    [Fact]
    public async Task Https_header_från_otrustad_källa_ger_ingen_HSTS()
    {
        var c = await Send("GET", "/healthz", "203.0.113.5", ("X-Forwarded-Proto", "https"));
        Assert.False(c.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Contains("ws://localhost", Csp(c));
    }

    [Fact]
    public async Task Render_läge_X_Forwarded_Host_ignoreras_i_CSP_och_Origin()
    {
        await f.DisposeAsync();
        f = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Gurka:Render", "true"));
        var c = await Send("GET", "/healthz", "10.0.0.5", ("X-Forwarded-For", "1.2.3.4, 104.16.0.1"), ("X-Forwarded-Proto", "https"), ("X-Forwarded-Host", "evil.example"));
        Assert.Equal("max-age=31536000", c.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Contains("wss://localhost", Csp(c));
        Assert.DoesNotContain("evil.example", Csp(c));
        var h = await Send("POST", Negotiate, "10.0.0.5", ("X-Forwarded-Host", "evil.example"), ("Origin", "https://evil.example"));
        Assert.Equal(403, h.Response.StatusCode);
    }
}
