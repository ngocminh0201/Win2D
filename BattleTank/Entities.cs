using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Numerics;
using Windows.UI;

namespace Win2D.BattleTank
{
    public enum Team { Player, Enemy }

    public enum TankDir { Up, Right, Down, Left }

    public static class TankDirExt
    {
        public static Vector2 ToUnit(this TankDir d) => d switch
        {
            TankDir.Up => new Vector2(0, -1),
            TankDir.Right => new Vector2(1, 0),
            TankDir.Down => new Vector2(0, 1),
            _ => new Vector2(-1, 0),
        };

        public static TankDir FromVector(Vector2 v)
        {
            if (MathF.Abs(v.X) > MathF.Abs(v.Y))
                return v.X >= 0 ? TankDir.Right : TankDir.Left;
            else
                return v.Y >= 0 ? TankDir.Down : TankDir.Up;
        }
    }

    public static class EnemyColors
    {
        // Brighter + varied, but still "tank-like".
        public static readonly Color[] Palette =
        {
            Color.FromArgb(255, 235,  90,  90), // red
            Color.FromArgb(255, 255, 165,  70), // orange
            Color.FromArgb(255, 255, 235,  90), // yellow
            Color.FromArgb(255,  90, 220, 120), // green
            Color.FromArgb(255,  80, 210, 255), // cyan
            Color.FromArgb(255, 120, 150, 255), // blue
            Color.FromArgb(255, 190, 120, 255), // purple
            Color.FromArgb(255, 255, 120, 210), // pink
        };

        public static Color Pick(Random rng) => Palette[rng.Next(Palette.Length)];

        public static Color AccentFor(Color body)
        {
            // A soft bright accent that reads well on dark bg.
            // (not too white to keep "material" look)
            byte r = (byte)Math.Min(255, body.R + 55);
            byte g = (byte)Math.Min(255, body.G + 55);
            byte b = (byte)Math.Min(255, body.B + 55);
            return Color.FromArgb(220, r, g, b);
        }
    }

    public struct Tank
    {
        public static float Size => 22f;
        public static float Half => Size * 0.5f;

        private static int _nextId;
        public int Id;

        public bool Alive;
        public bool IsPlayer;
        public Team Team;

        public Vector2 Pos;   // center
        public Vector2 Vel;
        public TankDir Dir;

        public float Speed;
        public float ShootCooldown;

        // cosmetics
        public Color BodyColor;
        public Color AccentColor;

        // AI
        public float AiTimer;
        public float FireTimer;

        public RectF Bounds => BoundsAt(Pos);
        public static RectF BoundsAt(Vector2 center) => new(center.X - Half, center.Y - Half, Size, Size);

        public static Tank CreatePlayer(Vector2 center) => new()
        {
            Id = ++_nextId,
            Alive = true,
            IsPlayer = true,
            Team = Team.Player,
            Pos = center,
            Dir = TankDir.Up,
            Speed = 120f,
            ShootCooldown = 0,
            BodyColor = Color.FromArgb(255, 60, 210, 110),
            AccentColor = Color.FromArgb(220, 210, 255, 230),
            AiTimer = 0,
            FireTimer = 0
        };

        public static Tank CreateEnemy(Vector2 center) => new()
        {
            Id = ++_nextId,
            Alive = true,
            IsPlayer = false,
            Team = Team.Enemy,
            Pos = center,
            Dir = TankDir.Down,
            Speed = 90f,
            ShootCooldown = 0,
            // Will be overridden at spawn for variety
            BodyColor = Color.FromArgb(255, 235, 90, 90),
            AccentColor = Color.FromArgb(220, 255, 255, 255),
            AiTimer = 0.4f,
            FireTimer = 0.8f
        };

        public void TrySetDirSnap(TankDir desired, float tileSize)
        {
            if (Dir == desired) return;

            // Snap to grid on perpendicular axis to avoid “lắc / drift” khi rẽ
            float eps = 2.2f;
            if (desired == TankDir.Up || desired == TankDir.Down)
            {
                float gx = MathF.Round(Pos.X / tileSize) * tileSize;
                if (MathF.Abs(Pos.X - gx) <= eps) Pos.X = gx;
                Dir = desired;
            }
            else
            {
                float gy = MathF.Round(Pos.Y / tileSize) * tileSize;
                if (MathF.Abs(Pos.Y - gy) <= eps) Pos.Y = gy;
                Dir = desired;
            }
        }

        public void Update(float dt)
        {
            if (ShootCooldown > 0) ShootCooldown -= dt;
        }

        public void Draw(CanvasDrawingSession ds)
        {
            var r = Bounds;

            var body = BodyColor;
            var accent = AccentColor.A == 0 ? Color.FromArgb(220, 255, 255, 255) : AccentColor;
            var outline = Color.FromArgb(180, 0, 0, 0);

            ds.FillRoundedRectangle(r.X, r.Y, r.W, r.H, 4, 4, body);
            ds.DrawRoundedRectangle(r.X, r.Y, r.W, r.H, 4, 4, outline, 2);

            // turret
            Vector2 forward = Dir.ToUnit();
            Vector2 muzzle = Pos + forward * (Size * 0.6f);
            Vector2 basep = Pos + forward * (Size * 0.1f);

            ds.DrawLine(basep, muzzle, Colors.Black, 4);

            // direction mark
            ds.FillCircle(Pos + forward * 6f, 2.2f, accent);
        }
    }

    public struct Bullet
    {
        public bool Alive;
        public Team Team;
        public Vector2 Pos; // center
        public TankDir Dir;

        public static float Size => 6f;
        public static float Half => Size * 0.5f;
        public float Speed;

        public RectF Bounds => new(Pos.X - Half, Pos.Y - Half, Size, Size);

        public static Bullet Create(Vector2 pos, TankDir dir, Team team) => new()
        {
            Alive = true,
            Pos = pos,
            Dir = dir,
            Team = team,
            Speed = 420f
        };

        public void Update(float dt)
        {
            Pos += Dir.ToUnit() * (Speed * dt);
        }

        public void Draw(CanvasDrawingSession ds)
        {
            var r = Bounds;
            ds.FillRoundedRectangle(r.X, r.Y, r.W, r.H, 2, 2, Team == Team.Player ? Colors.White : Colors.Orange);
        }
    }

    public struct Explosion
    {
        public bool Alive;
        private float _t;
        private readonly float _dur;
        private readonly bool _big;
        public Vector2 Pos;

        public Explosion(Vector2 pos, bool big)
        {
            Pos = pos;
            _big = big;
            _dur = big ? 0.28f : 0.18f;
            _t = 0;
            Alive = true;
        }

        public void Update(float dt)
        {
            _t += dt;
            if (_t >= _dur) Alive = false;
        }

        public void Draw(CanvasDrawingSession ds)
        {
            if (!Alive) return;
            float a = 1f - (_t / _dur);
            float radius = _big ? 22f : 12f;
            float r = radius * (0.6f + (1f - a) * 0.9f);

            byte alpha = (byte)(a * 200);
            ds.FillCircle(Pos, r, Color.FromArgb(alpha, 255, 220, 80));
            ds.FillCircle(Pos, r * 0.55f, Color.FromArgb(alpha, 255, 80, 60));
        }
    }
}
