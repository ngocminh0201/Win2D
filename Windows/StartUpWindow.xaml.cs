using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StartUpWindow : Window
    {
        private CanvasGeometry _geometry;
        private CanvasTextFormat _textFormat;
        private CanvasTextLayout _textLayout;
        private CanvasGeometry _textGeometry;
        public StartUpWindow()
        {
            InitializeComponent();
        }
        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // ===== 2) Geometry đơn giản nhất: 1 rounded-rectangle geometry =====
            _geometry = CanvasGeometry.CreateRoundedRectangle(
                sender,
                new Rect(40, 120, 260, 120),
                24, 24);

            // ===== 3) Text bình thường + Text -> Geometry =====
            _textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 48,
                WordWrapping = CanvasWordWrapping.NoWrap
            };

            _textLayout = new CanvasTextLayout(sender, "Vũ.", _textFormat, 1000, 1000);

            // Convert text outlines to geometry
            _textGeometry = CanvasGeometry.CreateText(_textLayout);
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Clear(Colors.Transparent);

            // ===== 1) Vector graphics đơn giản nhất: vẽ shape trực tiếp =====
            // (Vector ở đây là bạn vẽ bằng primitives: line/rect/circle...)
            ds.DrawLine(40, 60, 320, 60, Colors.Red, 4);
            ds.FillCircle(120, 60, 18, Colors.DeepSkyBlue);

            // ===== 2) Geometry đơn giản nhất: vẽ một CanvasGeometry =====
            ds.FillGeometry(_geometry, Colors.MediumPurple);
            ds.DrawGeometry(_geometry, Colors.White, 3);

            // ===== 3) Text bình thường + chính text đó biến thành geometry =====
            // 3a) Text render bình thường (layout)
            ds.DrawText("Vũ.", 40, 280, Colors.Orange, _textFormat);

            // 3b) Text -> geometry (fill/stroke như shape)
            ds.FillGeometry(_textGeometry, 40, 360, Colors.DeepSkyBlue);
            ds.DrawGeometry(_textGeometry, 40, 360, Colors.Black, 6);
        }
    }
}
