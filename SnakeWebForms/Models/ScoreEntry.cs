using System;

namespace SnakeWebForms.Models
{
    public class ScoreEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string PlayerName { get; set; } = "Player";
        public int Score { get; set; }
        public int Apples { get; set; }
        public string Difficulty { get; set; } = "Normal";
        public string Mode { get; set; } = "Classic";
        public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
