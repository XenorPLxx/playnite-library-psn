using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace PSNLibrary
{
  public class PSNLibrary : LibraryPlugin
  {
    // Playnite API access
    private readonly INotificationsAPI notifications;
    private static readonly ILogger logger = LogManager.GetLogger();

    // Settings access https://api.playnite.link/docs/master/tutorials/extensions/pluginSettings.html
    public PSNLibrarySettingsViewModel SettingsViewModel { get; set; }

    // LibraryPlugin properties https://api.playnite.link/docs/master/api/Playnite.SDK.Plugins.LibraryPlugin.html#properties
    public override Guid Id { get; } = Guid.Parse("e4ac81cb-1b1a-4ec9-8639-9a9633989a71");
    public override LibraryClient Client => null;
    public override string LibraryBackground => null;
    public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
    public override string Name => "PlayStation";

    // Refactorable
    public string ImportErrorMessageId { get; }

    // LibraryPlugin constructor 
    public PSNLibrary(IPlayniteAPI api) : base(api)
    {
      notifications = api.Notifications;
      SettingsViewModel = new PSNLibrarySettingsViewModel(this);
      Properties = new LibraryPluginProperties
      {
        CanShutdownClient = false,
        HasCustomizedGameImport = true,
        HasSettings = true
      };
    }

    // Refactorable
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
        var clientApi = new Services.PSNClient(this);
        var allGames = new List<GameMetadata>();
        allGames.AddRange(Services.GetGames.ParseAccountList(clientApi)); // AccountList has the best game names
        allGames.AddRange(Services.GetGames.ParsePlayedMobileList(clientApi));
        allGames.AddRange(Services.GetGames.ParsePlayedList(clientApi));

        // Migration is based on API that accepts titleId, that's why ParseThrophies is excluded
        if (SettingsViewModel.Settings.Migration)
        {
          Services.MigrateGames.call(this, allGames);
        }

        allGames.AddRange(Services.GetGames.ParseThrophies(this, clientApi));

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
          }
          else
          {
            bool changed = false;
            if (SettingsViewModel.Settings.LastPlayed)
            {
              var newLastActivity = group.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
              if (newLastActivity != null && (alreadyImported.LastActivity == null || newLastActivity.Value.ToUniversalTime() != alreadyImported.LastActivity.Value.ToUniversalTime()))
              {
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
        logger.Error(e, "Failed to import PSN games.");
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

    public override ISettings GetSettings(bool firstRunSettings)
    {
      return SettingsViewModel;
    }

    public override UserControl GetSettingsView(bool firstRunSettings)
    {
      return new PSNLibrarySettingsView();
    }
  }
}