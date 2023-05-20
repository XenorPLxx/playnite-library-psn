using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserName
  {
    public static string call(string name)
    {
      var gameName = name.
        RemoveTrademarks(" ").
        NormalizeGameName().
        Replace("full game", "", StringComparison.OrdinalIgnoreCase).
        Trim();

      return Regex.Replace(gameName, @"\s+", " ");
    }
  }
}
