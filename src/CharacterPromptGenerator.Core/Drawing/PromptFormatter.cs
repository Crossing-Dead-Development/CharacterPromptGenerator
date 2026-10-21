using System.Text;
using CharacterPromptGenerator.Core.Models;

namespace CharacterPromptGenerator.Core.Drawing;

public static class PromptFormatter
{
    public static string ItemText(string text, string? emoji) =>
        string.IsNullOrEmpty(emoji) || text.Contains(emoji, StringComparison.Ordinal) ? text : $"{emoji} {text}";

    /// <summary>複製用的純文字：每個分類一行，「分類：詞1、詞2」。</summary>
    public static string ToText(IEnumerable<(string CategoryName, string Text, string? Emoji)> items, DateTime? time = null)
    {
        var sb = new StringBuilder();
        sb.Append("【角色設計題目】");
        if (time is { } t) sb.Append(' ').Append(t.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine();
        foreach (var group in items.GroupBy(i => i.CategoryName))
            sb.Append(group.Key).Append('：').AppendLine(string.Join("、", group.Select(i => ItemText(i.Text, i.Emoji))));
        return sb.ToString().TrimEnd();
    }

    public static string ToText(IEnumerable<DrawSlot> slots, DateTime? time = null) =>
        ToText(slots.Select(s => (s.CategoryName, s.Text, s.Emoji)), time);

    public static string ToText(SavedResult r) =>
        ToText(r.Items.Select(i => (i.CategoryName, i.Text, i.Emoji)), r.CreatedAt);

    public static SavedResult Snapshot(IEnumerable<DrawSlot> slots, DateTime time) => new()
    {
        Id = Text.IdGenerator.New("h"),
        CreatedAt = time,
        Items = slots.Select(s => new SavedItem
        {
            CategoryId = s.CategoryId,
            CategoryName = s.CategoryName,
            EntryId = s.EntryId,
            Text = s.Text,
            Emoji = s.Emoji,
            Notes = s.Notes,
        }).ToList(),
    };
}
