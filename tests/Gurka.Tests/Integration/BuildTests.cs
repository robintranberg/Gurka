using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Gurka.Tests.Integration;

// Build är statisk och WebApplicationFactory anropar Build.Init vid start, så alla tester som rör Build eller startar servern
// ligger i samma collection och körs efter varandra.
[CollectionDefinition("Server")]
public class ServerCollection { }

// Versionshandslag: Boot = serverns källfiler (*.cs, *.csproj direkt i content root) när processen startade, Now = samma filer på disken nu.
// Olika betyder att servern behöver startas om. wwwroot ingår inte, eftersom den läses från disken utan omstart.
[Collection("Server")]
public class BuildTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "gurka-build-" + Guid.NewGuid().ToString("N"));
    string Web => Path.Combine(root, "wwwroot");
    static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    int tick;

    public BuildTests()
    {
        Directory.CreateDirectory(Web);
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        Write("wwwroot/index.html", "<!doctype html>");
        Write("wwwroot/app.js", "let a = 1;");
        Write("wwwroot/app.css", "body{}");
        Write("wwwroot/sw.js", "self.x = 1;");
        Write("Game.cs", "class Game {}");
        Write("Lobby.cs", "class Lobby {}");
        Write("Gurka.csproj", "<Project />");
        Write("sub/Extra.cs", "class Extra {}");
        Write("notes.txt", "hej");
    }

    public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } }

    // skriver filen och sätter en ny, unik tid så att cachen i Build.Now inte döljer ändringen
    void Write(string rel, string text)
    {
        var p = Path.Combine(root, rel);
        File.WriteAllText(p, text);
        File.SetLastWriteTimeUtc(p, T0.AddMinutes(++tick));
    }

    void Init() => Build.Init(root);

    [Fact]
    public void Efter_Init_är_Now_lika_med_Boot_och_tolv_hextecken()
    {
        Init();
        Assert.Matches("^[0-9a-f]{12}$", Build.Boot);
        Assert.Equal(Build.Boot, Build.Now());
    }

    [Theory]
    [InlineData("Game.cs", "class Gamf {}")]   // samma längd
    [InlineData("Lobby.cs", "class Lobby { }")]
    [InlineData("Gurka.csproj", "<Project></Project>")]
    public void Ändrad_fil_ger_ny_Now_men_Boot_står_kvar(string file, string text)
    {
        Init();
        var boot = Build.Boot;
        Write(file, text);
        Assert.NotEqual(boot, Build.Now());
        Assert.Matches("^[0-9a-f]{12}$", Build.Now());
        Assert.Equal(boot, Build.Boot);
    }

    [Fact]
    public void Återställt_innehåll_med_ny_tid_ger_Boot_igen()
    {
        Init();
        Write("Game.cs", "class Gamf {}");
        Assert.NotEqual(Build.Boot, Build.Now());
        Write("Game.cs", "class Game {}");
        Assert.Equal(Build.Boot, Build.Now());
    }

    [Theory]
    [InlineData("wwwroot/app.js", "let a = 2;")]
    [InlineData("wwwroot/index.html", "<!doctype HTML>")]
    [InlineData("wwwroot/app.css", "body{color:red}")]
    [InlineData("wwwroot/sw.js", "self.x = 2;")]
    [InlineData("sub/Extra.cs", "class Extra { int x; }")]
    [InlineData("notes.txt", "hej då")]
    public void Filer_utanför_listan_påverkar_inte(string file, string text)
    {
        Init();
        Write(file, text);
        Assert.Equal(Build.Boot, Build.Now());
    }

    [Fact]
    public void Filgränsen_ingår_så_att_ett_flyttat_tecken_ger_ny_hash()
    {
        // filerna hashas i ordningen Game.cs, Gurka.csproj: utan filnamn mellan dem blir den hopslagna texten densamma
        Init();
        Write("Game.cs", "class Game {");
        Write("Gurka.csproj", "}<Project />");
        Assert.NotEqual(Build.Boot, Build.Now());
    }

    [Fact]
    public void Borttagen_fil_räknas_som_tom()
    {
        Init();
        File.Delete(Path.Combine(root, "Lobby.cs"));
        var gone = Build.Now();
        Assert.NotEqual(Build.Boot, gone);
        Write("Lobby.cs", "");
        Assert.Equal(gone, Build.Now()); // tom fil = borttagen fil
        Write("Lobby.cs", "class Lobby {}");
        Assert.Equal(Build.Boot, Build.Now());
    }

    [Fact]
    public void Olika_innehåll_ger_olika_Boot()
    {
        Init();
        var a = Build.Boot;
        Write("Game.cs", "class Game { int x; }");
        Init();
        Assert.NotEqual(a, Build.Boot);
        Assert.Equal(Build.Boot, Build.Now());
    }

    [Fact]
    public void Varningen_loggas_en_gång_per_Init()
    {
        var log = new CountLog();
        Init();
        Build.Warn(log); Build.Warn(log);
        Assert.Equal(1, log.Warnings);
        Init();
        Build.Warn(log);
        Assert.Equal(2, log.Warnings);
    }

    // --- via servern ---

    static readonly Regex Js = new("^window\\.GURKA_BUILD=\"([0-9a-f]{12})\";$");

    [Fact]
    public async Task Build_js_har_Now_och_no_store()
    {
        await using var f = new WebApplicationFactory<Program>();
        var http = f.CreateClient();
        var r = await http.GetAsync("/build.js");
        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal("text/javascript; charset=utf-8", r.Content.Headers.ContentType!.ToString());
        Assert.Equal("no-store", r.Headers.CacheControl!.ToString());
        var body = await r.Content.ReadAsStringAsync();
        Assert.Equal($"window.GURKA_BUILD=\"{Build.Now()}\";", body);
        Assert.Matches(Js, body);
        Assert.Equal(Build.Boot, Js.Match(body).Groups[1].Value); // inga filer har ändrats sedan servern startade
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/app.js")]
    [InlineData("/app.css")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/lib/signalr.min.js")]
    public async Task Klientfiler_valideras_alltid_om(string path)
    {
        await using var f = new WebApplicationFactory<Program>();
        var r = await f.CreateClient().GetAsync(path);
        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal("no-cache", r.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Hello_skickar_Boot()
    {
        await using var f = new WebApplicationFactory<Program>();
        await using var c = await HubClient.ConnectAsync(f);
        var room = await c.Conn.InvokeAsync<string?>("NewRoom");
        Init(); // peka om Build på testmappen och ändra en fil, så att Boot och Now skiljer sig
        Write("Game.cs", "class Gamf {}");
        Assert.NotEqual(Build.Boot, Build.Now());
        var s = await c.Conn.InvokeAsync<JsonElement>("Hello", null, room);
        Assert.Equal(Build.Boot, s.GetProperty("build").GetString()); // när processen startade, inte filerna nu
        Assert.Matches("^[0-9a-f]{12}$", Build.Boot);
    }

    [Fact]
    public async Task Oförändrad_server_ger_samma_build_i_Hello_och_build_js()
    {
        // klienten laddar om när värdena skiljer sig, så en nystartad server måste ge exakt samma sträng i båda
        await using var f = new WebApplicationFactory<Program>();
        var js = await f.CreateClient().GetStringAsync("/build.js");
        await using var c = await HubClient.ConnectAsync(f);
        var room = await c.Conn.InvokeAsync<string?>("NewRoom");
        var s = await c.Conn.InvokeAsync<JsonElement>("Hello", null, room);
        Assert.Equal(Js.Match(js).Groups[1].Value, s.GetProperty("build").GetString());
    }

    [Fact]
    public async Task Ändrade_filer_syns_i_build_js_och_varnar_en_gång()
    {
        var log = new CountLog();
        await using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(new CountLogProvider(log))));
        var http = f.CreateClient();
        await http.GetAsync("/build.js"); // servern är startad och har gjort sin Init
        Init(); // peka om Build på testmappen
        var boot = Build.Boot;
        Assert.Equal($"window.GURKA_BUILD=\"{boot}\";", await http.GetStringAsync("/build.js"));
        Assert.Equal(0, log.Warnings);
        Write("Game.cs", "class Gamf {}");
        var now = Build.Now();
        Assert.NotEqual(boot, now);
        Assert.Equal($"window.GURKA_BUILD=\"{now}\";", await http.GetStringAsync("/build.js"));
        await http.GetStringAsync("/build.js");
        Assert.Equal(1, log.Warnings);
    }

    sealed class CountLog : ILogger
    {
        int n;
        public int Warnings => n;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
        {
            if (level == LogLevel.Warning && fmt(state, ex).Contains("starta om servern")) Interlocked.Increment(ref n);
        }
    }

    sealed class CountLogProvider(CountLog log) : ILoggerProvider
    {
        public ILogger CreateLogger(string name) => log;
        public void Dispose() { }
    }
}
