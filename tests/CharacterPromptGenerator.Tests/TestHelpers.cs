using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cpg-test-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}

sealed class FileSeed : ISeedSource
{
    static string SeedDir => System.IO.Path.Combine(AppContext.BaseDirectory, "SeedData");
    public string? ReadLibraryJson() => System.IO.File.ReadAllText(System.IO.Path.Combine(SeedDir, "default_library.json"));
    public string? ReadPresetsJson() => System.IO.File.ReadAllText(System.IO.Path.Combine(SeedDir, "default_presets.json"));
}

sealed class EmptySeed : ISeedSource
{
    public string? ReadLibraryJson() => null;
    public string? ReadPresetsJson() => null;
}

static class Lib
{
    /// <summary>小型測試詞庫：A 分類 n 筆、B 分類 2 筆。</summary>
    public static LibraryData Small(int aCount = 5)
    {
        var lib = new LibraryData();
        lib.Categories.Add(new Category { Id = "A", Name = "分類A", SortOrder = 0 });
        lib.Categories.Add(new Category { Id = "B", Name = "分類B", SortOrder = 1 });
        for (var i = 1; i <= aCount; i++)
            lib.Entries.Add(new Entry { Id = $"a{i}", CategoryId = "A", Text = $"詞{i}" });
        lib.Entries.Add(new Entry { Id = "b1", CategoryId = "B", Text = "蠟燭", Emoji = "🕯️" });
        lib.Entries.Add(new Entry { Id = "b2", CategoryId = "B", Text = "飛蛾", Emoji = "🦋" });
        return lib;
    }
}
