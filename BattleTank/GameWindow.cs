using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Win2D.BattleTank
{
    public sealed class GameWindow : Window
    {
        private readonly Grid _root = new();
        private readonly Grid _hud = new();
        private readonly CanvasSwapChainPanel _swapChainPanel = new();
        private readonly TextBox _keySink = new();

        private readonly TextBlock _scoreText = new();
        private readonly TextBlock _livesText = new();
        private readonly TextBlock _levelText = new();
        private readonly TextBlock _fpsText = new();

        private CanvasDevice? _device;
        private CanvasSwapChain? _swapChain;

        private readonly InputState _input = new();
        private readonly GameEngine _engine = new();

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastTicks;
        private double _accumulator;

        // Fixed-step để update ổn định (điều khiển mượt, collision không “nhảy”)
        private const double FixedDt = 1.0 / 120.0;   // 120Hz update
        private const int MaxStepsPerFrame = 5;

        // FPS counter
        private double _fpsAccum;
        private int _fpsFrames;

        public GameWindow()
        {
            Title = "BattleTank (WinUI 3 + Win2D)";

            BuildUI();
            Content = _root;

            _swapChainPanel.Loaded += (_, __) =>
            {
                _device = CanvasDevice.GetSharedDevice();
                _engine.ResetToLevel1();
                _lastTicks = _clock.ElapsedTicks;

                EnsureSwapChain();
                HookInput();

                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;
                Closed += (_, __) => Cleanup();
            };
        }

        private void BuildUI()
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Background
            _root.Background = new SolidColorBrush(Color.FromArgb(255, 10, 14, 24));

            // HUD bar
            _hud.Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
            _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            SetupHudText(_scoreText, "Score: 0", 0);
            SetupHudText(_livesText, "Lives: 3", 1);
            SetupHudText(_levelText, "Level: 1", 2);
            SetupHudText(_fpsText, "FPS: --", 3);

            _hud.Children.Add(_scoreText);
            _hud.Children.Add(_livesText);
            _hud.Children.Add(_levelText);
            _hud.Children.Add(_fpsText);

            Grid.SetRow(_hud, 0);
            _root.Children.Add(_hud);

            // SwapChainPanel
            _swapChainPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            _swapChainPanel.VerticalAlignment = VerticalAlignment.Stretch;

            // Container cho playfield
            var playfield = new Grid
            {
                Margin = new Thickness(18, 14, 18, 18)
            };

            playfield.Children.Add(_swapChainPanel);

            // Key sink overlay (focusable)
            _keySink.Opacity = 0.01; // gần như trong suốt nhưng vẫn nhận input
            _keySink.Background = new SolidColorBrush(Colors.Transparent);
            _keySink.BorderThickness = new Thickness(0);
            _keySink.IsReadOnly = true;
            _keySink.Text = ""; // không nhập text
            _keySink.IsSpellCheckEnabled = false;
            _keySink.IsTextPredictionEnabled = false;
            _keySink.HorizontalAlignment = HorizontalAlignment.Stretch;
            _keySink.VerticalAlignment = VerticalAlignment.Stretch;

            // Bắt cả PreviewKeyDown để chặn TextBox “ăn” arrow keys
            _keySink.PreviewKeyDown += OnKeyDown;
            _keySink.PreviewKeyUp += OnKeyUp;

            playfield.Children.Add(_keySink);

            Grid.SetRow(playfield, 1);
            _root.Children.Add(playfield);
        }

        private static void SetupHudText(TextBlock tb, string text, int col)
        {
            tb.Text = text;
            tb.Foreground = new SolidColorBrush(Colors.White);
            tb.Opacity = 0.92;
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.Margin = new Thickness(14, 0, 14, 0);
            tb.FontSize = 14;
            Grid.SetColumn(tb, col);
        }

        private void HookInput()
        {
            // Click vào vùng game là focus ngay để nhận phím
            _swapChainPanel.PointerPressed += (_, __) => _keySink.Focus(FocusState.Programmatic);

            // Focus ngay khi app mở
            _keySink.Focus(FocusState.Programmatic);

            Activated += (_, __) => _keySink.Focus(FocusState.Programmatic);

            _swapChainPanel.SizeChanged += (_, __) => EnsureSwapChain();
        }


        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            _input.OnKeyDown(e.Key);

            // One-shot keys
            if (e.Key == VirtualKey.Escape) _engine.TogglePause();
            if (e.Key == VirtualKey.Enter) _engine.TryRestartIfGameOver();

            e.Handled = true;
        }

        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            _input.OnKeyUp(e.Key);
            e.Handled = true;
        }

        private void EnsureSwapChain()
        {
            if (_device is null) return;

            float w = (float)_swapChainPanel.ActualWidth;
            float h = (float)_swapChainPanel.ActualHeight;
            if (w <= 2 || h <= 2) return;

            float raster = (float)(_swapChainPanel.XamlRoot?.RasterizationScale ?? 1f);
            float dpi = 96f * raster;

            if (_swapChain is null)
            {
                _swapChain = new CanvasSwapChain(_device, w, h, dpi);
                _swapChainPanel.SwapChain = _swapChain;
                return;
            }

            // Resize in DIPs (Win2D doc)
            // ResizeBuffers(Size) is in DIPs. :contentReference[oaicite:3]{index=3}
            // Use overload to also update DPI when needed.
            if (MathF.Abs(_swapChain.SizeInPixels.Width / (_swapChain.Dpi / 96f) - w) > 1.0f ||
                MathF.Abs(_swapChain.SizeInPixels.Height / (_swapChain.Dpi / 96f) - h) > 1.0f ||
                MathF.Abs(_swapChain.Dpi - dpi) > 0.5f)
            {
                _swapChain.ResizeBuffers(w, h, dpi);
            }
        }

        private void OnRendering(object? sender, object e)
        {
            if (_swapChain is null) return;

            // dt
            long now = _clock.ElapsedTicks;
            double dt = (now - _lastTicks) / (double)Stopwatch.Frequency;
            _lastTicks = now;

            // avoid huge dt when dragging window, breakpoints, etc.
            if (dt > 0.25) dt = 0.25;

            _accumulator += dt;

            int steps = 0;
            while (_accumulator >= FixedDt && steps < MaxStepsPerFrame)
            {
                _engine.Update((float)FixedDt, _input);
                _accumulator -= FixedDt;
                steps++;
            }

            using (var ds = _swapChain.CreateDrawingSession(Color.FromArgb(255, 8, 10, 18)))
            {
                _engine.Render(ds, _swapChain.Size.ToVector2());
            }

            // vsync present (syncInterval=1).  :contentReference[oaicite:4]{index=4}
            _swapChain.Present(1);

            UpdateHud(dt);
        }

        private void UpdateHud(double dt)
        {
            // Update HUD at a low rate to reduce XAML churn
            _fpsAccum += dt;
            _fpsFrames++;

            if (_fpsAccum >= 0.25)
            {
                double fps = _fpsFrames / _fpsAccum;
                _fpsText.Text = $"FPS: {fps:0}";

                _scoreText.Text = $"Score: {_engine.Score}";
                _livesText.Text = $"Lives: {_engine.Lives}";
                _levelText.Text = $"Level: {_engine.Level}";

                _fpsAccum = 0;
                _fpsFrames = 0;
            }
        }

        private void Cleanup()
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;

            _swapChain?.Dispose();
            _swapChain = null;

            _keySink.PreviewKeyDown -= OnKeyDown;
            _keySink.PreviewKeyUp -= OnKeyUp;
        }
    }

    file static class VectorExt
    {
        public static Vector2 ToVector2(this Size s) => new((float)s.Width, (float)s.Height);
    }
}
