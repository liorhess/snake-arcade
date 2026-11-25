using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SnakeWebForms.Models;

namespace SnakeWebForms.Services
{
    /// <summary>
    /// Storage abstraction for leaderboard scores.
    /// Swap JsonLeaderboardRepository with a PostgreSQL/EF/Npgsql implementation in Program.cs when ready.
    /// </summary>
    public interface ILeaderboardRepository
    {
        Task<IReadOnlyList<ScoreEntry>> GetTopScoresAsync(int limit = 10, CancellationToken ct = default);
        Task<int> GetHighScoreAsync(CancellationToken ct = default);
        Task<bool> AddScoreAsync(ScoreEntry entry, CancellationToken ct = default);
        Task ClearAsync(CancellationToken ct = default);
    }
}
