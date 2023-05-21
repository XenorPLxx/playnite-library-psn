using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
          findLastPlayed(psnLibrary, gameGroup, game);
          findPlaytime(psnLibrary, gameGroup, game);
          findPlayCount(psnLibrary, gameGroup, game);
          newlyImportedGames.Add(psnLibrary.PlayniteApi.Database.ImportGame(game, psnLibrary));
        }
        // If game is already in the database, just update activity related fields
        else
        {
          // Track if actually changed so there's no empty updates triggered
          bool gameChanged = false;
          findLastPlayed(psnLibrary, gameGroup, alreadyImportedGame, ref gameChanged);
          findPlaytime(psnLibrary, gameGroup, alreadyImportedGame, ref gameChanged);
          findPlayCount(psnLibrary, gameGroup, alreadyImportedGame, ref gameChanged);

          // Update existing database entry
          if (gameChanged) { psnLibrary.PlayniteApi.Database.Games.Update(alreadyImportedGame); }
        }
      }
      return newlyImportedGames;
    }

    private static void findLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame, ref bool gameChanged)
    {
      if (psnLibrary.SettingsViewModel.Settings.LastPlayed)
      {
        var newLastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
        if (newLastActivity != null && (alreadyImportedGame.LastActivity == null || newLastActivity.Value.ToUniversalTime() != alreadyImportedGame.LastActivity.Value.ToUniversalTime()))
        {
          alreadyImportedGame.LastActivity = newLastActivity;
          gameChanged = true;
        }
      }
    }

    private static void findLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.LastPlayed)
      {
        var newLastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
        newGame.LastActivity = newLastActivity;
      }
    }

    private static void findPlaytime(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame, ref bool gameChanged)
    {
      if (psnLibrary.SettingsViewModel.Settings.Playtime)
      {
        var newPlaytime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? alreadyImportedGame.Playtime;
        if (newPlaytime != alreadyImportedGame.Playtime)
        {
          alreadyImportedGame.Playtime = newPlaytime;
          gameChanged = true;
        }
      }
    }

    private static void findPlaytime(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Playtime)
      {
        var newPlaytime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? newGame.Playtime;
        newGame.Playtime = newPlaytime;
      }
    }

    private static void findPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game alreadyImportedGame, ref bool gameChanged)
    {
      if (psnLibrary.SettingsViewModel.Settings.PlayCount)
      {
        var newPlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? alreadyImportedGame.PlayCount;
        if (newPlayCount != alreadyImportedGame.PlayCount)
        {
          alreadyImportedGame.PlayCount = newPlayCount;
          gameChanged = true;
        }
      }
    }

    private static void findPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.PlayCount)
      {
        var newPlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? newGame.PlayCount;
        newGame.PlayCount = newPlayCount;
      }
    }
  }
}
