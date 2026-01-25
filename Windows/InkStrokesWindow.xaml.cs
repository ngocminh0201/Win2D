using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input.Inking;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InkStrokesWindow : Window
    {
        // Bitmap cache: nơi “rasterize” ink vào
        private CanvasRenderTarget? _inkRaster;

        // Stroke đang vẽ dở
        //private Stroke? _currentStroke;

        // Nếu bạn muốn vẫn giữ toàn bộ vector strokes (để undo, select…)
        //private readonly List<Stroke> _strokes = new();

        // Để detect resize/DPI change
        private Size _rasterSize;
        private float _rasterDpi;
        public InkStrokesWindow()
        {
            InitializeComponent();
        }
        //private void canvas_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        //{
        //    EnsureRaster(sender);

        //    // Clear trắng lần đầu
        //    using var ds = _inkRaster!.CreateDrawingSession();
        //    ds.Clear(Colors.Transparent);
        //}

        //private void canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        //{
        //    EnsureRaster((CanvasAnimatedControl)sender);

        //    var ds = args.DrawingSession;
        //    ds.Clear(Colors.White);

        //    // 1) Vẽ ink đã rasterize (rẻ)
        //    if (_inkRaster != null)
        //    {
        //        // Ví dụ: blur nhẹ để tạo “soft ink”
        //        var blur = new GaussianBlurEffect
        //        {
        //            Source = _inkRaster,
        //            BlurAmount = 1.8f
        //        };
        //        ds.DrawImage(_inkRaster);
        //    }

        //    // 2) Vẽ “wet ink” (stroke đang vẽ dở) trực tiếp để realtime mượt
        //    if (_currentStroke != null)
        //    {
        //        DrawStrokeVector(ds, _currentStroke);
        //    }
        //}

        //// ===== Pointer events: thu điểm + pressure =====

        //private void canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        //{
        //    var p = e.GetCurrentPoint(canvas);
        //    if (!p.Properties.IsLeftButtonPressed) return;

        //    _currentStroke = new Stroke();
        //    _currentStroke.Add(p.Position, GetPressure(p));

        //    canvas.CapturePointer(e.Pointer);
        //    canvas.Invalidate();
        //}

        //private void canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        //{
        //    if (_currentStroke == null) return;

        //    var p = e.GetCurrentPoint(canvas);
        //    if (!p.IsInContact) return;

        //    // Thêm điểm (có thể lọc bớt nếu quá dày)
        //    _currentStroke.Add(p.Position, GetPressure(p));
        //    canvas.Invalidate();
        //}

        //private void canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        //{
        //    CommitCurrentStrokeToRaster();
        //    canvas.ReleasePointerCapture(e.Pointer);
        //    canvas.Invalidate();
        //}

        //private void canvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
        //{
        //    _currentStroke = null;
        //    canvas.Invalidate();
        //}

        // ===== Core idea: “rasterize” stroke vào bitmap =====

        //private void CommitCurrentStrokeToRaster()
        //{
        //    if (_currentStroke == null || _inkRaster == null) return;
        //    if (_currentStroke.Points.Count < 2) { _currentStroke = null; return; }

        //    // Lưu vector stroke (phục vụ undo/select nếu muốn)
        //    _strokes.Add(_currentStroke);

        //    // Rasterize: vẽ stroke vào CanvasRenderTarget đúng 1 lần
        //    using (var ds = _inkRaster.CreateDrawingSession())
        //    {
        //        DrawStrokeVector(ds, _currentStroke);
        //    }

        //    _currentStroke = null;
        //}

        //private void EnsureRaster(CanvasAnimatedControl sender)
        //{
        //    if (sender.Size.Width <= 1 || sender.Size.Height <= 1) return;

        //    float dpi = sender.Dpi;
        //    bool needRecreate =
        //        _inkRaster == null ||
        //        _rasterSize.Width != sender.Size.Width ||
        //        _rasterSize.Height != sender.Size.Height ||
        //        Math.Abs(_rasterDpi - dpi) > 0.01f;

        //    if (!needRecreate) return;

        //    _rasterSize = sender.Size;
        //    _rasterDpi = dpi;

        //    _inkRaster?.Dispose();
        //    _inkRaster = new CanvasRenderTarget(
        //        sender.Device,
        //        (float)_rasterSize.Width,
        //        (float)_rasterSize.Height,
        //        _rasterDpi);

        //    // Nếu recreate bitmap: bạn có thể “replay” toàn bộ _strokes vào bitmap
        //    using var ds = _inkRaster.CreateDrawingSession();
        //    ds.Clear(Colors.Transparent);
        //    foreach (var s in _strokes)
        //        DrawStrokeVector(ds, s);
        //}

        //private static float GetPressure(Microsoft.UI.Input.PointerPoint p)
        //{
        //    // Pen pressure: thường 0..1. Mouse/touch có thể trả 0 hoặc 0.5 tuỳ máy.
        //    float pressure = (float)p.Properties.Pressure;
        //    return pressure <= 0 ? 0.5f : pressure;
        //}

        //// Vẽ stroke dạng polyline “có độ dày theo pressure”
        //private static void DrawStrokeVector(CanvasDrawingSession ds, Stroke stroke)
        //{
        //    // Base thickness
        //    const float minW = 1.5f;
        //    const float maxW = 6.0f;

        //    for (int i = 1; i < stroke.Points.Count; i++)
        //    {
        //        var a = stroke.Points[i - 1];
        //        var b = stroke.Points[i];

        //        float pa = stroke.Pressures[i - 1];
        //        float pb = stroke.Pressures[i];

        //        float w = Lerp(minW, maxW, (pa + pb) * 0.5f);

        //        ds.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, Colors.SpringGreen, w);
        //    }
        //}

        //private static float Lerp(float a, float b, float t)
        //    => a + (b - a) * Math.Clamp(t, 0f, 1f);

        //private sealed class Stroke
        //{
        //    public List<Point> Points { get; } = new();
        //    public List<float> Pressures { get; } = new();

        //    public void Add(Point p, float pressure)
        //    {
        //        Points.Add(p);
        //        Pressures.Add(pressure);
        //    }
        //}
        private void canvas_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            EnsureRaster(sender);

            using var ds = _inkRaster!.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
        }
        private static int x = 0;
        private void canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            x++;
            Debug.WriteLine("ccc"+ x);
            EnsureRaster((CanvasAnimatedControl)sender);

            var ds = args.DrawingSession;
            ds.Clear(Colors.White);

            if (_inkRaster != null)
            {
                // Không nilon: chỉ vẽ “tờ giấy”
                ds.DrawImage(_inkRaster);
            }
        }
        private Point? _lastPoint = null;
        private float _lastPressure = 0.5f;
        private bool _isDrawing = false;
        private void canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            EnsureRaster(canvas);

            var p = e.GetCurrentPoint(canvas);
            if (!p.Properties.IsLeftButtonPressed) return;

            _isDrawing = true;
            _lastPoint = p.Position;
            _lastPressure = GetPressure(p);

            canvas.CapturePointer(e.Pointer);
        }

        private void canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDrawing || _inkRaster == null || _lastPoint == null) return;

            var p = e.GetCurrentPoint(canvas);
            if (!p.IsInContact) return;

            var currPoint = p.Position;
            var currPressure = GetPressure(p);

            // Vẽ thẳng vào bitmap ngay tại đây (commit từng đoạn)
            using (var ds = _inkRaster.CreateDrawingSession())
            {
                float w = GetWidth((_lastPressure + currPressure) * 0.5f);
                ds.DrawLine(
                    (float)_lastPoint.Value.X, (float)_lastPoint.Value.Y,
                    (float)currPoint.X, (float)currPoint.Y,
                    Colors.Black, w);
            }

            _lastPoint = currPoint;
            _lastPressure = currPressure;

            // Request redraw để thấy cập nhật
            canvas.Invalidate();
        }

        private void canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDrawing = false;
            _lastPoint = null;
            canvas.ReleasePointerCapture(e.Pointer);
            canvas.Invalidate();
        }

        private void canvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _isDrawing = false;
            _lastPoint = null;
            canvas.Invalidate();
        }

        private void EnsureRaster(CanvasAnimatedControl sender)
        {
            if (sender.Size.Width <= 1 || sender.Size.Height <= 1) return;

            float dpi = sender.Dpi;
            bool needRecreate =
                _inkRaster == null ||
                _rasterSize.Width != sender.Size.Width ||
                _rasterSize.Height != sender.Size.Height ||
                Math.Abs(_rasterDpi - dpi) > 0.01f;

            if (!needRecreate) return;

            _rasterSize = sender.Size;
            _rasterDpi = dpi;

            _inkRaster?.Dispose();
            _inkRaster = new CanvasRenderTarget(
                sender.Device,
                (float)_rasterSize.Width,
                (float)_rasterSize.Height,
                _rasterDpi);

            // Bitmap mới -> xóa sạch
            using var ds = _inkRaster.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
        }

        private static float GetPressure(Microsoft.UI.Input.PointerPoint p)
        {
            float pressure = (float)p.Properties.Pressure;
            return pressure <= 0 ? 0.5f : pressure;
        }

        private static float GetWidth(float pressure)
        {
            const float minW = 1.5f;
            const float maxW = 6.0f;
            pressure = Math.Clamp(pressure, 0f, 1f);
            return minW + (maxW - minW) * pressure;
        }
    }
}
