using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SnakeWebForms.Models;
using SnakeWebForms.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind to cloud PORT environment variable (Railway / Render) when provided
string portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
}

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DatabaseOptions>();
// Future DB swap: register a PostgreSQL ILeaderboardRepository here when DatabaseOptions.IsDatabaseConfigured is true
builder.Services.AddSingleton<ILeaderboardRepository, JsonLeaderboardRepository>();
builder.Services.AddSingleton<GameSessionManager>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check for Railway / Render
app.MapGet("/health", (DatabaseOptions dbOptions) => Results.Ok(new
{
    status = "ok",
    storage = dbOptions.IsDatabaseConfigured ? "database-configured" : "json-file",
    utcTime = DateTime.UtcNow
}));

// --- Game Session API ---

var gameApi = app.MapGroup("/api/game");

gameApi.MapGet("/state", async (HttpContext ctx, GameSessionManager sessions, ILeaderboardRepository scores, CancellationToken ct) =>
{
    SnakeGameEngine engine = sessions.GetOrCreateEngine(ctx);
    int highScore = await scores.GetHighScoreAsync(ct);

    lock (engine.SyncRoot)
    {
        if (engine.Status == GameStatus.Running)
        {
            engine.TogglePause();
        }

        return Results.Ok(engine.ToDto(highScore, false));
    }
});

gameApi.MapPost("/start", async (StartGameRequest req, HttpContext ctx, GameSessionManager sessions, ILeaderboardRepository scores, CancellationToken ct) =>
{
    SnakeGameEngine engine = sessions.GetOrCreateEngine(ctx);
    int highScore = await scores.GetHighScoreAsync(ct);

    lock (engine.SyncRoot)
    {
        engine.Reset(req?.PlayerName, req?.Difficulty, req?.WrapWalls ?? false, startImmediately: true);
        return Results.Ok(engine.ToDto(highScore, false));
    }
});

gameApi.MapPost("/tick", async (TickRequest req, HttpContext ctx, GameSessionManager sessions, ILeaderboardRepository scores, CancellationToken ct) =>
{
    SnakeGameEngine engine = sessions.GetOrCreateEngine(ctx);
    ScoreEntry pendingScore = null;

    lock (engine.SyncRoot)
    {
        engine.QueueDirections(req?.Directions);
        engine.Tick();

        if ((engine.Status == GameStatus.GameOver || engine.Status == GameStatus.Won) && !engine.ScoreRecorded)
        {
            engine.ScoreRecorded = true;
            if (engine.Score > 0)
            {
                pendingScore = new ScoreEntry
                {
                    PlayerName = engine.PlayerName,
                    Score = engine.Score,
                    Apples = engine.ApplesEaten,
                    Difficulty = engine.Difficulty,
                    Mode = engine.WrapWalls ? "Wrap" : "Classic",
                    PlayedAtUtc = DateTime.UtcNow
                };
            }
        }
    }

    bool leaderboardUpdated = false;
    if (pendingScore != null)
    {
        leaderboardUpdated = await scores.AddScoreAsync(pendingScore, ct);
    }

    int highScore = await scores.GetHighScoreAsync(ct);

    lock (engine.SyncRoot)
    {
        return Results.Ok(engine.ToDto(highScore, leaderboardUpdated));
    }
});

gameApi.MapPost("/pause", async (HttpContext ctx, GameSessionManager sessions, ILeaderboardRepository scores, CancellationToken ct) =>
{
    SnakeGameEngine engine = sessions.GetOrCreateEngine(ctx);
    int highScore = await scores.GetHighScoreAsync(ct);

    lock (engine.SyncRoot)
    {
        engine.TogglePause();
        return Results.Ok(engine.ToDto(highScore, false));
    }
});

// --- Leaderboard / Scores API (DB-ready) ---

var scoresApi = app.MapGroup("/api/scores");

scoresApi.MapGet("/", async (int? limit, ILeaderboardRepository scores, CancellationToken ct) =>
{
    IReadOnlyList<ScoreEntry> top = await scores.GetTopScoresAsync(limit ?? 10, ct);
    int highScore = top.Count > 0 ? top[0].Score : 0;
    return Results.Ok(new
    {
        highScore,
        scores = top
    });
});

scoresApi.MapPost("/", async (CreateScoreRequest req, ILeaderboardRepository scores, CancellationToken ct) =>
{
    if (req == null || req.Score <= 0)
    {
        return Results.BadRequest(new { error = "Score must be greater than 0." });
    }

    var entry = new ScoreEntry
    {
        PlayerName = req.PlayerName,
        Score = req.Score,
        Apples = Math.Max(0, req.Apples),
        Difficulty = req.Difficulty,
        Mode = req.Mode,
        PlayedAtUtc = DateTime.UtcNow
    };

    await scores.AddScoreAsync(entry, ct);
    return Results.Created($"/api/scores/{entry.Id}", entry);
});

scoresApi.MapDelete("/", async (ILeaderboardRepository scores, CancellationToken ct) =>
{
    await scores.ClearAsync(ct);
    return Results.Ok(new { cleared = true, highScore = 0 });
});

app.Run();

public record StartGameRequest(string PlayerName, string Difficulty, bool WrapWalls);
public record TickRequest(string[] Directions);
public record CreateScoreRequest(string PlayerName, int Score, int Apples, string Difficulty, string Mode);
