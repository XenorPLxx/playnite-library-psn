using System.Text.RegularExpressions;

namespace PlayStationLibrary;

/// <summary>
/// Game name clean-up. Playnite's own <c>NormalizeGameName</c> lives in the main application rather
/// than the SDK, so the logic is reproduced here instead of linking the Playnite repository.
/// </summary>
public static partial class PlayStationGameName
{
    [GeneratedRegex(@"[™©®]")]
    private static partial Regex MarksRegex();

    [GeneratedRegex(@"\[.*?\]")]
    private static partial Regex BracketsRegex();

    [GeneratedRegex(@"\(.*?\)")]
    private static partial Regex ParenthesesRegex();

    [GeneratedRegex(@"\s*:\s*")]
    private static partial Regex ColonRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@",\s*The$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingTheRegex();

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Marks become a space rather than nothing, so "Mario®Kart" stays two words.
        var normalized = MarksRegex().Replace(name, " ");
        normalized = normalized.Replace('_', ' ').Replace('.', ' ').Replace('’', '\'');
        normalized = RemoveUnlessThatEmptiesTheString(normalized, BracketsRegex());
        normalized = RemoveUnlessThatEmptiesTheString(normalized, ParenthesesRegex());
        normalized = ColonRegex().Replace(normalized, ": ");
        normalized = normalized.Replace("full game", string.Empty, StringComparison.OrdinalIgnoreCase);
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        if (TrailingTheRegex().IsMatch(normalized))
        {
            normalized = "The " + TrailingTheRegex().Replace(normalized, string.Empty);
        }

        return normalized.Trim();
    }

    /// <summary>
    /// Trophy list entries are named after the trophy set rather than the game, e.g.
    /// "Bloodborne Trophies" or "Synapse Trophy Set". The suffix has to go, otherwise the game is
    /// not recognised as one already imported under its real name and appears twice.
    /// </summary>
    public static string NormalizeTrophyTitle(string? name)
    {
        var normalized = Normalize(name);

        // Longest first, so "Trophy Set" is not left as a stray "Set" by stripping "Trophy".
        foreach (var suffix in new[] { "Trophy Set", "Trophy List", "Trophies", "Trophy" })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^suffix.Length].Trim();
                break;
            }
        }

        return normalized;
    }

    private static string RemoveUnlessThatEmptiesTheString(string input, Regex pattern)
    {
        var output = pattern.Replace(input, string.Empty);
        return string.IsNullOrWhiteSpace(output) ? input : output;
    }
}
