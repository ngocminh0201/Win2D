using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;

namespace Win2D.BattleTank
{
    public sealed class GameEngine
    {
        // World layout
        public int Level { get; private set; } = 1;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; } = 0;

        public bool Paused => _paused;
        public bool GameOver => _gameOver;

        private bool _paused;
        private bool _gameOver;

        private readonly Random _rng = new(1234);

        private readonly TileMap _map = new(tileSize: 28f, width: 50, height: 26);

        private readonly List<Tank> _tanks = new();
        private readonly List<Bullet> _bullets = new();
        private readonly List<Explosion> _fx = new();

        private Tank _player = default!;

        // Enemy flow
        private int _enemiesRemaining = 20;
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
            Lives = 3;
            Score = 0;

            _paused = false;
            _gameOver = false;

            _bullets.Clear();
            _fx.Clear();
            _tanks.Clear();

            _map.LoadLevel1();

            float ts = _map.TileSize;

            // Spawn ở gần đáy, lệch phải 1 ô so với base để bắn lên không cắt vào base
            float spawnX = ts * (_map.Width / 2f + 0.5f);  // 13.5 tiles => tâm ô cột 13
            float spawnY = _map.WorldSize.Y - ts * 0.5f;   // sát đáy nhưng không chạm biên

            _player = Tank.CreatePlayer(new Vector2(spawnX, spawnY));
            _tanks.Add(_player);   // <-- thiếu dòng này nên player biến mất

            _enemiesRemaining = 20;
            _enemySpawnTimer = 0.6f;
        }

        public void TogglePause()
        {
            if (_gameOver) return;
            _paused = !_paused;
        }

        public void TryRestartIfGameOver()
        {
            if (_gameOver) ResetToLevel1();
        }

        public void Update(float dt, InputState input)
        {
            _time += dt;

            if (_paused) return;
            if (_gameOver) return;

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

        private void SpawnEnemies(float dt)
        {
            _enemySpawnTimer -= dt;
            if (_enemySpawnTimer > 0) return;

            // Limit enemies on screen
            int aliveEnemies = 0;
            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].Alive && !_tanks[i].IsPlayer) aliveEnemies++;

            if (_enemiesRemaining > 0 && aliveEnemies < 4)
            {
                var spawn = _enemySpawns[_rng.Next(_enemySpawns.Length)];
                var enemy = Tank.CreateEnemy(spawn);
                if (!_map.IsRectBlocked(enemy.Bounds) && !AnyTankIntersects(enemy.Bounds))
                {
                    _tanks.Add(enemy);
                    _enemiesRemaining--;
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

            // Spawn ở gần đáy, lệch phải 1 ô so với base để bắn lên không cắt vào base
            float spawnX = ts * (_map.Width / 2f + 0.5f);  // 13.5 tiles => tâm ô cột 13
            float spawnY = _map.WorldSize.Y - ts * 0.5f;   // sát đáy nhưng không chạm biên

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

        private void UpdateEnemyTank(ref Tank t, float dt)
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
                if (_rng.NextDouble() < 0.55) TryFire(ref t);
                t.FireTimer = 0.9f + (float)_rng.NextDouble() * 0.9f;
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
            if (!t.IsPlayer && teamBullets >= 1) return;

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
            {
                ds.DrawText("PAUSED (Esc)", 18, 70, Colors.White);
            }
            if (_gameOver)
            {
                ds.DrawText("GAME OVER (Enter để chơi lại)", 18, 96, Colors.OrangeRed);
            }

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
