using System.IO;
using System.Text.Json;
using Playnite;

namespace PlayStationLibrary;

/// <summary>
/// Remembers how far each game's trophy set had been completed at the last refresh.
/// </summary>
/// <remarks>
/// Reading one game's trophies costs two or three requests, while the trophy title list reports
/// every set's completion percentage in a single request. Comparing against the previous run lets an
/// unchanged game be skipped entirely, which is most of them on a typical refresh. The Playnite game
/// id and requested locale are part of the key: deleting and reimporting a game must not make its new
/// achievement collection look up to date merely because its previous instance had the same trophy set,
/// and changing language must retrieve localized trophy text again.
/// </remarks>
public sealed class PlayStationTrophyProgress
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly string progressPath;
    // This character cannot occur in a Playnite game id, a PlayStation communication id, or a locale.
    // It also distinguishes the per-game, per-locale cache from earlier cache formats.
    private const char CacheKeySeparator = '\u001F';

    private readonly Dictionary<string, string> progressByGameTrophySetAndLocale;

    public PlayStationTrophyProgress(string userDataDir)
    {
        progressPath = Path.Combine(userDataDir, "trophy-progress.json");
        progressByGameTrophySetAndLocale = Read();
    }

    /// <summary>
    /// True when this game, set, and locale are known and sit at exactly the completion they had last
    /// time, meaning its trophies cannot have changed.
    /// </summary>
    public bool IsUnchanged(string gameId, string communicationId, string locale, string currentState)
    {
        return progressByGameTrophySetAndLocale.TryGetValue(BuildCacheKey(gameId, communicationId, locale), out var previous) &&
               string.Equals(previous, currentState, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the same game and trophy set have previously been read with another requested
    /// locale. This distinguishes a language refresh from an ordinary progress refresh, so the
    /// latter never overwrites user-edited trophy text.
    /// </summary>
    public bool HasAnotherLocale(string gameId, string communicationId, string locale)
    {
        var prefix = gameId + CacheKeySeparator + communicationId + CacheKeySeparator;
        var currentKey = BuildCacheKey(gameId, communicationId, locale);
        return progressByGameTrophySetAndLocale.Keys.Any(key =>
            key.StartsWith(prefix, StringComparison.Ordinal) &&
            !string.Equals(key, currentKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// True when this game and trophy set have been read before under any locale. False means the
    /// cache is new, was deleted, or was migrated from a format that did not record the locale, so
    /// the language of any trophies already in the collection is unknown.
    /// </summary>
    public bool HasAnyLocale(string gameId, string communicationId)
    {
        var prefix = gameId + CacheKeySeparator + communicationId + CacheKeySeparator;
        return progressByGameTrophySetAndLocale.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    public void Record(string gameId, string communicationId, string locale, string state)
    {
        var prefix = gameId + CacheKeySeparator + communicationId + CacheKeySeparator;
        var currentKey = BuildCacheKey(gameId, communicationId, locale);
        foreach (var outdatedKey in progressByGameTrophySetAndLocale.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(key, currentKey, StringComparison.Ordinal))
                     .ToList())
        {
            progressByGameTrophySetAndLocale.Remove(outdatedKey);
        }

        progressByGameTrophySetAndLocale[currentKey] = state;
    }

    /// <summary>Forgets a game's localized set so the next refresh fetches it again.</summary>
    public void Forget(string gameId, string communicationId, string locale)
    {
        progressByGameTrophySetAndLocale.Remove(BuildCacheKey(gameId, communicationId, locale));
    }

    /// <summary>
    /// Drops entries for games that are no longer in the library, which would otherwise accumulate
    /// forever as games are removed and reimported.
    /// </summary>
    public void PruneMissingGames(Func<string, bool> gameExists)
    {
        foreach (var staleKey in progressByGameTrophySetAndLocale.Keys
                     .Where(key => !gameExists(key.Split(CacheKeySeparator)[0]))
                     .ToList())
        {
            progressByGameTrophySetAndLocale.Remove(staleKey);
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(progressPath, JsonSerializer.Serialize(progressByGameTrophySetAndLocale));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to store PlayStation trophy progress.");
        }
    }

    private Dictionary<string, string> Read()
    {
        if (!File.Exists(progressPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            // Read loosely so that an entry written by an earlier build is discarded quietly rather
            // than failing the whole file.
            var storedProgress = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(progressPath));
            if (storedProgress == null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            // Earlier formats did not include every part of the achievement identity, or recorded a
            // bare completion percentage. Neither can say whether this game still has its collection,
            // whether the trophy text has the requested language, or whether a trophy was unlocked
            // without moving the percentage, so they are discarded once.
            var progress = storedProgress
                .Where(entry =>
                    entry.Key.Count(character => character == CacheKeySeparator) == 2 &&
                    entry.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(entry => entry.Key, entry => entry.Value.GetString()!, StringComparer.Ordinal);
            if (progress.Count != storedProgress.Count)
            {
                logger.Debug("Discarding an outdated PlayStation trophy-progress cache so achievements are refreshed once.");
            }

            return progress;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to read PlayStation trophy progress.");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string BuildCacheKey(string gameId, string communicationId, string locale)
    {
        return gameId + CacheKeySeparator + communicationId + CacheKeySeparator + locale;
    }
}
