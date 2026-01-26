using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Numerics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows.Text
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TextToGeometryWindow : Window
    {
        public TextToGeometryWindow()
        {
            InitializeComponent();
        }
        CanvasTextFormat _format;

        void Canvas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            _format = new CanvasTextFormat
            {
                FontFamily = "Segoe UI Black",
                FontSize = 110,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            // 1) Tạo layout cho chữ
            string text = "MASK";
            var layout = new CanvasTextLayout(sender, text, _format, 0.0f, 0.0f);

            // 2) Vị trí đặt chữ
            var pos = new Vector2(80, 80);

            // 3) Text -> Geometry (chữ thành path)
            using CanvasGeometry textGeo = CanvasGeometry.CreateText(layout);
            using CanvasGeometry movedGeo = textGeo.Transform(Matrix3x2.CreateTranslation(pos));

            // --- A) Fill bằng gradient ---
            var gradient = new CanvasLinearGradientBrush(sender, new[]
            {
        new CanvasGradientStop { Position = 0.0f, Color = Colors.DeepSkyBlue },
        new CanvasGradientStop { Position = 1.0f, Color = Colors.MediumPurple }
    })
            {
                StartPoint = new Vector2(pos.X, pos.Y),
                EndPoint = new Vector2(pos.X + 500, pos.Y + 0)
            };

            ds.FillGeometry(movedGeo, gradient);

            // --- B) Stroke (vẽ viền chữ) ---
            var strokeStyle = new CanvasStrokeStyle
            {
                LineJoin = CanvasLineJoin.Round
            };

            ds.DrawGeometry(movedGeo, Colors.White, 8.0f, strokeStyle);

            // --- C) Clip theo chữ: chỉ vẽ "bên trong chữ" ---
            // Tạo layer có mask là geometry của chữ.
            using (ds.CreateLayer(1.0f, movedGeo))
            {
                // Vẽ vài thứ "to bự" - nhưng chỉ hiện trong vùng chữ
                ds.DrawLine(pos + new Vector2(-50, 20), pos + new Vector2(700, 220), Colors.Yellow, 18);
                ds.DrawLine(pos + new Vector2(-50, 120), pos + new Vector2(700, 320), Colors.LimeGreen, 18);
                ds.DrawCircle(pos + new Vector2(250, 120), 120, Colors.OrangeRed, 16);
            }

            // (Tuỳ chọn) Vẽ bounding box để dễ hình dung vùng layout
            // var bounds = movedGeo.ComputeBounds();
            // ds.DrawRectangle(bounds, Colors.Red, 2);
        }
    }
}
