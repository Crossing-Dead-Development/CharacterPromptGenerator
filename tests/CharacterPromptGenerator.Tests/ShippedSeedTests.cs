using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

/// <summary>發行版內嵌的 SeedData 是空白的：詞庫由使用者自行匯入（其他測試用 Fixtures\ 的測試詞庫）。</summary>
public class ShippedSeedTests
{
    sealed class ShippedSeed : ISeedSource
    {
        static string Dir
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CharacterPromptGenerator.slnx"))) dir = dir.Parent;
                return Path.Combine(dir!.FullName, "src", "CharacterPromptGenerator.App", "SeedData");
            }
        }

        public string? ReadLibraryJson() => File.ReadAllText(Path.Combine(Dir, "default_library.json"));
        public string? ReadPresetsJson() => File.ReadAllText(Path.Combine(Dir, "default_presets.json"));
    }

    [Fact]
    public void FirstRun_StartsWithAnEmptyLibrary_WithoutWarnings()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new ShippedSeed());
        Assert.True(store.FirstRun);
        Assert.Empty(store.Library.Categories);
        Assert.Empty(store.Library.Entries);
        Assert.Empty(store.Presets);
        Assert.Empty(store.LoadMessages);
        Assert.True(File.Exists(store.Paths.LibraryFile));
    }
}
