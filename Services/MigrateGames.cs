using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class MigrateGames
  {
    public static void call(PSNLibrary library, List<GameMetadata> games)
    {
      library.SettingsViewModel.BeginEdit();
      library.SettingsViewModel.Settings.Migration = false;
      library.SettingsViewModel.EndEdit();

      var pluginGames = library.PlayniteApi.Database.Games.Where(x => x.PluginId == library.Id);

      if (pluginGames.Count() > 0)
      {
        string[] titleIdsArray = games.GroupBy(x => x.GameId).Select(x => x.FirstOrDefault()).Select(x => x.GameId).ToArray();

        var gamesWithIds = new PSNClient(library).GetTrohpiesWithIdsMobile(titleIdsArray).GetAwaiter().GetResult();

        foreach (var game in pluginGames)
        {
          if (game.GameId.Contains("#ACCOUNT#"))
          {
            game.GameId = game.GameId.Substring(9);
            library.PlayniteApi.Database.Games.Update(game);
          }
          else if (game.GameId.Contains("#TROPHY#"))
          {
            var communicationId = game.GameId.Substring(8);
            string npTitleId = gamesWithIds.FirstOrDefault(p => p.trophyTitles.Any(c => c.npCommunicationId == communicationId))?.npTitleId ?? null;
            game.GameId = npTitleId != null ? npTitleId : communicationId;
            library.PlayniteApi.Database.Games.Update(game);
          }
        }
      }
    }
  }
}
