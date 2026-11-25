using System;
using System.Collections.Generic;

namespace SnakeWebForms.Models
{
    [Serializable]
    public class GameStateDto
    {
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
        public List<GridPoint> Snake { get; set; }
        public GridPoint Food { get; set; }
        public GridPoint BonusFood { get; set; }
        public int BonusTicksRemaining { get; set; }
        public string Direction { get; set; }
        public int Score { get; set; }
        public int HighScore { get; set; }
        public int ApplesEaten { get; set; }
        public string Status { get; set; }
        public int TickMs { get; set; }
        public string PlayerName { get; set; }
        public string Difficulty { get; set; }
        public bool WrapWalls { get; set; }
        public bool LeaderboardUpdated { get; set; }
        public string Message { get; set; }
    }
}
