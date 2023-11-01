using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace PSNLibrary.Services
{
  internal class ImportGames
  {
    public static List<Game> call(PSNLibrary psnLibrary, List<GameMetadata> gamesFromApi)
    {
      var newlyImportedGames = new List<Game>();

      // Games list is already parsed by this point, so we group by GameId for processing
      foreach (var gameGroup in gamesFromApi.GroupBy(a => a.GameId))
      {
        // We take first game from the group, as it's sortes by priority already
        var game = gameGroup.First();

        // If game is excluded from import, omit processing
        if (psnLibrary.PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(game.GameId, psnLibrary.Id)) { continue; }

        // Look trough local database if the game is already imported
        var alreadyImportedGame = psnLibrary.PlayniteApi.Database.Games.FirstOrDefault(a => a.GameId == game.GameId && a.PluginId == psnLibrary.Id);

        // If game is not imported, import and add it to the return list
        if (alreadyImportedGame == null)
        {
          game.Source = new MetadataNameProperty("PlayStation");
          setLastPlayed(psnLibrary, gameGroup, game);
          setPlaytime(psnLibrary, gameGroup, game);
          setPlayCount(psnLibrary, gameGroup, game);
          setTags(psnLibrary, gameGroup, game);
          newlyImportedGames.Add(psnLibrary.PlayniteApi.Database.ImportGame(game, psnLibrary));
        }
        // If game is already in the database, just update activity related fields
        else
        {
          // Track if actually changed so there's no empty updates triggered
          bool gameChanged = false;
          gameChanged |= setLastPlayed(psnLibrary, gameGroup, alreadyImportedGame);
          gameChanged |= setPlaytime(psnLibrary, gameGroup, alreadyImportedGame);
          gameChanged |= setPlayCount(psnLibrary, gameGroup, alreadyImportedGame);
          gameChanged |= setTags(psnLibrary, gameGroup, alreadyImportedGame);

          // Update existing database entry
          if (gameChanged) { psnLibrary.PlayniteApi.Database.Games.Update(alreadyImportedGame); }
        }
      }
      return newlyImportedGames;
    }

    private static bool setLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.LastPlayed)
      {
        var newLastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
        if (newLastActivity != null && (alreadyImportedGame.LastActivity == null || newLastActivity.Value.ToUniversalTime() > alreadyImportedGame.LastActivity.Value.ToUniversalTime()))
        {
          alreadyImportedGame.LastActivity = newLastActivity;
          return true;
        }
      }
      return false;
    }

    private static void setLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.LastPlayed)
      {
        var newLastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
        newGame.LastActivity = newLastActivity;
      }
    }

    private static bool setPlaytime(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Playtime)
      {
        var newPlaytime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? alreadyImportedGame.Playtime;
        if (newPlaytime != alreadyImportedGame.Playtime)
        {
          alreadyImportedGame.Playtime = newPlaytime;
          return true;
        }
      }
      return false;
    }

    private static void setPlaytime(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Playtime)
      {
        var newPlaytime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? newGame.Playtime;
        newGame.Playtime = newPlaytime;
      }
    }

    private static bool setPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.PlayCount)
      {
        var newPlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? alreadyImportedGame.PlayCount;
        if (newPlayCount != alreadyImportedGame.PlayCount)
        {
          alreadyImportedGame.PlayCount = newPlayCount;
          return true;
        }
      }
      return false;
    }

    private static void setPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.PlayCount)
      {
        var newPlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? newGame.PlayCount;
        newGame.PlayCount = newPlayCount;
      }
    }

    private static bool setTags(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Tags)
      {
        var newTags = gameGroup.FirstOrDefault(a => a.Tags?.Count != 0)?.Tags;
        
        newTags?.ForEach(newTag =>
        {
          if (alreadyImportedGame.TagIds == null)
          {
            alreadyImportedGame.TagIds = new List<Guid>();
          }
          alreadyImportedGame.TagIds.AddMissing(((Playnite.SDK.Models.MetadataIdProperty)newTag).Id);
        });
        
        return true;
      }
      return false;
    }

    private static void setTags(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Tags && !psnLibrary.SettingsViewModel.Settings.NoTags)
      {
        var newTags = gameGroup.FirstOrDefault(a => a.Tags?.Count != 0)?.Tags ?? newGame.Tags;
        newGame.Tags = newTags;
      }
      else
      {
        newGame.Tags = null;
      }
    }
  }
}
