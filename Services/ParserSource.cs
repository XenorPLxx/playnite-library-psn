using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Services
{
  internal class ParserSource
  {
    public static string call(string subscriptionName, PSNLibrary psnLibrary)
    {
      string source = "PlayStation";

      switch (subscriptionName)
      {
        case "PS_PLUS":
          source = "PlayStation Plus";
          break;

        default:
          break;
      }
      return source;
    }
  }
}
