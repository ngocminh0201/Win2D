using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CommandListsWindow : Window
    {
        private CanvasCommandList _layerBackground; // Layer 0
        private CanvasCommandList _layerUI;         // Layer 1

        private CanvasTextFormat _titleFormat;
        private Size _recordedSize;
        public CommandListsWindow()
        {
            InitializeComponent();
        }

        private void canvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            _titleFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 28,
                FontWeight = FontWeights.SemiBold
            };

            RecordLayersIfNeeded(sender, force: true);
        }

        private void canvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedDrawEventArgs args)
        {
            RecordLayersIfNeeded((CanvasAnimatedControl)sender, force: false);

            var ds = args.DrawingSession;

            // Replay theo thứ tự: background -> UI -> dynamic
            if (_layerBackground != null) ds.DrawImage(_layerBackground);
            if (_layerUI != null) ds.DrawImage(_layerUI);

            // Layer động (vẽ trực tiếp mỗi frame)
            DrawDynamic(ds, sender, args);
        }

        private void RecordLayersIfNeeded(CanvasAnimatedControl sender, bool force)
        {
            if (sender.Size.Width <= 1 || sender.Size.Height <= 1)
                return;

            bool resized =
                _recordedSize.Width != sender.Size.Width ||
                _recordedSize.Height != sender.Size.Height;

            if (!force && !resized && _layerBackground != null && _layerUI != null)
                return;

            _recordedSize = sender.Size;

            // Dispose layer cũ
            _layerBackground?.Dispose();
            _layerUI?.Dispose();

            // Record lại layer 0: background
            _layerBackground = new CanvasCommandList(sender.Device);
            using (var ds = _layerBackground.CreateDrawingSession())
            {
                ds.Clear(Colors.Black);
                DrawGrid(ds, _recordedSize, 40, Color.FromArgb(255, 30, 30, 30));

                // Ví dụ thêm gradient-ish stripes (tĩnh)
                for (int i = 0; i < 6; i++)
                {
                    float y = 90 + i * 40;
                    byte a = (byte)(40 + i * 10);
                    ds.FillRectangle(0, y, (float)_recordedSize.Width, 20, Color.FromArgb(a, 0, 140, 255));
                }
            }

            // Record lại layer 1: UI / frame / text
            _layerUI = new CanvasCommandList(sender.Device);
            using (var ds = _layerUI.CreateDrawingSession())
            {
                // Thanh header
                ds.FillRectangle(0, 0, (float)_recordedSize.Width, 80, Color.FromArgb(255, 18, 18, 18));
                ds.DrawText("MULTI-LAYER RECORD & REPLAY", 20, 22, Colors.White, _titleFormat);

                // Panel bên trái
                ds.FillRoundedRectangle(20, 110, 360, 220, 14, 14, Color.FromArgb(255, 20, 20, 20));
                ds.DrawRoundedRectangle(20, 110, 360, 220, 14, 14, Colors.Orange, 2);

                var fmt = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 16 };
                ds.DrawText("Layer 0: Background (recorded)\nLayer 1: UI Frame (recorded)\nLayer 2: Dynamic (per-frame)",
                    40, 140, Colors.LightGray, fmt);

                // Khung demo vùng animation
                float w = (float)_recordedSize.Width - 420;
                float h = 220;
                float x = 400;
                float y = 110;

                ds.DrawRoundedRectangle(x, y, w, h, 16, 16, Color.FromArgb(255, 70, 70, 70), 2);
                ds.DrawText("Dynamic area (not recorded)", x + 20, y + 20, Colors.WhiteSmoke, fmt);
            }
        }

        private void DrawDynamic(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            float t = (float)args.Timing.TotalTime.TotalSeconds;

            // Vẽ động trong “dynamic area” ở bên phải
            float areaX = 400;
            float areaY = 110;
            float areaW = (float)sender.Size.Width - 420;
            float areaH = 220;

            float cx = areaX + areaW * 0.5f;
            float cy = areaY + areaH * 0.65f;

            float x = cx + (float)Math.Sin(t * 2.2f) * (areaW * 0.35f);
            float y = cy + (float)Math.Cos(t * 1.3f) * 35f;

            ds.FillCircle(x, y, 22f, Colors.DeepSkyBlue);
            ds.DrawCircle(x, y, 22f, Colors.White, 3f);

            // Một chút “parallax” giả: vẽ thêm vài hạt chạy (động)
            for (int i = 0; i < 20; i++)
            {
                float p = (t * 60 + i * 30) % areaW;
                float px = areaX + p;
                float py = areaY + 60 + (i % 5) * 30;
                ds.FillCircle(px, py, 2.5f, Color.FromArgb(180, 255, 255, 255));
            }
        }

        private static void DrawGrid(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, Size size, int step, Color color)
        {
            float w = (float)size.Width;
            float h = (float)size.Height;

            for (float x = 0; x <= w; x += step)
                ds.DrawLine(x, 0, x, h, color, 1);

            for (float y = 0; y <= h; y += step)
                ds.DrawLine(0, y, w, y, color, 1);
        }
    }
}
