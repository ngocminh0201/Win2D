using Microsoft.Graphics.Canvas;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Win2D.BattleTank
{
    /// <summary>
    /// Central place to load and store game images (Win2D CanvasBitmap).
    /// Put your files into the app package: Assets/nem.jpeg, Assets/cocrau.jpg, Assets/rauma.webp
    /// </summary>
    public static class GameAssets
    {
        private static bool _loaded;

        public static CanvasBitmap? Bullet { get; private set; }
        public static CanvasBitmap? Brick { get; private set; }
        public static CanvasBitmap? Steel { get; private set; }

        public static async Task LoadAsync(CanvasDevice device)
        {
            if (_loaded) return;
            _loaded = true;

            Bullet = await LoadBitmap(device, "ms-appx:///Assets/nem.jpeg");
            Brick  = await LoadBitmap(device, "ms-appx:///Assets/cocrau.jpg");
            Steel  = await LoadBitmap(device, "ms-appx:///Assets/rauma.webp");
        }

        private static async Task<CanvasBitmap?> LoadBitmap(CanvasDevice device, string uri)
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
                using var stream = await file.OpenReadAsync();
                return await CanvasBitmap.LoadAsync(device, stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
