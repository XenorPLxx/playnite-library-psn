using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserCategory
  {
    public static string call(string categoryId)
    {
      string category = null;

      switch (categoryId)
      {
        case "ps4_game":
          category = "sony_playstation4";
          break;

        case "ps5_native_game":
          category = "sony_playstation5";
          break;

        default:
          break;
      }
      return category;
    }
  }
}
