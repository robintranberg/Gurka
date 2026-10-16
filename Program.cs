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
var app = b.Build();

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
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = types });
app.MapHub<GameHub>("/hub");
app.MapHealthChecks("/health");
app.Run();

public partial class Program { } // för WebApplicationFactory i integrationstesterna
