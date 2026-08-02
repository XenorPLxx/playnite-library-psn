using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PSNLibrary.Services
{
  internal static class SyncGames
  {
    public static List<GameMetadata> MergeAndSyncExistingGames(PSNLibrary psnLibrary, List<GameMetadata> gamesFromApi)
    {
      var gamesToImport = new List<GameMetadata>();

      // The API lists are ordered by priority before this point.
      foreach (var gameGroup in gamesFromApi.GroupBy(a => a.GameId))
      {
        var game = gameGroup.First();

        if (psnLibrary.PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(game.GameId, psnLibrary.Id))
        {
          continue;
        }

        SyncExistingGame(psnLibrary, gameGroup);

        SetSource(psnLibrary, gameGroup, game);
        SetLastPlayed(psnLibrary, gameGroup, game);
        SetPlaytime(gameGroup, game);
        SetPlayCount(psnLibrary, gameGroup, game);
        SetTags(psnLibrary, gameGroup, game);
        gamesToImport.Add(game);
      }

      return gamesToImport;
    }

    private static void SyncExistingGame(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup)
    {
      var existingGame = psnLibrary.PlayniteApi.Database.Games.FirstOrDefault(a => a.GameId == gameGroup.Key && a.PluginId == psnLibrary.Id);
      if (existingGame == null)
      {
        return;
      }

      var gameChanged = false;
      gameChanged |= SetSource(psnLibrary, gameGroup, existingGame);
      gameChanged |= SetLastPlayed(psnLibrary, gameGroup, existingGame);
      gameChanged |= SetPlaytime(psnLibrary, gameGroup, existingGame);
      gameChanged |= SetPlayCount(psnLibrary, gameGroup, existingGame);
      gameChanged |= SetTags(psnLibrary, gameGroup, existingGame);

      if (gameChanged)
      {
        psnLibrary.PlayniteApi.Database.Games.Update(existingGame);
      }
    }

    private static bool SetLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game existingGame)
    {
      if (!psnLibrary.SettingsViewModel.Settings.AlwaysUpdateExistingLastPlayed)
      {
        return false;
      }

      var newLastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
      if (newLastActivity != null && (existingGame.LastActivity == null || newLastActivity.Value.ToUniversalTime() > existingGame.LastActivity.Value.ToUniversalTime()))
      {
        existingGame.LastActivity = newLastActivity;
        return true;
      }

      return false;
    }

    private static void SetLastPlayed(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      newGame.LastActivity = gameGroup.FirstOrDefault(a => a.LastActivity != null)?.LastActivity;
    }

    private static bool SetPlaytime(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game existingGame)
    {
      if (!psnLibrary.SettingsViewModel.Settings.AlwaysUpdateExistingPlaytime)
      {
        return false;
      }

      var newPlaytime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? existingGame.Playtime;
      if (newPlaytime != existingGame.Playtime)
      {
        existingGame.Playtime = newPlaytime;
        return true;
      }

      return false;
    }

    private static void SetPlaytime(IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      newGame.Playtime = gameGroup.FirstOrDefault(a => a.Playtime != 0)?.Playtime ?? newGame.Playtime;
    }

    private static bool SetPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game existingGame)
    {
      if (!psnLibrary.SettingsViewModel.Settings.AlwaysUpdateExistingPlayCount)
      {
        return false;
      }

      var newPlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? existingGame.PlayCount;
      if (newPlayCount != existingGame.PlayCount)
      {
        existingGame.PlayCount = newPlayCount;
        return true;
      }

      return false;
    }

    private static void SetPlayCount(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      newGame.PlayCount = gameGroup.FirstOrDefault(a => a.PlayCount != 0)?.PlayCount ?? newGame.PlayCount;
    }

    private static bool SetTags(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game existingGame)
    {
      if (!psnLibrary.SettingsViewModel.Settings.Tags)
      {
        return false;
      }

      var playStationPlusTag = psnLibrary.PlayniteApi.Database.Tags.FirstOrDefault(tag => tag.Name == "PlayStation Plus");
      var hasPlayStationPlusTag = playStationPlusTag != null && gameGroup
        .SelectMany(game => game.Tags ?? Enumerable.Empty<MetadataProperty>())
        .OfType<MetadataIdProperty>()
        .Any(tag => tag.Id == playStationPlusTag.Id);
      if (hasPlayStationPlusTag)
      {
        if (existingGame.TagIds == null)
        {
          existingGame.TagIds = new List<Guid>();
        }

        var startingCount = existingGame.TagIds.Count;
        existingGame.TagIds.AddMissing(playStationPlusTag.Id);
        return startingCount < existingGame.TagIds.Count;
      }

      return playStationPlusTag != null && existingGame.TagIds?.Remove(playStationPlusTag.Id) == true;
    }

    private static void SetTags(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.Tags && !psnLibrary.SettingsViewModel.Settings.NoTags)
      {
        newGame.Tags = gameGroup.FirstOrDefault(a => a.Tags?.Count != 0)?.Tags ?? newGame.Tags;
      }
      else
      {
        newGame.Tags = null;
      }
    }

    private static bool SetSource(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, Game existingGame)
    {
      var hasPlayStationSource = gameGroup.Any(game => game.Source?.ToString() == "PlayStation");
      var hasPlayStationPlusSource = gameGroup.Any(game => game.Source?.ToString() == "PlayStation Plus");
      if (psnLibrary.SettingsViewModel.Settings.PlusSource)
      {
        if (hasPlayStationSource && (existingGame.Source?.ToString() == "PlayStation Plus" || existingGame.Source == null))
        {
          existingGame.SourceId = psnLibrary.PlayniteApi.Database.Sources.Add("PlayStation").Id;
          return true;
        }

        if (hasPlayStationPlusSource && !hasPlayStationSource && (existingGame.Source?.ToString() != "PlayStation Plus" || existingGame.Source == null))
        {
          existingGame.SourceId = psnLibrary.PlayniteApi.Database.Sources.Add("PlayStation Plus").Id;
          return true;
        }

        if (existingGame.Source == null)
        {
          existingGame.SourceId = psnLibrary.PlayniteApi.Database.Sources.Add("PlayStation").Id;
          return true;
        }
      }
      else if (existingGame.Source?.ToString() == "PlayStation Plus" || existingGame.Source == null)
      {
        existingGame.SourceId = psnLibrary.PlayniteApi.Database.Sources.Add("PlayStation").Id;
        return true;
      }

      return false;
    }

    private static void SetSource(PSNLibrary psnLibrary, IGrouping<string, GameMetadata> gameGroup, GameMetadata newGame)
    {
      if (psnLibrary.SettingsViewModel.Settings.PlusSource && gameGroup.Any(g => g.Source?.ToString() == "PlayStation Plus") && !gameGroup.Any(g => g.Source?.ToString() == "PlayStation"))
      {
        newGame.Source = new MetadataNameProperty("PlayStation Plus");
      }
      else
      {
        newGame.Source = new MetadataNameProperty("PlayStation");
      }
    }
  }
}
