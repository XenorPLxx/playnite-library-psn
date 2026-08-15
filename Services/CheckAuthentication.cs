using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class CheckAuthentication
  {
    public static bool call(PSNLibrary psnLibrary, PSNClient psnClient, CancellationToken cancellationToken = default(CancellationToken))
    {
      try
      {
        psnClient.CheckAuthentication(cancellationToken).GetAwaiter().GetResult();
        return true;
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception e)
      {
        PSNLibrary.logger.Error(e, "PSN_CheckAuthentication");
        psnLibrary.PlayniteApi.Notifications.Add(
          new NotificationMessage(
            "PSN_CheckAuthentication",
            "PSN: Authentication check failed.",
            NotificationType.Error,
            () => psnLibrary.OpenSettingsView())
          );
        return false;
      }
    }

    public static bool call(PSNClient psnClient)
    {
      try
      {
        psnClient.CheckAuthentication().GetAwaiter().GetResult();
        return true;
      }
      catch
      {
        return false;
      }
    }
  }
}
