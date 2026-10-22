using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

static class TestCulture
{
    /// <summary>其他測試會比對中文訊息：不論電腦的顯示語言，一律以繁體中文執行。</summary>
    [ModuleInitializer]
    internal static void UseChinese() => L.Use(L.Chinese);
}

public class LocalizationTests
{
    static readonly string RepoRoot = FindRepoRoot();
    static readonly string ResxDir = Path.Combine(RepoRoot, "src", "CharacterPromptGenerator.Core", "Localization");

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CharacterPromptGenerator.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到 CharacterPromptGenerator.slnx");
    }

    static Dictionary<string, string> ReadResx(string name) =>
        XDocument.Load(Path.Combine(ResxDir, name)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    static IEnumerable<string> Placeholders(string text) =>
        Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[:,][^}]*)?\}").Select(m => m.Groups[1].Value).Distinct().Order();

    [Fact]
    public void BothLanguages_HaveTheSameKeys_AndPlaceholders()
    {
        var zh = ReadResx("Strings.resx");
        var en = ReadResx("Strings.en.resx");
        Assert.True(zh.Count > 300);
        Assert.Empty(zh.Keys.Except(en.Keys));
        Assert.Empty(en.Keys.Except(zh.Keys));
        Assert.Empty(zh.Where(kv => string.IsNullOrWhiteSpace(en[kv.Key])).Select(kv => kv.Key));
        var mismatched = zh.Where(kv => !Placeholders(kv.Value).SequenceEqual(Placeholders(en[kv.Key]))).Select(kv => kv.Key);
        Assert.Empty(mismatched);
    }

    [Fact]
    public void EveryKeyUsedInSource_Exists()
    {
        var keys = ReadResx("Strings.resx").Keys.ToHashSet();
        var used = new List<(string File, string Key)>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && Path.GetFileName(f) != "L.cs"
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\bL\.[TF]\(""([A-Za-z0-9_]+)""|\{l:T ([A-Za-z0-9_]+)\}"))
                used.Add((Path.GetFileName(file), m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
        }
        Assert.True(used.Count > 300);
        Assert.Empty(used.Where(u => !keys.Contains(u.Key)).Select(u => $"{u.File}: {u.Key}"));
    }

    [Fact]
    public void EnglishStrings_LoadFromSatelliteAssembly()
    {
        Assert.Equal("詞庫中已有相同詞條", L.T("Plan_SkipDupInLibrary"));
        Assert.Equal("Already in the word bank", L.T("Plan_SkipDupInLibrary", L.English));
        Assert.Equal("Character Design Prompt Generator", L.T("App_Title", L.English));
    }

    [Fact]
    public void UnknownKey_ShowsTheKey()
    {
        Assert.Equal("No_Such_Key", L.T("No_Such_Key"));
    }

    [Theory]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("ZH-tw", "zh-TW")]
    [InlineData("en", "en")]
    public void Resolve_KnownCodes(string code, string expected) => Assert.Equal(expected, L.Resolve(code));

    [Theory]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("zh-CN", "zh-TW")]   // 簡體中文系統也顯示中文介面
    [InlineData("zh-Hans-CN", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("zh-SG", "zh-TW")]
    [InlineData("zh", "zh-TW")]
    [InlineData("en-US", "en")]
    [InlineData("ja-JP", "en")]      // 非中文一律英文
    [InlineData("ko-KR", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]           // 不變文化（例如精簡環境）
    public void Resolve_FollowsSystemLanguage_WhenNotSet(string system, string expected) =>
        Assert.Equal(expected, L.Resolve(null, [system]));

    [Fact]
    public void Resolve_SavedChoice_WinsOverSystemLanguage()
    {
        Assert.Equal(L.English, L.Resolve("en", ["zh-TW"]));
        Assert.Equal(L.Chinese, L.Resolve("zh-tw", ["ja-JP"]));
        Assert.Equal(L.English, L.Resolve("xx", ["ja-JP"])); // 不認識的設定值當作沒有設定
    }

    [Fact]
    public void Resolve_UnknownCode_FallsBackToASupportedLanguage()
    {
        Assert.Contains(L.Resolve("fr-FR"), L.Languages.Select(l => l.Code));
        Assert.Contains(L.Resolve(null), L.Languages.Select(l => l.Code));
    }

    [Fact]
    public void LanguageSetting_IsSaved_AndCanBeReadBeforeOpeningTheStore()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        Assert.Null(AppStore.PeekLanguage(paths)); // 尚未有 settings.json：跟隨系統

        var store = AppStore.Open(paths, new FileSeed());
        Assert.Null(store.Settings.Language);
        store.Settings.Language = L.English;
        store.SaveSettings();
        Assert.Equal(L.English, AppStore.PeekLanguage(paths));
        Assert.Equal(L.English, AppStore.Open(paths, new FileSeed()).Settings.Language);

        File.WriteAllText(paths.SettingsFile, "{ broken");
        Assert.Null(AppStore.PeekLanguage(paths)); // 損壞時不丟例外，由 AppStore.Open 照常處理
    }

    [Fact]
    public void Formatting_FillsArguments()
    {
        var doc = ImportReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", "broken", "newer_version.json"));
        Assert.True(doc.IsFatal);
        Assert.Contains("格式版本為 99，", doc.FatalError);
    }
}
