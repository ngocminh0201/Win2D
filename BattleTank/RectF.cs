using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Win2D.BattleTank
{
    public readonly struct RectF
    {
        public readonly float X, Y, W, H;

        public float Right => X + W;
        public float Bottom => Y + H;
        public Vector2 Center => new(X + W * 0.5f, Y + H * 0.5f);

        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public bool Contains(Vector2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

        public bool Intersects(RectF b)
            => !(b.X >= Right || b.Right <= X || b.Y >= Bottom || b.Bottom <= Y);
    }
}
