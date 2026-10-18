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
    // kör samma middleware i samma ordning som Program.cs (Cloudflare, Render, ForwardedHeaders) och returnerar den IP som Lobby/Flood ser
    static async Task<(string key, HttpContext c)> Run(string remote, string? xff = null, string? cf = null, bool cfOn = false, string? proto = null, bool render = false, string? xfh = null)
    {
        var o = new ForwardedHeadersOptions();
        if (render) Net.Proxies(o, true); else Net.Proxies(o); // den gamla överlagringen ska vara oförändrad
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        c.Request.Scheme = "http";
        if (xff != null) c.Request.Headers["X-Forwarded-For"] = xff;
        if (proto != null) c.Request.Headers["X-Forwarded-Proto"] = proto;
        if (cf != null) c.Request.Headers["CF-Connecting-IP"] = cf;
        if (xfh != null) c.Request.Headers["X-Forwarded-Host"] = xfh;
        c.Request.Host = new HostString("gurka.example");
        var fwd = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(o));
        await Net.Cloudflare(cfOn)(c, () => Net.Render(render)(c, () => fwd.Invoke(c)));
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

    // --- Render (Gurka:Render=true): Net.Render hoppar över interna hopp (privata nät, 100.64.0.0/10) och högst ett Cloudflare-hopp ---

    [Fact]
    public async Task Render_klient_via_Cloudflare_och_lastbalanserare()
    {
        var (ip, _) = await Run("10.0.0.5", "1.2.3.4, 104.16.0.1", render: true);
        Assert.Equal("1.2.3.4", ip);
    }

    [Fact]
    public async Task Utan_Render_stannar_kedjan_vid_Cloudflare()
    {
        var (ip, _) = await Run("10.0.0.5", "1.2.3.4, 104.16.0.1");
        Assert.Equal("104.16.0.1", ip);
    }

    [Theory]
    [InlineData("6.6.6.6, 1.2.3.4, 162.158.1.1")]
    [InlineData("6.6.6.6, 104.16.0.9, 1.2.3.4, 162.158.1.1")] // förfalskad Cloudflare-adress längre till vänster hjälper inte
    [InlineData("10.9.9.9, 1.2.3.4, 162.158.1.1")]            // inte heller en förfalskad privat adress
    public async Task Render_förfalskad_kedja_ger_adressen_som_Cloudflare_såg(string xff)
    {
        var (ip, _) = await Run("10.0.0.5", xff, render: true);
        Assert.Equal("1.2.3.4", ip);
    }

    [Fact]
    public async Task Render_tre_proxyer_i_rad()
    {
        // klient -> Cloudflare -> Renders CGNAT-nät -> lastbalanserare i privat nät
        var (ip, _) = await Run("10.0.0.5", "1.2.3.4, 162.158.1.1, 100.64.0.9", render: true);
        Assert.Equal("1.2.3.4", ip);
    }

    [Theory]
    [InlineData(true, "1.2.3.4")]
    [InlineData(false, "100.64.1.2")]
    public async Task CGNAT_nätet_är_betrott_bara_med_Render(bool render, string want)
    {
        var (ip, _) = await Run("100.64.1.2", "1.2.3.4", render: render);
        Assert.Equal(want, ip);
    }

    [Fact]
    public async Task Render_IPv6_från_Cloudflare()
    {
        var (ip, _) = await Run("10.0.0.5", "2001:db8:1:2::7, 2606:4700::1", render: true);
        Assert.Equal("2001:db8:1:2::/64", ip);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("100.128.0.1")] // precis utanför 100.64.0.0/10
    public async Task Render_direktanslutning_utifrån_kan_inte_sätta_headern(string remote)
    {
        var (ip, c) = await Run(remote, "1.2.3.4", proto: "https", render: true);
        Assert.Equal(remote, ip);
        Assert.Equal("http", c.Request.Scheme);
    }

    [Theory]
    [InlineData("104.28.0.1")]  // precis utanför 104.24.0.0/14 (och 104.16.0.0/13)
    [InlineData("172.80.0.1")]  // precis utanför 172.64.0.0/13
    [InlineData("162.160.0.1")] // precis utanför 162.158.0.0/15
    public async Task Render_adress_utanför_Cloudflares_intervall_är_inte_betrodd(string hop)
    {
        var (ip, _) = await Run("10.0.0.5", "1.2.3.4, " + hop, render: true);
        Assert.Equal(hop, ip);
    }

    [Fact]
    public async Task Render_https_från_proxyn_följer_med()
    {
        var (_, c) = await Run("10.0.0.5", "1.2.3.4, 104.16.0.1", proto: "https, https", render: true);
        Assert.Equal("https", c.Request.Scheme);
    }

    [Theory]
    [InlineData("9.9.9.9, 104.16.0.2, 104.16.0.1", "104.16.0.2")] // bara ett Cloudflare-hopp: en Worker/WARP kan inte välja sin IP
    [InlineData("1.2.3.4, 104.16.0.1, 10.1.1.1", "1.2.3.4")]      // interna hopp efter Cloudflare
    [InlineData("skräp, 1.2.3.4, 104.16.0.1", "1.2.3.4")]         // skräp till vänster om klienten läses aldrig
    [InlineData("1.2.3.4:5678, 104.16.0.1", "1.2.3.4")]           // ip:port
    [InlineData("1.2.3.4 , , 104.16.0.1", "1.2.3.4")]             // mellanslag och tomma poster
    [InlineData("::ffff:1.2.3.4, 104.16.0.1", "1.2.3.4")]         // IPv4-mappad räknas som IPv4
    [InlineData("[2001:db8:1:2::7]:443, 2606:4700::1", "2001:db8:1:2::/64")]
    public async Task Render_tolkning_av_kedjan(string xff, string want)
    {
        var (ip, _) = await Run("10.0.0.5", xff, render: true);
        Assert.Equal(want, ip);
    }

    [Theory]
    [InlineData("1.2.3.4, skräp")]       // trasig post från en proxy: behåll anslutningens IP
    [InlineData("")]
    [InlineData("10.1.1.1, 100.64.0.9")] // bara interna hopp
    [InlineData("104.16.0.1")]           // bara ett Cloudflare-hopp
    public async Task Render_utan_klientadress_behålls_anslutningens_ip(string xff)
    {
        var (ip, _) = await Run("10.0.0.5", xff, render: true);
        Assert.Equal("10.0.0.5", ip);
    }

    [Theory]
    [InlineData("8.8.8.8", "1.2.3.4")]
    [InlineData("104.16.0.1", "1.2.3.4")]  // en Cloudflare-adress som ansluter direkt är ingen intern proxy
    [InlineData("100.128.0.1", "1.2.3.4")]
    public void Client_direktanslutning_ger_null(string remote, string xff) => Assert.Null(Net.Client(IPAddress.Parse(remote), xff));

    [Fact]
    public void Client_IPv4_mappad_klient_returneras_som_IPv4() =>
        Assert.Equal(IPAddress.Parse("1.2.3.4"), Net.Client(IPAddress.Parse("10.0.0.5"), "::ffff:1.2.3.4, 104.16.0.1"));

    [Fact]
    public void Client_IPv4_mappad_anslutning_räknas_som_intern() =>
        Assert.Equal(IPAddress.Parse("1.2.3.4"), Net.Client(IPAddress.Parse("::ffff:10.0.0.5"), "1.2.3.4, 104.16.0.1"));

    [Fact]
    public async Task Render_X_Forwarded_Host_används_aldrig()
    {
        var (_, c) = await Run("10.0.0.5", "1.2.3.4, 104.16.0.1", proto: "https", render: true, xfh: "evil.example");
        Assert.Equal("gurka.example", c.Request.Host.Value);
        var (_, d) = await Run("10.0.0.5", "1.2.3.4", xfh: "publik.example"); // utan Render: betrodd proxy får sätta värden, som förut
        Assert.Equal("publik.example", d.Request.Host.Value);
    }

    [Theory]
    [InlineData("https", "https")]
    [InlineData("http, https", "https")] // sista posten gäller
    [InlineData("https, http", "http")]
    [InlineData("ftp", "http")]          // okänt schema ignoreras
    public async Task Render_schema_från_sista_X_Forwarded_Proto(string proto, string want)
    {
        var (_, c) = await Run("10.0.0.5", "1.2.3.4, 104.16.0.1", proto: proto, render: true);
        Assert.Equal(want, c.Request.Scheme);
    }

    // --- Origin ---

    [Theory]
    [InlineData(null, "a.b", true)]
    [InlineData("", "a.b", true)]
    [InlineData("https://a.b", "a.b", true)]
    [InlineData("http://a.b:5000", "a.b:5000", true)]
    [InlineData("https://A.B", "a.b", true)]
    [InlineData("https://a.b", "A.B", true)] // Host-headern kan ha versaler
    [InlineData("https://evil.x", "a.b", false)]
    [InlineData("https://a.b.evil.x", "a.b", false)]
    [InlineData("https://a.b:444", "a.b", false)]
    [InlineData("http://a.b", "a.b:5000", false)]
    [InlineData("null", "a.b", false)]
    [InlineData("inte en url", "a.b", false)]
    [InlineData("ftp://a.b", "a.b", false)]
    [InlineData("file:///a.b", "a.b", false)]
    public void Samma_ursprung(string? origin, string host, bool want) => Assert.Equal(want, Net.SameOrigin(origin, host));

    // --- B13: tak per IPv6 /48 ---

    [Theory]
    [InlineData("2001:db8:1:2::/64", "2001:db8:1::/48")]
    [InlineData("2001:db8:1:ffff::/64", "2001:db8:1::/48")]
    [InlineData("2001:db8:2:2::/64", "2001:db8:2::/48")]
    [InlineData("1.2.3.4", "1.2.3.4")] // IPv4 oförändrad
    [InlineData("::1", "::1")]
    [InlineData("?", "?")]
    public void Wide_grupperar_IPv6_per_48(string key, string want) => Assert.Equal(want, Net.Wide(key));

    // --- PORT ---

    [Theory]
    [InlineData(null, 5000)]
    [InlineData("", 5000)]
    [InlineData("abc", 5000)]
    [InlineData("0", 5000)]
    [InlineData("-1", 5000)]
    [InlineData("70000", 5000)]
    [InlineData("65536", 5000)]
    [InlineData("1", 1)]
    [InlineData("10000", 10000)]
    [InlineData("65535", 65535)]
    public void Port_från_miljön(string? port, int want) => Assert.Equal(want, Net.Port(port));
}
