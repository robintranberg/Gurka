using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using ProxyNet = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

// Klientens riktiga IP bakom proxy (Codespaces port forwarding, Cloudflare-tunnel, Render). Taken per IP i Lobby och Flood bygger på detta.
public static class Net
{
    // X-Forwarded-For/Proto/Host godtas bara från loopback och privata nät. Middleware går från höger i X-Forwarded-For
    // och stannar vid första adress som inte är betrodd, så en klient kan inte förfalska sin IP genom att skicka headern själv.
    public static void Proxies(ForwardedHeadersOptions o) => Proxies(o, false);

    // Render (Gurka:Render=true): trafiken går klient -> Cloudflare -> Renders interna proxy (10.x) -> appen, och båda lägger till i
    // X-Forwarded-For utan att rensa det klienten skickat. Antalet interna hopp är inte dokumenterat, så Client() hoppar över interna
    // adresser och högst ETT Cloudflare-hopp. Fler Cloudflare-adresser godtas inte: en Cloudflare Worker eller WARP utgår från
    // Cloudflares nät och skulle annars kunna välja sin IP själv. Blir det fel hamnar vi på den strikta sidan (en proxy-IP).
    static readonly ProxyNet[] Hops = Nets("127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "100.64.0.0/10", "::1/128");
    static readonly ProxyNet[] Cf = Nets(
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20",
        "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13",
        "131.0.72.0/22", "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32");
    static ProxyNet[] Nets(params string[] s) => s.Select(c => c.Split('/')).Select(p => new ProxyNet(IPAddress.Parse(p[0]), int.Parse(p[1]))).ToArray();
    static IPAddress V4(IPAddress a) => a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;
    static bool In(ProxyNet[] n, IPAddress a) { a = V4(a); return n.Any(x => x.Contains(a)); }

    public static void Proxies(ForwardedHeadersOptions o, bool render)
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost; // Host = den publika värden, så CSP pekar wss rätt
        o.ForwardLimit = 2; // t.ex. Cloudflare-tunnel + Codespaces port forwarding
        o.KnownNetworks.Clear(); o.KnownProxies.Clear();
        o.KnownProxies.Add(IPAddress.IPv6Loopback);
        foreach (var (a, n) in new[] { ("127.0.0.0", 8), ("10.0.0.0", 8), ("172.16.0.0", 12), ("192.168.0.0", 16) })
            o.KnownNetworks.Add(new ProxyNet(IPAddress.Parse(a), n));
        if (render) o.ForwardedHeaders = ForwardedHeaders.None; // Render() sköter IP och schema; X-Forwarded-Host behövs inte (Host är redan den publika)
    }

    // klientens IP bakom Render: från höger i X-Forwarded-For, förbi interna hopp och högst ett Cloudflare-hopp. null = behåll anslutningens IP
    public static IPAddress? Client(IPAddress remote, string? xff)
    {
        if (!In(Hops, remote)) return null; // direktanslutning: ingen proxy att lita på
        var p = (xff ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries); bool cf = false;
        for (int i = p.Length - 1; i >= 0; i--)
        {
            if (!IPEndPoint.TryParse(p[i], out var e)) return null; // trasig kedja från en proxy: var hellre strikt
            var a = V4(e.Address);
            if (In(Hops, a)) continue;
            if (!cf && In(Cf, a)) { cf = true; continue; }
            return a;
        }
        return null;
    }

    public static Func<HttpContext, Func<Task>, Task> Render(bool on) => (c, next) =>
    {
        var ip = c.Connection.RemoteIpAddress;
        if (on && ip != null && In(Hops, ip))
        {
            if (Client(ip, c.Request.Headers["X-Forwarded-For"].ToString()) is { } real) c.Connection.RemoteIpAddress = real;
            var pr = c.Request.Headers["X-Forwarded-Proto"].ToString().Split(',', StringSplitOptions.TrimEntries).LastOrDefault();
            if (pr is "http" or "https") c.Request.Scheme = pr;
        }
        return next();
    };

    // Origin från webbläsaren måste peka på samma värd (host[:port]) som anropet; saknad Origin = inte en webbläsare
    public static bool SameOrigin(string? origin, string host) =>
        string.IsNullOrEmpty(origin) || Uri.TryCreate(origin, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")
            && string.Equals(u.Authority, host, StringComparison.OrdinalIgnoreCase);

    // porten när ingen urls är angiven: PORT (Render sätter den, standard 10000) om den är ett giltigt portnummer, annars 5000
    public static int Port(string? port) => int.TryParse(port, out var p) && p is > 0 and < 65536 ? p : 5000;

    // CF-Connecting-IP sätts av Cloudflare och skrivs över om klienten skickar den. Men via en annan proxy (t.ex. Codespaces)
    // kommer också trafiken från loopback och headern kan förfalskas – därför bara när Gurka:Cloudflare=true (GURKA__CLOUDFLARE=true).
    public static Func<HttpContext, Func<Task>, Task> Cloudflare(bool on) => (c, next) =>
    {
        var ip = c.Connection.RemoteIpAddress;
        if (on && ip != null && IPAddress.IsLoopback(ip) && IPAddress.TryParse(c.Request.Headers["CF-Connecting-IP"].ToString(), out var real))
        {
            c.Connection.RemoteIpAddress = real;
            c.Request.Headers.Remove("X-Forwarded-For"); // redan avgjort; tunnelns egen kedja ska inte tolkas igen
        }
        return next();
    };

    // bredare nyckel för tak: en IPv6-nyckel (/64) blir dess /48, allt annat är oförändrat
    public static string Wide(string key)
    {
        if (!key.EndsWith("/64") || !IPAddress.TryParse(key[..^3], out var a)) return key;
        var b = a.GetAddressBytes(); Array.Clear(b, 6, 10);
        return new IPAddress(b) + "/48";
    }

    // nyckel för tak per IP: IPv4 som den är, IPv6 per /64 (en värd har ofta många adresser i samma nät)
    public static string Key(IPAddress? ip)
    {
        if (ip == null) return "?";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6 || IPAddress.IsLoopback(ip)) return ip.ToString();
        var b = ip.GetAddressBytes(); Array.Clear(b, 8, 8);
        return new IPAddress(b) + "/64";
    }
}
