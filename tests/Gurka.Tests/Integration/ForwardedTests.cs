using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gurka.Tests.Integration;

// Klientens IP bakom proxy (Net.cs): X-Forwarded-For godtas bara från loopback och privata nät, CF-Connecting-IP bara när det är påslaget.
public class ForwardedTests
{
    // kör samma två middleware som Program.cs och returnerar den IP som Lobby/Flood ser
    static async Task<(string key, HttpContext c)> Run(string remote, string? xff = null, string? cf = null, bool cfOn = false, string? proto = null)
    {
        var o = new ForwardedHeadersOptions(); Net.Proxies(o);
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        c.Request.Scheme = "http";
        if (xff != null) c.Request.Headers["X-Forwarded-For"] = xff;
        if (proto != null) c.Request.Headers["X-Forwarded-Proto"] = proto;
        if (cf != null) c.Request.Headers["CF-Connecting-IP"] = cf;
        var fwd = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(o));
        await Net.Cloudflare(cfOn)(c, () => fwd.Invoke(c));
        return (Net.Key(c.Connection.RemoteIpAddress), c);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.17.0.1")]  // docker-nät
    [InlineData("192.168.1.10")]
    public async Task Betrodd_proxy_med_header_ger_klientens_ip(string proxy)
    {
        var (ip, c) = await Run(proxy, "203.0.113.7", proto: "https");
        Assert.Equal("203.0.113.7", ip);
        Assert.Equal("https", c.Request.Scheme);
    }

    [Theory]
    [InlineData("203.0.113.50")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")] // precis utanför 172.16.0.0/12
    public async Task Opålitlig_källa_ignorerar_headern(string remote)
    {
        var (ip, c) = await Run(remote, "1.2.3.4", proto: "https");
        Assert.Equal(remote, ip);
        Assert.Equal("http", c.Request.Scheme);
    }

    [Fact]
    public async Task Förfalskad_kedja_ger_adressen_som_proxyn_såg()
    {
        // klienten skickar själv "1.2.3.4", proxyn lägger till klientens riktiga adress sist
        var (ip, _) = await Run("127.0.0.1", "1.2.3.4, 203.0.113.7");
        Assert.Equal("203.0.113.7", ip);
    }

    [Fact]
    public async Task Två_betrodda_proxyer_i_rad_ger_klientens_ip()
    {
        // t.ex. Cloudflare-tunnel bakom Codespaces: klient -> 10.0.0.5 -> loopback
        var (ip, _) = await Run("127.0.0.1", "203.0.113.7, 10.0.0.5");
        Assert.Equal("203.0.113.7", ip);
    }

    [Fact]
    public async Task Utan_header_behålls_anslutningens_ip()
    {
        var (ip, _) = await Run("192.168.1.10");
        Assert.Equal("192.168.1.10", ip);
    }

    [Fact]
    public async Task CF_Connecting_IP_godtas_från_loopback_när_det_är_påslaget()
    {
        var (ip, _) = await Run("127.0.0.1", xff: "9.9.9.9", cf: "203.0.113.9", cfOn: true);
        Assert.Equal("203.0.113.9", ip);
    }

    [Fact]
    public async Task CF_Connecting_IP_ignoreras_när_det_är_avslaget()
    {
        var (ip, _) = await Run("127.0.0.1", cf: "203.0.113.9");
        Assert.Equal("127.0.0.1", ip);
    }

    [Theory]
    [InlineData("203.0.113.50")]
    [InlineData("192.168.1.10")] // bara loopback, där cloudflared körs
    public async Task CF_Connecting_IP_ignoreras_om_anslutningen_inte_kommer_från_loopback(string remote)
    {
        var (ip, _) = await Run(remote, cf: "1.2.3.4", cfOn: true);
        Assert.Equal(remote, ip);
    }

    [Fact]
    public async Task Ogiltig_CF_Connecting_IP_ignoreras()
    {
        var (ip, _) = await Run("127.0.0.1", cf: "inte-en-ip", cfOn: true);
        Assert.Equal("127.0.0.1", ip);
    }

    [Theory]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:bbbb::9", "2001:db8:1:2::/64")]
    [InlineData("::ffff:192.168.1.10", "192.168.1.10")]
    [InlineData("::1", "::1")]
    public void Nyckeln_för_tak_per_ip(string ip, string key) => Assert.Equal(key, Net.Key(IPAddress.Parse(ip)));

    [Fact]
    public void Nyckel_utan_ip() => Assert.Equal("?", Net.Key(null));
}
