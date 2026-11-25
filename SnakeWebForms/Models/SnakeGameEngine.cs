using System;
using System.Collections.Generic;
using System.Linq;

namespace SnakeWebForms.Models
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum GameStatus
    {
        Ready,
        Running,
        Paused,
        GameOver,
        Won
    }

    public class SnakeGameEngine
    {
        public const int DefaultWidth = 20;
        public const int DefaultHeight = 20;
        private const int PointsPerApple = 10;
        private const int PointsPerBonus = 30;
        private const int BonusDurationTicks = 35;

        private readonly Random _random = new Random();
        private readonly Queue<Direction> _directionQueue = new Queue<Direction>();
        private readonly List<GridPoint> _snake = new List<GridPoint>();

        public object SyncRoot { get; } = new object();

        public int GridWidth { get; }
        public int GridHeight { get; }
        public Direction CurrentDirection { get; private set; }
        public GridPoint Food { get; private set; }
        public GridPoint BonusFood { get; private set; }
        public int BonusTicksRemaining { get; private set; }
        public int Score { get; private set; }
        public int ApplesEaten { get; private set; }
        public GameStatus Status { get; private set; }
        public string PlayerName { get; private set; }
        public string Difficulty { get; private set; }
        public bool WrapWalls { get; private set; }
        public string LastMessage { get; private set; }
        public bool ScoreRecorded { get; set; }

        public SnakeGameEngine(int width = DefaultWidth, int height = DefaultHeight)
        {
            GridWidth = width;
            GridHeight = height;
            Reset("Player", "Normal", false, startImmediately: false);
        }

        public void Reset(string playerName, string difficulty, bool wrapWalls, bool startImmediately)
        {
            PlayerName = NormalizePlayerName(playerName);
            Difficulty = NormalizeDifficulty(difficulty);
            WrapWalls = wrapWalls;

            _snake.Clear();
            _directionQueue.Clear();

            int startX = GridWidth / 2;
            int startY = GridHeight / 2;
            _snake.Add(new GridPoint(startX, startY));
            _snake.Add(new GridPoint(startX - 1, startY));
            _snake.Add(new GridPoint(startX - 2, startY));

            CurrentDirection = Direction.Right;
            Score = 0;
            ApplesEaten = 0;
            BonusFood = null;
            BonusTicksRemaining = 0;
            ScoreRecorded = false;
            Status = startImmediately ? GameStatus.Running : GameStatus.Ready;
            LastMessage = startImmediately ? "Go! Eat apples to grow." : "Press New Game or an arrow key to start.";

            Food = SpawnEmptyCell();
        }

        public void QueueDirections(IEnumerable<string> directionNames)
        {
            if (directionNames == null)
            {
                return;
            }

            foreach (string raw in directionNames)
            {
                if (!Enum.TryParse(raw, true, out Direction next))
                {
                    continue;
                }

                if (_directionQueue.Count >= 3)
                {
                    break;
                }

                Direction referenceDir = _directionQueue.Count > 0
                    ? _directionQueue.Last()
                    : CurrentDirection;

                if (next == referenceDir || IsOpposite(referenceDir, next))
                {
                    continue;
                }

                _directionQueue.Enqueue(next);
            }
        }

        public void TogglePause()
        {
            if (Status == GameStatus.Running)
            {
                Status = GameStatus.Paused;
                LastMessage = "Paused — press Space or Resume to continue.";
            }
            else if (Status == GameStatus.Paused)
            {
                Status = GameStatus.Running;
                LastMessage = "Resumed!";
            }
        }

        public void Tick()
        {
            if (Status != GameStatus.Running)
            {
                return;
            }

            if (_directionQueue.Count > 0)
            {
                CurrentDirection = _directionQueue.Dequeue();
            }

            GridPoint head = _snake[0];
            GridPoint nextHead = StepPoint(head, CurrentDirection);

            if (WrapWalls)
            {
                nextHead.X = (nextHead.X + GridWidth) % GridWidth;
                nextHead.Y = (nextHead.Y + GridHeight) % GridHeight;
            }
            else if (nextHead.X < 0 || nextHead.X >= GridWidth || nextHead.Y < 0 || nextHead.Y >= GridHeight)
            {
                Status = GameStatus.GameOver;
                LastMessage = "Ouch! You hit the wall.";
                return;
            }

            bool eatingFood = Food != null && nextHead.Equals(Food);
            bool eatingBonus = BonusFood != null && nextHead.Equals(BonusFood);
            bool willGrow = eatingFood || eatingBonus;

            // Check self-collision (tail cell is safe if the snake is not growing this tick)
            int checkLength = willGrow ? _snake.Count : _snake.Count - 1;
            for (int i = 0; i < checkLength; i++)
            {
                if (_snake[i].Equals(nextHead))
                {
                    Status = GameStatus.GameOver;
                    LastMessage = "Game over! You ran into yourself.";
                    return;
                }
            }

            _snake.Insert(0, nextHead);
            if (!willGrow)
            {
                _snake.RemoveAt(_snake.Count - 1);
            }

            // Expire bonus apple timer
            if (BonusFood != null && !eatingBonus)
            {
                BonusTicksRemaining--;
                if (BonusTicksRemaining <= 0)
                {
                    BonusFood = null;
                    BonusTicksRemaining = 0;
                }
            }

            if (eatingFood)
            {
                ApplesEaten++;
                Score += PointsPerApple;
                LastMessage = "Yum! +" + PointsPerApple + " pts";

                if (_snake.Count >= GridWidth * GridHeight)
                {
                    Food = null;
                    BonusFood = null;
                    Status = GameStatus.Won;
                    LastMessage = "Unbelievable! You filled the entire board!";
                    return;
                }

                Food = SpawnEmptyCell();

                if (ApplesEaten % 5 == 0 && BonusFood == null)
                {
                    BonusFood = SpawnEmptyCell();
                    if (BonusFood != null)
                    {
                        BonusTicksRemaining = BonusDurationTicks;
                        LastMessage = "Golden Apple appeared! Grab it for +" + PointsPerBonus + " pts!";
                    }
                }
            }
            else if (eatingBonus)
            {
                Score += PointsPerBonus;
                BonusFood = null;
                BonusTicksRemaining = 0;
                LastMessage = "Golden Apple! +" + PointsPerBonus + " pts!";
            }
        }

        public int GetTickIntervalMs()
        {
            int baseMs;
            int minMs;

            switch (Difficulty)
            {
                case "Easy":
                    baseMs = 185;
                    minMs = 115;
                    break;
                case "Hard":
                    baseMs = 95;
                    minMs = 58;
                    break;
                case "Insane":
                    baseMs = 70;
                    minMs = 46;
                    break;
                default:
                    baseMs = 135;
                    minMs = 78;
                    break;
            }

            int speedReduction = ApplesEaten * 2;
            return Math.Max(minMs, baseMs - speedReduction);
        }

        public GameStateDto ToDto(int highScore, bool leaderboardUpdated)
        {
            return new GameStateDto
            {
                GridWidth = GridWidth,
                GridHeight = GridHeight,
                Snake = _snake.Select(p => new GridPoint(p.X, p.Y)).ToList(),
                Food = Food == null ? null : new GridPoint(Food.X, Food.Y),
                BonusFood = BonusFood == null ? null : new GridPoint(BonusFood.X, BonusFood.Y),
                BonusTicksRemaining = BonusTicksRemaining,
                Direction = CurrentDirection.ToString(),
                Score = Score,
                HighScore = Math.Max(highScore, Score),
                ApplesEaten = ApplesEaten,
                Status = Status.ToString(),
                TickMs = GetTickIntervalMs(),
                PlayerName = PlayerName,
                Difficulty = Difficulty,
                WrapWalls = WrapWalls,
                LeaderboardUpdated = leaderboardUpdated,
                Message = LastMessage
            };
        }

        private GridPoint SpawnEmptyCell()
        {
            var occupied = new HashSet<GridPoint>(_snake);
            if (Food != null)
            {
                occupied.Add(Food);
            }

            if (BonusFood != null)
            {
                occupied.Add(BonusFood);
            }

            var freeCells = new List<GridPoint>(GridWidth * GridHeight - occupied.Count);
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    var candidate = new GridPoint(x, y);
                    if (!occupied.Contains(candidate))
                    {
                        freeCells.Add(candidate);
                    }
                }
            }

            if (freeCells.Count == 0)
            {
                return null;
            }

            return freeCells[_random.Next(freeCells.Count)];
        }

        private static GridPoint StepPoint(GridPoint p, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:
                    return new GridPoint(p.X, p.Y - 1);
                case Direction.Down:
                    return new GridPoint(p.X, p.Y + 1);
                case Direction.Left:
                    return new GridPoint(p.X - 1, p.Y);
                default:
                    return new GridPoint(p.X + 1, p.Y);
            }
        }

        private static bool IsOpposite(Direction a, Direction b)
        {
            return (a == Direction.Up && b == Direction.Down)
                || (a == Direction.Down && b == Direction.Up)
                || (a == Direction.Left && b == Direction.Right)
                || (a == Direction.Right && b == Direction.Left);
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

        private static string NormalizeDifficulty(string difficulty)
        {
            switch ((difficulty ?? string.Empty).Trim())
            {
                case "Easy":
                case "Normal":
                case "Hard":
                case "Insane":
                    return difficulty.Trim();
                default:
                    return "Normal";
            }
        }
    }
}
