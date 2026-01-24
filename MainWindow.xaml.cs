using Microsoft.UI.Xaml;
using Win2D.BattleTank;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Win2D
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new Windows.StartUpWindow();
            window.Activate();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var window = new Windows.PrimitiveShapesWindow();
            window.Activate();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var window = new Windows.GradientWindow();
            window.Activate();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var window = new GameWindow();
            window.Activate();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var window = new Windows.ClippingWindow();
            window.Activate();
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            var window = new ProgresCircle.ProgressCircle();
            window.Activate();
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            var window = new Windows.CommandListsWindow();
            window.Activate();
        }
    }
}
