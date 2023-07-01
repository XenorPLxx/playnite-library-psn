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
    // Playnite logger access
    public static readonly ILogger logger = LogManager.GetLogger();

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
      var newlyImportedGames = new List<Game>();

      Exception importError = null;
      if (!SettingsViewModel.Settings.ConnectAccount)
      {
        // Refactorable: notification about accuont not connected
        return newlyImportedGames;
      }

      try
      {
        // PSNClient gets reused and needs access to settings, so this looks to be the best place to put it
        var psnClient = new Services.PSNClient(this);
        var gamesFromApi = new List<GameMetadata>();

        // Check for authentication
        if (Services.CheckAuthentication.call(this, psnClient))
        { 
          // Start loading games from different APIs
          gamesFromApi.AddRange(Services.GetGames.LoadAccountGameList(this, psnClient)); // AccountList has the best game names
          gamesFromApi.AddRange(Services.GetGames.LoadMobilePlayedGameList(this, psnClient));
          gamesFromApi.AddRange(Services.GetGames.LoadPlayedGameList(this, psnClient));

          // Migration is based on API that accepts titleId, which trophy list API does not support
          if (SettingsViewModel.Settings.Migration) { Services.MigrateGames.call(this, gamesFromApi); }

          // Load games for legacy platforms usign trophy list
          gamesFromApi.AddRange(Services.GetGames.LoadTrophyList(this, psnClient));

          // Merge games from different APIs prioritizing according to order above and import all new games and changed games to Playnite
          newlyImportedGames = Services.ImportGames.call(this, gamesFromApi);
        }
      }
      catch (Exception e) when (!Debugger.IsAttached)
      {
        // notifications
        logger.Error(e, "Failed to import PSN games.");
        importError = e;
      }

      if (importError != null)
      {
        PlayniteApi.Notifications.Add(new NotificationMessage(
            ImportErrorMessageId,
            string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
            Environment.NewLine + importError.Message,
            NotificationType.Error,
            () => OpenSettingsView()));
      }
      else
      {
        PlayniteApi.Notifications.Remove(ImportErrorMessageId);
      }

      return newlyImportedGames;
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