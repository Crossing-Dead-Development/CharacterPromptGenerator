using System.Text;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Import;

public static class LibraryExporter
{
    public static string ToJson(LibraryData lib)
    {
        var copy = lib.Clone();
        copy.ExportedAt = DateTime.Now;
        copy.Categories = copy.OrderedCategories().ToList();
        return JsonOptions.Serialize(copy);
    }

    /// <summary>CSV 以分類名稱表示分類；額外附上 enabled 欄，再匯入時可還原停用狀態。</summary>
    public static string ToCsv(LibraryData lib)
    {
        var sb = new StringBuilder();
        sb.Append("category,text,emoji,notes,tags,enabled\r\n");
        foreach (var cat in lib.OrderedCategories())
        {
            foreach (var e in lib.Entries.Where(e => e.CategoryId == cat.Id))
            {
                sb.Append(CsvParser.Escape(cat.Name)).Append(',')
                  .Append(CsvParser.Escape(e.Text)).Append(',')
                  .Append(CsvParser.Escape(e.Emoji)).Append(',')
                  .Append(CsvParser.Escape(e.Notes)).Append(',')
                  .Append(CsvParser.Escape(TextNormalizer.JoinTags(e.Tags))).Append(',')
                  .Append(e.Enabled ? "true" : "false")
                  .Append("\r\n");
            }
        }
        return sb.ToString();
    }

    public static void ExportJson(LibraryData lib, string path) =>
        AtomicFile.WriteAllText(path, ToJson(lib), keepBak: false);

    /// <summary>CSV 加 BOM，讓 Excel 直接開啟時正確顯示中文與 Emoji。</summary>
    public static void ExportCsv(LibraryData lib, string path) =>
        AtomicFile.WriteAllText(path, ToCsv(lib), withBom: true, keepBak: false);
}
