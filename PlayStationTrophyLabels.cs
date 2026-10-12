namespace PlayStationLibrary;

/// <summary>
/// Labels Sony does not supply. These follow the requested trophy language, which can differ from
/// the Playnite UI language used by Loc. Covers the plugin's translated languages; others use English.
/// </summary>
internal sealed record PlayStationTrophyLabels(
    string BaseGame, string AdditionalContent, string Bronze, string Silver, string Gold, string Platinum)
{
    public static PlayStationTrophyLabels ForLocale(string locale)
    {
        // The cache locale is a complete Accept-Language list, e.g. "fr-CA, fr;q=0.9".
        var language = locale.Split(['-', '_', ',', ';'])[0].Trim().ToLowerInvariant();
        return language switch
        {
            "cs" => new("Základní hra", "Dodatečný obsah", "Bronz", "Stříbro", "Zlato", "Platina"),
            "de" => new("Hauptspiel", "Zusätzliche Inhalte", "Bronze", "Silber", "Gold", "Platin"),
            "es" => new("Juego base", "Contenido adicional", "Bronce", "Plata", "Oro", "Platino"),
            "fr" => new("Jeu de base", "Contenu additionnel", "Bronze", "Argent", "Or", "Platine"),
            "it" => new("Gioco base", "Contenuti aggiuntivi", "Bronzo", "Argento", "Oro", "Platino"),
            "nl" => new("Basisspel", "Aanvullende content", "Brons", "Zilver", "Goud", "Platina"),
            "pl" => new("Podstawowa gra", "Dodatkowa zawartość", "Brąz", "Srebro", "Złoto", "Platyna"),
            "pt" => new("Jogo base", "Conteúdo adicional", "Bronze", "Prata", "Ouro", "Platina"),
            _ => new("Base Game", "Additional Content", "Bronze", "Silver", "Gold", "Platinum")
        };
    }
}
