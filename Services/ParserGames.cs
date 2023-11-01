using Playnite.SDK;
using Playnite.SDK.Models;
using PSNLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserGames
  {
    public static List<GameMetadata> call(List<AccountTitlesResponseData.AccountTitlesRetrieve.Title> gamesToParse, PSNLibrary psnLibrary)
    {
      var parsedGames = new List<GameMetadata>();
      foreach (var title in gamesToParse)
      {
        var gameName = ParserName.call(title.name);

        string platform = ParserPlatform.call(title.platform);
        var tag = ParserSubscription.call(title.subscriptionService, psnLibrary);

        parsedGames.Add(new GameMetadata
        {
          GameId = title.titleId,
          Name = gameName,
          //CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.image.url) : null,
          Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) },
          Tags = tag == Guid.Empty ? null : new HashSet<MetadataProperty> { new MetadataIdProperty(tag) }
        });
      }

      return parsedGames;
    }

    // TODO: Figure out smarter way to share code without overloading
    public static List<GameMetadata> call(List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title> gamesToParse)
    {
      var parsedGames = new List<GameMetadata>();
      foreach (var title in gamesToParse)
      {
        var gameName = ParserName.call(title.name);

        string platform = ParserPlatform.call(title.platform);

        parsedGames.Add(new GameMetadata
        {
          GameId = title.titleId,
          Name = gameName,
          //CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.image.url) : null,
          Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) },
          LastActivity = title.lastPlayedDateTime
        });
      }

      return parsedGames;
    }

    // TODO: Figure out smarter way to share code without overloading
    public static List<GameMetadata> call(List<PlayedTitlesMobile.PlayedTitleMobile> gamesToParse)
    {
      var parsedGames = new List<GameMetadata>();
      foreach (var title in gamesToParse)
      {
        var gameName = ParserName.call(title.name);

        string platform = ParserCategory.call(title.category);

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
          //CoverImage = SettingsViewModel.Settings.DownloadImageMetadata ? new MetadataFile(title.imageUrl) : null,
          Platforms = platform.IsNullOrEmpty() ? null : new HashSet<MetadataProperty> { new MetadataSpecProperty(platform) },
          Playtime = playtime,
          LastActivity = title.lastPlayedDateTime,
          PlayCount = title.playCount
        });
      }


      return parsedGames;
    }
  }
}
