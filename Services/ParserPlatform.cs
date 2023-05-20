using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserPlatform
  {
    public static string call(string platformId)
    {
      string platform = null;

      switch (platformId)
      {
        case "PSP":
          platform = "sony_psp";
          break;

        case "PSVITA":
          platform = "sony_vita";
          break;

        case "PS3":
          platform = "sony_playstation3";
          break;

        case "PS4":
          platform = "sony_playstation4";
          break;

        case "PS5":
          platform = "sony_playstation5";
          break;

        default:
          break;
      }
      return platform;
    }
  }
}
