using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
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
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GradientWindow : Window
    {
        public GradientWindow()
        {
            InitializeComponent();
        }

        private void Canvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {

        }

        private void Canvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Clear(Colors.Transparent);

            ds.Antialiasing = CanvasAntialiasing.Antialiased; // Khử răng cưa

            // Gradient fill
            var rect = new Vector2(300, 250);
            var size = new Vector2(260, 90);
            using var gradient = new CanvasLinearGradientBrush(ds.Device, new CanvasGradientStop[]
            {
                new(0f, Colors.DeepSkyBlue),
                new(0.5f, Colors.MediumPurple),
                new(1f, Colors.Orange),
            })
            {
                StartPoint = new Vector2(rect.X, rect.Y),
                EndPoint = new Vector2(rect.X + size.X, rect.Y)
            };
            ds.FillRoundedRectangle(rect.X, rect.Y, size.X, size.Y, 16, 16, gradient);

            // Stroke: dash + cap/join
            var stroke = new CanvasStrokeStyle
            {
                DashStyle = CanvasDashStyle.Dash,
                StartCap = CanvasCapStyle.Round,
                EndCap = CanvasCapStyle.Round,
                LineJoin = CanvasLineJoin.Round
            };

            ds.DrawRoundedRectangle(rect.X, rect.Y, size.X, size.Y, 16, 16, Colors.White, 5, stroke);


            rect = new Vector2(600, 250);
            size = new Vector2(260, 90);

            var center = new Vector2(rect.X + size.X * 0.5f, rect.Y + size.Y * 0.5f);

            // Radial gradient brush
            using var radial = new CanvasRadialGradientBrush(ds.Device, new CanvasGradientStop[]
            {
                new CanvasGradientStop(0.0f, Colors.DeepSkyBlue),          // tâm (sáng nhất)
                new CanvasGradientStop(0.35f, Colors.AliceBlue),
                new CanvasGradientStop(0.70f, Colors.MediumPurple),
                new CanvasGradientStop(1.0f, Colors.Orange)          // rìa (xa nhất)
            })
            {
                Center = center,

                // Bán kính theo trục X/Y (cho phép elip). Thử đổi các số này để thấy hiệu ứng.
                RadiusX = size.X * 0.95f,
                RadiusY = size.Y * 0.55f,

                OriginOffset = new Vector2(-30, -10)
            };
            // Fill rounded rect bằng radial gradient
            ds.FillRoundedRectangle(rect.X, rect.Y, size.X, size.Y, 16, 16, radial);

            // Viền để dễ nhìn
            ds.DrawRoundedRectangle(rect.X, rect.Y, size.X, size.Y, 16, 16, Colors.White, 2);
        }
    }
}
