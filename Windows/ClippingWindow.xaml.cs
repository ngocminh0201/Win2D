using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
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
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ClippingWindow : Window
    {
        public ClippingWindow()
        {
            InitializeComponent();
        }
        private CanvasTextFormat _maskTextFormat = new()
        {
            FontFamily = "Segoe UI Black",
            FontSize = 72,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };
        private void canvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {

        }

        private void canvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            var size = sender.Size.ToVector2();
            float t = (float)args.Timing.TotalTime.TotalSeconds;

            ds.Clear(Color.FromArgb(255, 12, 14, 20));

            // Layout: 2 ô trái/phải
            var pad = 24f;
            var cellW = (size.X - pad * 3) / 2f;
            var cellH = size.Y - pad * 2;

            var leftRect = new Rect(pad, pad, cellW, cellH);
            var rightRect = new Rect(pad * 2 + cellW, pad, cellW, cellH);

            DrawPanelChrome(ds, leftRect, "CLIPPING (CreateLayer + Geometry)");
            DrawPanelChrome(ds, rightRect, "MASKING (AlphaMaskEffect)");

            // Pattern: sọc chéo chạy (đây là thứ “vẽ tràn” ra ngoài)
            // Ta sẽ dùng nó cho cả clipping và masking để thấy khác nhau.
            using var stripes = new CanvasCommandList(ds.Device);
            using (var pds = stripes.CreateDrawingSession())
            {
                pds.Clear(Colors.Transparent);

                // nền nhạt
                pds.FillRectangle(0, 0, (float)leftRect.Width, (float)leftRect.Height,
                    Color.FromArgb(255, 18, 22, 32));

                // vẽ sọc chéo, và cho chúng "trôi"
                // Dịch theo thời gian để nhìn chuyển động rõ ràng.
                var offset = (t * 120f) % 80f;

                // xoay hệ tọa độ cho ra sọc chéo đẹp
                pds.Transform =
                    Matrix3x2.CreateTranslation(-200, -200) *
                    Matrix3x2.CreateRotation(-0.55f) *
                    Matrix3x2.CreateTranslation(200, 200);

                for (float x = -400; x < 1200; x += 24f)
                {
                    var xx = x + offset;
                    pds.FillRectangle(xx, -400, 12, 1400, Color.FromArgb(200, 90, 200, 255));
                    pds.FillRectangle(xx + 12, -400, 12, 1400, Color.FromArgb(200, 255, 160, 90));
                }

                // reset transform
                pds.Transform = Matrix3x2.Identity;

                // thêm vài hạt sáng để nhìn “bị cắt” cho rõ
                for (int i = 0; i < 18; i++)
                {
                    float fx = (float)(Math.Sin(t * 1.2 + i) * 0.5 + 0.5) * (float)leftRect.Width;
                    float fy = (float)(Math.Cos(t * 0.9 + i * 1.7) * 0.5 + 0.5) * (float)leftRect.Height;

                    pds.FillCircle(fx, fy, 6,
                        Color.FromArgb(180, 255, 255, 255));
                }
            }

            // ========= (1) CLIPPING =========
            // Tạo geometry ngôi sao làm vùng clip
            var leftCenter = new Vector2(
                (float)leftRect.X + (float)leftRect.Width / 2f,
                (float)leftRect.Y + (float)leftRect.Height / 2f);

            using var star = CreateStarGeometry(
                ds.Device,
                leftCenter,
                outerRadius: MathF.Min((float)leftRect.Width, (float)leftRect.Height) * 0.33f,
                innerRadius: MathF.Min((float)leftRect.Width, (float)leftRect.Height) * 0.15f,
                points: 5);

            // CreateLayer(opacity, geometry) => mọi thứ vẽ trong using sẽ bị "cắt" theo geometry
            using (ds.CreateLayer(1f, star))
            {
                // Vẽ pattern "tràn" nhưng sẽ bị clip trong ngôi sao
                ds.DrawImage(stripes, new Vector2((float)leftRect.X, (float)leftRect.Y));
            }

            // Viền để thấy vùng clip là ngôi sao
            ds.DrawGeometry(star, Color.FromArgb(255, 235, 235, 255), 2f);

            // ========= (2) MASKING =========
            // Source: cùng pattern sọc đó
            // AlphaMask: ta tạo 1 mask bằng cách vẽ chữ “MASK” + vòng tròn mềm (white = hiện, black = ẩn)
            var rightTopLeft = new Vector2((float)rightRect.X, (float)rightRect.Y);

            using var maskCmd = new CanvasCommandList(ds.Device);
            using (var mds = maskCmd.CreateDrawingSession())
            {
                mds.Clear(Colors.Transparent);

                // 1) 2 chữ "MASK" cạnh nhau
                var textRect = new Rect(0, 0, rightRect.Width, rightRect.Height);
                float y = (float)textRect.Y + (float)textRect.Height / 2f - 10;

                // đặt 2 vị trí theo 2/5 và 3/5 chiều ngang để nhìn “cạnh nhau” rõ ràng
                float xLeft = (float)textRect.X + (float)textRect.Width * 0.30f;
                float xRight = (float)textRect.X + (float)textRect.Width * 0.70f;

                mds.DrawText("MASK", xLeft, y, Color.FromArgb(80, 255, 0, 0), _maskTextFormat);
                mds.DrawText("MASK", xRight, y, Color.FromArgb(255, 255, 0, 0), _maskTextFormat);


                // 2) thêm 1 vòng tròn mềm bằng radial gradient alpha (để thấy “mask” không chỉ là hình cứng)
                var c = new Vector2((float)rightRect.Width * 0.78f, (float)rightRect.Height * 0.78f);
                using var radial = new Microsoft.Graphics.Canvas.Brushes.CanvasRadialGradientBrush(ds.Device,
                    new[]
                    {
                    new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop(0f, Color.FromArgb(255,255,255,255)),
                    new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop(1f, Color.FromArgb(0,255,255,255)),
                    })
                {
                    Center = c,
                    RadiusX = 110,
                    RadiusY = 110
                };
                mds.FillCircle(c, 110, radial);
            }

            var masked = new AlphaMaskEffect
            {
                Source = stripes,     // màu lấy từ pattern
                AlphaMask = maskCmd   // alpha quyết định chỗ nào hiện/ẩn
            };

            ds.DrawImage(masked, rightTopLeft);

            // Vẽ viền chữ nhật để bạn thấy mask đang diễn ra "trong panel" chứ không phải do layout
            ds.DrawRectangle(rightRect, Color.FromArgb(80, 255, 255, 255), 1f);
        }

        private static void DrawPanelChrome(CanvasDrawingSession ds, Rect r, string title)
        {
            // nền panel
            ds.FillRoundedRectangle(r, 16, 16, Color.FromArgb(255, 16, 18, 26));
            ds.DrawRoundedRectangle(r, 16, 16, Color.FromArgb(50, 255, 255, 255), 1f);

            // title
            ds.DrawText(title, (float)r.X + 14, (float)r.Y + 12,
                Color.FromArgb(200, 220, 230, 255),
                new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 14 });
        }

        private static CanvasGeometry CreateStarGeometry(
            ICanvasResourceCreator rc,
            Vector2 center,
            float outerRadius,
            float innerRadius,
            int points)
        {
            using var pb = new CanvasPathBuilder(rc);

            float a0 = -MathF.PI / 2f;
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                float a = a0 + i * (MathF.PI / points);

                var p = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                if (i == 0) pb.BeginFigure(p);
                else pb.AddLine(p);
            }

            pb.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(pb);
        }
    }
}
