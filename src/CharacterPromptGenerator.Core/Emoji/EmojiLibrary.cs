using System.IO.Compression;
using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Core.Emoji;

public enum EmojiImageSource { None, Override, Pack }

public sealed record EmojiLookup(string Cluster, string? Key, EmojiImageSource Source)
{
    public bool Found => Source != EmojiImageSource.None;
}

/// <summary>
/// 本機 Emoji 圖庫：fluent-emoji-3d.zip（Fluent Emoji 3D PNG，發行版內嵌在 EXE 中），
/// 加上使用者資料夾中的 EmojiOverrides\&lt;鍵&gt;.png（可自行補圖或替換，優先使用）。
/// 圖庫不存在或損壞時不會丟例外，只是所有查詢都找不到（介面改顯示替代圖示）。
/// </summary>
public sealed class EmojiLibrary : IDisposable
{
    readonly ZipArchive? _zip;
    readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.Ordinal);
    readonly object _lock = new();

    public string? PackPath { get; }
    public string? OverrideDir { get; }
    /// <summary>圖庫無法使用時的原因（給 UI 顯示）。</summary>
    public string? PackError { get; }
    public int PackCount => _entries.Count;

    public EmojiLibrary(string? packPath, string? overrideDir)
        : this(packPath, packPath is null ? null : () => File.Exists(packPath)
            ? new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null, overrideDir)
    {
    }

    /// <summary>從任意來源開啟圖庫（發行版用 EXE 內嵌的資源）。openPack 回傳 null 表示圖庫不存在。</summary>
    public EmojiLibrary(string? packPath, Func<Stream?>? openPack, string? overrideDir)
    {
        PackPath = packPath;
        OverrideDir = overrideDir;
        if (openPack is null) return;
        try
        {
            var stream = openPack();
            if (stream is null)
            {
                PackError = L.F("Emoji_PackMissing", packPath);
                return;
            }
            _zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in _zip.Entries)
            {
                if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                _entries[entry.FullName[..^4].ToLowerInvariant()] = entry;
            }
            if (_entries.Count == 0) PackError = L.F("Emoji_PackEmpty", packPath);
        }
        catch (Exception ex)
        {
            _zip?.Dispose();
            _zip = null;
            _entries.Clear();
            PackError = L.F("Emoji_PackUnreadable", ex.Message, packPath);
        }
    }

    public static EmojiLibrary Empty { get; } = new((string?)null, null);

    public static string OverrideFileName(string key) => key + ".png";

    /// <summary>找出這個 Emoji 要用的圖（不讀取內容）。</summary>
    public EmojiLookup Lookup(string cluster)
    {
        foreach (var key in EmojiText.CandidateKeys(cluster))
        {
            if (OverrideDir is not null && File.Exists(Path.Combine(OverrideDir, OverrideFileName(key))))
                return new EmojiLookup(cluster, key, EmojiImageSource.Override);
            if (_entries.ContainsKey(key)) return new EmojiLookup(cluster, key, EmojiImageSource.Pack);
        }
        return new EmojiLookup(cluster, null, EmojiImageSource.None);
    }

    /// <summary>讀取圖檔內容；讀取失敗回傳 null，不丟例外。</summary>
    public byte[]? ReadImage(EmojiLookup lookup)
    {
        if (lookup.Key is null) return null;
        try
        {
            if (lookup.Source == EmojiImageSource.Override && OverrideDir is not null)
                return File.ReadAllBytes(Path.Combine(OverrideDir, OverrideFileName(lookup.Key)));
            if (lookup.Source == EmojiImageSource.Pack && _entries.TryGetValue(lookup.Key, out var entry))
            {
                lock (_lock) // ZipArchive 不支援同時讀取
                {
                    using var s = entry.Open();
                    using var ms = new MemoryStream((int)entry.Length);
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
        catch
        {
            // 單張圖損壞時改顯示替代圖示
        }
        return null;
    }

    public void Dispose() => _zip?.Dispose();
}
