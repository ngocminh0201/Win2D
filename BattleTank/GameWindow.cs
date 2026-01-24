using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Microsoft.UI.Xaml.Documents;
using Windows.UI.Text;
using Microsoft.UI.Text;

namespace Win2D.BattleTank
{
    public sealed class GameWindow : Window
    {
        private readonly Grid _root = new();
        private readonly Grid _hud = new();
        private readonly CanvasSwapChainPanel _swapChainPanel = new();
        private readonly TextBox _keySink = new();

        // GameOver video layer
        private readonly Grid _videoLayer = new();
        private readonly MediaPlayerElement _videoPlayer = new();
        private readonly TextBlock _videoHint = new();

        private MediaPlayer? _mediaPlayer;
        private bool _showingGameOverVideo;

        private readonly TextBlock _scoreText = new();
        private readonly TextBlock _livesText = new();
        private readonly TextBlock _levelText = new();
        private readonly TextBlock _remainingText = new();
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

            _swapChainPanel.Loaded += async (_, __) =>
            {
                _device = CanvasDevice.GetSharedDevice();
                await GameAssets.LoadAsync(_device);
                _engine.ResetToLevel1();
                _lastTicks = _clock.ElapsedTicks;

                InitGameOverVideo();

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

            // 5 columns: Score | Lives | Level | Remaining | FPS
            for (int i = 0; i < 5; i++)
                _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            SetupHudText(_scoreText, "Score: 0", 0);
            SetupHudText(_livesText, "Lives: 3", 1);
            SetupHudText(_levelText, "Level: 1/3", 2);
            SetupHudText(_remainingText, "Tank còn lại: 10/10", 3);
            SetupHudText(_fpsText, "FPS: --", 4);

            _hud.Children.Add(_scoreText);
            _hud.Children.Add(_livesText);
            _hud.Children.Add(_levelText);
            _hud.Children.Add(_remainingText);
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

            // GameOver video overlay
            _videoLayer.Visibility = Visibility.Collapsed;
            _videoLayer.Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));

            _videoPlayer.AreTransportControlsEnabled = false;
            _videoPlayer.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
            _videoPlayer.HorizontalAlignment = HorizontalAlignment.Stretch;
            _videoPlayer.VerticalAlignment = VerticalAlignment.Stretch;

            _videoHint.Text = "MÀY THUA RỒI\nMày bị phạt nghe bài ca này kkkk";
            _videoHint.Foreground = new SolidColorBrush(Colors.Orange);
            _videoHint.FontSize = 24;
            _videoHint.FontWeight = FontWeights.Bold;
            _videoHint.Opacity = 1;
            _videoHint.HorizontalAlignment = HorizontalAlignment.Center;
            _videoHint.VerticalAlignment = VerticalAlignment.Center;
            _videoHint.Margin = new Thickness(0, 0, 0, 28);
            _videoHint.TextAlignment = TextAlignment.Center;

            _videoLayer.Children.Add(_videoPlayer);
            _videoLayer.Children.Add(_videoHint);
            playfield.Children.Add(_videoLayer);

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

        private void InitGameOverVideo()
        {
            // Prepare MediaPlayer for GameOver scene
            _mediaPlayer?.Dispose();
            _mediaPlayer = new MediaPlayer
            {
                IsLoopingEnabled = true,
                AutoPlay = false,
                Volume = 1.0,
            };

            // Video must be added to project as: Assets/thanhhoa.mp4
            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/thanhhoa.mp4"));

            // NOTE (WinUI 3): MediaPlayerElement.MediaPlayer is read-only.
            // Use SetMediaPlayer(...) to attach our MediaPlayer instance.
            _videoPlayer.SetMediaPlayer(_mediaPlayer);
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
            if (e.Key == VirtualKey.Enter)
            {
                if (_showingGameOverVideo)
                {
                    HideGameOverVideoAndRestart();
                }
                else
                {
                    _engine.HandleEnter();
                }
            }

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

            // If we are showing the GameOver video, skip game update/render.
            if (_showingGameOverVideo) return;

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

            // Transition to GameOver video scene
            if (_engine.GameOver && !_showingGameOverVideo)
            {
                ShowGameOverVideo();
                return;
            }

            using (var ds = _swapChain.CreateDrawingSession(Color.FromArgb(255, 8, 10, 18)))
            {
                _engine.Render(ds, _swapChain.Size.ToVector2());
            }

            // vsync present (syncInterval=1)
            _swapChain.Present(1);

            UpdateHud(dt);
        }

        private void ShowGameOverVideo()
        {
            _showingGameOverVideo = true;

            // Hide HUD + game surface and show video
            _hud.Visibility = Visibility.Collapsed;
            _swapChainPanel.Visibility = Visibility.Collapsed;
            _videoLayer.Visibility = Visibility.Visible;

            // Keep key focus for Enter
            _keySink.Focus(FocusState.Programmatic);

            try
            {
                _mediaPlayer?.Play();
            }
            catch
            {
                // If video fails to play (missing asset/codec), still show hint so user can press Enter.
            }
        }

        private void HideGameOverVideoAndRestart()
        {
            try
            {
                _mediaPlayer?.Pause();
                if (_mediaPlayer is not null)
                {
                    // Ensure we restart from the beginning next time.
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                    _mediaPlayer.PlaybackSession.PlaybackRate = 1.0;
                }
            }
            catch { }

            _videoLayer.Visibility = Visibility.Collapsed;
            _swapChainPanel.Visibility = Visibility.Visible;
            _hud.Visibility = Visibility.Visible;

            _showingGameOverVideo = false;
            _engine.ResetToLevel1();

            // Reset timing accumulator to avoid a big dt spike on return
            _accumulator = 0;
            _lastTicks = _clock.ElapsedTicks;

            _keySink.Focus(FocusState.Programmatic);
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
                _levelText.Text = $"Level: {_engine.Level}/3";
                _remainingText.Text = $"Tank còn lại: {_engine.RemainingToWin}/{_engine.KillGoalPerLevel}";

                _fpsAccum = 0;
                _fpsFrames = 0;
            }
        }

        private void Cleanup()
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;

            _swapChain?.Dispose();
            _swapChain = null;

            try { _videoPlayer.SetMediaPlayer(null); } catch { }

            _mediaPlayer?.Dispose();
            _mediaPlayer = null;

            _keySink.PreviewKeyDown -= OnKeyDown;
            _keySink.PreviewKeyUp -= OnKeyUp;
        }
    }

    file static class VectorExt
    {
        public static Vector2 ToVector2(this Size s) => new((float)s.Width, (float)s.Height);
    }
}
