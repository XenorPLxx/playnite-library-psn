using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserSubscription
  {
    public static Guid call(string subscriptionName, PSNLibrary psnLibrary)
    {  
      var tag = Guid.Empty;

      switch (subscriptionName)
      {
        case "PS_PLUS":
          tag = psnLibrary.PlayniteApi.Database.Tags.Add("PlayStation Plus").Id;
          break;

        default:
          break;
      }
      return tag;
    }
  }
}
