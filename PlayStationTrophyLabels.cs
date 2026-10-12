namespace PlayStationLibrary;

/// <summary>
/// Labels Sony does not supply. These follow the requested trophy language, which can differ from
/// the Playnite UI language used by Loc. Covers every language in the trophy locale selector;
/// unknown languages use English. These are display names, never part of the stable trophy IDs.
/// </summary>
internal sealed record PlayStationTrophyLabels(
    string BaseGame, string AdditionalContent, string Bronze, string Silver, string Gold, string Platinum)
{
    public static PlayStationTrophyLabels ForLocale(string? locale)
    {
        // The cache locale is a complete Accept-Language list, e.g. "fr-CA, fr;q=0.9".
        // Use its first preference, retaining script/region information for Chinese. UI language
        // tags can also use underscores (e.g. zh_TW) when the trophy language is Automatic.
        var parts = (locale ?? string.Empty).Split([',', ';'])[0].Trim().Replace('_', '-')
            .ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);
        var language = parts.FirstOrDefault();
        if (language == "zh")
        {
            // An explicit script wins over the region: zh-Hans-HK is Simplified, not Traditional.
            // Without a script, Taiwan/Hong Kong/Macao use Traditional; other tags use Simplified.
            var traditional = parts.Contains("hant") ||
                (!parts.Contains("hans") && parts.Any(part => part is "tw" or "hk" or "mo"));
            return traditional
                ? new("遊戲本體", "追加內容", "銅", "銀", "金", "白金")
                : new("游戏本体", "追加内容", "铜", "银", "金", "白金");
        }

        return language switch
        {
            "ar" => new("اللعبة الأساسية", "محتوى إضافي", "برونزية", "فضية", "ذهبية", "بلاتينية"),
            "bg" => new("Основна игра", "Допълнително съдържание", "Бронз", "Сребро", "Злато", "Платина"),
            "cs" => new("Základní hra", "Dodatečný obsah", "Bronz", "Stříbro", "Zlato", "Platina"),
            "da" => new("Grundspil", "Ekstra indhold", "Bronze", "Sølv", "Guld", "Platin"),
            "de" => new("Hauptspiel", "Zusätzliche Inhalte", "Bronze", "Silber", "Gold", "Platin"),
            "el" => new("Βασικό παιχνίδι", "Πρόσθετο περιεχόμενο", "Χάλκινο", "Ασημένιο", "Χρυσό", "Πλατινένιο"),
            "es" => new("Juego base", "Contenido adicional", "Bronce", "Plata", "Oro", "Platino"),
            "fi" => new("Peruspeli", "Lisäsisältö", "Pronssi", "Hopea", "Kulta", "Platina"),
            "fr" => new("Jeu de base", "Contenu additionnel", "Bronze", "Argent", "Or", "Platine"),
            "he" => new("משחק הבסיס", "תוכן נוסף", "ארד", "כסף", "זהב", "פלטינה"),
            "hr" => new("Osnovna igra", "Dodatni sadržaj", "Bronca", "Srebro", "Zlato", "Platina"),
            "hu" => new("Alapjáték", "Kiegészítő tartalom", "Bronz", "Ezüst", "Arany", "Platina"),
            "it" => new("Gioco base", "Contenuti aggiuntivi", "Bronzo", "Argento", "Oro", "Platino"),
            "ja" => new("本編", "追加コンテンツ", "ブロンズ", "シルバー", "ゴールド", "プラチナ"),
            "ko" => new("기본 게임", "추가 콘텐츠", "브론즈", "실버", "골드", "플래티넘"),
            "nl" => new("Basisspel", "Aanvullende content", "Brons", "Zilver", "Goud", "Platina"),
            "no" or "nb" => new("Grunnspill", "Tilleggsinnhold", "Bronse", "Sølv", "Gull", "Platina"),
            "pl" => new("Podstawowa gra", "Dodatkowa zawartość", "Brąz", "Srebro", "Złoto", "Platyna"),
            "pt" => new("Jogo base", "Conteúdo adicional", "Bronze", "Prata", "Ouro", "Platina"),
            "ro" => new("Joc de bază", "Conținut suplimentar", "Bronz", "Argint", "Aur", "Platină"),
            "ru" => new("Основная игра", "Дополнительный контент", "Бронза", "Серебро", "Золото", "Платина"),
            "sk" => new("Základná hra", "Dodatočný obsah", "Bronz", "Striebro", "Zlato", "Platina"),
            "sl" => new("Osnovna igra", "Dodatna vsebina", "Bron", "Srebro", "Zlato", "Platina"),
            "sr" => new("Основна игра", "Додатни садржај", "Бронза", "Сребро", "Злато", "Платина"),
            "sv" => new("Grundspel", "Extra innehåll", "Brons", "Silver", "Guld", "Platina"),
            "th" => new("เกมหลัก", "เนื้อหาเพิ่มเติม", "ทองแดง", "เงิน", "ทอง", "แพลทินัม"),
            "tr" => new("Ana oyun", "Ek içerik", "Bronz", "Gümüş", "Altın", "Platin"),
            "uk" => new("Основна гра", "Додатковий вміст", "Бронза", "Срібло", "Золото", "Платина"),
            _ => new("Base Game", "Additional Content", "Bronze", "Silver", "Gold", "Platinum")
        };
    }
}
