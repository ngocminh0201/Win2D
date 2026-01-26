using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
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
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows.Text
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TextRenderingWindow : Window
    {
        public TextRenderingWindow()
        {
            InitializeComponent();
        }
        private CanvasTextFormat _format2;

        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            _format2 = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontWeight = FontWeights.Bold,
                FontSize = 26,
                WordWrapping = CanvasWordWrapping.Wrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center,
            };
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            string text =
                "Tiếng Việt có dấu: Trường học, Nguyễn.\n" +
                "ภาษาไทยมีวรรณยุกต์: สวัสดีครับ\n" +
                "Arabic (RTL): مرحبا بك\n" +
                "Emoji + ZWJ: 👨‍👩‍👧‍👦  👩🏽‍💻";

            // Khung layout để wrap theo width/height
            float boxWidth = (float)sender.ActualWidth - 40;
            float boxHeight = (float)sender.ActualHeight - 40;
            using (var layout = new CanvasTextLayout(ds, text, _format2, boxWidth, boxHeight))
            {
                // Vẽ text layout (shaping/bidi/fallback do DirectWrite xử lý)
                ds.DrawTextLayout(layout, 20, 20, Colors.Black);
            }
        }
    }
}
