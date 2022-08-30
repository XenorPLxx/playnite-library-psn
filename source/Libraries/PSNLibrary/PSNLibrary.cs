using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PSNLibrary.Models;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PSNLibrary
{
    [LoadPlugin]
    public class PSNLibrary : LibraryPluginBase<PSNLibrarySettingsViewModel>
    {
        public PSNLibrary(IPlayniteAPI api) : base(
            "PlayStation",
            Guid.Parse("e4ac81cb-1b1a-4ec9-8639-9a9633989a71"),
            new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true, HasSettings = true },
            null,
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png"),
            (_) => new PSNLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new PSNLibrarySettingsViewModel(this, api);
        }

        private string ParsePlatform(string platformId)
        {
            string platform = null;

            switch (platformId)
            {
                case "PSP":
                    platform = "sony_psp";
                    break;

                case "PSVITA":
                    platform = "sony_vita";
                    break;

                case "PS3":
                    platform = "sony_playstation3";
                    break;

                case "PS4":
                    platform = "sony_playstation4";
                    break;

                case "PS5":
                    platform = "sony_playstation5";
                    break;

                default:
                    break;
            }
            return platform;
        }

        private string ParseCategory(string category)
        {
            string platform = null;

            switch (category)
            {
                case "ps4_game":
                    platform = "sony_playstation4";
                    break;

                case "ps5_native_game":
                    platform = "sony_playstation5";
                    break;

                default:
                    break;
            }
            return platform;
        }

        private string FixGameName(string name)
        {
            var gameName = name.
                RemoveTrademarks(" ").
                NormalizeGameName().
                Replace("full game", "", StringComparison.OrdinalIgnoreCase).
                Trim();
            return Regex.Replace(gameName, @"\s+", " ");
        }

        private List<GameMetadata> parseGames(List<AccountTitlesResponseData.AccountTitlesRetrieve.Title> gamesToParse)
        {
            var parsedGames = new List<GameMetadata>();
            foreach (var title in gamesToParse)
            {
                var gameName = FixGameName(title.name);

                string platform = ParsePlatform(title.platform);

                parsedGames.Add(new GameMetadata
                {
                    GameId = title.titleId,
                    Name = gameName,
                    CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.image.url) : null,
                    Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) }
                });
            }

            return parsedGames;
        }

        // TODO: Figure out smarter way to share code without overloading
        private List<GameMetadata> parseGames(List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title> gamesToParse)
        {
            var parsedGames = new List<GameMetadata>();
            foreach (var title in gamesToParse)
            {
                var gameName = FixGameName(title.name);

                string platform = ParsePlatform(title.platform);

                parsedGames.Add(new GameMetadata
                {
                    GameId = title.titleId,
                    Name = gameName,
                    CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.image.url) : null,
                    Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) },
                    LastActivity = title.lastPlayedDateTime
                });
            }

            return parsedGames;
        }

        // TODO: Figure out smarter way to share code without overloading
        private List<GameMetadata> parseGames(List<PlayedTitlesMobile.PlayedTitleMobile> gamesToParse)
        {
            var parsedGames = new List<GameMetadata>();        
            foreach (var title in gamesToParse)
            {
                var gameName = FixGameName(title.name);

                string platform = ParseCategory(title.category);

                ulong playtime = 0;

                foreach (Group group in Regex.Match(title.playDuration, "^PT(\\d+[A-Z])+$").Groups)
                {
                    foreach (Capture capture in group.Captures)
                    {
                        string type = capture.Value.Substring(capture.Value.Length - 1, 1);
                        if (int.TryParse(capture.Value.Substring(0, capture.Value.Length - 1), out int number))
                        {
                            switch (type)
                            {
                                case "S":
                                    playtime = playtime + (ulong)number;
                                    break;

                                case "M":
                                    playtime = playtime + ((ulong)number * 60);
                                    break;

                                case "H":
                                    playtime = playtime + ((ulong)number * 60 * 60);
                                    break;

                                default:
                                    break;
                            }
                        }
                    }

                }

                parsedGames.Add(new GameMetadata
                {
                    GameId = title.titleId,
                    Name = gameName,
                    CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.imageUrl) : null,
                    Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) },
                    Playtime = playtime,
                    LastActivity = title.lastPlayedDateTime,
                    PlayCount = title.playCount
                });
            }
            

            return parsedGames;
        }

        private List<GameMetadata> ParseAccountList(PSNAccountClient clientApi)
        {
            var gamesToParse = clientApi.GetAccountTitles().GetAwaiter().GetResult();
            return parseGames(gamesToParse);
        }
        
        private List<GameMetadata> ParsePlayedList(PSNAccountClient clientApi)
        {
            var gamesToParse = clientApi.GetPlayedTitles().GetAwaiter().GetResult();            
            return parseGames(gamesToParse);
        }

        private List<GameMetadata> ParsePlayedMobileList(PSNAccountClient clientApi)
        {
            var gamesToParse = clientApi.GetPlayedTitlesMobile().GetAwaiter().GetResult();
            return parseGames(gamesToParse);
        }

        private List<GameMetadata> ParseThrophies(PSNAccountClient clientApi)
        {
            var parsedGames = new List<GameMetadata>();
            foreach (var title in clientApi.GetTrohpiesMobile().GetAwaiter().GetResult())
            {
                var gameName = FixGameName(title.trophyTitleName);
                gameName = gameName.
                    TrimEndString("Trophies", StringComparison.OrdinalIgnoreCase).
                    TrimEndString("Trophy", StringComparison.OrdinalIgnoreCase).
                    Trim();

                var newGame = new GameMetadata
                {
                    GameId = title.npCommunicationId,
                    Name = gameName
                };

                var legacyGames = false;

                newGame.Platforms = new HashSet<MetadataProperty> { };

                if (title.trophyTitlePlatform?.Contains("PSP") == true && SettingsViewModel.Settings.PSP)
                {
                    newGame.Platforms.Add(new MetadataSpecProperty("sony_psp"));
                    legacyGames = true;
                }
                else if (title.trophyTitlePlatform?.Contains("PSVITA") == true && SettingsViewModel.Settings.PSVITA)
                {
                    newGame.Platforms.Add(new MetadataSpecProperty("sony_vita"));
                    legacyGames = true;
                }
                else if (title.trophyTitlePlatform?.Contains("PS3") == true && SettingsViewModel.Settings.PS3)
                {
                    newGame.Platforms.Add(new MetadataSpecProperty("sony_playstation3"));
                    legacyGames = true;
                }

                // PS4 and PS5 games are added based on different APIs, but for PS3/VITA/PSP games only trophies API is available.
                if (legacyGames)
                {
                    parsedGames.Add(newGame);
                }
            }

            return parsedGames;
        }

        private void MigrateGames(PSNAccountClient clientApi, List<GameMetadata> games)
        {
            SettingsViewModel.BeginEdit();
            SettingsViewModel.Settings.Migration = false;
            SettingsViewModel.EndEdit();

            var pluginGames = PlayniteApi.Database.Games.Where(x => x.PluginId == Id);

            if (pluginGames.Count() > 0)
            {
                string[] titleIdsArray = games.GroupBy(x => x.GameId).Select(x => x.FirstOrDefault()).Select(x => x.GameId).ToArray();

                var gamesWithIds = clientApi.GetTrohpiesWithIdsMobile(titleIdsArray).GetAwaiter().GetResult();

                foreach (var game in pluginGames)
                {
                    if (game.GameId.Contains("#ACCOUNT#"))
                    {
                        game.GameId = game.GameId.Substring(9);
                        PlayniteApi.Database.Games.Update(game);
                    }
                    else if (game.GameId.Contains("#TROPHY#"))
                    {
                        var communicationId = game.GameId.Substring(8);
                        string npTitleId = gamesWithIds.FirstOrDefault(p => p.trophyTitles.Any(c => c.npCommunicationId == communicationId))?.npTitleId ?? null;
                        game.GameId = npTitleId != null ? npTitleId : communicationId;
                        PlayniteApi.Database.Games.Update(game);
                    }
                }
            }
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            var importedGames = new List<Game>();

            Exception importError = null;
            if (!SettingsViewModel.Settings.ConnectAccount)
            {
                return importedGames;
            }

            try
            {
                var clientApi = new PSNAccountClient(this, PlayniteApi);
                var allGames = new List<GameMetadata>();
                allGames.AddRange(ParseAccountList(clientApi)); // AccountList has the best game names
                allGames.AddRange(ParsePlayedMobileList(clientApi));
                allGames.AddRange(ParsePlayedList(clientApi));

                // Migration is based on API that accepts titleId, that's why ParseThrophies is excluded
                if (SettingsViewModel.Settings.Migration)
                {
                    MigrateGames(clientApi, allGames);
                }
                
                allGames.AddRange(ParseThrophies(clientApi));

                // This need to happen to merge games from different APIs
                foreach (var group in allGames.GroupBy(a => a.GameId))
                {
                    var game = group.First();
                    if (PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(game.GameId, Id))
                    {
                        continue;
                    }

                    var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.GameId == game.GameId && a.PluginId == Id);
                    if (alreadyImported == null)
                    {
                        game.Source = new MetadataNameProperty("PlayStation");
                        importedGames.Add(PlayniteApi.Database.ImportGame(game, this));
                    } else
                    {
                        bool changed = false;
                        if (SettingsViewModel.Settings.LastPlayed)
                        {
                            var newLastActivity = group.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
                            if (newLastActivity != null && (alreadyImported.LastActivity == null || newLastActivity.Value.ToUniversalTime() != alreadyImported.LastActivity.Value.ToUniversalTime())) {
                                alreadyImported.LastActivity = newLastActivity;
                                changed = true;
                            }
                        }
                        if (SettingsViewModel.Settings.Playtime)
                        {
                            var newPlaytime = group.FirstOrDefault(a => a.LastActivity != null)?.Playtime ?? alreadyImported.Playtime;
                            if (newPlaytime != alreadyImported.Playtime)
                            {
                                alreadyImported.Playtime = newPlaytime;
                                changed = true;
                            }

                            var newPlayCount = group.FirstOrDefault(a => a.LastActivity != null)?.PlayCount ?? alreadyImported.PlayCount;
                            if (newPlayCount != alreadyImported.PlayCount)
                            {
                                alreadyImported.PlayCount = newPlayCount;
                                changed = true;
                            }
                        }
                        if ((SettingsViewModel.Settings.LastPlayed || SettingsViewModel.Settings.Playtime) && changed)
                        { 
                            PlayniteApi.Database.Games.Update(alreadyImported);
                        }
                    }
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to import PSN games.");
                importError = e;
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return importedGames;
        }
    }
}