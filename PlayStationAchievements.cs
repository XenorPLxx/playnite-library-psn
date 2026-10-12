using System.IO;
using Playnite;

namespace PlayStationLibrary;

/// <summary>
/// Turns PlayStation trophies into Playnite achievements.
/// </summary>
/// <remarks>
/// Resolving a game id to its trophy set costs a request per five games, so the result is cached on
/// the game as an external identifier and only looked up once.
/// </remarks>
public sealed class PlayStationAchievements
{
    // Trophy-list-only games use this stable identifier at library-import time. Store-title games
    // use the repeatable cache type below because a single title can contain multiple sets.
    internal const string TrophySetIdType = "psn_trophy_set";
    internal const string TrophySetIdName = "PlayStation trophy set";

    // Each game can have more than one trophy set, so cached values use their own repeatable
    // external-identifier type instead of pretending that one game maps to one trophy set.
    private const string TrophySetCacheIdType = "psn_trophy_sets";
    private const string TrophySetCacheIdName = "PlayStation trophy sets";

    // Trophy set ids are always of the form NPWRxxxxx_00, which is how a game imported from the
    // trophy list can be recognised without any lookup at all.
    private const string TrophySetIdPrefix = "NPWR";

    /// <summary>
    /// The original trophy service, which covers PlayStation 3, PSP and Vita. PlayStation 5 sets are
    /// served by "trophy2" instead, so this is only a fallback when the API did not say.
    /// </summary>
    internal const string DefaultTrophyServiceName = "trophy";

    /// <summary>Each game costs two requests; this keeps a full refresh from hammering Sony.</summary>
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Stands in for a set the trophy title list does not report, which happens for owned but never
    /// played games.
    /// </summary>
    private const string NoReportedProgress = "unreported";

    /// <summary>
    /// Combines completion with the set's own last-updated stamp. The percentage alone is an integer,
    /// so a single unlock in a set of more than a hundred trophies would not move it.
    /// </summary>
    private static string FormatState(PlayStationTrophyTitle title) =>
        $"{title.Progress}@{title.LastPlayed?.ToUnixTimeSeconds() ?? 0}";

    private static readonly ILogger logger = LogManager.GetLogger<PlayStationAchievements>();
    private readonly IPlayniteApi playniteApi;
    private readonly PlayStationTrophyProgress trophyProgress;

    private sealed record TrophySetResolution(IReadOnlyList<PlayStationTrophySet> Sets);
    private sealed record TrophySetState(
        PlayStationTrophySet Set,
        string Progress,
        bool RefreshTrophyText,
        string DisplayName);

    public PlayStationAchievements(IPlayniteApi playniteApi)
    {
        this.playniteApi = playniteApi;
        trophyProgress = new PlayStationTrophyProgress(playniteApi.UserDataDir);
    }

    public async Task<List<ImportableAchievements>> GetAchievementsAsync(
        PlayStationSession session,
        IReadOnlyCollection<Game> games,
        string locale,
        string causation,
        CancellationToken cancellationToken)
    {
        // Alpha2's game-menu command uses ManualEdit; library and post-play updates remain cached.
        if (string.Equals(causation, BuiltInCausations.ManualEdit, StringComparison.Ordinal))
        {
            foreach (var game in games)
            {
                trophyProgress.ForgetGame(game.Id);
            }

            // Persist the invalidation even if the network request fails or the user cancels.
            trophyProgress.Save();
        }

        var labels = PlayStationTrophyLabels.ForLocale(locale);
        var currentTrophyTitles = await GetCurrentTrophyTitlesAsync(session, cancellationToken);
        var trophySets = await ResolveTrophySetsAsync(session, games, cancellationToken);
        var results = new List<ImportableAchievements>();
        var completedStates = new List<(string GameId, TrophySetState State)>();
        var requestedGameIds = games
            .Select(game => game.Id)
            .Where(gameId => !string.IsNullOrWhiteSpace(gameId))
            .Select(gameId => gameId!)
            .ToHashSet(StringComparer.Ordinal);
        // P11's standard achievement import preserves the text of an existing achievement. That
        // protects historical data, but also prevents a refreshed trophy language from reaching
        // trophies already in the collection. Keep them ready for a targeted metadata update
        // after we have downloaded a fresh trophy definition.
        var existingAchievementsByGame = playniteApi.Library.GameAchievements
            .Where(achievement =>
                !string.IsNullOrWhiteSpace(achievement.GameId) &&
                requestedGameIds.Contains(achievement.GameId!))
            .GroupBy(achievement => achievement.GameId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!trophySets.TryGetValue(game.Id, out var resolution))
            {
                continue;
            }

            var setStates = resolution.Sets
                .Select(trophySet =>
                {
                    currentTrophyTitles.TryGetValue(trophySet.CommunicationId, out var currentTitle);
                    return new TrophySetState(
                        trophySet,
                        currentTitle is null ? NoReportedProgress : FormatState(currentTitle),
                        trophyProgress.HasAnotherLocale(game.Id, trophySet.CommunicationId, locale) ||
                        !trophyProgress.HasAnyLocale(game.Id, trophySet.CommunicationId),
                        !string.IsNullOrWhiteSpace(currentTitle?.Name) ? currentTitle.Name :
                        !string.IsNullOrWhiteSpace(trophySet.Name) ? trophySet.Name :
                        trophySet.CommunicationId);
                })
                .ToList();

            // Playnite imports one payload per game. If any set changed, re-read every set so that
            // a collection is always supplied as one complete, collision-free achievement list.
            var needsRefresh = setStates.Any(state =>
                state.RefreshTrophyText ||
                !trophyProgress.IsUnchanged(game.Id, state.Set.CommunicationId, locale, state.Progress));
            if (!needsRefresh)
            {
                continue;
            }

            var importedAchievements = new List<ImportableAchievement>();
            var metadataToRefresh = new Dictionary<string, PlayStationTrophy>(StringComparer.Ordinal);
            var complete = true;
            var isCollection = setStates.Count > 1;

            // Do not mark any member of a collection current before the complete payload and its
            // metadata are ready. A failed member or write must remain retryable next time.
            trophyProgress.ForgetGame(game.Id);
            trophyProgress.Save();

            foreach (var state in setStates)
            {
                try
                {
                    await Task.Delay(RequestDelay, cancellationToken);
                    var trophies = await session.GetTrophiesAsync(state.Set, cancellationToken);
                    if (trophies.Count == 0)
                    {
                        throw new InvalidDataException($"No trophy definitions returned for '{state.Set.CommunicationId}'.");
                    }

                    var groupNames = await GetGroupNamesAsync(session, state.Set, trophies, cancellationToken);
                    var importedSet = trophies.Select(trophy =>
                        ToAchievement(trophy, groupNames, state.Set, state.DisplayName, isCollection, labels)).ToList();
                    importedAchievements.AddRange(importedSet);
                    if (state.RefreshTrophyText)
                    {
                        foreach (var trophy in trophies)
                        {
                            metadataToRefresh.Add(FormatAchievementId(state.Set, trophy.Id), trophy);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // Returning an incomplete collection could hide the other set from Playnite's
                    // standard importer. Leave the existing game untouched until every set can be
                    // read, while still allowing the rest of the library to continue.
                    complete = false;
                    logger.Error(e, $"Failed to read PlayStation trophies for '{state.Set.CommunicationId}'.");
                }
            }

            if (complete && importedAchievements.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await UpdateTrophyMetadataAsync(
                        game.Id, importedAchievements, metadataToRefresh, existingAchievementsByGame);
                    foreach (var state in setStates)
                    {
                        completedStates.Add((game.Id, state));
                    }
                }
                catch (Exception e)
                {
                    // Return the complete import for normal progress handling, but leave its cache
                    // invalid so a later sync retries every metadata write, even without new progress.
                    logger.Error(e, $"Failed to refresh PlayStation achievement metadata for '{game.Name}'.");
                }

                results.Add(new ImportableAchievements(game.Id, importedAchievements));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Defer recording successes until the batch can be returned. If another game cancels the
        // operation, Playnite receives no payload and all attempted games must remain retryable.
        foreach (var (gameId, state) in completedStates)
        {
            // Also remember unreported sets, or those members would never be cached.
            trophyProgress.Record(gameId, state.Set.CommunicationId, locale, state.Progress);
        }

        trophyProgress.PruneMissingGames(gameId => playniteApi.Library.Games.Contains(gameId));
        trophyProgress.Save();
        return results;
    }

    /// <summary>
    /// P11's standard importer preserves text on an existing achievement, so changing the trophy
    /// language otherwise leaves existing entries in their previously imported language. We only
    /// update those records after reading a new definition, and only replace textual metadata;
    /// unlock dates and all progress remain entirely Playnite-managed.
    /// </summary>
    private async Task UpdateTrophyMetadataAsync(
        string gameId,
        IReadOnlyCollection<ImportableAchievement> imported,
        IReadOnlyDictionary<string, PlayStationTrophy> metadataToRefresh,
        IReadOnlyDictionary<string, List<GameAchievement>> existingAchievementsByGame)
    {
        var typeNames = imported.Where(item => item.AchievementType is not null)
            .Select(item => item.AchievementType!).DistinctBy(type => type.Id)
            .ToDictionary(type => type.Id, type => type.Name, StringComparer.Ordinal);
        var trophyNames = imported.Where(item => item.TrophyType is not null)
            .Select(item => item.TrophyType!).DistinctBy(type => type.Id)
            .ToDictionary(type => type.Id, type => type.Name, StringComparer.Ordinal);
        var refreshed = imported.Where(item => metadataToRefresh.ContainsKey(item.AchievementId)).ToList();
        await SynchronizeLabelsAsync(
            playniteApi.Library.GameAchievementTypes, typeNames,
            refreshed.Where(item => item.AchievementType is not null)
                .Select(item => item.AchievementType!.Id).ToHashSet(StringComparer.Ordinal),
            (id, name) => new GameAchievementType { Id = id, Name = name });
        await SynchronizeLabelsAsync(
            playniteApi.Library.GameAchievementTrophies, trophyNames,
            refreshed.Where(item => item.TrophyType is not null)
                .Select(item => item.TrophyType!.Id).ToHashSet(StringComparer.Ordinal),
            (id, name) => new GameAchievementTrophy { Id = id, Name = name });

        if (!existingAchievementsByGame.TryGetValue(gameId, out var existingAchievements))
        {
            return;
        }

        var updates = new List<GameAchievement>();
        foreach (var existing in existingAchievements)
        {
            if (string.IsNullOrWhiteSpace(existing.AchievementId) ||
                !metadataToRefresh.TryGetValue(existing.AchievementId, out var trophy))
            {
                continue;
            }

            var update = existing.GetCopy();
            var changed = false;
            if (!string.IsNullOrWhiteSpace(trophy.Name) && !string.Equals(update.Name, trophy.Name, StringComparison.Ordinal))
            {
                update.Name = trophy.Name;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(trophy.Detail) && !string.Equals(update.Description, trophy.Detail, StringComparison.Ordinal))
            {
                update.Description = trophy.Detail;
                changed = true;
            }

            if (changed)
            {
                updates.Add(update);
            }
        }

        if (updates.Count > 0)
        {
            await playniteApi.Library.GameAchievements.UpdateAsync(updates);
        }
    }

    private static async Task SynchronizeLabelsAsync<T>(
        ILibraryCollection<T> collection,
        IReadOnlyDictionary<string, string> names,
        HashSet<string> refreshIds,
        Func<string, string, T> create) where T : LibraryObject
    {
        // Register each distinct descriptor before returning the import. Alpha2's importer does
        // not reliably deduplicate multiple new category/tier IDs in one payload. These collections
        // also let us rename the existing shared records without changing TypeId or TrophyId.
        var missing = names.Where(item => !collection.Contains(item.Key))
            .Select(item => create(item.Key, item.Value)).ToList();
        if (missing.Count > 0)
        {
            await collection.AddAsync(missing);
        }

        if (refreshIds.Count > 0)
        {
            await collection.UpdateAsync(refreshIds, item =>
            {
                var name = names[item.Id];
                if (string.Equals(item.Name, name, StringComparison.Ordinal))
                {
                    return false;
                }

                item.Name = name;
                return true;
            });
        }
    }

    private static async Task<Dictionary<string, PlayStationTrophyTitle>> GetCurrentTrophyTitlesAsync(
        PlayStationSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var titles = await session.GetTrophyTitlesAsync(cancellationToken);
            return titles
                .Where(title => !string.IsNullOrWhiteSpace(title.NpCommunicationId))
                .GroupBy(title => title.NpCommunicationId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // Without it every game is simply read in full, as before.
            logger.Error(e, "Failed to read PlayStation trophy progress. Every game will be refreshed.");
            return [];
        }
    }

    /// <summary>
    /// Maps each game to all of its trophy sets, preferring what is already known: the game id
    /// itself for titles imported from the trophy list, then the collection-aware cache, and only
    /// then a lookup.
    /// </summary>
    private async Task<Dictionary<string, TrophySetResolution>> ResolveTrophySetsAsync(
        PlayStationSession session,
        IReadOnlyCollection<Game> games,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, TrophySetResolution>(StringComparer.Ordinal);
        var unresolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var game in games)
        {
            var libraryGameId = game.LibraryGameId;
            if (string.IsNullOrWhiteSpace(libraryGameId))
            {
                continue;
            }

            if (libraryGameId.StartsWith(TrophySetIdPrefix, StringComparison.Ordinal))
            {
                // Imported from the trophy list, so the game id is already the trophy set id. The
                // importer records the service returned by Sony (notably "trophy2" for PS5) in
                // the public trophy-set identifier.
                var directIdentifiers = game.ExternalIdentifiers ?? [];
                var importedSet = directIdentifiers
                    .Where(identifier => string.Equals(identifier.TypeId, TrophySetIdType, StringComparison.Ordinal))
                    .Select(identifier => identifier.IdValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => ParseTrophySetIdentifier(value!))
                    .FirstOrDefault(trophySet => string.Equals(
                        trophySet.CommunicationId,
                        libraryGameId,
                        StringComparison.Ordinal));

                resolved[game.Id] = new TrophySetResolution(
                    [new PlayStationTrophySet(
                        libraryGameId,
                        importedSet?.ServiceName ?? DefaultTrophyServiceName)]);
                continue;
            }

            var identifiers = game.ExternalIdentifiers ?? [];
            var cachedSets = identifiers
                .Where(identifier => string.Equals(identifier.TypeId, TrophySetCacheIdType, StringComparison.Ordinal))
                .Select(identifier => identifier.IdValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => ParseCachedTrophySet(value!))
                .ToList();
            if (cachedSets.Count > 0)
            {
                resolved[game.Id] = new TrophySetResolution(
                    cachedSets.Distinct().ToList());
                continue;
            }

            unresolved[game.Id] = libraryGameId;
        }

        if (unresolved.Count > 0)
        {
            Dictionary<string, List<PlayStationTrophySet>> mappings;
            try
            {
                mappings = await session.GetTrophySetsAsync(unresolved.Values, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                // Games already resolved from the cache must still be refreshed, so a failed lookup
                // degrades to "resolve nothing new this time" rather than losing the whole import.
                logger.Error(e, "Failed to resolve PlayStation trophy sets.");
                mappings = [];
            }

            var unmapped = 0;
            foreach (var (playniteGameId, libraryGameId) in unresolved)
            {
                if (mappings.TryGetValue(libraryGameId, out var mappedSets))
                {
                    resolved[playniteGameId] = new TrophySetResolution(mappedSets);
                }
                else
                {
                    unmapped++;
                }
            }

            if (unmapped > 0)
            {
                // PlayStation resolves a game id to its trophy set only once the account has trophy
                // progress for it, so an owned but never played game cannot be resolved at all. It
                // starts working by itself the first time the game is played.
                logger.Info(
                    $"PlayStation returned no trophy set for {unmapped} of {unresolved.Count} games. " +
                    "That is expected for titles the account has never played.");
            }
        }

        await CacheTrophySetsAsync(games, resolved);
        return resolved;
    }

    /// <summary>Stores every resolved trophy set on the game so the lookup is not repeated.</summary>
    private async Task CacheTrophySetsAsync(IReadOnlyCollection<Game> games, Dictionary<string, TrophySetResolution> trophySets)
    {
        var toUpdate = new List<Game>();
        foreach (var game in games)
        {
            if (!trophySets.TryGetValue(game.Id, out var resolution) ||
                string.IsNullOrWhiteSpace(game.LibraryGameId) ||
                game.LibraryGameId.StartsWith(TrophySetIdPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var identifiers = game.ExternalIdentifiers ??= [];
            var existingCache = identifiers
                .Where(identifier => string.Equals(identifier.TypeId, TrophySetCacheIdType, StringComparison.Ordinal))
                .Select(identifier => identifier.IdValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => ParseCachedTrophySet(value!))
                .ToList();
            var resolvedSets = resolution.Sets.ToHashSet();
            if (existingCache.ToHashSet().SetEquals(resolvedSets))
            {
                continue;
            }

            foreach (var identifier in identifiers
                         .Where(identifier => string.Equals(identifier.TypeId, TrophySetCacheIdType, StringComparison.Ordinal))
                         .ToList())
            {
                identifiers.Remove(identifier);
            }

            foreach (var trophySet in resolution.Sets)
            {
                identifiers.Add(new ExternalIdentifier(
                    TrophySetCacheIdType,
                    FormatCachedTrophySet(trophySet)));
            }

            toUpdate.Add(game);
        }

        if (toUpdate.Count > 0)
        {
            try
            {
                await EnsureIdentifierTypeExistsAsync();
                await playniteApi.Library.Games.UpdateAsync(toUpdate);
            }
            catch (Exception e)
            {
                // Losing the cache only costs a repeated lookup next time.
                logger.Error(e, "Failed to cache PlayStation trophy set ids.");
            }
        }
    }

    /// <summary>
    /// Identifier types are a shared collection; without an entry here the value shows up with a
    /// blank type in the game's Advanced tab.
    /// </summary>
    private async Task EnsureIdentifierTypeExistsAsync()
    {
        if (!playniteApi.Library.ExternalIdentifierTypes.Contains(TrophySetCacheIdType))
        {
            await playniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType(TrophySetCacheIdType, TrophySetCacheIdName));
        }
    }

    /// <summary>Formats a trophy-set identifier stored in the external-identifier cache.</summary>
    internal static string FormatTrophySetIdentifier(string communicationId, string serviceName) =>
        communicationId + ":" + serviceName;

    /// <summary>
    /// Stores the friendly set name with the private cache entry. Collection members can be absent
    /// from Sony's trophy-title list when they have never been played, so keeping the name here
    /// prevents their category from degrading to an NPWR identifier on a later sync.
    /// </summary>
    private static string FormatCachedTrophySet(PlayStationTrophySet trophySet)
    {
        var identifier = FormatTrophySetIdentifier(trophySet.CommunicationId, trophySet.ServiceName);
        return string.IsNullOrWhiteSpace(trophySet.Name)
            ? identifier
            : identifier + "|" + Uri.EscapeDataString(trophySet.Name);
    }

    private static PlayStationTrophySet ParseTrophySetIdentifier(string value)
    {
        var separator = value.LastIndexOf(':');
        return separator > 0
            ? new PlayStationTrophySet(value[..separator], value[(separator + 1)..])
            : new PlayStationTrophySet(value, DefaultTrophyServiceName);
    }

    private static PlayStationTrophySet ParseCachedTrophySet(string value)
    {
        var nameSeparator = value.IndexOf('|');
        var identifier = nameSeparator < 0 ? value : value[..nameSeparator];
        var trophySet = ParseTrophySetIdentifier(identifier);
        if (nameSeparator < 0)
        {
            return trophySet;
        }

        var name = Uri.UnescapeDataString(value[(nameSeparator + 1)..]);
        return string.IsNullOrWhiteSpace(name) ? trophySet : trophySet with { Name = name };
    }

    /// <summary>
    /// Only worth a request when the game actually has more than the base group; most do not.
    /// </summary>
    private static async Task<Dictionary<string, string>> GetGroupNamesAsync(
        PlayStationSession session,
        PlayStationTrophySet trophySet,
        List<PlayStationTrophy> trophies,
        CancellationToken cancellationToken)
    {
        var hasAdditionalGroups = trophies.Any(trophy =>
            !string.IsNullOrWhiteSpace(trophy.GroupId) &&
            !string.Equals(trophy.GroupId, "default", StringComparison.OrdinalIgnoreCase));
        if (!hasAdditionalGroups)
        {
            return [];
        }

        // A failed request must not replace existing localized DLC names with fallback labels.
        // Let the game's refresh fail so its metadata and cache can be retried together.
        return await session.GetTrophyGroupNamesAsync(trophySet, cancellationToken);
    }

    private static ImportableAchievement ToAchievement(
        PlayStationTrophy trophy,
        Dictionary<string, string> groupNames,
        PlayStationTrophySet trophySet,
        string trophySetName,
        bool isCollection,
        PlayStationTrophyLabels labels)
    {
        return new ImportableAchievement(
            FormatAchievementId(trophySet, trophy.Id),
            trophy.Name ?? trophy.Id.ToString())
        {
            Description = trophy.Detail,
            UnlockedIcon = trophy.IconUrl,
            LockedIcon = trophy.IconUrl,
            UnlockedDate = trophy.Earned ? trophy.EarnedDate : null,
            Hidden = trophy.Hidden,
            // Sony reports the share of players who earned it, which is already a percentage.
            Rarity = trophy.EarnedRate ?? 0,
            TrophyType = GetTrophyType(trophy.Type, labels),
            AchievementType = GetAchievementGroup(
                trophy.GroupId,
                groupNames,
                trophySet.CommunicationId,
                trophySetName,
                isCollection,
                labels)
        };
    }

    /// <summary>
    /// Trophy ids are only unique within one communication id. Qualifying them lets every set in
    /// a collection be imported into Playnite's one achievement list for that game.
    /// </summary>
    private static string FormatAchievementId(PlayStationTrophySet trophySet, int trophyId)
    {
        return trophySet.CommunicationId + ":" + trophyId;
    }

    /// <summary>
    /// Separates base-game trophies from additional content. Collections are flattened into
    /// "trophy set — category", because Playnite achievement types do not support nesting.
    /// </summary>
    private static ImportableGameAchievementType? GetAchievementGroup(
        string? groupId,
        Dictionary<string, string> groupNames,
        string communicationId,
        string trophySetName,
        bool isCollection,
        PlayStationTrophyLabels labels)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        if (string.Equals(groupId, "default", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportableGameAchievementType(
                isCollection ? $"psn_base_{communicationId}" : "psn_base",
                FormatAchievementGroupName(trophySetName, labels.BaseGame, isCollection));
        }

        // Prefer the pack's real name over a generic label when it could be read.
        var categoryName = groupNames.TryGetValue(groupId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : labels.AdditionalContent;
        return new ImportableGameAchievementType(
            $"psn_dlc_{communicationId}_{groupId}",
            FormatAchievementGroupName(trophySetName, categoryName, isCollection));
    }

    private static string FormatAchievementGroupName(string trophySetName, string categoryName, bool isCollection)
    {
        return isCollection ? trophySetName + " — " + categoryName : categoryName;
    }

    private static ImportableGameAchievementTrophy? GetTrophyType(string? type, PlayStationTrophyLabels labels)
    {
        return type?.ToLowerInvariant() switch
        {
            "bronze" => new ImportableGameAchievementTrophy("psn_bronze", labels.Bronze, null),
            "silver" => new ImportableGameAchievementTrophy("psn_silver", labels.Silver, null),
            "gold" => new ImportableGameAchievementTrophy("psn_gold", labels.Gold, null),
            "platinum" => new ImportableGameAchievementTrophy("psn_platinum", labels.Platinum, null),
            _ => null
        };
    }
}
