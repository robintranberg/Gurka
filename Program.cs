var b = WebApplication.CreateBuilder(args);
b.Services.AddSignalR();
var app = b.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<GameHub>("/hub");
app.Run();
