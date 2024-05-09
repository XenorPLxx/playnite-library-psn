using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PSNLibrary.Models;

namespace PSNLibrary.Services
{
  internal class GetGames
  {
    public static List<GameMetadata> LoadAccountGameList(PSNLibrary psnLibrary, PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetAccountTitles().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse, psnLibrary);
      }
      catch (Exception e)
      {
        PSNLibrary.logger.Error(e, "PSN_LoadAccountGameList");
        psnLibrary.PlayniteApi.Notifications.Add(new NotificationMessage("PSN_LoadAccountGameList", "PSN: API 1 (out of 4) unresponsive.", NotificationType.Error));
        return new List<GameMetadata>();
      }
    }

    public static List<GameMetadata> LoadPlayedGameList(PSNLibrary psnLibrary, PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetPlayedTitles().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse, psnLibrary);
      }
      catch (Exception e)
      {
        PSNLibrary.logger.Error(e, "PSN_LoadPlayedGameList");
        psnLibrary.PlayniteApi.Notifications.Add(new NotificationMessage("PSN_LoadPlayedGameList", "PSN: API 2 (out of 4) unresponsive.", NotificationType.Error));
        return new List<GameMetadata>();
      }
    }

    public static List<GameMetadata> LoadMobilePlayedGameList(PSNLibrary psnLibrary, PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetPlayedTitlesMobile().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse);
      }
      catch (Exception e)
      {
        PSNLibrary.logger.Error(e, "PSN_LoadMobilePlayedGameList");
        psnLibrary.PlayniteApi.Notifications.Add(new NotificationMessage("PSN_LoadMobilePlayedGameList", "PSN: API 3 (out of 4) unresponsive.", NotificationType.Error));
        return new List<GameMetadata>();
      }
    }

    public static List<GameMetadata> LoadTrophyList(PSNLibrary psnLibrary, PSNClient psnClient)
    {
      var parsedGames = new List<GameMetadata>();
      var titles = new List<TrophyTitleMobile>();

      try
      {
        titles = psnClient.GetTrohpiesMobile().GetAwaiter().GetResult();
      }
      catch (Exception e)
      {
        PSNLibrary.logger.Error(e, "PSN_LoadTrophyList");
        psnLibrary.PlayniteApi.Notifications.Add(new NotificationMessage("PSN_LoadTrophyList", "PSN: API 4 (out of 4) unresponsive.", NotificationType.Error));
        return parsedGames;
      }

      foreach (var title in titles)
      {
        var gameName = ParserName.call(title.trophyTitleName);

        gameName = gameName.
            TrimEndString("Trophies", StringComparison.OrdinalIgnoreCase).
            TrimEndString("Trophy", StringComparison.OrdinalIgnoreCase).
            Trim();

        var newGame = new GameMetadata
        {
          GameId = title.npCommunicationId,
          LastActivity = title.lastUpdatedDateTime,
          Name = gameName
        };

        var legacyGames = false;

        newGame.Platforms = new HashSet<MetadataProperty> { };
        var trophyPlatforms = title.trophyTitlePlatform?.Split(',');

        if (trophyPlatforms?.Contains("PSP") == true && psnLibrary.SettingsViewModel.Settings.PSP)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("sony_psp"));
          legacyGames = true;
        }
        if (trophyPlatforms?.Contains("PSVITA") == true && psnLibrary.SettingsViewModel.Settings.PSVITA)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("sony_vita"));
          legacyGames = true;
        }
        if (trophyPlatforms?.Contains("PS3") == true && psnLibrary.SettingsViewModel.Settings.PS3)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("sony_playstation3"));
          legacyGames = true;
        }
        if (trophyPlatforms?.Contains("PSPC") == true && psnLibrary.SettingsViewModel.Settings.PC)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("pc_windows"));
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
  }
}
