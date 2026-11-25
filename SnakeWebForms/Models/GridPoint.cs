using System;

namespace SnakeWebForms.Models
{
    [Serializable]
    public class GridPoint : IEquatable<GridPoint>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridPoint()
        {
        }

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GridPoint);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }
    }
}
