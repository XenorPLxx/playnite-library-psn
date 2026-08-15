using Playnite;

namespace PlayStationLibrary;

public sealed class PlayStationLibraryPlugin : Plugin
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private static readonly IdImportableProperty playStationSource = new("playstation", "PlayStation");
    private static readonly NameImportableProperty playStationPlusSource = new("PlayStation Plus");
    private static readonly NameImportableProperty playStationPlusTag = new("PlayStation Plus");

    private const string PlusMembership = "PS_PLUS";

    /// <summary>Platforms only the trophy list reports, each behind its own setting.</summary>
    private static readonly string[] LegacyPlatformTokens = ["PS3", "PSP", "PSVITA"];
    private const string ImportErrorNotification = "playstation_import_error";
    private const string AchievementsErrorNotification = "playstation_achievements_error";

    public const string Id = "Xenor.PlayStationLibrary";

    public IPlayniteApi PlayniteApi { get; private set; } = null!;

    public PlayStationLibraryPluginSettings Settings { get; private set; } = new();

    public PlayStationLibraryPlugin()
    {
        LibrarySettings = new LibrarySupport
        {
            LibraryName = "PlayStation",
            CanCloseOriginalClient = false,
            CanOpenOriginalClient = false,
            CanImportPlaytime = true,
            // PlayStation reports an aggregate play time rather than individual sessions, so the
            // sessions below are derived from the change between imports.
            CanImportPlaySessions = true
        };

        AchievementsSettings = new AchievementsSupport { SupportedLibraries = [Id] };
    }

    public override async Task<List<ImportableAchievements>> GetAchievementsAsync(GetAchievementsArgs args)
    {
        var games = args.Games.Where(game => game.LibraryId == Id).ToList();
        if (!Settings.ConnectAccount || games.Count == 0)
        {
            return [];
        }

        // Cache trophy text by the complete Accept-Language preference list. Changing the
        // fallback behaviour must refresh existing trophies just like choosing a new language.
        var achievementLocale = PlayStationLocales.ToTrophyAcceptLanguage(Settings.Locale, PlayniteApi.Settings.Language);
        try
        {
            using var session = await new PlayStationAccountClient(PlayniteApi).CreateSessionAsync(Settings.Npsso, args.CancelToken, Settings.Locale);
            var achievements = await new PlayStationAchievements(PlayniteApi).GetAchievementsAsync(session, games, achievementLocale, args.CancelToken);
            PlayniteApi.Notifications.Remove(AchievementsErrorNotification);
            return achievements;
        }
        catch (OperationCanceledException) when (args.CancelToken.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception e)
        {
            // An expired NPSSO must not escape the plugin; the import path reports the same way.
            logger.Error(e, "Failed to import PlayStation achievements.");
            PlayniteApi.Notifications.Add(new NotificationMessage(
                AchievementsErrorNotification,
                Loc.playstation_achievements_error() + Environment.NewLine + e.Message,
                NotificationSeverity.Error,
                async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(Id)));
            return [];
        }
    }

    public override Task InitializeAsync(InitializeArgs args)
    {
        PlayniteApi = args.Api;
        Loc.Api = args.Api;
        Settings = PlayStationLibrarySettingsHandler.LoadSettings(PlayniteApi.UserDataDir);
        return Task.CompletedTask;
    }

    public override async Task<List<ImportableGame>> GetGamesAsync(LibraryGetGamesArgs args)
    {
        if (!Settings.ConnectAccount || args.CancelToken.IsCancellationRequested)
        {
            return [];
        }

        try
        {
            using var session = await new PlayStationAccountClient(PlayniteApi).CreateSessionAsync(Settings.Npsso, args.CancelToken, Settings.Locale);
            var titles = new List<PlayStationImportedTitle>();

            // Each API is queried independently so that one of them failing degrades the import
            // instead of losing the whole library. Order matters: it decides which source wins for
            // fields present in more than one, and the purchased list has the best game names.
            titles.AddRange(await LoadAsync("1", "purchased games", args.CancelToken, LoadPurchasedTitlesAsync));
            titles.AddRange(await LoadAsync("3", "play history", args.CancelToken, LoadMobilePlayedTitlesAsync));
            titles.AddRange(await LoadAsync("2", "recently played games", args.CancelToken, LoadPlayedTitlesAsync));
            titles.AddRange(await LoadAsync("4", "trophy list", args.CancelToken, LoadTrophyTitlesAsync));

            var games = Merge(titles, args.CancelToken);
            PlayniteApi.Notifications.Remove(ImportErrorNotification);
            return games;

            async Task<List<PlayStationImportedTitle>> LoadPurchasedTitlesAsync(CancellationToken token) =>
                ParsePurchasedTitles(await session.GetPurchasedTitlesAsync(token));
            async Task<List<PlayStationImportedTitle>> LoadMobilePlayedTitlesAsync(CancellationToken token) =>
                ParseMobilePlayedTitles(await session.GetMobilePlayedTitlesAsync(token));
            async Task<List<PlayStationImportedTitle>> LoadPlayedTitlesAsync(CancellationToken token) =>
                ParsePlayedTitles(await session.GetPlayedTitlesAsync(token));
            async Task<List<PlayStationImportedTitle>> LoadTrophyTitlesAsync(CancellationToken token) =>
                ParseTrophyTitles(await session.GetTrophyTitlesAsync(token));
        }
        catch (OperationCanceledException) when (args.CancelToken.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to import PlayStation games.");
            PlayniteApi.Notifications.Add(new NotificationMessage(
                ImportErrorNotification,
                Loc.library_import_error("PlayStation") + Environment.NewLine + e.Message,
                NotificationSeverity.Error,
                async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(Id)));
            return [];
        }
    }

    public override Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
    {
        return Task.FromResult<PluginSettingsHandler?>(new PlayStationLibrarySettingsHandler(this, args));
    }

    internal bool SaveSettings(PlayStationLibraryPluginSettings settings)
    {
        Settings = settings;
        return PlayStationLibrarySettingsHandler.SaveSettings(PlayniteApi.UserDataDir, settings);
    }

    /// <summary>
    /// Runs one of the four sources, downgrading a failure to a notification so the remaining
    /// sources still contribute.
    /// </summary>
    private async Task<List<PlayStationImportedTitle>> LoadAsync(
        string apiNumber,
        string description,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<List<PlayStationImportedTitle>>> load)
    {
        var notificationId = "playstation_api_" + apiNumber;
        try
        {
            var titles = await load(cancellationToken);
            PlayniteApi.Notifications.Remove(notificationId);
            return titles;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.Error(e, $"Failed to load the PlayStation {description} (API {apiNumber} of 4).");
            PlayniteApi.Notifications.Add(new NotificationMessage(
                notificationId,
                $"PlayStation: could not load the {description} (API {apiNumber} of 4)." + Environment.NewLine + e.Message,
                NotificationSeverity.Error,
                async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(Id)));
            return [];
        }
    }

    private List<PlayStationImportedTitle> ParsePurchasedTitles(List<PlayStationPurchasedTitle> titles)
    {
        var parsed = new List<PlayStationImportedTitle>();
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.TitleId))
            {
                continue;
            }

            parsed.Add(new PlayStationImportedTitle
            {
                GameId = title.TitleId,
                Name = PlayStationGameName.Normalize(title.Name),
                Platforms = GetPlatforms(title.Platform),
                IsPlusEntitlement = string.Equals(title.Membership, PlusMembership, StringComparison.OrdinalIgnoreCase)
            });
        }

        return parsed;
    }

    private List<PlayStationImportedTitle> ParseMobilePlayedTitles(List<PlayStationPlayedTitle> titles)
    {
        var parsed = new List<PlayStationImportedTitle>();
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.TitleId))
            {
                continue;
            }

            parsed.Add(new PlayStationImportedTitle
            {
                GameId = title.TitleId,
                Name = PlayStationGameName.Normalize(title.Name),
                Platforms = GetPlatformsFromCategory(title.Category),
                LastPlayed = title.LastPlayed,
                PlayTime = title.PlayTime
            });
        }

        return parsed;
    }

    private List<PlayStationImportedTitle> ParsePlayedTitles(List<PlayStationPlayedTitle> titles)
    {
        var parsed = new List<PlayStationImportedTitle>();
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.TitleId))
            {
                continue;
            }

            parsed.Add(new PlayStationImportedTitle
            {
                GameId = title.TitleId,
                Name = PlayStationGameName.Normalize(title.Name),
                Platforms = GetPlatforms(title.Platform),
                LastPlayed = title.LastPlayed
            });
        }

        return parsed;
    }

    private List<PlayStationImportedTitle> ParseTrophyTitles(List<PlayStationTrophyTitle> titles)
    {
        var parsed = new List<PlayStationImportedTitle>();
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.NpCommunicationId))
            {
                continue;
            }

            // The trophy list reports a comma-separated set, e.g. "PS3,PSVITA,PS4". Only the
            // platforms that no other API covers are taken from here, and a title is imported only
            // if at least one of them is enabled — otherwise a PS4/PS5 entry would be duplicated
            // under its unrelated npCommunicationId. PSPC is deliberately absent: PS5 and PC share
            // one trophy set, so honouring it registered a PC copy of every PS5 game.
            var tokens = (title.Platform ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var platforms = new List<ImportableProperty>();
            foreach (var token in tokens)
            {
                var platform = token.ToUpperInvariant() switch
                {
                    "PS3" when Settings.ImportPs3 => "sony_playstation3",
                    "PSP" when Settings.ImportPsp => "sony_psp",
                    "PSVITA" when Settings.ImportPsVita => "sony_vita",
                    _ => null
                };
                if (platform != null)
                {
                    platforms.Add(new SpecImportableProperty(platform));
                }
            }

            // A title is legacy if it names a legacy platform at all, regardless of whether that
            // platform is enabled. Otherwise disabling PlayStation 3 would not exclude PlayStation 3
            // games, it would quietly reclassify them as trophy-only ones.
            var isLegacy = tokens.Any(token => LegacyPlatformTokens.Contains(token, StringComparer.OrdinalIgnoreCase));
            if (isLegacy)
            {
                if (platforms.Count == 0)
                {
                    continue;
                }
            }
            else
            {
                if (!Settings.ImportTrophyOnlyGames)
                {
                    continue;
                }

                // Modern platforms are normally covered by the other APIs; these entries only earn
                // their place if nothing else reported the game, which Merge decides by name. An
                // entry naming neither PS4 nor PS5 (a PSPC-only set, say) has no platform we would
                // import, so it is skipped rather than added with none.
                platforms = GetPlatforms(tokens.FirstOrDefault(token =>
                    token.Equals("PS5", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("PS4", StringComparison.OrdinalIgnoreCase)));
                if (platforms.Count == 0)
                {
                    continue;
                }
            }

            parsed.Add(new PlayStationImportedTitle
            {
                GameId = title.NpCommunicationId,
                Name = PlayStationGameName.NormalizeTrophyTitle(title.Name),
                Platforms = platforms,
                LastPlayed = title.LastPlayed,
                TrophyServiceName = title.ServiceName,
                IsTrophyOnlyCandidate = !isLegacy
            });
        }

        return parsed;
    }

    private List<ImportableGame> Merge(List<PlayStationImportedTitle> titles, CancellationToken cancellationToken)
    {
        var games = new List<ImportableGame>();
        var playTimeHistory = new PlayStationPlayTimeHistory(PlayniteApi.UserDataDir);

        // There is no endpoint mapping a trophy set back to a title id, so a trophy-only entry can
        // only be recognised as an already-known game by its name.
        var knownNames = titles
            .Where(title => !title.IsTrophyOnlyCandidate && !string.IsNullOrWhiteSpace(title.Name))
            .Select(title => title.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in titles.GroupBy(title => title.GameId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = group.ToList();

            // The first entry wins for identity, because sources were queried best-name-first.
            var name = entries.Select(entry => entry.Name).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (entries.All(entry => entry.IsTrophyOnlyCandidate) && knownNames.Contains(name))
            {
                continue;
            }

            var platforms = entries.Select(entry => entry.Platforms).FirstOrDefault(value => value.Count > 0) ?? [];
            // Sony reports exactly one entitlement per title, so a PlayStation Plus title is never
            // also a separate purchase.
            var isPlus = entries.Any(entry => entry.IsPlusEntitlement);

            var game = new ImportableGame(name, Id, group.Key)
            {
                Source = Settings.UsePlusSource && isPlus ? playStationPlusSource : playStationSource,
                Platforms = platforms,
                LastPlayedDate = entries.Select(entry => entry.LastPlayed).FirstOrDefault(value => value != null),
                PlayTime = entries.Select(entry => entry.PlayTime).FirstOrDefault(value => value != 0)
            };

            if (Settings.ImportPlusTag && isPlus)
            {
                game.Tags = [playStationPlusTag];
            }

            // Games discovered through the trophy list are already keyed by their trophy set, so the
            // achievement lookup can be skipped entirely for them later on.
            if (group.Key.StartsWith("NPWR", StringComparison.Ordinal))
            {
                // The service name has to come from the API: PlayStation 5 sets are served by
                // trophy2 and asking for the wrong one returns "Resource not found".
                var serviceName = entries
                    .Select(entry => entry.TrophyServiceName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? PlayStationAchievements.DefaultTrophyServiceName;
                game.ExternalIdentifiers =
                [
                    new ImportableExternalIdentifier(
                        PlayStationAchievements.TrophySetIdType,
                        PlayStationAchievements.TrophySetIdName,
                        PlayStationAchievements.FormatTrophySetIdentifier(group.Key, serviceName))
                ];
            }

            if (game.PlayTime > 0)
            {
                var session = playTimeHistory.GetSessionSincePreviousImport(group.Key, game.PlayTime, game.LastPlayedDate);
                if (session != null)
                {
                    game.Sessions = [session];
                }

                playTimeHistory.Record(group.Key, game.PlayTime);
            }

            games.Add(game);
        }

        playTimeHistory.Save();
        return games;
    }

    private static List<ImportableProperty> GetPlatforms(string? platform)
    {
        return platform?.ToUpperInvariant() switch
        {
            "PSP" => [new SpecImportableProperty("sony_psp")],
            "PSVITA" => [new SpecImportableProperty("sony_vita")],
            "PS3" => [new SpecImportableProperty("sony_playstation3")],
            "PS4" => [new SpecImportableProperty("sony_playstation4")],
            "PS5" => [new SpecImportableProperty("sony_playstation5")],
            _ => []
        };
    }

    private static List<ImportableProperty> GetPlatformsFromCategory(string? category)
    {
        return category switch
        {
            "ps4_game" => [new SpecImportableProperty("sony_playstation4")],
            "ps5_native_game" => [new SpecImportableProperty("sony_playstation5")],
            _ => []
        };
    }

    private sealed class PlayStationImportedTitle
    {
        public required string GameId { get; init; }

        /// <summary>The trophy service a trophy-list entry belongs to: "trophy" or "trophy2".</summary>
        public string? TrophyServiceName { get; init; }
        public string? Name { get; init; }
        public List<ImportableProperty> Platforms { get; init; } = [];
        public DateTimeOffset? LastPlayed { get; init; }
        public uint PlayTime { get; init; }
        public bool IsPlusEntitlement { get; init; }

        /// <summary>A trophy entry for a modern platform, only imported if nothing else reported it.</summary>
        public bool IsTrophyOnlyCandidate { get; init; }
    }
}
