using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D.ProgresCircle;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ProgressCircle : Window
{
    private CanvasBitmap? _sheet;

    // Grid frame của sprite sheet (ảnh bạn: 960x1152 => 10x12 => 96x96)
    private int _cols = 10;
    private int _rows = 12;

    private int _totalFrames;
    private int _frameIndex;

    // Tổng thời gian chạy hết tất cả frame rồi lặp
    private const double LoopSeconds = 2.0;
    private double _t; // accumulated time (seconds)
    public ProgressCircle()
    {
        InitializeComponent();
    }
    private void Canvas_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
    {
        args.TrackAsyncAction(LoadAsync(sender).AsAsyncAction());
    }

    private async System.Threading.Tasks.Task LoadAsync(CanvasAnimatedControl sender)
    {
        // Load từ Assets trong package
        var uri = new Uri("ms-appx:///Assets/ProgressFrames.jpg");
        _sheet = await CanvasBitmap.LoadAsync(sender, uri);

        // Nếu ảnh không đúng 10x12, vẫn đảm bảo an toàn:
        // - Ưu tiên giữ 10x12 nếu chia hết và frame vuông
        // - Nếu không, bạn có thể chỉnh _cols/_rows theo ảnh thực tế
        var w = (int)_sheet.SizeInPixels.Width;
        var h = (int)_sheet.SizeInPixels.Height;

        if (w % _cols != 0 || h % _rows != 0)
        {
            // fallback đơn giản: thử suy ra frame vuông theo "ước số"
            // (nếu bạn đổi sprite sheet khác)
            InferGrid(w, h, out _cols, out _rows);
        }

        _totalFrames = _cols * _rows;
        _frameIndex = 0;
        _t = 0;
    }

    private void Canvas_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
    {
        if (_sheet == null || _totalFrames <= 0) return;

        _t += args.Timing.ElapsedTime.TotalSeconds;

        // time trong [0..LoopSeconds)
        var loopT = _t % LoopSeconds;

        // map sang frame 0..totalFrames-1
        var u = loopT / LoopSeconds; // 0..1
        var idx = (int)(u * _totalFrames);

        if (idx >= _totalFrames) idx = _totalFrames - 1;
        _frameIndex = idx;
    }

    private void Canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        if (_sheet == null) return;

        var ds = args.DrawingSession;
        ds.Clear(Colors.White);

        // Kích thước 1 frame
        float frameW = (float)_sheet.SizeInPixels.Width / _cols;
        float frameH = (float)_sheet.SizeInPixels.Height / _rows;

        // Lấy source rect theo thứ tự: trái->phải, trên->dưới
        int col = _frameIndex % _cols;
        int row = _frameIndex / _cols;

        var src = new Rect(col * frameW, row * frameH, frameW, frameH);

        // Vẽ ra giữa canvas, scale vừa khung (giữ tỉ lệ)
        var cw = (float)sender.Size.Width;
        var ch = (float)sender.Size.Height;

        var scale = MathF.Min(cw / frameW, ch / frameH) * 0.85f;
        var dw = frameW * scale;
        var dh = frameH * scale;

        var dest = new Rect((cw - dw) * 0.5f, (ch - dh) * 0.5f, dw, dh);

        ds.DrawImage(_sheet, dest, src);
    }

    // Heuristic fallback: tìm lưới có frame vuông hợp lý
    private static void InferGrid(int w, int h, out int cols, out int rows)
    {
        cols = 10; rows = 12; // default

        // tìm frame size vuông: s là ước chung của w và h,
        // chọn s trong khoảng [24..256] và ưu tiên gần 96
        int bestS = -1;
        int bestScore = int.MaxValue;

        for (int s = 24; s <= 256; s++)
        {
            if (w % s != 0 || h % s != 0) continue;
            int c = w / s;
            int r = h / s;
            if (c <= 0 || r <= 0) continue;

            int score = Math.Abs(s - 96) + Math.Abs(c * r - 120); // ưu tiên giống ảnh mẫu
            if (score < bestScore)
            {
                bestScore = score;
                bestS = s;
                cols = c;
                rows = r;
            }
        }

        if (bestS < 0)
        {
            // nếu chịu: coi như 1 hàng
            cols = 1;
            rows = 1;
        }
    }
}
