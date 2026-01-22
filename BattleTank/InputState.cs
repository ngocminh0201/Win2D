using System.Collections.Generic;
using System.Numerics;
using Windows.System;

namespace Win2D.BattleTank
{
    public sealed class InputState
    {
        private readonly HashSet<VirtualKey> _down = new();

        public bool Up => _down.Contains(VirtualKey.W) || _down.Contains(VirtualKey.Up);
        public bool Down => _down.Contains(VirtualKey.S) || _down.Contains(VirtualKey.Down);
        public bool Left => _down.Contains(VirtualKey.A) || _down.Contains(VirtualKey.Left);
        public bool Right => _down.Contains(VirtualKey.D) || _down.Contains(VirtualKey.Right);

        public bool Fire => _down.Contains(VirtualKey.Space) || _down.Contains(VirtualKey.X);

        public void OnKeyDown(VirtualKey key) => _down.Add(key);
        public void OnKeyUp(VirtualKey key) => _down.Remove(key);

        public Vector2 MoveAxis()
        {
            float x = (Right ? 1 : 0) - (Left ? 1 : 0);
            float y = (Down ? 1 : 0) - (Up ? 1 : 0);

            var v = new Vector2(x, y);
            if (v.LengthSquared() > 1f) v = Vector2.Normalize(v);
            return v;
        }
    }
}
