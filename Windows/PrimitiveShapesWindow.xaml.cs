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
    public sealed partial class PrimitiveShapesWindow : Window
    {
        public PrimitiveShapesWindow()
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

            // 1) Primitive: line / rect / circle
            ds.DrawLine(40, 40, 260, 40, Colors.White, 2);

            ds.FillRectangle(40, 70, 120, 70, Colors.DodgerBlue);
            ds.DrawRectangle(40, 70, 120, 70, Colors.White, 2);

            ds.FillCircle(230, 105, 35, Colors.Orange);
            ds.DrawCircle(230, 105, 35, Colors.White, 2);

            // 2) Complex path: vẽ “mũi tên/chevron” bằng CanvasPathBuilder
            using var pb = new CanvasPathBuilder(ds.Device);
            pb.BeginFigure(new Vector2(60, 190));
            pb.AddLine(new Vector2(160, 190));
            pb.AddLine(new Vector2(160, 170));
            pb.AddLine(new Vector2(220, 210));
            pb.AddLine(new Vector2(160, 250));
            pb.AddLine(new Vector2(160, 230));
            pb.AddLine(new Vector2(60, 230));
            pb.EndFigure(CanvasFigureLoop.Closed);

            using var geo = CanvasGeometry.CreatePath(pb);
            //ds.FillGeometry(geo, Colors.MediumSeaGreen);
            ds.DrawGeometry(geo, Colors.Red, 2);

        }
    }
}
