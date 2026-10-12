using PlayStationLibrary;

internal static class TrophyLabelChecks
{
    public static void Run()
    {
        var english = PlayStationTrophyLabels.ForLocale("en-US");
        var locales = PlayStationLocales.All.Where(option => option.Locale != PlayStationLocales.Automatic).ToList();
        var translations = new HashSet<PlayStationTrophyLabels>();
        foreach (var option in locales)
        {
            var header = PlayStationLocales.ToTrophyAcceptLanguage(option.Locale, "en_US");
            var labels = PlayStationTrophyLabels.ForLocale(header);
            translations.Add(labels);
            string[] values = [labels.BaseGame, labels.AdditionalContent, labels.Bronze, labels.Silver, labels.Gold, labels.Platinum];
            Equal(false, values.Any(string.IsNullOrWhiteSpace), $"All six labels populated for {option.Locale}");
            Equal(labels, PlayStationTrophyLabels.ForLocale(option.Locale.ToUpperInvariant().Replace('-', '_')),
                $"Header, casing and underscore handling for {option.Locale}");

            // Every non-English selector language must have its own labels, not silently fall back.
            // This also catches future selector additions that lack a translation table entry.
            Equal(option.Locale.StartsWith("en-", StringComparison.Ordinal), labels == english,
                $"English fallback only for English selector locales: {option.Locale}");
        }

        var simplified = new PlayStationTrophyLabels("游戏本体", "追加内容", "铜", "银", "金", "白金");
        var traditional = new PlayStationTrophyLabels("遊戲本體", "追加內容", "銅", "銀", "金", "白金");
        foreach (var locale in new[] { "zh", "zh-CN", "zh_SG", "zh-Hans", "zh-Hans-HK", "zh-Hans-TW", "zh-Hans-CN, zh;q=0.9" })
        {
            Equal(simplified, PlayStationTrophyLabels.ForLocale(locale), $"Simplified Chinese: {locale}");
        }

        foreach (var locale in new[] { "zh-TW", "zh_HK", "zh-MO", "zh-Hant", "zh-Hant-CN", " ZH_hAnT_TW ;q=1, zh;q=0.9" })
        {
            Equal(traditional, PlayStationTrophyLabels.ForLocale(locale), $"Traditional Chinese: {locale}");
        }

        Equal(traditional, PlayStationTrophyLabels.ForLocale(
            PlayStationLocales.ToTrophyAcceptLanguage(PlayStationLocales.Automatic, "zh_TW")), "Automatic uses UI Chinese region");
        Equal("本編", PlayStationTrophyLabels.ForLocale(
            PlayStationLocales.ToTrophyAcceptLanguage(PlayStationLocales.Automatic, "ja_JP")).BaseGame, "Automatic Japanese labels");
        Equal("برونزية", PlayStationTrophyLabels.ForLocale("ar-AE").Bronze, "Arabic bronze label");
        Equal("플래티넘", PlayStationTrophyLabels.ForLocale("ko-KR").Platinum, "Korean platinum label");
        Equal("ทองแดง", PlayStationTrophyLabels.ForLocale("th-TH").Bronze, "Thai bronze label");
        Equal("Srebro", PlayStationTrophyLabels.ForLocale("pl-PL, pl;q=0.9").Silver, "Polish labels unchanged");
        Equal(PlayStationTrophyLabels.ForLocale("no-NO"), PlayStationTrophyLabels.ForLocale("nb_NO"), "Bokmal language alias");
        Equal(PlayStationTrophyLabels.ForLocale("fr-FR"), PlayStationTrophyLabels.ForLocale("fr-CA, fr;q=0.9"), "French regional fallback");

        foreach (var locale in new string?[] { null, "", "  ", "unknown", "xx-YY, fr;q=0.9", "en-GB" })
        {
            Equal(english, PlayStationTrophyLabels.ForLocale(locale), $"English fallback: {locale ?? "null"}");
        }

        Console.WriteLine($"PASS: trophy labels for {locales.Count} selectable locales ({translations.Count} label sets), Chinese scripts and English fallback.");
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
        }
    }
}
