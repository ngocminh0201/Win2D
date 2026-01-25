using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime; // AsAsyncAction()
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SVGImageWindow : Window
    {
        private CanvasSvgDocument? _svg;
        private readonly DispatcherTimer _timer;
        private float _time;
        public SVGImageWindow()
        {
            InitializeComponent();

            // LEFT: XAML Image loads SVG
            SvgImage.Source = new SvgImageSource(new Uri("ms-appx:///Assets/sample.svg"));

            // simple animation loop for mask reveal
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _timer.Tick += (_, __) =>
            {
                _time += 1f / 60f;
                MyCanvas.Invalidate();
            };
            _timer.Start();
        }
        private void MyCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // Track async loading properly (avoid "Method name expected")
            args.TrackAsyncAction(LoadSvgAsync(sender).AsAsyncAction());
        }

        private async Task LoadSvgAsync(CanvasControl sender)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/sample.svg"));

            using var stream = await file.OpenReadAsync();
            _svg = await CanvasSvgDocument.LoadAsync(sender, stream);
        }

        private void MyCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Clear(Transparent());

            if (_svg == null) return;

            // 3 blocks (stacked vertically)
            DrawLabel(ds, "1) Normal DrawSvg (like Image)", 0, 0);
            DrawSvgInBox(ds, _svg, new Rect(20, 28, 170, 170));

            DrawLabel(ds, "2) Tint + Shadow (effects)", 0, 145);
            DrawWithTintShadow(ds, sender, new Vector2(20, 173), 170);

            DrawLabel(ds, "3) AlphaMask reveal (animated)", 0, 290);
            float t01 = (float)(Math.Sin(_time) * 0.5 + 0.5); // 0..1
            DrawRevealMasked(ds, sender, new Vector2(20, 318), 170, t01);
        }

        // -------- Helpers: Safe Colors (avoid Windows.UI.Colors vs Microsoft.UI.Colors mismatch) --------
        private static Color ARGB(byte a, byte r, byte g, byte b)
            => Color.FromArgb(a, r, g, b);

        private static Color White() => ARGB(255, 255, 255, 255);
        private static Color WhiteSmoke() => ARGB(255, 245, 245, 245);
        private static Color Transparent() => ARGB(0, 0, 0, 0);

        private static void DrawLabel(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, string text, float x, float y)
        {
            ds.DrawText(text, x, y, White());
            ds.DrawLine(x, y + 20, x + 420, y + 20, WhiteSmoke(), 1);
        }

        // -------- Core: Draw SVG using Rect overload (fix "Matrix3x2 -> Size" error) --------
        private static void DrawSvgInBox(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, CanvasSvgDocument svg, Rect box)
        {
            var old = ds.Transform;

            // Move origin to box.X, box.Y
            ds.Transform = Matrix3x2.CreateTranslation((float)box.X, (float)box.Y) * old;

            // Draw at (0,0) with specified size
            ds.DrawSvg(svg, new Size(box.Width, box.Height));

            ds.Transform = old;
        }


        // -------- Render SVG to a target (so we can apply effects) --------
        private CanvasRenderTarget RenderSvgToTarget(CanvasControl canvas, float size)
        {
            var rt = new CanvasRenderTarget(canvas, size, size, canvas.Dpi);

            using (var ds = rt.CreateDrawingSession())
            {
                ds.Clear(Transparent());
                ds.DrawSvg(_svg!, new Size(size, size)); // <-- Size overload
            }

            return rt;
        }


        // -------- Effect #1: Tint + Shadow --------
        private void DrawWithTintShadow(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, CanvasControl canvas, Vector2 pos, float size)
        {
            var src = RenderSvgToTarget(canvas, size);

            var tint = new ColorMatrixEffect
            {
                Source = src,
                ColorMatrix = new Matrix5x4
                {
                    // Slight "teal-ish" tint
                    M11 = 0.85f,
                    M12 = 0.05f,
                    M13 = 0.05f,
                    M21 = 0.10f,
                    M22 = 1.05f,
                    M23 = 0.10f,
                    M31 = 0.05f,
                    M32 = 0.15f,
                    M33 = 1.00f,
                    M44 = 1.00f
                }
            };

            var opacity = new OpacityEffect { Source = tint, Opacity = 0.85f };

            var shadow = new ShadowEffect
            {
                Source = opacity,
                BlurAmount = 18
            };

            ds.DrawImage(shadow, pos + new Vector2(10, 10));
            ds.DrawImage(opacity, pos);
        }

        // -------- Build mask with CanvasGradientStop[] (fix Color[]/float[] constructor errors) --------
        private static CanvasRenderTarget CreateSweepMask(CanvasControl canvas, float width, float height, float t01)
        {
            var rt = new CanvasRenderTarget(canvas, width, height, canvas.Dpi);

            using (var ds = rt.CreateDrawingSession())
            {
                ds.Clear(Transparent());

                float x = width * t01;

                // stops: white -> white -> transparent
                var stops = new CanvasGradientStop[]
                {
                    new CanvasGradientStop { Position = 0.0f, Color = White() },
                    new CanvasGradientStop { Position = 0.7f, Color = White() },
                    new CanvasGradientStop { Position = 1.0f, Color = Transparent() }
                };

                var brush = new CanvasLinearGradientBrush(canvas, stops)
                {
                    StartPoint = new Vector2(x - 90, 0),
                    EndPoint = new Vector2(x + 90, 0)
                };

                ds.FillRectangle(0, 0, width, height, brush);
            }

            return rt;
        }

        // -------- Effect #2: AlphaMask reveal --------
        private void DrawRevealMasked(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, CanvasControl canvas, Vector2 pos, float size, float t01)
        {
            var src = RenderSvgToTarget(canvas, size);
            var mask = CreateSweepMask(canvas, (float)src.Size.Width, (float)src.Size.Height, t01);

            var masked = new AlphaMaskEffect
            {
                Source = src,
                AlphaMask = mask
            };

            ds.DrawImage(masked, pos);
        }
    }
}
