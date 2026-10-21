using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

/// <summary>以 samples\ 中實際交付的範例檔，對內建示範詞庫跑完整匯入流程。</summary>
public class SampleFileTests
{
    static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    static AppStore SeededStore(TempDir dir) => AppStore.Open(new DataPaths(dir.Path), new FileSeed());

    [Theory]
    // 範例檔刻意包含示範詞庫已有的詞（蠟燭、中年、鐘錶匠…），用來展示重複略過
    [InlineData("import_example.csv", 6, 2, 1)]
    [InlineData("import_example.txt", 5, 3, 1)]
    [InlineData("import_example.json", 3, 2, 1)]
    public void ExampleFiles_ImportCleanly_AndAreIdempotent(string file, int expectedAdded, int expectedSkipped, int expectedNewCategories)
    {
        using var dir = new TempDir();
        var store = SeededStore(dir);
        var before = store.Library.Entries.Count;

        var doc = ImportReader.ReadFile(Sample(file));
        Assert.False(doc.IsFatal, doc.FatalError);
        Assert.Empty(doc.Errors);
        var plan = ImportPlanner.Build(store.Library, doc, new ImportOptions());
        Assert.True(plan.CanApply, plan.BlockReason);
        Assert.Equal(expectedAdded, plan.Added.Count);
        Assert.Equal(expectedSkipped, plan.Skipped.Count);
        Assert.All(plan.Skipped, s => Assert.Equal("詞庫中已有相同詞條", s.Reason));
        Assert.Equal(expectedNewCategories, plan.NewCategories.Count);
        store.ApplyImport(plan);
        Assert.Equal(before + expectedAdded, store.Library.Entries.Count);

        // 同一份檔案再匯入一次：不會倍增
        var again = ImportPlanner.Build(store.Library, ImportReader.ReadFile(Sample(file)), new ImportOptions());
        Assert.Empty(again.Added);
        Assert.False(again.CanApply);
    }

    [Fact]
    public void CsvExample_KeepsMultilineNotesCommasAndEmoji()
    {
        var doc = ImportReader.ReadFile(Sample("import_example.csv"));
        var tuner = doc.Rows.Single(r => r.Text == "調音師");
        Assert.Contains("\n", tuner.Notes);
        Assert.Equal("🎹", tuner.Emoji);
        Assert.Contains(doc.Rows, r => r.Text == "說書人, 兼賣藥");
        Assert.Equal("⛈️", doc.Rows.Single(r => r.Text == "暴風雨前夕").Emoji);
    }

    [Theory]
    [InlineData("broken/broken_syntax.json")]
    [InlineData("broken/newer_version.json")]
    [InlineData("broken/big5_encoding.csv")]
    [InlineData("broken/wrong_header.csv")]
    [InlineData("broken/unclosed_quote.csv")]
    [InlineData("broken/empty.txt")]
    public void BrokenFiles_AreRejected_AndLibraryUntouched(string file)
    {
        using var dir = new TempDir();
        var store = SeededStore(dir);
        var raw = File.ReadAllText(store.Paths.LibraryFile);

        var doc = ImportReader.ReadFile(Sample(file));
        Assert.True(doc.IsFatal);
        Assert.False(string.IsNullOrWhiteSpace(doc.FatalError));
        var plan = ImportPlanner.Build(store.Library, doc, new ImportOptions());
        Assert.False(plan.CanApply);
        Assert.ThrowsAny<Exception>(() => store.ApplyImport(plan));
        Assert.Equal(raw, File.ReadAllText(store.Paths.LibraryFile));
    }

    [Fact]
    public void PartiallyBadFiles_ReportLinesAndKeepValidRows()
    {
        var bad = ImportReader.ReadFile(Sample("broken/bad_rows.csv"));
        Assert.False(bad.IsFatal);
        Assert.Equal(["醫師", "漁民"], bad.Rows.Select(r => r.Text));
        Assert.Equal([3, 4, 5], bad.Errors.Select(e => e.Line!.Value));

        var noHeader = ImportReader.ReadFile(Sample("broken/no_header.txt"));
        Assert.Empty(noHeader.Rows);
        Assert.Equal(2, noHeader.Errors.Count);
    }
}
