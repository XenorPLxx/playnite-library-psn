namespace PlayStationLibrary;

/// <summary>
/// A PlayStation region and language. The same list the Universal PSN Metadata extension offers, so
/// both extensions can be pointed at the same region.
/// </summary>
public sealed record PlayStationLocale(string DisplayName, string Locale)
{
    public override string ToString() => DisplayName;
}

public static class PlayStationLocales
{
    /// <summary>Empty locale means "follow the Playnite UI language".</summary>
    public const string Automatic = "";

    public static IReadOnlyList<PlayStationLocale> All { get; } =
    [
        new("Automatic (use Playnite language)", Automatic),
        new("Argentina — Español", "es-ar"),
        new("Australia — English", "en-au"),
        new("Austria — Deutsch", "de-at"),
        new("Bahrain — English", "en-bh"),
        new("Bahrain — العربية", "ar-bh"),
        new("Belgium — Français", "fr-be"),
        new("Belgium — Nederlands", "nl-be"),
        new("Bolivia — Español", "es-bo"),
        new("Brazil — Português", "pt-br"),
        new("Bulgaria — Български", "bg-bg"),
        new("Bulgaria — English", "en-bg"),
        new("Canada — English", "en-ca"),
        new("Canada — Français", "fr-ca"),
        new("Chile — Español", "es-cl"),
        new("China — 简体中文", "zh-hans-cn"),
        new("Colombia — Español", "es-co"),
        new("Costa Rica — Español", "es-cr"),
        new("Croatia — Hrvatski", "hr-hr"),
        new("Croatia — English", "en-hr"),
        new("Cyprus — English", "en-cy"),
        new("Czech Republic — Čeština", "cs-cz"),
        new("Czech Republic — English", "en-cz"),
        new("Denmark — Dansk", "da-dk"),
        new("Denmark — English", "en-dk"),
        new("Ecuador — Español", "es-ec"),
        new("El Salvador — Español", "es-sv"),
        new("Finland — Suomi", "fi-fi"),
        new("Finland — English", "en-fi"),
        new("France — Français", "fr-fr"),
        new("Germany — Deutsch", "de-de"),
        new("Greece — Ελληνικά", "el-gr"),
        new("Greece — English", "en-gr"),
        new("Guatemala — Español", "es-gt"),
        new("Honduras — Español", "es-hn"),
        new("Hong Kong — English", "en-hk"),
        new("Hong Kong — 简体中文", "zh-hans-hk"),
        new("Hong Kong — 繁體中文", "zh-hant-hk"),
        new("Hungary — Magyar", "hu-hu"),
        new("Hungary — English", "en-hu"),
        new("Iceland — English", "en-is"),
        new("India — English", "en-in"),
        new("Indonesia — English", "en-id"),
        new("Ireland — English", "en-ie"),
        new("Israel — עברית", "he-il"),
        new("Israel — English", "en-il"),
        new("Italy — Italiano", "it-it"),
        new("Japan — 日本語", "ja-jp"),
        new("Korea — 한국어", "ko-kr"),
        new("Kuwait — English", "en-kw"),
        new("Kuwait — العربية", "ar-kw"),
        new("Lebanon — English", "en-lb"),
        new("Lebanon — العربية", "ar-lb"),
        new("Luxembourg — Deutsch", "de-lu"),
        new("Luxembourg — Français", "fr-lu"),
        new("Malaysia — English", "en-my"),
        new("Malta — English", "en-mt"),
        new("Mexico — Español", "es-mx"),
        new("Netherlands — Nederlands", "nl-nl"),
        new("New Zealand — English", "en-nz"),
        new("Nicaragua — Español", "es-ni"),
        new("Norway — Norsk", "no-no"),
        new("Norway — English", "en-no"),
        new("Oman — English", "en-om"),
        new("Oman — العربية", "ar-om"),
        new("Panama — Español", "es-pa"),
        new("Paraguay — Español", "es-py"),
        new("Peru — Español", "es-pe"),
        new("Philippines — English", "en-ph"),
        new("Poland — Polski", "pl-pl"),
        new("Poland — English", "en-pl"),
        new("Portugal — Português", "pt-pt"),
        new("Qatar — English", "en-qa"),
        new("Qatar — العربية", "ar-qa"),
        new("Romania — Română", "ro-ro"),
        new("Romania — English", "en-ro"),
        new("Russia — Русский", "ru-ru"),
        new("Saudi Arabia — English", "en-sa"),
        new("Saudi Arabia — العربية", "ar-sa"),
        new("Serbia — Српски", "sr-rs"),
        new("Singapore — English", "en-sg"),
        new("Slovakia — Slovenčina", "sk-sk"),
        new("Slovakia — English", "en-sk"),
        new("Slovenia — Slovenščina", "sl-si"),
        new("Slovenia — English", "en-si"),
        new("South Africa — English", "en-za"),
        new("Spain — Español", "es-es"),
        new("Sweden — Svenska", "sv-se"),
        new("Sweden — English", "en-se"),
        new("Switzerland — Deutsch", "de-ch"),
        new("Switzerland — Français", "fr-ch"),
        new("Switzerland — Italiano", "it-ch"),
        new("Taiwan — English", "en-tw"),
        new("Taiwan — 繁體中文", "zh-hant-tw"),
        new("Thailand — ไทย", "th-th"),
        new("Thailand — English", "en-th"),
        new("Turkey — Türkçe", "tr-tr"),
        new("Turkey — English", "en-tr"),
        new("Ukraine — Українська", "uk-ua"),
        new("Ukraine — Русский", "ru-ua"),
        new("United Arab Emirates — English", "en-ae"),
        new("United Arab Emirates — العربية", "ar-ae"),
        new("United Kingdom — English", "en-gb"),
        new("United States — English", "en-us"),
        new("Uruguay — Español", "es-uy"),
        new("Vietnam — English", "en-vn"),
    ];

    /// <summary>
    /// Converts a stored locale such as "zh-hans-cn" into the casing an Accept-Language header
    /// expects ("zh-Hans-CN").
    /// </summary>
    public static string ToAcceptLanguage(string? locale, string? playniteLanguage)
    {
        var value = string.IsNullOrWhiteSpace(locale)
            ? (playniteLanguage ?? string.Empty).Replace('_', '-')
            : locale;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "en-US";
        }

        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = i == 0
                ? parts[i].ToLowerInvariant()
                : parts[i].Length == 2 ? parts[i].ToUpperInvariant() : char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
        }

        return string.Join('-', parts);
    }

    /// <summary>
    /// Builds the language-preference list sent to the trophy API. Sony does not publish a list of
    /// every regional variant it supports, so an exact preference such as es-AR must be followed by
    /// usable language fallbacks. Without them an unsupported regional tag can make the API fall
    /// back to the account's preferred language instead.
    /// </summary>
    public static string ToTrophyAcceptLanguage(string? locale, string? playniteLanguage)
    {
        var exactLocale = ToAcceptLanguage(locale, playniteLanguage);
        var parts = exactLocale.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return exactLocale;
        }

        var language = parts[0];
        var preferences = new List<string> { exactLocale };

        // Sony uses es-419 for Latin American Spanish in the PlayStation mobile APIs. It is a
        // better fallback than generic Spanish for country-specific Latin American selections.
        if (string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(exactLocale, "es-ES", StringComparison.OrdinalIgnoreCase))
        {
            preferences.Add("es-419;q=0.9");
            preferences.Add("es;q=0.8");
        }
        else
        {
            preferences.Add(language + ";q=0.9");
        }

        return string.Join(", ", preferences);
    }
}
