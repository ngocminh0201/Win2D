using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Windows.UI;

namespace Win2D.BattleTank
{
    public sealed class GameEngine
    {
        // World layout
        public int Level { get; private set; } = 1;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; } = 0;

        // New rule: kill 10 enemies to win a level
        public int KillGoalPerLevel => KillGoal;
        public int KillsThisLevel => _killsThisLevel;
        public int RemainingToWin => Math.Max(0, KillGoal - _killsThisLevel);

        public bool Paused => _paused;
        public bool GameOver => _gameOver;
        public bool LevelWon => _levelWon;
        public bool GameCleared => _gameCleared;

        private bool _paused;
        private bool _gameOver;
        private bool _levelWon;
        private bool _gameCleared;

        private const int TotalLevels = 3;
        private const int KillGoal = 10;

        private int _killsThisLevel;

        private readonly Random _rng = new(1234);

        private readonly TileMap _map = new(tileSize: 28f, width: 50, height: 26);

        private readonly List<Tank> _tanks = new();
        private readonly List<Bullet> _bullets = new();
        private readonly List<Explosion> _fx = new();

        private Tank _player = default!;

        // Enemy flow
        private int _enemiesRemainingToSpawn = KillGoal;
        private float _enemySpawnTimer;
        private readonly Vector2[] _enemySpawns;

        // Rendering time effects
        private float _time;

        public GameEngine()
        {
            // 3 spawn points at top
            float ts = _map.TileSize;
            _enemySpawns = new[]
            {
                new Vector2(ts * 2.0f, ts * 2.0f),
                new Vector2(ts * (_map.Width / 2f), ts * 2.0f),
                new Vector2(ts * (_map.Width - 2.0f), ts * 2.0f),
            };
        }

        public void ResetToLevel1()
        {
            Level = 1;
            Lives = 1;
            Score = 0;

            _paused = false;
            _gameOver = false;
            _levelWon = false;
            _gameCleared = false;

            LoadLevel(Level, resetTransient: true);
        }

        public void TogglePause()
        {
            if (_gameOver || _levelWon || _gameCleared) return;
            _paused = !_paused;
        }

        /// <summary>
        /// Enter key behavior:
        /// - If GameOver: restart from Level 1
        /// - If LevelWon: advance to next level (or finish game)
        /// - If GameCleared: restart from Level 1
        /// </summary>
        public void HandleEnter()
        {
            if (_gameOver || _gameCleared)
            {
                ResetToLevel1();
                return;
            }

            if (_levelWon)
            {
                if (Level < TotalLevels)
                {
                    Level++;
                    LoadLevel(Level, resetTransient: true);
                }
                else
                {
                    // All clear
                    _levelWon = false;
                    _gameCleared = true;
                }
            }
        }

        public void Update(float dt, InputState input)
        {
            _time += dt;

            if (_paused) return;
            if (_gameOver) return;
            if (_levelWon) return;
            if (_gameCleared) return;

            SpawnEnemies(dt);

            // Update tanks
            for (int i = 0; i < _tanks.Count; i++)
            {
                var t = _tanks[i];
                if (!t.Alive) continue;

                if (t.IsPlayer)
                    UpdatePlayerTank(ref t, dt, input);
                else
                    UpdateEnemyTank(ref t, dt);

                _tanks[i] = t;
            }

            // Update bullets
            for (int i = 0; i < _bullets.Count; i++)
            {
                var b = _bullets[i];
                if (!b.Alive) continue;

                b.Update(dt);

                // Offscreen
                if (!_map.WorldRect.Contains(b.Pos))
                {
                    b.Alive = false;
                    _bullets[i] = b;
                    continue;
                }

                // Bullet vs map
                if (_map.TryBulletHit(b.Bounds, b.Dir, out var hitBase))
                {
                    b.Alive = false;
                    _bullets[i] = b;

                    if (hitBase)
                    {
                        _gameOver = true;
                        SpawnExplosion(b.Pos, big: true);
                    }
                    else
                    {
                        SpawnExplosion(b.Pos, big: false);
                    }
                    continue;
                }

                _bullets[i] = b;
            }

            // Bullet vs bullet
            for (int i = 0; i < _bullets.Count; i++)
            {
                if (!_bullets[i].Alive) continue;
                for (int j = i + 1; j < _bullets.Count; j++)
                {
                    if (!_bullets[j].Alive) continue;

                    if (_bullets[i].Bounds.Intersects(_bullets[j].Bounds))
                    {
                        var bi = _bullets[i]; bi.Alive = false; _bullets[i] = bi;
                        var bj = _bullets[j]; bj.Alive = false; _bullets[j] = bj;
                        SpawnExplosion((_bullets[i].Pos + _bullets[j].Pos) * 0.5f, big: false);
                    }
                }
            }

            // Bullet vs tanks
            for (int i = 0; i < _bullets.Count; i++)
            {
                var b = _bullets[i];
                if (!b.Alive) continue;

                for (int t = 0; t < _tanks.Count; t++)
                {
                    var tank = _tanks[t];
                    if (!tank.Alive) continue;
                    if (tank.Team == b.Team) continue;

                    if (b.Bounds.Intersects(tank.Bounds))
                    {
                        b.Alive = false;
                        _bullets[i] = b;

                        tank.Alive = false;
                        _tanks[t] = tank;

                        SpawnExplosion(tank.Pos, big: true);

                        if (tank.IsPlayer)
                        {
                            Lives--;
                            if (Lives <= 0) _gameOver = true;
                            else RespawnPlayer();
                        }
                        else
                        {
                            Score += 100;

                            // NEW: win condition
                            _killsThisLevel++;
                            if (_killsThisLevel >= KillGoal)
                            {
                                _levelWon = true;
                                _enemiesRemainingToSpawn = 0; // stop spawning
                            }
                        }
                        break;
                    }
                }
            }

            // Update FX
            for (int i = 0; i < _fx.Count; i++)
            {
                var fx = _fx[i];
                fx.Update(dt);
                _fx[i] = fx;
            }
            _fx.RemoveAll(f => !f.Alive);
            _bullets.RemoveAll(b => !b.Alive);

            // Remove dead enemy tanks
            // (keep player in list even if dead, will respawn)
            _tanks.RemoveAll(t => !t.Alive && !t.IsPlayer);
        }

        private void LoadLevel(int level, bool resetTransient)
        {
            if (resetTransient)
            {
                _bullets.Clear();
                _fx.Clear();

                // Keep player in list but we rebuild all tanks cleanly for each level
                _tanks.Clear();
            }

            _map.LoadLevel(level);

            _killsThisLevel = 0;
            _levelWon = false;
            _gameOver = false;

            float ts = _map.TileSize;

            // Spawn near bottom, slightly right of base (avoid immediate base line)
            float spawnX = ts * (_map.Width / 2f + 0.5f);
            float spawnY = _map.WorldSize.Y - ts * 0.5f;

            _player = Tank.CreatePlayer(new Vector2(spawnX, spawnY));
            _tanks.Add(_player);

            _enemiesRemainingToSpawn = KillGoal;
            _enemySpawnTimer = 0.55f;
        }

        private void SpawnEnemies(float dt)
        {
            if (_enemiesRemainingToSpawn <= 0) return;

            _enemySpawnTimer -= dt;
            if (_enemySpawnTimer > 0) return;

            // Limit enemies on screen
            int aliveEnemies = 0;
            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].Alive && !_tanks[i].IsPlayer) aliveEnemies++;

            if (aliveEnemies < 6)
            {
                var spawn = _enemySpawns[_rng.Next(_enemySpawns.Length)];
                var enemy = Tank.CreateEnemy(spawn);

                enemy.Kind = PickEnemyKind();
                enemy.FireTimer = 0.25f + NextEnemyFireInterval(enemy) * 0.35f; // slight delay then start shooting


                // NEW: colorful enemies
                enemy.BodyColor = EnemyColors.Pick(_rng);
                enemy.AccentColor = EnemyColors.AccentFor(enemy.BodyColor);

                if (!_map.IsRectBlocked(enemy.Bounds) && !AnyTankIntersects(enemy.Bounds))
                {
                    _tanks.Add(enemy);
                    _enemiesRemainingToSpawn--;
                }
            }

            _enemySpawnTimer = 0.65f;
        }

        private bool AnyTankIntersects(RectF r)
        {
            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].Alive && _tanks[i].Bounds.Intersects(r))
                    return true;
            return false;
        }

        private void RespawnPlayer()
        {
            float ts = _map.TileSize;

            float spawnX = ts * (_map.Width / 2f + 0.5f);
            float spawnY = _map.WorldSize.Y - ts * 0.5f;

            _player = Tank.CreatePlayer(new Vector2(spawnX, spawnY));

            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].IsPlayer) { _tanks[i] = _player; return; }

            _tanks.Add(_player);
        }

        private void UpdatePlayerTank(ref Tank t, float dt, InputState input)
        {
            Vector2 move = input.MoveAxis();
            if (move.LengthSquared() > 0.0001f)
            {
                // Choose 4-dir
                var dir = TankDirExt.FromVector(move);
                t.TrySetDirSnap(dir, _map.TileSize);

                MoveTank(ref t, dt);
            }
            else
            {
                t.Vel = Vector2.Zero;
            }

            // Fire (hold to fire with cooldown)
            if (input.Fire)
                TryFire(ref t);

            t.Update(dt);
        }

        private EnemyKind PickEnemyKind()
        {
            // Weight harder enemies more as levels go up
            // L1: mostly type1, a few type2, rare type3
            // Higher levels: more type2/type3
            double r = _rng.NextDouble();
            double w3 = Math.Min(0.10 + Level * 0.03, 0.35); // up to 35%
            double w2 = Math.Min(0.25 + Level * 0.04, 0.50); // up to 50%
            double w1 = Math.Max(0.0, 1.0 - w2 - w3);

            if (r < w3) return EnemyKind.Type3;
            if (r < w3 + w2) return EnemyKind.Type2;
            return EnemyKind.Type1;
        }

        private float NextEnemyFireInterval(in Tank t)
        {
            // Each type ramps up (shoots faster) over time.
            // We ease from a starting interval range to a faster (smaller) interval range.
            float age = MathF.Max(0, t.Age);

            // (startMin, startMax, endMin, endMax, accel)
            float startMin, startMax, endMin, endMax, accel;
            switch (t.Kind)
            {
                default:
                case EnemyKind.Type1:
                    startMin = 0.95f; startMax = 1.65f;
                    endMin = 0.55f; endMax = 0.95f;
                    accel = 0.08f; // slow ramp
                    break;

                case EnemyKind.Type2:
                    startMin = 0.80f; startMax = 1.35f;
                    endMin = 0.45f; endMax = 0.78f;
                    accel = 0.11f; // medium ramp
                    break;

                case EnemyKind.Type3:
                    startMin = 0.65f; startMax = 1.10f;
                    endMin = 0.32f; endMax = 0.60f;
                    accel = 0.16f; // fast ramp
                    break;
            }

            // k: 0 -> 1 over time
            float k = 1f - MathF.Exp(-accel * age);
            float min = startMin + (endMin - startMin) * k;
            float max = startMax + (endMax - startMax) * k;

            if (max < min) max = min + 0.01f;

            return min + (float)_rng.NextDouble() * (max - min);
        }

        private double EnemyFireChance(in Tank t)
        {
            // Type3 more aggressive
            return t.Kind switch
            {
                EnemyKind.Type1 => 0.55,
                EnemyKind.Type2 => 0.65,
                _ => 0.75,
            };
        }

        void UpdateEnemyTank(ref Tank t, float dt)
        {
            // Simple AI: change direction sometimes + when blocked
            t.AiTimer -= dt;
            if (t.AiTimer <= 0)
            {
                t.AiTimer = 0.35f + (float)_rng.NextDouble() * 0.9f;
                t.TrySetDirSnap((TankDir)_rng.Next(4), _map.TileSize);
            }

            // Move
            bool moved = MoveTank(ref t, dt);
            if (!moved)
            {
                // Turn immediately if stuck
                t.TrySetDirSnap((TankDir)_rng.Next(4), _map.TileSize);
            }

            // Fire sometimes
            t.FireTimer -= dt;
            if (t.FireTimer <= 0)
            {
                if (_rng.NextDouble() < EnemyFireChance(t)) TryFire(ref t);
                t.FireTimer = NextEnemyFireInterval(t);
            }

            t.Update(dt);
        }

        private bool MoveTank(ref Tank t, float dt)
        {
            t.Vel = t.Dir.ToUnit() * t.Speed;
            Vector2 next = t.Pos + t.Vel * dt;

            var nextBounds = Tank.BoundsAt(next);

            // Map collision
            if (_map.IsRectBlocked(nextBounds)) { t.Vel = Vector2.Zero; return false; }

            // Tank vs tank collision
            for (int i = 0; i < _tanks.Count; i++)
            {
                if (!_tanks[i].Alive) continue;
                if (_tanks[i].Id == t.Id) continue;

                if (nextBounds.Intersects(_tanks[i].Bounds))
                {
                    t.Vel = Vector2.Zero;
                    return false;
                }
            }

            t.Pos = next;
            return true;
        }

        private void TryFire(ref Tank t)
        {
            if (t.ShootCooldown > 0) return;

            // Limit bullets per team to keep classic feel
            int teamBullets = 0;
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Alive && _bullets[i].Team == t.Team)
                    teamBullets++;

            if (t.IsPlayer && teamBullets >= 2) return;
            if (!t.IsPlayer && teamBullets >= 3) return;

            var muzzle = t.Pos + t.Dir.ToUnit() * (Tank.Size * 0.62f);
            var b = Bullet.Create(muzzle, t.Dir, t.Team);
            _bullets.Add(b);

            t.ShootCooldown = t.IsPlayer ? 0.18f : 0.55f;
        }

        private void SpawnExplosion(Vector2 pos, bool big)
        {
            _fx.Add(new Explosion(pos, big));
        }

        public void Render(CanvasDrawingSession ds, Vector2 surfaceSize)
        {
            Debug.WriteLine("Renderr...");
            // Fit world to panel (letterbox)
            Vector2 world = _map.WorldSize;
            float scale = MathF.Min(surfaceSize.X / world.X, surfaceSize.Y / world.Y);
            Vector2 offset = (surfaceSize - world * scale) * 0.5f;

            // Background subtle grid
            ds.Transform = Matrix3x2.Identity;
            DrawBackdrop(ds, surfaceSize, _time);

            ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offset);

            _map.Draw(ds, _time);

            // Tanks
            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].Alive)
                    _tanks[i].Draw(ds);

            // Bullets
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Alive)
                    _bullets[i].Draw(ds);

            // FX
            for (int i = 0; i < _fx.Count; i++)
                _fx[i].Draw(ds);

            // Overlay text
            ds.Transform = Matrix3x2.Identity;

            if (_paused)
                ds.DrawText("PAUSED (Esc)", 18, 70, Colors.White);

            if (_levelWon)
            {
                string msg = Level < TotalLevels
                    ? $"VICTORY!  (Enter -> Level {Level + 1})"
                    : "VICTORY!  (Enter -> ALL CLEAR)";
                ds.DrawText(msg, 18, 96, Colors.White);
            }

            if (_gameCleared)
                ds.DrawText("ALL CLEAR! (Enter để chơi lại)", 18, 96, Colors.DeepSkyBlue);

            if (_gameOver)
                ds.DrawText("GAME OVER (Enter để chơi lại)", 18, 96, Colors.OrangeRed);

            ds.Transform = Matrix3x2.Identity;
        }

        private static void DrawBackdrop(CanvasDrawingSession ds, Vector2 size, float t)
        {
            // soft vignette look (simple shapes, cheap)
            ds.FillRectangle(0, 0, size.X, size.Y, Color.FromArgb(255, 8, 10, 18));

            // animated faint stripes
            float stripe = 48f;
            float off = (t * 22f) % stripe;
            for (float x = -stripe; x < size.X + stripe; x += stripe)
            {
                ds.FillRectangle(x + off, 0, 10, size.Y, Color.FromArgb(18, 120, 180, 255));
            }
        }
    }
}
