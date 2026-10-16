using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using ProxyNet = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

// Klientens riktiga IP bakom proxy (Codespaces port forwarding, Cloudflare-tunnel). Taken per IP i Lobby och Flood bygger på detta.
public static class Net
{
    // X-Forwarded-For/Proto/Host godtas bara från loopback och privata nät. Middleware går från höger i X-Forwarded-For
    // och stannar vid första adress som inte är betrodd, så en klient kan inte förfalska sin IP genom att skicka headern själv.
    public static void Proxies(ForwardedHeadersOptions o)
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost; // Host = den publika värden, så CSP pekar wss rätt
        o.ForwardLimit = 2; // t.ex. Cloudflare-tunnel + Codespaces port forwarding
        o.KnownNetworks.Clear(); o.KnownProxies.Clear();
        o.KnownProxies.Add(IPAddress.IPv6Loopback);
        foreach (var (a, n) in new[] { ("127.0.0.0", 8), ("10.0.0.0", 8), ("172.16.0.0", 12), ("192.168.0.0", 16) })
            o.KnownNetworks.Add(new ProxyNet(IPAddress.Parse(a), n));
    }

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
