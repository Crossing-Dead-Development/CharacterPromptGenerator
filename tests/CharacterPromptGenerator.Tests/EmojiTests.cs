using System.IO.Compression;
using System.Text.Json;
using CharacterPromptGenerator.Core.Emoji;
using CharacterPromptGenerator.Core.Import;

namespace CharacterPromptGenerator.Tests;

public class EmojiTextTests
{
    [Theory]
    [InlineData("🕯️", "1f56f")]                        // 變體選擇符 FE0F 被去掉
    [InlineData("🕯", "1f56f")]                         // 沒有 FE0F 也找同一張
    [InlineData("❤️", "2764")]
    [InlineData("🏳️‍🌈", "1f3f3-200d-1f308")]           // ZWJ 序列中間的 FE0F
    [InlineData("🧑🏾‍🎨", "1f9d1-1f3fe-200d-1f3a8")]     // 膚色 + ZWJ
    [InlineData("1️⃣", "31-20e3")]                      // 數字鍵帽
    [InlineData("🇹🇼", "1f1f9-1f1fc")]                  // 國旗（區域指示符號）
    public void Key_NormalizesSequences(string emoji, string expected) =>
        Assert.Equal(expected, EmojiText.Key(emoji));

    [Fact]
    public void CandidateKeys_FallBackToDefaultSkinTone()
    {
        Assert.Equal(["1f44b-1f3ff", "1f44b"], EmojiText.CandidateKeys("👋🏿"));
        Assert.Equal(["1f56f"], EmojiText.CandidateKeys("🕯️"));
    }

    [Fact]
    public void Split_KeepsComplexSequencesAsSingleEmoji()
    {
        var segs = EmojiText.Split("家庭 👨‍👩‍👧 與 🧑🏾‍🎨、🇹🇼！");
        Assert.Equal(["家庭 ", "👨‍👩‍👧", " 與 ", "🧑🏾‍🎨", "、", "🇹🇼", "！"], segs.Select(s => s.Text));
        Assert.Equal([false, true, false, true, false, true, false], segs.Select(s => s.IsEmoji));
    }

    [Fact]
    public void Split_JoinedTextRoundTripsExactly()
    {
        const string text = "🕯️ 蠟燭 ☂️ ©2024 ⚓ #️⃣ 👋🏽";
        Assert.Equal(text, string.Concat(EmojiText.Split(text).Select(s => s.Text)));
    }

    [Theory]
    [InlineData("☂️", true)]   // 有 FE0F
    [InlineData("⚓", true)]    // Emoji_Presentation
    [InlineData("🕯", true)]
    [InlineData("#️⃣", true)]
    [InlineData("☂", false)]   // 文字預設樣式，沒有 FE0F：一般文字中不換成圖片
    [InlineData("☂︎", false)]   // FE0E 明確要求文字樣式
    [InlineData("©", false)]
    [InlineData("A", false)]
    [InlineData("中", false)]
    public void IsDefinitelyEmoji(string cluster, bool expected) =>
        Assert.Equal(expected, EmojiText.IsDefinitelyEmoji(cluster));

    [Fact]
    public void ZwjComponents_SplitsOnJoinerOnly()
    {
        Assert.Equal(["👨", "👩", "👧"], EmojiText.ZwjComponents("👨‍👩‍👧"));
        Assert.Equal(["🏳️", "🌈"], EmojiText.ZwjComponents("🏳️‍🌈"));
        Assert.Empty(EmojiText.ZwjComponents("👋🏿"));
    }

    [Fact]
    public void ReplacementCharacter_IsNotTreatedAsEmoji()
    {
        var bad = "蠟燭" + (char)0xFFFD;
        Assert.All(EmojiText.Split(bad), s => Assert.False(s.IsEmoji));
    }
}

public class EmojiLibraryTests
{
    static string MakePack(TempDir dir, params string[] keys)
    {
        var path = dir.File("pack.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var k in keys)
        {
            using var w = new BinaryWriter(zip.CreateEntry(k + ".png").Open());
            w.Write([0x89, (byte)'P', (byte)'N', (byte)'G', (byte)k.Length]);
        }
        return path;
    }

    [Fact]
    public void Lookup_FindsWithOrWithoutVariationSelector_AndSkinToneFallback()
    {
        using var dir = new TempDir();
        using var lib = new EmojiLibrary(MakePack(dir, "1f56f", "1f44b", "1f3f3-200d-1f308"), null);
        Assert.Equal(3, lib.PackCount);
        Assert.Equal("1f56f", lib.Lookup("🕯️").Key);
        Assert.Equal("1f56f", lib.Lookup("🕯").Key);
        Assert.Equal("1f3f3-200d-1f308", lib.Lookup("🏳️‍🌈").Key);
        Assert.Equal("1f44b", lib.Lookup("👋🏿").Key);
        Assert.NotNull(lib.ReadImage(lib.Lookup("🕯️")));
    }

    [Fact]
    public void Missing_ReturnsNotFound_WithoutThrowing()
    {
        using var dir = new TempDir();
        using var lib = new EmojiLibrary(MakePack(dir, "1f56f"), null);
        var lookup = lib.Lookup("🪼");
        Assert.False(lookup.Found);
        Assert.Null(lib.ReadImage(lookup));
    }

    [Fact]
    public void Override_TakesPrecedenceOverPack()
    {
        using var dir = new TempDir();
        var overrides = Directory.CreateDirectory(dir.File("overrides")).FullName;
        File.WriteAllBytes(Path.Combine(overrides, "1fabc.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(overrides, "1f56f.png"), [4, 5, 6]);
        using var lib = new EmojiLibrary(MakePack(dir, "1f56f"), overrides);
        Assert.Equal(EmojiImageSource.Override, lib.Lookup("🪼").Source);
        var candle = lib.Lookup("🕯️");
        Assert.Equal(EmojiImageSource.Override, candle.Source);
        Assert.Equal([4, 5, 6], lib.ReadImage(candle));
    }

    [Fact]
    public void MissingOrCorruptPack_ReportsErrorInsteadOfCrashing()
    {
        using var dir = new TempDir();
        using var missing = new EmojiLibrary(dir.File("nope.zip"), null);
        Assert.NotNull(missing.PackError);
        Assert.False(missing.Lookup("🕯️").Found);

        File.WriteAllText(dir.File("bad.zip"), "這不是 zip");
        using var corrupt = new EmojiLibrary(dir.File("bad.zip"), null);
        Assert.NotNull(corrupt.PackError);
        Assert.False(corrupt.Lookup("🕯️").Found);
    }
}

/// <summary>以實際隨程式發行的 Fluent Emoji 圖庫驗證。</summary>
public class FluentEmojiPackTests
{
    static string PackPath => Path.Combine(AppContext.BaseDirectory, "Emoji", "fluent-emoji-3d.zip");

    [Fact]
    public void Pack_HasImagesLicenseAndManifest()
    {
        using var lib = new EmojiLibrary(PackPath, null);
        Assert.Null(lib.PackError);
        Assert.True(lib.PackCount >= 3000);

        using var zip = ZipFile.OpenRead(PackPath);
        using var license = new StreamReader(zip.GetEntry("LICENSE.txt")!.Open());
        var text = license.ReadToEnd();
        Assert.Contains("MIT License", text);
        Assert.Contains("Copyright (c) Microsoft Corporation", text);
        using var manifest = JsonDocument.Parse(zip.GetEntry("manifest.json")!.Open());
        Assert.Equal(lib.PackCount, manifest.RootElement.GetProperty("count").GetInt32());
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Emoji", "LICENSE-fluentui-emoji.txt")));
    }

    [Theory]
    [InlineData("🫧")] [InlineData("🪞")] [InlineData("🪼")]          // Windows 10 字型沒有的新 Emoji
    [InlineData("🕯️")] [InlineData("🗝️")] [InlineData("☂️")]           // 含變體選擇符
    [InlineData("👋🏿")] [InlineData("🧑🏾‍🎨")] [InlineData("🏳️‍🌈")]      // 膚色、ZWJ
    [InlineData("❤️")] [InlineData("#️⃣")]
    public void Pack_ResolvesCommonAndTrickySequences(string emoji)
    {
        using var lib = new EmojiLibrary(PackPath, null);
        var lookup = lib.Lookup(emoji);
        Assert.True(lookup.Found, $"{emoji} ({EmojiText.Describe(emoji)}) 找不到圖片");
        var png = lib.ReadImage(lookup)!;
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
    }

    [Fact]
    public void MissingZwjSequence_ComponentsAreAvailableForFallback()
    {
        using var lib = new EmojiLibrary(PackPath, null);
        Assert.False(lib.Lookup("👨‍👩‍👧").Found); // Fluent 沒有這個家庭組合
        Assert.All(EmojiText.ZwjComponents("👨‍👩‍👧"), part => Assert.True(lib.Lookup(part).Found));
        Assert.False(lib.Lookup("🫟").Found);   // Emoji 16，圖庫沒有：介面顯示替代圖示
    }

    [Fact]
    public void EveryEmojiInShippedData_HasAnImage()
    {
        using var lib = new EmojiLibrary(PackPath, null);
        var texts = new List<string>();
        var seed = ImportReader.ReadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SeedData", "default_library.json")));
        texts.AddRange(seed.Rows.SelectMany(r => new[] { r.Emoji ?? "", r.Text }));
        foreach (var f in new[] { "import_example.json", "import_example.csv", "import_example.txt" })
            texts.AddRange(ImportReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", f)).Rows.SelectMany(r => new[] { r.Emoji ?? "", r.Text }));

        var emoji = texts.SelectMany(EmojiText.Split).Where(s => s.IsEmoji).Select(s => s.Text).Distinct().ToList();
        Assert.True(emoji.Count >= 25);
        var missing = emoji.Where(e => !lib.Lookup(e).Found).Select(e => $"{e} {EmojiText.Describe(e)}").ToList();
        Assert.Empty(missing);
    }
}
