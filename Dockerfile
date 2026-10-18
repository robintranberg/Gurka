# Gurka i en container (t.ex. Render). Bygg: docker build -t gurka .  Kör: docker run -e PORT=10000 -p 10000:10000 gurka
# Steg 1: publicera servern med SDK. Testerna (tests/) ingår inte, se .dockerignore och Gurka.csproj.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Gurka.csproj ./
RUN dotnet restore Gurka.csproj
COPY . ./
RUN dotnet publish Gurka.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Steg 2: bara runtime. Kör som användaren app (icke-root, finns i aspnet-imagen).
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
# servern lyssnar på 0.0.0.0:$PORT (Program.cs). Render sätter PORT; 10000 är Renders standard.
# ASPNETCORE_HTTP_PORTS töms så att imagens 8080 inte krockar med PORT.
# Loggen visar bara varningar från ASP.NET (annars en rad per anrop, även Renders hälsokoll).
ENV PORT=10000 \
    ASPNETCORE_HTTP_PORTS= \
    DOTNET_EnableDiagnostics=0 \
    Logging__LogLevel__Default=Information \
    Logging__LogLevel__Microsoft.AspNetCore=Warning
USER app
EXPOSE 10000
ENTRYPOINT ["dotnet", "Gurka.dll"]
