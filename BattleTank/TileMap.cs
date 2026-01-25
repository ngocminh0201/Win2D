using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Windows.Foundation;
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

        // ========= LEVELS =========

        public void LoadLevel(int level)
        {
            ClearAll();

            // Solid steel border for clean bounds + classic feel
            for (int x = 0; x < Width; x++)
            {
                Set(x, 0, Tile.Steel());
                Set(x, Height - 1, Tile.Steel());
            }
            for (int y = 0; y < Height; y++)
            {
                Set(0, y, Tile.Steel());
                Set(Width - 1, y, Tile.Steel());
            }

            // Base + guard bricks
            //PlaceBase();

            // Text content per level
            if (level == 1)
            {
                PlaceTextAsBricks("36", scale: 2, spacing: 1, yTop: 5);
                PlaceDecorLevel1();
            }
            else if (level == 2)
            {
                // Map 2: render "THANH" on the first line (centered) and "HOA" on the second line (centered).
                // Keep the playfield clean: no extra obstacles overlapping the text.
                PlaceDecorLevel2();
                PlaceTextLinesAsBricks(new[] { "THANH", "HOA" }, scale: 1, spacing: 2, lineSpacing: 1, yTop: 8);
            }
            else // level 3
            {
                PlaceTextAsBricks("3636", scale: 2, spacing: 1, yTop: 5);
                PlaceDecorLevel3();
            }

            // Make sure spawn zones are free
            CarveSpawnZones();
        }

        private void ClearAll()
        {
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = Tile.Empty();
        }

        private void Set(int x, int y, Tile t)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            _tiles[y * Width + x] = t;
        }

        private Tile Get(int x, int y) => _tiles[y * Width + x];

        private RectF TileRect(int x, int y) => new(x * TileSize, y * TileSize, TileSize, TileSize);

        private void PlaceBase()
        {
            // Put base near bottom center (classic)
            int bx = Width / 2;
            int by = Height - 2;

            // Clear area
            for (int y = by - 2; y <= by + 1; y++)
                for (int x = bx - 2; x <= bx + 2; x++)
                    Set(x, y, Tile.Empty());

            // Guard bricks ring, base in center
            for (int y = by - 1; y <= by + 1; y++)
            {
                for (int x = bx - 1; x <= bx + 1; x++)
                {
                    if (x == bx && y == by) continue;
                    Set(x, y, Tile.BrickFull());
                }
            }

            Set(bx, by, Tile.Base());

            // Remove the 2 bricks beside the player tank (left/right of the spawn lane)
            Set(bx - 1, by + 1, Tile.Empty());
            Set(bx + 1, by + 1, Tile.Empty());
        }

        private void CarveSpawnZones()
        {
            // Enemy spawn tiles (match GameEngine)
            // (2,2), (mid,2), (w-2,2)
            int mid = Width / 2;
            CarveCircle(2, 2, 2);
            CarveCircle(mid, 2, 2);
            CarveCircle(Width - 2, 2, 2);

            // Player spawn (match GameEngine: x=Width/2, y=Height-1)
            CarveCircle(Width / 2, Height - 1, 2);
        }

        private void CarveCircle(int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r)
                        Set(x, y, Tile.Empty());
                }
        }

        private void PlaceDecorLevel1()
        {
            // Some water lanes + steel pillars (deterministic, no RNG)
            for (int x = 6; x < Width - 6; x += 6)
            {
                Set(x, 12, Tile.Water());
                Set(x, 13, Tile.Water());
            }

            for (int y = 6; y < Height - 8; y += 5)
            {
                Set(10, y, Tile.Steel());
                Set(Width - 11, y, Tile.Steel());
            }

            // Bush patches
            for (int x = 4; x < Width - 4; x += 8)
            {
                Set(x, 18, Tile.Bush());
                Set(x + 1, 18, Tile.Bush());
            }
        }

        private void PlaceDecorLevel2()
        {
            // Intentionally empty for map 2:
            // Besides the 4-side steel border and the base guard bricks,
            // do not place any additional obstacles/tiles that could cover the "THANH HOA" text.
        }

        private void PlaceDecorLevel3()
        {
            // More "maze-like" pillars
            for (int y = 4; y < Height - 6; y++)
            {
                if (y % 2 == 0)
                {
                    Set(8, y, Tile.BrickFull());
                    Set(Width - 9, y, Tile.BrickFull());
                }
            }

            // Water pools
            for (int y = 10; y <= 13; y++)
                for (int x = 22; x <= 27; x++)
                    if ((x + y) % 2 == 0) Set(x, y, Tile.Water());
        }

        private void PlaceTextAsBricks(string text, int scale, int spacing, int yTop)
        {
            string norm = TextRaster.Normalize(text);

            // Compute width in tiles
            int charW = TextRaster.CharWidth;
            int charH = TextRaster.CharHeight;

            int glyphCount = norm.Length;
            int totalPxW = glyphCount * charW + Math.Max(0, glyphCount - 1) * spacing;
            int totalW = totalPxW * scale;
            int totalH = charH * scale;

            int startX = Math.Max(1, (Width - totalW) / 2);
            int startY = Math.Clamp(yTop, 1, Height - totalH - 2);

            int cursor = 0;
            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                var glyph = TextRaster.GetGlyph(ch);

                for (int gy = 0; gy < charH; gy++)
                {
                    for (int gx = 0; gx < charW; gx++)
                    {
                        if (!glyph[gy, gx]) continue;

                        int px = startX + (cursor + gx) * scale;
                        int py = startY + gy * scale;

                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                Set(px + sx, py + sy, Tile.BrickFull());
                    }
                }

                cursor += charW + spacing;
            }
        }
        /// <summary>
        /// Places multiple text lines made of bricks.
        /// Both lines share the same center (common centering) so the whole 2-line block feels aligned.
        /// </summary>
        private void PlaceTextLinesAsBricks(string[] lines, int scale, int spacing, int lineSpacing, int yTop)
        {
            if (lines is null || lines.Length == 0) return;

            int charW = TextRaster.CharWidth;
            int charH = TextRaster.CharHeight;

            // Normalize and compute each line width in tiles
            var normLines = new string[lines.Length];
            var lineWidths = new int[lines.Length];
            int maxW = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string norm = TextRaster.Normalize(lines[i] ?? "");
                normLines[i] = norm;

                int glyphCount = norm.Length;
                int totalPxW = glyphCount * charW + Math.Max(0, glyphCount - 1) * spacing;
                int totalW = totalPxW * scale;
                lineWidths[i] = totalW;
                if (totalW > maxW) maxW = totalW;
            }

            int totalH = (lines.Length * charH * scale) + (Math.Max(0, lines.Length - 1) * lineSpacing * scale);
            int startY = Math.Clamp(yTop, 1, Height - totalH - 2);

            // Common centered block X so both lines share the same visual center.
            int blockStartX = Math.Max(1, (Width - maxW) / 2);

            for (int li = 0; li < normLines.Length; li++)
            {
                // Center each line within the common block, but keep rounding consistent.
                int lineStartX = blockStartX + Math.Max(0, (maxW - lineWidths[li]) / 2);
                int lineY = startY + li * (charH * scale + lineSpacing * scale);
                PlaceTextLineAt(normLines[li], scale, spacing, lineStartX, lineY);
            }
        }

        private void PlaceTextLineAt(string norm, int scale, int spacing, int startX, int startY)
        {
            int charW = TextRaster.CharWidth;
            int charH = TextRaster.CharHeight;

            int cursor = 0;
            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                var glyph = TextRaster.GetGlyph(ch);

                for (int gy = 0; gy < charH; gy++)
                {
                    for (int gx = 0; gx < charW; gx++)
                    {
                        if (!glyph[gy, gx]) continue;

                        int px = startX + (cursor + gx) * scale;
                        int py = startY + gy * scale;

                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                Set(px + sx, py + sy, Tile.BrickFull());
                    }
                }

                cursor += charW + spacing;
            }
        }

        // ========= COLLISION =========

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

        // ========= DRAW =========

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
                                var img = GameAssets.Brick;
                                if (img is not null)
                                {
                                    ds.DrawImage(img, new Rect(q.X, q.Y, q.W, q.H));
                                    // thin outline to keep grid readable
                                    ds.DrawRectangle(q.X, q.Y, q.W, q.H, Color.FromArgb(90, 0, 0, 0), 1);
                                }
                                else
                                {
                                    ds.FillRectangle(q.X, q.Y, q.W, q.H, Color.FromArgb(255, 168, 72, 42));
                                    ds.DrawRectangle(q.X, q.Y, q.W, q.H, Color.FromArgb(140, 0, 0, 0), 1);
                                }
                            }
                            break;

                        case TileKind.Steel:
                            {
                                var img = GameAssets.Steel;
                                if (img is not null)
                                {
                                    ds.DrawImage(img, new Rect(r.X, r.Y, r.W, r.H));
                                    ds.DrawRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(120, 0, 0, 0), 2);
                                }
                                else
                                {
                                    ds.FillRectangle(r.X, r.Y, r.W, r.H, Color.FromArgb(255, 140, 150, 165));
                                    ds.DrawRectangle(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, Color.FromArgb(120, 0, 0, 0), 2);
                                }
                            }
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
            var list = new List<RectF>(4);
            if ((BrickMask & 1) != 0) list.Add(new RectF(ox, oy, hx, hy));
            if ((BrickMask & 2) != 0) list.Add(new RectF(ox + hx, oy, hx, hy));
            if ((BrickMask & 4) != 0) list.Add(new RectF(ox, oy + hy, hx, hy));
            if ((BrickMask & 8) != 0) list.Add(new RectF(ox + hx, oy + hy, hx, hy));
            return list.ToArray();
        }
    }

    file static class TextRaster
    {
        // 7x7 glyphs (each row is 7 chars). '#': brick, '.': empty
        public const int CharWidth = 7;
        public const int CharHeight = 7;

        // Return a 7x7 bool glyph.
        public static bool[,] GetGlyph(char c)
        {
            if (!_glyphs.TryGetValue(c, out var rows))
                rows = _glyphs['?'];

            var g = new bool[CharHeight, CharWidth];
            for (int y = 0; y < CharHeight; y++)
                for (int x = 0; x < CharWidth; x++)
                    g[y, x] = rows[y][x] == '#';
            return g;
        }

        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            // Keep Vietnamese diacritics (e.g., "HÓA").
            // Normalize to FormC to prefer composed characters (Ó instead of O + combining mark).
            string formC = s.Normalize(NormalizationForm.FormC);

            var sb = new StringBuilder(formC.Length);
            foreach (var ch in formC)
            {
                if (char.IsWhiteSpace(ch)) { sb.Append(' '); continue; }

                char up = char.ToUpperInvariant(ch);
                if (up == 'Đ') up = 'D';

                // If the input still contains combining marks, drop them.
                var cat = CharUnicodeInfo.GetUnicodeCategory(up);
                if (cat == UnicodeCategory.NonSpacingMark) continue;

                sb.Append(up);
            }

            return sb.ToString();
        }

        private static readonly Dictionary<char, string[]> _glyphs = new()
        {
            // digits
            ['3'] = new[]
            {
                "#######",
                "......#",
                "......#",
                "#######",
                "......#",
                "......#",
                "#######",
            },
            ['6'] = new[]
            {
                "#######",
                "#......",
                "#......",
                "#######",
                "#.....#",
                "#.....#",
                "#######",
            },

            // letters used in "THANH HÓA"
            ['T'] = new[]
            {
                "#######",
                "...#...",
                "...#...",
                "...#...",
                "...#...",
                "...#...",
                "...#...",
            },
            ['H'] = new[]
            {
                "#.....#",
                "#.....#",
                "#.....#",
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
            },
            ['A'] = new[]
            {
                "..###..",
                ".#...#.",
                "#.....#",
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
            },

            // N: make the diagonal \ longer / more natural
            ['N'] = new[]
            {
                "#.....#",
                "##....#",
                "#.#...#",
                "#..#..#",
                "#...#.#",
                "#....##",
                "#.....#",
            },

            ['O'] = new[]
            {
                "..###..",
                ".#...#.",
                "#.....#",
                "#.....#",
                "#.....#",
                ".#...#.",
                "..###..",
            },

            [' '] = new[]
            {
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
            },

            ['?'] = new[]
            {
                "#######",
                "......#",
                "....##.",
                "...#...",
                "...#...",
                ".......",
                "...#...",
            },
        };
    }

}
