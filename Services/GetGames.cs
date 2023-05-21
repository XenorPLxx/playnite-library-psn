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
    public static List<GameMetadata> LoadAccountGameList(PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetAccountTitles().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse);
      }
      catch (Exception e)
      {
        //logger.Error(e, "PSN_ParseAccountList");
        //notifications.Add(new NotificationMessage("PSN_ParseAccountList", "PSN: Account games list couldn't be parsed.", NotificationType.Error));
        return new List<GameMetadata>();
      }
    }

    public static List<GameMetadata> LoadPlayedGameList(PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetPlayedTitles().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse);
      }
      catch (Exception e)
      {
        //logger.Error(e, "PSN_ParsePlayedList");
        //notifications.Add(new NotificationMessage("PSN_ParsePlayedList", "PSN: Played games list couldn't be parsed.", NotificationType.Error));
        return new List<GameMetadata>();
      }
    }

    public static List<GameMetadata> LoadMobilePlayedGameList(PSNClient psnClient)
    {
      try
      {
        var gamesToParse = psnClient.GetPlayedTitlesMobile().GetAwaiter().GetResult();
        return ParserGames.call(gamesToParse);
      }
      catch (Exception e)
      {
        //logger.Error(e, "PSN_ParsePlayedMobileList");
        //notifications.Add(new NotificationMessage("PSN_ParsePlayedMobileList", "PSN: Mobile played games list couldn't be parsed.", NotificationType.Error));
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
        //logger.Error(e, "PSN_ParseThrophies");
        //notifications.Add(new NotificationMessage("PSN_ParseThrophies", "PSN: Trophy list couldn't be parsed.", NotificationType.Error));
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

        if (title.trophyTitlePlatform?.Contains("PSP") == true && psnLibrary.SettingsViewModel.Settings.PSP)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("sony_psp"));
          legacyGames = true;
        }
        else if (title.trophyTitlePlatform?.Contains("PSVITA") == true && psnLibrary.SettingsViewModel.Settings.PSVITA)
        {
          newGame.Platforms.Add(new MetadataSpecProperty("sony_vita"));
          legacyGames = true;
        }
        else if (title.trophyTitlePlatform?.Contains("PS3") == true && psnLibrary.SettingsViewModel.Settings.PS3)
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
  }
}
