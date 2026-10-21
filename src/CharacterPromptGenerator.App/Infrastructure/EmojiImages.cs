using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CharacterPromptGenerator.Core.Emoji;

namespace CharacterPromptGenerator.App.Infrastructure;

/// <summary>
/// 把 Emoji 字串轉成 WPF 圖片（Fluent Emoji 3D）。結果會快取並 Freeze，可在多個控制項共用。
/// 不使用任何系統字型，所以 Windows 10 與 11 顯示相同的圖。
/// </summary>
public static class EmojiImages
{
    static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.Ordinal);

    public static EmojiLibrary Library { get; private set; } = EmojiLibrary.Empty;

    public static string PackPath => Path.Combine(AppContext.BaseDirectory, "Emoji", "fluent-emoji-3d.zip");

    public static void Initialize(string overrideDir)
    {
        Library = new EmojiLibrary(PackPath, overrideDir);
        Cache.Clear();
    }

    /// <summary>重新掃描（使用者在 EmojiOverrides 放入新圖之後）。</summary>
    public static void ClearCache() => Cache.Clear();

    public static ImageSource? Get(string cluster) => Get(Library.Lookup(cluster));

    public static ImageSource? Get(EmojiLookup lookup)
    {
        if (!lookup.Found) return null;
        var cacheKey = $"{lookup.Source}:{lookup.Key}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
        ImageSource? image = null;
        var bytes = Library.ReadImage(lookup);
        if (bytes is not null)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                image = bmp;
            }
            catch
            {
                // 圖檔損壞：快取為 null，介面顯示替代圖示
            }
        }
        Cache[cacheKey] = image;
        return image;
    }
}
