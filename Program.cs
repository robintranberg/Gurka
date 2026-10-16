var b = WebApplication.CreateBuilder(args);
b.WebHost.UseUrls("http://0.0.0.0:5000"); // nås även från andra enheter i nätverket, t.ex. mobilen
b.Services.AddSignalR();
var app = b.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<GameHub>("/hub");
app.Run();
