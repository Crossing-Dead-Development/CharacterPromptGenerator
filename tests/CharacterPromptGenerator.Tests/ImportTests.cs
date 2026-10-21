using System.Text;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  蠟燭  ", "蠟燭")]
    [InlineData("Old   TV", "old tv")]
    [InlineData("Old　\tTV", "old tv")]
    public void Key_TrimsCollapsesAndIgnoresCase(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Key(input));

    [Fact]
    public void Clean_PreservesEmojiSequences()
    {
        // ZWJ 家庭、膚色修飾、變體選擇符
        foreach (var emoji in new[] { "👨‍👩‍👧", "👍🏽", "🕯️", "🏳️‍🌈" })
        {
            Assert.Equal(emoji, TextNormalizer.Clean($"  {emoji} "));
            Assert.Equal(emoji, TextNormalizer.Key(emoji));
        }
    }
}

public class CsvParserTests
{
    [Fact]
    public void Parses_QuotesCommasNewlinesAndBom()
    {
        var csv = CsvParser.Bom + "category,text,emoji,notes,tags\r\n" +
                  "核心意象與 Emoji,蠟燭,🕯️,\"融蠟, 和火焰\",光源|宗教\r\n" +
                  "職業,\"說 \"\"你好\"\" 的人\",,\"第一行\n第二行\",\n" +
                  "年齡,中年,,,\n";
        var rec = CsvParser.Parse(csv);
        Assert.Equal(4, rec.Count);
        Assert.Equal("category", rec[0].Fields[0]);
        Assert.Equal("融蠟, 和火焰", rec[1].Fields[3]);
        Assert.Equal("說 \"你好\" 的人", rec[2].Fields[1]);
        Assert.Equal("第一行\n第二行", rec[2].Fields[3]);
        Assert.Equal(3, rec[2].Line);
        Assert.Equal(5, rec[3].Line); // 欄位內換行後，行號仍正確
    }

    [Fact]
    public void UnterminatedQuote_Throws()
    {
        var ex = Assert.Throws<CsvParseException>(() => CsvParser.Parse("a,b\n\"oops,c\n"));
        Assert.Equal(2, ex.Line);
    }

    [Fact]
    public void Escape_RoundTrips()
    {
        var values = new[] { "plain", "a,b", "q\"uote", "line\nbreak", "", " pad " };
        var line = string.Join(",", values.Select(CsvParser.Escape));
        Assert.Equal(values, CsvParser.Parse(line)[0].Fields);
    }
}

public class ImportReaderTests
{
    [Fact]
    public void Csv_SpecExample_Works()
    {
        var doc = ImportReader.ReadCsv("""
            category,text,emoji,notes,tags
            核心意象與 Emoji,蠟燭,🕯️,融蠟和火焰,光源|宗教
            核心意象與 Emoji,飛蛾,🦋,夜行昆蟲,昆蟲|夜晚
            年齡／年齡感,中年,,角色年齡約四十至五十歲,
            """);
        Assert.False(doc.IsFatal);
        Assert.Empty(doc.Errors);
        Assert.Equal(3, doc.Rows.Count);
        Assert.Equal(["光源", "宗教"], doc.Rows[0].Tags);
        Assert.Equal("🦋", doc.Rows[1].Emoji);
        Assert.Null(doc.Rows[2].Emoji);
    }

    [Fact]
    public void Csv_MissingHeader_IsFatal()
    {
        var doc = ImportReader.ReadCsv("蠟燭,🕯️\n飛蛾,🦋\n");
        Assert.True(doc.IsFatal);
    }

    [Fact]
    public void Csv_BadRows_ReportLineNumbers()
    {
        var doc = ImportReader.ReadCsv("category,text\n,沒有分類\n職業,\n職業,醫師,多的欄位\n職業,廚師\n");
        Assert.Single(doc.Rows);
        Assert.Equal([2, 3, 4], doc.Errors.Select(e => e.Line!.Value));
    }

    [Fact]
    public void Txt_SpecExample_Works()
    {
        var doc = ImportReader.ReadTxt("""
            # 註解
            [核心意象與 Emoji]
            🕯️ 蠟燭
            🦋 飛蛾

            [職業／身分]
            鐘錶匠
            圖書管理員
            """);
        Assert.Empty(doc.Errors);
        Assert.Equal(4, doc.Rows.Count);
        Assert.Equal("🕯️ 蠟燭", doc.Rows[0].Text);
        Assert.Equal("職業／身分", doc.Rows[3].CategoryName);
    }

    [Fact]
    public void Txt_LineBeforeHeader_IsError()
    {
        var doc = ImportReader.ReadTxt("孤兒\n[A]\n詞\n");
        Assert.Single(doc.Errors);
        Assert.Equal(1, doc.Errors[0].Line);
        Assert.Single(doc.Rows);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":99,\"entries\":[]}")]
    public void Json_BrokenOrIncompatible_IsFatal(string json) =>
        Assert.True(ImportReader.ReadJson(json).IsFatal);

    [Fact]
    public void Json_AcceptsCategoryNameInsteadOfId()
    {
        var doc = ImportReader.ReadJson("""{"schemaVersion":1,"entries":[{"category":"職業","text":"醫師","tags":"a|b"}]}""");
        Assert.False(doc.IsFatal);
        Assert.Equal("職業", doc.Rows[0].CategoryName);
        Assert.Equal(["a", "b"], doc.Rows[0].Tags);
    }

    [Fact]
    public void Decode_RejectsEmptyAndNonUtf8()
    {
        Assert.NotNull(TextFileLoader.Decode([]).Error);
        Assert.NotNull(TextFileLoader.Decode("   \n "u8.ToArray()).Error);
        // Big5 的「中文」
        Assert.NotNull(TextFileLoader.Decode([0xA4, 0xA4, 0xA4, 0xE5]).Error);
        Assert.Equal("中文🕯️", TextFileLoader.Decode([.. new byte[] { 0xEF, 0xBB, 0xBF }, .. Encoding.UTF8.GetBytes("中文🕯️")]).Text);
        Assert.Equal("中文", TextFileLoader.Decode([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("中文")]).Text);
    }
}

public class ImportPlannerTests
{
    static ImportPlan Merge(LibraryData lib, ImportDocument doc, bool allowNew = true) =>
        ImportPlanner.Build(lib, doc, new ImportOptions { Mode = ImportMode.Merge, AllowNewCategories = allowNew });

    [Fact]
    public void MergingSameFileTwice_DoesNotDuplicate()
    {
        var lib = Lib.Small(2);
        var doc = ImportReader.ReadCsv("category,text\n分類A,新詞\n分類A,  新詞  \n分類B,蠟燭\n新分類,X\n");
        var plan1 = Merge(lib, doc);
        Assert.True(plan1.CanApply);
        Assert.Equal(2, plan1.Added.Count);
        Assert.Contains(plan1.Skipped, s => s.Reason == "檔案內重複");
        Assert.Contains(plan1.Skipped, s => s.Reason == "詞庫中已有相同詞條" && s.Text == "蠟燭");
        Assert.Single(plan1.NewCategories);

        var plan2 = Merge(plan1.ResultLibrary, doc);
        Assert.Empty(plan2.Added);
        Assert.False(plan2.CanApply);
        Assert.Equal(plan1.ResultLibrary.Entries.Count, plan2.ResultLibrary.Entries.Count);
    }

    [Fact]
    public void SameTextInDifferentCategories_IsAllowed()
    {
        var lib = Lib.Small(0);
        var plan = Merge(lib, ImportReader.ReadCsv("category,text\n分類A,蠟燭\n"));
        Assert.Single(plan.Added);
        Assert.Equal(2, plan.ResultLibrary.Entries.Count(e => e.Text == "蠟燭"));
    }

    [Fact]
    public void CaseInsensitiveDuplicate()
    {
        var lib = Lib.Small(0);
        lib.Entries.Add(new Entry { Id = "x", CategoryId = "A", Text = "Old TV" });
        var plan = Merge(lib, ImportReader.ReadCsv("category,text\n分類A,old   tv\n"));
        Assert.Empty(plan.Added);
    }

    [Fact]
    public void DisallowNewCategories_SkipsRows()
    {
        var plan = Merge(Lib.Small(0), ImportReader.ReadCsv("category,text\n不存在,X\n分類A,Y\n"), allowNew: false);
        Assert.Single(plan.Added);
        Assert.Empty(plan.NewCategories);
        Assert.Contains(plan.Skipped, s => s.CategoryName == "不存在");
    }

    [Fact]
    public void IdConflict_GetsNewIdAndKeepsExisting()
    {
        var lib = Lib.Small(1);
        var json = """{"schemaVersion":1,"categories":[{"id":"A","name":"分類A"}],"entries":[{"id":"a1","categoryId":"A","text":"不同的內容","notes":"新備註"}]}""";
        var plan = Merge(lib, ImportReader.ReadJson(json));
        Assert.True(plan.CanApply);
        Assert.Equal(1, plan.IdConflictsResolved);
        var result = plan.ResultLibrary;
        Assert.Equal("詞1", result.Entries.Single(e => e.Id == "a1").Text);
        Assert.Contains(result.Entries, e => e.Text == "不同的內容" && e.Id != "a1");
    }

    [Fact]
    public void Merge_DoesNotOverwriteExistingNotes()
    {
        var lib = Lib.Small(0);
        lib.Entries[0].Notes = "原本的備註";
        var plan = Merge(lib, ImportReader.ReadCsv("category,text,notes\n分類B,蠟燭,新的備註\n"));
        Assert.Equal("原本的備註", plan.ResultLibrary.Entries.Single(e => e.Text == "蠟燭").Notes);
    }

    [Fact]
    public void Replace_WithNoEntries_IsBlocked()
    {
        var plan = ImportPlanner.Build(Lib.Small(), ImportReader.ReadJson("""{"schemaVersion":1,"categories":[],"entries":[]}"""),
            new ImportOptions { Mode = ImportMode.Replace });
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void FatalDocument_IsBlockedAndLeavesLibraryAlone()
    {
        var lib = Lib.Small();
        var plan = Merge(lib, ImportReader.ReadJson("{ broken"));
        Assert.False(plan.CanApply);
        Assert.Same(lib, plan.ResultLibrary);
    }

    [Fact]
    public void JsonExportImport_RoundTripIsLossless()
    {
        var lib = Lib.Small(3);
        lib.Entries[0].Tags = ["標籤1", "標籤2"];
        lib.Entries[0].Notes = "有,逗號\n和換行";
        lib.Entries[1].Enabled = false;
        lib.Categories[1].Enabled = false;
        lib.Categories[1].DefaultDrawCount = 4;
        lib.Categories[1].Description = "說明";

        var plan = ImportPlanner.Build(new LibraryData(), ImportReader.ReadJson(LibraryExporter.ToJson(lib)),
            new ImportOptions { Mode = ImportMode.Replace });
        Assert.True(plan.CanApply);
        Assert.Equal(Core.Storage.JsonOptions.Serialize(Normalize(lib)), Core.Storage.JsonOptions.Serialize(Normalize(plan.ResultLibrary)));
    }

    [Fact]
    public void CsvExportImport_RoundTripKeepsContent()
    {
        var lib = Lib.Small(3);
        lib.Entries[0].Tags = ["a", "b"];
        lib.Entries[0].Notes = "有,逗號\n和\"引號\"";
        lib.Entries[1].Enabled = false;
        var plan = ImportPlanner.Build(new LibraryData(), ImportReader.ReadCsv(LibraryExporter.ToCsv(lib)),
            new ImportOptions { Mode = ImportMode.Replace });
        Assert.True(plan.CanApply);
        var r = plan.ResultLibrary;
        Assert.Equal(lib.Entries.Count, r.Entries.Count);
        var e0 = r.Entries.Single(e => e.Text == "詞1");
        Assert.Equal(lib.Entries[0].Notes, e0.Notes);
        Assert.Equal(["a", "b"], e0.Tags);
        Assert.False(r.Entries.Single(e => e.Text == "詞2").Enabled);
        Assert.Equal("🕯️", r.Entries.Single(e => e.Text == "蠟燭").Emoji);
    }

    static LibraryData Normalize(LibraryData lib)
    {
        var c = lib.Clone();
        c.ExportedAt = null;
        c.Categories = c.OrderedCategories().ToList();
        c.Entries = c.Entries.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        return c;
    }
}
