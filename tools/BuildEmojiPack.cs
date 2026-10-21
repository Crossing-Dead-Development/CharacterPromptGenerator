#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property Nullable=enable

// 產生 Fluent Emoji 3D 圖庫包（fluent-emoji-3d.zip）。
//
// 用法：
//   dotnet run tools/BuildEmojiPack.cs -- <fluentui-emoji-unicode 目錄> <microsoft/fluentui-emoji 目錄> <輸出 zip> [像素尺寸=160]
//
// 來源：
//   1. shuding/fluentui-emoji-unicode：assets/<hex-hex…>_3d.png（優先）
//   2. microsoft/fluentui-emoji：依 assets/*/metadata.json 的 unicode 補上 1 沒有的 Emoji
// 輸出 zip 內每張圖命名為「正規化鍵.png」（小寫十六進位、以 - 連接、去掉 FE0E/FE0F），
// 並附上 LICENSE.txt 與 manifest.json。

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: BuildEmojiPack.cs <shuding-dir> <microsoft-dir> <out.zip> [size]");
    return 2;
}
var shudingDir = args[0];
var microsoftDir = args[1];
var outZip = Path.GetFullPath(args[2]);
var size = args.Length > 3 ? int.Parse(args[3]) : 160;

static string Key(string seq) => string.Join('-', seq.ToLowerInvariant().Replace(' ', '-').Split('-', StringSplitOptions.RemoveEmptyEntries)
    .Where(c => c is not ("fe0f" or "fe0e")).Select(c => Convert.ToInt32(c, 16).ToString("x")));

var sources = new SortedDictionary<string, (string Path, string Origin)>(StringComparer.Ordinal);

// 1. shuding：只取十六進位命名的檔案（忽略以字元命名的重複檔）
var hexName = new Regex(@"^([0-9a-f]+(?:-[0-9a-f]+)*)_3d\.png$");
foreach (var file in Directory.EnumerateFiles(Path.Combine(shudingDir, "assets")))
{
    var m = hexName.Match(Path.GetFileName(file));
    if (m.Success) sources.TryAdd(Key(m.Groups[1].Value), (file, "shuding/fluentui-emoji-unicode"));
}
var fromShuding = sources.Count;

// 2. microsoft：補上缺少的序列
var toneFolder = new Dictionary<string, string>
{
    ["1f3fb"] = "Light", ["1f3fc"] = "Medium-Light", ["1f3fd"] = "Medium", ["1f3fe"] = "Medium-Dark", ["1f3ff"] = "Dark",
};
foreach (var meta in Directory.EnumerateFiles(Path.Combine(microsoftDir, "assets"), "metadata.json", SearchOption.AllDirectories))
{
    using var doc = JsonDocument.Parse(File.ReadAllText(meta));
    var root = doc.RootElement;
    var dir = Path.GetDirectoryName(meta)!;
    var hasTones = root.TryGetProperty("unicodeSkintones", out var tones) && tones.ValueKind == JsonValueKind.Array;
    var seqs = hasTones ? tones.EnumerateArray().Select(t => t.GetString()!).ToList() : [root.GetProperty("unicode").GetString()!];
    foreach (var seq in seqs)
    {
        var key = Key(seq);
        if (sources.ContainsKey(key)) continue;
        var tone = key.Split('-').FirstOrDefault(toneFolder.ContainsKey);
        var sub = hasTones ? Path.Combine(tone is null ? "Default" : toneFolder[tone], "3D") : "3D";
        var folder = Path.Combine(dir, sub);
        var png = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.png").FirstOrDefault() : null;
        if (png is null)
        {
            Console.Error.WriteLine($"warning: no 3D image for {key} in {folder}");
            continue;
        }
        sources[key] = (png, "microsoft/fluentui-emoji");
    }
}
Console.WriteLine($"images: {sources.Count} (shuding {fromShuding}, microsoft {sources.Count - fromShuding}), size {size}px");

// 3. 縮放並寫入 zip
Directory.CreateDirectory(Path.GetDirectoryName(outZip)!);
var tmp = outZip + ".tmp";
if (File.Exists(tmp)) File.Delete(tmp); // 上次中斷留下的暫存檔
using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
{
    foreach (var (key, (path, _)) in sources)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.DecodePixelWidth = size;
        bmp.EndInit();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var buffer = new MemoryStream(); // PngBitmapEncoder 需要可 Seek 的串流
        encoder.Save(buffer);
        var entry = zip.CreateEntry(key + ".png", CompressionLevel.NoCompression); // PNG 已壓縮
        using var s = entry.Open();
        buffer.Position = 0;
        buffer.CopyTo(s);
    }

    var license = zip.CreateEntry("LICENSE.txt");
    using (var w = new StreamWriter(license.Open()))
        w.Write(File.ReadAllText(Path.Combine(microsoftDir, "LICENSE")));

    var manifest = new
    {
        name = "Microsoft Fluent Emoji (3D)",
        license = "MIT, Copyright (c) Microsoft Corporation",
        imageSize = size,
        keyFormat = "lowercase hex code points joined by '-', FE0E/FE0F removed",
        generatedAt = DateTime.UtcNow.ToString("o"),
        sources = new[]
        {
            new { repo = "https://github.com/shuding/fluentui-emoji-unicode", commit = Git(shudingDir), count = fromShuding },
            new { repo = "https://github.com/microsoft/fluentui-emoji", commit = Git(microsoftDir), count = sources.Count - fromShuding },
        },
        count = sources.Count,
    };
    var m = zip.CreateEntry("manifest.json");
    using (var w = new StreamWriter(m.Open()))
        w.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
}
File.Move(tmp, outZip, overwrite: true);
Console.WriteLine($"wrote {outZip} ({new FileInfo(outZip).Length / 1024 / 1024.0:0.0} MB)");
return 0;

static string Git(string dir)
{
    var head = Path.Combine(dir, ".git", "HEAD");
    if (!File.Exists(head)) return "unknown";
    var text = File.ReadAllText(head).Trim();
    if (!text.StartsWith("ref: ")) return text;
    var refFile = Path.Combine(dir, ".git", text[5..]);
    if (File.Exists(refFile)) return File.ReadAllText(refFile).Trim();
    var packed = Path.Combine(dir, ".git", "packed-refs");
    return File.Exists(packed)
        ? System.IO.File.ReadLines(packed).Where(l => l.EndsWith(text[5..])).Select(l => l.Split(' ')[0]).FirstOrDefault() ?? "unknown"
        : "unknown";
}
