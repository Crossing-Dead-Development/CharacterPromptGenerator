using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharacterPromptGenerator.Core.Storage;

public static class JsonOptions
{
    /// <summary>所有資料檔共用：camelCase、縮排、中文不跳脫（方便人工閱讀）。</summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        Encoder = ReadableJsonEncoder.Instance,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Default);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Default);
}
