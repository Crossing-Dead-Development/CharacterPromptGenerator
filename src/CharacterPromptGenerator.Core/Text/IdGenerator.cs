namespace CharacterPromptGenerator.Core.Text;

public static class IdGenerator
{
    public static string New(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    /// <summary>產生一個不在 <paramref name="used"/> 內的新 ID。</summary>
    public static string NewUnique(string prefix, ICollection<string> used)
    {
        while (true)
        {
            var id = New(prefix);
            if (!used.Contains(id)) return id;
        }
    }
}
