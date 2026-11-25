using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using SnakeWebForms.Models;

namespace SnakeWebForms.Services
{
    public class JsonLeaderboardRepository : ILeaderboardRepository
    {
        private const int MaxStoredEntries = 50;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _filePath;

        public JsonLeaderboardRepository(DatabaseOptions options, IWebHostEnvironment env)
        {
            string baseDir = Path.IsPathRooted(options.DataDir)
                ? options.DataDir
                : Path.Combine(env.ContentRootPath, options.DataDir);

            Directory.CreateDirectory(baseDir);
            _filePath = Path.Combine(baseDir, "leaderboard.json");
        }

        public async Task<IReadOnlyList<ScoreEntry>> GetTopScoresAsync(int limit = 10, CancellationToken ct = default)
        {
            int safeLimit = Math.Clamp(limit, 1, MaxStoredEntries);
            await _lock.WaitAsync(ct);
            try
            {
                List<ScoreEntry> entries = await ReadUnsafeAsync(ct);
                return entries
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.Apples)
                    .ThenBy(e => e.PlayedAtUtc)
                    .Take(safeLimit)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<int> GetHighScoreAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ScoreEntry> top = await GetTopScoresAsync(1, ct);
            return top.Count > 0 ? top[0].Score : 0;
        }

        public async Task<bool> AddScoreAsync(ScoreEntry entry, CancellationToken ct = default)
        {
            if (entry == null || entry.Score <= 0)
            {
                return false;
            }

            entry.PlayerName = NormalizePlayerName(entry.PlayerName);
            entry.Difficulty = string.IsNullOrWhiteSpace(entry.Difficulty) ? "Normal" : entry.Difficulty.Trim();
            entry.Mode = string.IsNullOrWhiteSpace(entry.Mode) ? "Classic" : entry.Mode.Trim();
            if (entry.PlayedAtUtc == default)
            {
                entry.PlayedAtUtc = DateTime.UtcNow;
            }

            await _lock.WaitAsync(ct);
            try
            {
                List<ScoreEntry> entries = await ReadUnsafeAsync(ct);
                entries.Add(entry);

                List<ScoreEntry> trimmed = entries
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.Apples)
                    .ThenBy(e => e.PlayedAtUtc)
                    .Take(MaxStoredEntries)
                    .ToList();

                await WriteUnsafeAsync(trimmed, ct);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await WriteUnsafeAsync(new List<ScoreEntry>(), ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<List<ScoreEntry>> ReadUnsafeAsync(CancellationToken ct)
        {
            if (!File.Exists(_filePath))
            {
                return new List<ScoreEntry>();
            }

            try
            {
                using FileStream stream = File.OpenRead(_filePath);
                List<ScoreEntry> data = await JsonSerializer.DeserializeAsync<List<ScoreEntry>>(stream, JsonOptions, ct);
                return data ?? new List<ScoreEntry>();
            }
            catch
            {
                return new List<ScoreEntry>();
            }
        }

        private async Task WriteUnsafeAsync(List<ScoreEntry> entries, CancellationToken ct)
        {
            string tempPath = _filePath + ".tmp";
            using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, ct);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }

        private static string NormalizePlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Player";
            }

            string trimmed = name.Trim();
            return trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
        }
    }
}
