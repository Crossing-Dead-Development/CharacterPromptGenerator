using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CharacterPromptGenerator.Core.Models;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>
/// 編譯期產生的序列化程式碼。Android 版 Release 會裁剪未使用的程式碼，不能依賴反射，
/// 所有資料檔型別都要列在這裡；沒列到的型別序列化時會直接丟例外，而不是悄悄改走反射。
/// </summary>
[JsonSerializable(typeof(LibraryData))]
[JsonSerializable(typeof(PresetFile))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HistoryFile))]
[JsonSerializable(typeof(BackupManifest))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;

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
        TypeInfoResolver = CoreJsonContext.Default,
    };

    static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Default.GetTypeInfo(typeof(T));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Info<T>());

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize(json, Info<T>());
}
