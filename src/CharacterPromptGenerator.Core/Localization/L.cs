using System.Globalization;
using System.Resources;

namespace CharacterPromptGenerator.Core.Localization;

/// <summary>介面語言選項。Code 存在 settings.json，DisplayName 一律用該語言本身的寫法。</summary>
public sealed record UiLanguage(string Code, string DisplayName);

/// <summary>
/// 介面文字。字串放在 Strings.resx（繁體中文，預設）、Strings.en.resx（英文）與 Strings.ja.resx（日文），
/// 以 key 取用：XAML 用 {l:T Key}，程式用 L.T("Key") / L.F("Key", args)。
/// 語言在啟動時決定一次（Windows 版切換後需要重新啟動，手機版會立即重建畫面）。
/// </summary>
public static class L
{
    public const string Chinese = "zh-TW";
    public const string English = "en";
    public const string Japanese = "ja";

    public static IReadOnlyList<UiLanguage> Languages { get; } =
    [
        new(Chinese, "繁體中文"),
        new(English, "English"),
        new(Japanese, "日本語"),
    ];

    static readonly ResourceManager Resources =
        new("CharacterPromptGenerator.Core.Localization.Strings", typeof(L).Assembly);

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(Resolve(null));

    /// <summary>目前使用的語言代碼（zh-TW、en 或 ja）。</summary>
    public static string Current => Culture.Name;

    /// <summary>沒有設定或設定值不認識時，依系統顯示語言決定（Windows 與 Android 相同規則）。</summary>
    public static string Resolve(string? code) =>
        Resolve(code, [CultureInfo.CurrentUICulture.Name, CultureInfo.InstalledUICulture.Name]);

    /// <summary>
    /// <paramref name="code"/> 是支援的語言就直接使用；否則看系統語言（第一個不是空白的文化名稱，通常是目前的顯示語言）：
    /// 中文（繁體、簡體、香港、新加坡…都算）→ 繁體中文，日文 → 日文，其他一律英文。
    /// </summary>
    public static string Resolve(string? code, IEnumerable<string> systemCultures)
    {
        if (Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) is { } chosen)
            return chosen.Code;
        var system = systemCultures.FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "";
        if (system.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Chinese;
        if (system.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return Japanese;
        return English;
    }

    public static void Use(string? code)
    {
        Culture = CultureInfo.GetCultureInfo(Resolve(code));
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    /// <summary>取得字串；key 不存在時回傳 key 本身，畫面上一眼就看得出漏了什麼。</summary>
    public static string T(string key) => Resources.GetString(key, Culture) ?? key;

    /// <summary>以指定語言取得字串（不改變目前語言）。</summary>
    public static string T(string key, string code) =>
        Resources.GetString(key, CultureInfo.GetCultureInfo(Resolve(code))) ?? key;

    /// <summary>帶參數的字串（string.Format 格式：{0}、{1:HH:mm}…）。</summary>
    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);
}
