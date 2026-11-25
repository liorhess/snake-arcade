# Snake Arcade (ASP.NET Core .NET 10)

Cross-platform C# ASP.NET Core backend + HTML5 `<canvas>` Snake game, ready for Docker deployment on **Railway** or **Render** and custom domain **liorswebsite.com**.

## Run Locally

```bash
dotnet run --project SnakeWebForms/SnakeWebForms.csproj
```

Then open `http://localhost:8080`.

## API Endpoints

- `GET /health` — health check for Railway / Render
- `GET /api/game/state` — current session state
- `POST /api/game/start` — start a new game (`{ playerName, difficulty, wrapWalls }`)
- `POST /api/game/tick` — advance the C# game loop (`{ directions }`)
- `POST /api/game/pause` — toggle pause
- `GET /api/scores?limit=10` — top leaderboard scores
- `POST /api/scores` — record a score
- `DELETE /api/scores` — clear leaderboard

## Connecting a Database Later (e.g. PostgreSQL)

All score storage is isolated behind `ILeaderboardRepository` (`Services/ILeaderboardRepository.cs`), and `DatabaseOptions` already reads `DATABASE_URL` (or `ConnectionStrings:DefaultConnection`). To switch from the default JSON file store to PostgreSQL later, add your PostgreSQL repository implementation and register it in `Program.cs`.

## Deploy to Railway / Render + `liorswebsite.com`

1. Push this repo to GitHub.
2. In **Railway** or **Render**, create a new Web Service from the GitHub repo — it automatically detects `Dockerfile` and sets `PORT`.
3. Set the Health Check path to `/health`.
4. Add custom domain `liorswebsite.com` (and/or `www.liorswebsite.com`) in Railway/Render settings and copy the DNS `CNAME` / `A` records into your domain registrar. HTTPS certificates are provisioned automatically.
