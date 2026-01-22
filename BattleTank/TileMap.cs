using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Numerics;
using Windows.UI;

namespace Win2D.BattleTank
{
    public sealed class TileMap
    {
        public int Width { get; }
        public int Height { get; }
        public float TileSize { get; }

        public Vector2 WorldSize => new(Width * TileSize, Height * TileSize);
        public RectF WorldRect => new(0, 0, WorldSize.X, WorldSize.Y);

        private readonly Tile[] _tiles;

        public TileMap(float tileSize, int width, int height)
        {
            TileSize = tileSize;
            Width = width;
            Height = height;
            _tiles = new Tile[width * height];
        }

        public void LoadLevel1()
        {
            // 26x26 (vừa “retro”, vừa đủ chỗ né)
            // '.' empty, 'B' brick, 'S' steel, 'W' water, 'T' trees(bush), 'I' ice, 'E' base
            string[] rows = Level1Layout.Rows;
            if (rows.Length != Height) throw new InvalidOperationException("Layout height mismatch");
            for (int y = 0; y < Height; y++)
            {
                if (rows[y].Length != Width) throw new InvalidOperationException("Layout width mismatch");
                for (int x = 0; x < Width; x++)
                {
                    char c = rows[y][x];
                    _tiles[y * Width + x] = c switch
                    {
                        'B' => Tile.BrickFull(),
                        'S' => Tile.Steel(),
                        'W' => Tile.Water(),
                        'T' => Tile.Bush(),
                        'I' => Tile.Ice(),
                        'E' => Tile.Base(),
                        _ => Tile.Empty(),
                    };
                }
            }
        }

        public bool IsRectBlocked(RectF r)
        {
            // clamp
            if (r.X < 0 || r.Y < 0 || r.Right > WorldSize.X || r.Bottom > WorldSize.Y)
                return true;

            int minX = (int)MathF.Floor(r.X / TileSize);
            int maxX = (int)MathF.Floor((r.Right - 0.001f) / TileSize);
            int minY = (int)MathF.Floor(r.Y / TileSize);
            int maxY = (int)MathF.Floor((r.Bottom - 0.001f) / TileSize);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var tile = Get(x, y);
                    if (tile.Kind == TileKind.Empty || tile.Kind == TileKind.Bush || tile.Kind == TileKind.Ice) continue;

                    if (tile.Kind == TileKind.Brick)
                    {
                        // Partial bricks: test each existing quadrant
                        foreach (var q in tile.BrickQuadrants(TileSize, x, y))
                            if (r.Intersects(q)) return true;
                    }
                    else
                    {
                        // Steel, Water, Base are solid
                        var tr = TileRect(x, y);
                        if (r.Intersects(tr)) return true;
                    }
                }

            return false;
        }

        public bool TryBulletHit(RectF bulletRect, TankDir dir, out bool hitBase)
        {
            hitBase = false;

            // Determine tiles overlapped by bullet rect
            int minX = Clamp((int)MathF.Floor(bulletRect.X / TileSize), 0, Width - 1);
            int maxX = Clamp((int)MathF.Floor((bulletRect.Right - 0.001f) / TileSize), 0, Width - 1);
            int minY = Clamp((int)MathF.Floor(bulletRect.Y / TileSize), 0, Height - 1);
            int maxY = Clamp((int)MathF.Floor((bulletRect.Bottom - 0.001f) / TileSize), 0, Height - 1);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var idx = y * Width + x;
                    var tile = _tiles[idx];

                    if (tile.Kind == TileKind.Empty || tile.Kind == TileKind.Bush || tile.Kind == TileKind.Ice)
                        continue;

                    if (tile.Kind == TileKind.Base)
                    {
                        // Base destroyed => game over
                        hitBase = true;
                        _tiles[idx] = Tile.Empty();
                        return true;
                    }

                    if (tile.Kind == TileKind.Steel || tile.Kind == TileKind.Water)
                    {
                        // Bullet blocked
                        return bulletRect.Intersects(TileRect(x, y));
                    }

                    if (tile.Kind == TileKind.Brick)
                    {
                        // Hit one quadrant depending on impact point (use bullet center biased toward travel dir)
                        Vector2 p = bulletRect.Center;
                        p += dir.ToUnit() * 2.5f;

                        if (HitBrickQuadrant(x, y, p))
                        {
                            tile = _tiles[idx];
                            // If brick tile is now empty, ok
                            return true;
                        }
                    }
                }

            return false;
        }

        private bool HitBrickQuadrant(int tx, int ty, Vector2 hitPoint)
        {
            int idx = ty * Width + tx;
            var tile = _tiles[idx];
            if (tile.Kind != TileKind.Brick || tile.BrickMask == 0) return false;

            float localX = hitPoint.X - tx * TileSize;
            float localY = hitPoint.Y - ty * TileSize;

            byte bit;
            bool right = localX >= TileSize * 0.5f;
            bool bottom = localY >= TileSize * 0.5f;

            if (!right && !bottom) bit = 1;       // TL
            else if (right && !bottom) bit = 2;   // TR
            else if (!right && bottom) bit = 4;   // BL
            else bit = 8;                         // BR

            if ((tile.BrickMask & bit) == 0) return false;

            tile.BrickMask &= (byte)~bit;
            tile.Kind = tile.BrickMask == 0 ? TileKind.Empty : TileKind.Brick;
            _tiles[idx] = tile;
            return true;
        }

        public void Draw(CanvasDrawingSession ds, float time)
        {
            // map background
            ds.FillRectangle(0, 0, WorldSize.X, WorldSize.Y, Color.FromArgb(255, 12, 16, 26));

            // tiles
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var tile = _tiles[y * Width + x];
                    if (tile.Kind == TileKind.Empty) continue;

                    var r = TileRect(x, y);

                    switch (tile.Kind)
                    {
                        case TileKind.Brick:
                            foreach (var q in tile.BrickQuadrants(TileSize, x, y))
                            {
                                ds.FillRectangle(q.X, q.Y, q.W, q.H, Color.FromArgb(255, 168, 72, 42));
                                ds.DrawRectangle(q.X, q.Y, q.W, q.H, Color.FromArgb(140, 0, 0, 0), 1);
                            }
                            break;

                        case TileKind.Steel:
                            ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(255, 140, 150, 165));
                            ds.DrawRectangle(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, Color.FromArgb(120, 0, 0, 0), 2);
                            break;

                        case TileKind.Water:
                            {
                                // cheap animated waves
                                float wave = (MathF.Sin(time * 3f + x * 0.7f + y * 0.9f) + 1) * 0.5f;
                                byte a = (byte)(90 + wave * 80);
                                ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(255, 22, 90, 160));
                                ds.FillRectangle(r.X, r.Y + r.H * 0.35f, r.W, r.H * 0.18f, Color.FromArgb(a, 180, 240, 255));
                            }
                            break;

                        case TileKind.Bush:
                            ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(110, 60, 200, 90));
                            break;

                        case TileKind.Ice:
                            ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(220, 210, 240, 255));
                            ds.DrawLine(r.X, r.Y + r.H * 0.5f, r.X + r.W, r.Y + r.H * 0.5f, Color.FromArgb(90, 40, 90, 140), 2);
                            break;

                        case TileKind.Base:
                            // simple "eagle" icon style
                            ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(255, 70, 70, 70));
                            ds.FillRectangle(r.X + r.W * 0.2f, r.Y + r.H * 0.25f, r.W * 0.6f, r.H * 0.5f, Color.FromArgb(255, 230, 200, 40));
                            ds.DrawRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(200, 0, 0, 0), 2);
                            break;
                    }
                }

            // border
            ds.DrawRectangle(0, 0, WorldSize.X, WorldSize.Y, Color.FromArgb(160, 0, 0, 0), 3);
        }

        private Tile Get(int x, int y) => _tiles[y * Width + x];

        private RectF TileRect(int x, int y) => new(x * TileSize, y * TileSize, TileSize, TileSize);

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public enum TileKind { Empty, Brick, Steel, Water, Bush, Ice, Base }

    public struct Tile
    {
        public TileKind Kind;
        public byte BrickMask; // for Brick: 1 TL, 2 TR, 4 BL, 8 BR

        public static Tile Empty() => new() { Kind = TileKind.Empty, BrickMask = 0 };
        public static Tile BrickFull() => new() { Kind = TileKind.Brick, BrickMask = 0xF };
        public static Tile Steel() => new() { Kind = TileKind.Steel };
        public static Tile Water() => new() { Kind = TileKind.Water };
        public static Tile Bush() => new() { Kind = TileKind.Bush };
        public static Tile Ice() => new() { Kind = TileKind.Ice };
        public static Tile Base() => new() { Kind = TileKind.Base };

        public RectF[] BrickQuadrants(float ts, int x, int y)
        {
            if (Kind != TileKind.Brick || BrickMask == 0) return Array.Empty<RectF>();

            float hx = ts * 0.5f;
            float hy = ts * 0.5f;
            float ox = x * ts;
            float oy = y * ts;

            // TL, TR, BL, BR
            var list = new System.Collections.Generic.List<RectF>(4);
            if ((BrickMask & 1) != 0) list.Add(new RectF(ox, oy, hx, hy));
            if ((BrickMask & 2) != 0) list.Add(new RectF(ox + hx, oy, hx, hy));
            if ((BrickMask & 4) != 0) list.Add(new RectF(ox, oy + hy, hx, hy));
            if ((BrickMask & 8) != 0) list.Add(new RectF(ox + hx, oy + hy, hx, hy));
            return list.ToArray();
        }
    }

    file static class Level1Layout
    {
        public static readonly string[] Rows =
        {
        "....................S........S....................",
        ".S....BBBB....BBBB...........BBBB....BBBB....S....",
        ".....B..B.....S....B..B.....S.....B..B.....S......",
        "..BBBB....BBBB..............BBBB....BBBB....BBBB..",
        "....W..W....TTTT....W..W....TTTT....W..W....TTTT..",
        "....W..W....T..T....W..W....T..T....W..W....T..T..",
        "....W..W....TTTT....W..W....TTTT....W..W....TTTT..",
        "..........IIII..........IIII..........IIII........",
        "..B.B.B........BBBBBBBB........BBBBBBBB........B..",
        "..B...B........B....S.B........B.S....B........B..",
        "..B.B.B........BBBBBBBB........BBBBBBBB........B..",
        "........S..................S..................S...",
        ".....BBBB.....WWWWWW..............WWWWWW.....BBBB.",
        ".....B..B.....W....W....BBBBBB....W....W.....B..B.",
        ".....B..B.....W....W....B....B....W....W.....B..B.",
        ".....BBBB.....WWWWWW....B....B....WWWWWW.....BBBB.",
        "..........TTTT..........TTTT..........TTTT........",
        "..S....B....B....S....B....B....S....B....B....S..",
        "..S....B....B....S....B....B....S....B....B....S..",
        "........BBBBBBBB..............BBBBBBBB............",
        "....I..I..I..I....S....I..I..I..I....S....I..I..I.",
        "....I..I..I..I....S....I..I..I..I....S....I..I..I.",
        "......BBBB....BBBB..............BBBB....BBBB......",
        "....................BBBBB.........................",
        "....................BBEBB.........................",
        "....................BBBBB.........................",
    };
    }

}
