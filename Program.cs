using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;

var b = WebApplication.CreateBuilder(args);
// standard: nås även från andra enheter i nätverket, t.ex. mobilen. --urls och ASPNETCORE_URLS går före.
if (string.IsNullOrEmpty(b.Configuration["urls"])) b.WebHost.UseUrls("http://0.0.0.0:5000");
b.Services.AddSingleton<Lobby>();
b.Services.AddHostedService<Sweeper>();
b.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = 4 * 1024; // spelets anrop är små; stoppar stora meddelanden
    o.MaximumParallelInvocationsPerClient = 1;
    o.KeepAliveInterval = TimeSpan.FromSeconds(10);
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    o.EnableDetailedErrors = false;
    o.AddFilter(new Flood());
});
b.Services.AddHealthChecks();
b.Services.Configure<ForwardedHeadersOptions>(Net.Proxies);
var app = b.Build();
Build.Init(app.Environment.ContentRootPath);

// bakom Codespaces/Cloudflare: klientens IP och https från betrodda proxyer (se Net.cs). Måste ligga först.
app.Use(Net.Cloudflare(app.Configuration.GetValue<bool>("Gurka:Cloudflare")));
app.UseForwardedHeaders();

// säkerhetsheaders; skript och stil bara från egna filer (klienten sätter stil via CSSOM, inga style-attribut)
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers; var host = ctx.Request.Host.Value ?? "";
    var ws = System.Text.RegularExpressions.Regex.IsMatch(host, @"^[A-Za-z0-9.\-:\[\]]+$") ? $" ws://{host} wss://{host}" : ""; // äldre Safari räknar inte ws till 'self'
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "SAMEORIGIN"; // frontend testar smala bredder i iframe
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    h["Content-Security-Policy"] = $"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'{ws}; manifest-src 'self'; worker-src 'self'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'self'";
    await next();
});
app.UseDefaultFiles();
var types = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
types.Mappings[".webmanifest"] = "application/manifest+json";
// klientfilerna valideras alltid om (ETag), så webbläsaren aldrig kör en gammal cachad app.js
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = types, OnPrepareResponse = c =>
{
    if (Path.GetExtension(c.File.Name) is ".html" or ".js" or ".css" or ".webmanifest") c.Context.Response.Headers.CacheControl = "no-cache";
} });
// versionshandslag: build.js = serverns källfiler på disken nu, Hello.build = när processen startade. Olika => servern behöver startas om.
app.MapGet("/build.js", (HttpContext ctx, ILogger<Program> log) =>
{
    var n = Build.Now(); if (n != Build.Boot) Build.Warn(log);
    ctx.Response.Headers.CacheControl = "no-store";
    return Results.Text($"window.GURKA_BUILD=\"{n}\";", "text/javascript; charset=utf-8");
});
app.MapHub<GameHub>("/hub");
app.MapHealthChecks("/health");
app.Run();

public partial class Program { } // för WebApplicationFactory i integrationstesterna

// byggidentitet: SHA-256 (12 hex) över serverns källfiler – *.cs och *.csproj direkt i content root (inte tests/, bin/, obj/), finns bara vid dotnet run.
// wwwroot ingår inte: den läses från disken och kräver ingen omstart. Boot räknas när processen startar; Now räknas om när någon fil
// ändrats (storlek/tid). En gammal process mot ny serverkod syns som Boot != Now.
public static class Build
{
    static readonly object L = new();
    static string[] files = Array.Empty<string>();
    static string? key; static string now = "";
    static bool warned;
    public static string Boot { get; private set; } = "";

    public static void Init(string contentRoot)
    {
        lock (L)
        {
            files = !Directory.Exists(contentRoot) ? Array.Empty<string>() // bara översta nivån
                : Directory.GetFiles(contentRoot, "*.cs").Concat(Directory.GetFiles(contentRoot, "*.csproj")).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            key = null; warned = false;
            Boot = Now();
        }
    }

    public static string Now()
    {
        lock (L)
        {
            try
            {
                var k = string.Join("|", files.Select(f => { var i = new FileInfo(f); return i.Exists ? $"{i.Length}:{i.LastWriteTimeUtc.Ticks}" : "-"; }));
                if (k == key) return now;
                using var h = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                foreach (var f in files) { h.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(f) + "|")); if (File.Exists(f)) h.AppendData(File.ReadAllBytes(f)); }
                now = Convert.ToHexString(h.GetHashAndReset())[..12].ToLowerInvariant(); key = k;
            }
            catch (IOException) { } // filen skrivs just nu: behåll senaste värdet och försök igen nästa gång
            catch (UnauthorizedAccessException) { }
            return now;
        }
    }

    public static void Warn(ILogger log)
    {
        lock (L) { if (warned) return; warned = true; }
        log.LogWarning("Filerna har ändrats sedan servern startade – starta om servern (dotnet run).");
    }
}
