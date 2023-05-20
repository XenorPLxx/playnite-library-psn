using Playnite.SDK;
using Playnite.SDK.Data;
using PSNLibrary.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Web;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Http.Headers;
using System.Security;

namespace PSNLibrary.Services
{
  public class ApiRedirectResponse
  {
    public string redirectUrl { get; set; }
    public string sid { get; set; }
  }
  public class PSNClient
  {
    private static readonly ILogger logger = LogManager.GetLogger();
    private IPlayniteAPI api;
    private MobileTokens mobileToken;
    private readonly PSNLibrary library;
    private readonly string tokenPath;
    private const int pageRequestLimit = 100;
    private const string loginUrl = @"https://web.np.playstation.com/api/session/v1/signin?redirect_uri=https://io.playstation.com/central/auth/login%3FpostSignInURL=https://www.playstation.com/home%26cancelURL=https://www.playstation.com/home&smcid=web:pdc";
    private const string gameListUrl = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={{\"isActive\":true,\"platform\":[\"ps3\",\"ps4\",\"ps5\"],\"start\":{0},\"size\":{1},\"subscriptionService\":\"NONE\"}}&extensions={{\"persistedQuery\":{{\"version\":1,\"sha256Hash\":\"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2\"}}}}";
    private const string playedListUrl = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getUserGameList&variables=%7B%22limit%22%3A100%2C%22categories%22%3A%22ps4_game%2Cps5_native_game%22%7D&extensions=%7B%22persistedQuery%22%3A%7B%22version%22%3A1%2C%22sha256Hash%22%3A%22e780a6d8b921ef0c59ec01ea5c5255671272ca0d819edb61320914cf7a78b3ae%22%7D%7D";
    private const string mobileCodeUrl = "https://ca.account.sony.com/api/authz/v3/oauth/authorize?access_type=offline&client_id=09515159-7237-4370-9b40-3806e67c0891&redirect_uri=com.scee.psxandroid.scecompcall%3A%2F%2Fredirect&response_type=code&scope=psn%3Amobile.v2.core%20psn%3Aclientapp";
    private const string mobileTokenUrl = "https://ca.account.sony.com/api/authz/v3/oauth/token";
    private const string mobileTokenAuth = "MDk1MTUxNTktNzIzNy00MzcwLTliNDAtMzgwNmU2N2MwODkxOnVjUGprYTV0bnRCMktxc1A=";
    private const string playedMobileListUrl = "https://m.np.playstation.com/api/gamelist/v2/users/me/titles?categories=ps4_game,ps5_native_game&limit=200&offset={0}";
    private const string trophiesMobileUrl = @"https://m.np.playstation.com/api/trophy/v1/users/me/trophyTitles?limit=250&offset={0}";
    private const string trophiesWithIdsMobileUrl = @"https://m.np.playstation.com/api/trophy/v1/users/me/titles/trophyTitles?npTitleIds={0}";

    public PSNClient(PSNLibrary library)
    {
      this.library = library;
      this.api = library.PlayniteApi;
      tokenPath = Path.Combine(library.GetPluginUserDataPath(), "token.json");
    }

    public void Login()
    {
      var loggedIn = false;

      if (File.Exists(tokenPath))
      {
        File.Delete(tokenPath);
      }

      var webViewSettings = new WebViewSettings();
      webViewSettings.WindowHeight = 700;
      webViewSettings.WindowWidth = 580;
      webViewSettings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36";
      using (var view = api.WebViews.CreateView(webViewSettings))
      {
        view.LoadingChanged += (s, e) =>
        {
          var address = view.GetCurrentAddress();
          if (address.StartsWith(@"https://www.playstation.com/"))
          {
            loggedIn = true;
            view.Close();
          }
        };

        view.DeleteDomainCookies(".sony.com");
        view.DeleteDomainCookies(".ca.account.sony.com");
        view.DeleteDomainCookies("ca.account.sony.com");
        view.DeleteDomainCookies(".playstation.com");
        view.DeleteDomainCookies("io.playstation.com");
        view.Navigate(loginUrl);
        view.OpenDialog();
      }

      if (!loggedIn)
      {
        return;
      }

      dumpCookies();

      return;
    }

    private IEnumerable<Playnite.SDK.HttpCookie> dumpCookies()
    {
      var view = api.WebViews.CreateOffscreenView();

      var cookies = view.GetCookies();


      var cookieContainer = new CookieContainer();
      foreach (var cookie in cookies)
      {
        if (cookie.Domain == ".playstation.com")
        {
          cookieContainer.Add(new Uri("https://web.np.playstation.com"), new Cookie(cookie.Name, cookie.Value));
        }
        if (cookie.Domain == ".ca.account.sony.com")
        {
          cookieContainer.Add(new Uri("https://ca.account.sony.com"), new Cookie(cookie.Name, cookie.Value));
        }
        if (cookie.Domain == "ca.account.sony.com")
        {
          cookieContainer.Add(new Uri("https://ca.account.sony.com"), new Cookie(cookie.Name, cookie.Value));
        }
        if (cookie.Domain == ".sony.com")
        {
          cookieContainer.Add(new Uri("https://ca.account.sony.com"), new Cookie(cookie.Name, cookie.Value));
        }
      }

      WriteCookiesToDisk(cookieContainer);

      view.Dispose();
      return cookies;
    }

    private void WriteCookiesToDisk(CookieContainer cookieJar)
    {
      File.Delete(tokenPath);
      using (Stream stream = File.Create(tokenPath))
      {
        try
        {
          Console.Out.Write("Writing cookies to disk... ");
          BinaryFormatter formatter = new BinaryFormatter();
          formatter.Serialize(stream, cookieJar);
          Console.Out.WriteLine("Done.");
        }
        catch (Exception e)
        {
          Console.Out.WriteLine("Problem writing cookies to disk: " + e.GetType());
        }
      }
    }

    private CookieContainer ReadCookiesFromDisk()
    {
      try
      {
        using (Stream stream = File.Open(tokenPath, FileMode.Open))
        {
          Console.Out.Write("Reading cookies from disk... ");
          BinaryFormatter formatter = new BinaryFormatter();
          Console.Out.WriteLine("Done.");
          return (CookieContainer)formatter.Deserialize(stream);
        }
      }
      catch (Exception e)
      {
        Console.Out.WriteLine("Problem reading cookies from disk: " + e.GetType());
        return new CookieContainer();
      }
    }

    private async Task<bool> getMobileToken()
    {
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        string mobileCode;
        try
        {
          var mobileCodeResponse = await httpClient.GetAsync(mobileCodeUrl);
          mobileCode = HttpUtility.ParseQueryString(mobileCodeResponse.Headers.Location.Query)["code"];
        }
        catch
        {
          TryRefreshCookies();
          try
          {
            var mobileCodeResponse = await httpClient.GetAsync(mobileCodeUrl);
            mobileCode = HttpUtility.ParseQueryString(mobileCodeResponse.Headers.Location.Query)["code"];
          }
          catch
          {
            return false;
          }
        }

        HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("post"), mobileTokenUrl);
        var requestMessageForm = new List<KeyValuePair<string, string>>();
        requestMessageForm.Add(new KeyValuePair<string, string>("code", mobileCode));
        requestMessageForm.Add(new KeyValuePair<string, string>("redirect_uri", "com.scee.psxandroid.scecompcall://redirect"));
        requestMessageForm.Add(new KeyValuePair<string, string>("grant_type", "authorization_code"));
        requestMessageForm.Add(new KeyValuePair<string, string>("token_format", "jwt"));
        requestMessage.Content = new FormUrlEncodedContent(requestMessageForm);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", mobileTokenAuth);

        var mobileTokenResponse = await httpClient.SendAsync(requestMessage);
        var strResponse = await mobileTokenResponse.Content.ReadAsStringAsync();
        mobileToken = Serialization.FromJson<MobileTokens>(strResponse);
        return true;
      }
    }


    public void ClearAuthentication()
    {
      if (File.Exists(tokenPath))
      {
        File.Delete(tokenPath);
      }

      using (var view = api.WebViews.CreateOffscreenView())
      {
        view.DeleteDomainCookies(".sony.com");
        view.DeleteDomainCookies(".ca.account.sony.com");
        view.DeleteDomainCookies("ca.account.sony.com");
        view.DeleteDomainCookies(".playstation.com");
        view.DeleteDomainCookies("io.playstation.com");
        view.Close();
      }
    }

    public async Task CheckAuthentication()
    {
      string npsso = library.SettingsViewModel.Settings.Npsso;
      if (!File.Exists(tokenPath) && npsso == null)
      {
        throw new Exception("User is not authenticated: token file doesn't exist.");
      }
      else
      {
        if (!await GetIsUserLoggedIn())
        {
          TryRefreshCookies();
          if (!await GetIsUserLoggedIn())
          {
            throw new Exception("User is not authenticated.");
          }
          else
          {
            if (mobileToken == null)
            {
              if (!await getMobileToken())
              {
                throw new Exception("User is not authenticated.");
              }
            }
          }
        }
        else
        {
          if (mobileToken == null)
          {
            if (!await getMobileToken())
            {
              throw new Exception("User is not authenticated.");
            }
          }
        }
      }
    }

    public async Task<List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title>> GetPlayedTitles()
    {
      await CheckAuthentication();

      var titles = new List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title>();

      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        var resp = httpClient.GetAsync(playedListUrl).GetAwaiter().GetResult();
        var strResponse = await resp.Content.ReadAsStringAsync();
        var titles_part = Serialization.FromJson<PlayedTitles>(strResponse);
        titles.AddRange(titles_part.data.gameLibraryTitlesRetrieve.games);
      }

      return titles;
    }

    public async Task<List<AccountTitlesResponseData.AccountTitlesRetrieve.Title>> GetAccountTitles()
    {
      await CheckAuthentication();

      var titles = new List<AccountTitlesResponseData.AccountTitlesRetrieve.Title>();

      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {

        var itemCount = 0;
        var offset = -pageRequestLimit;

        do
        {
          object[] args = { offset, pageRequestLimit };
          var resp = httpClient.GetAsync(gameListUrl.Format(offset + pageRequestLimit, pageRequestLimit)).GetAwaiter().GetResult();
          var strResponse = await resp.Content.ReadAsStringAsync();
          var titles_part = Serialization.FromJson<AccountTitles>(strResponse);
          titles.AddRange(titles_part.data.purchasedTitlesRetrieve.games);
          offset = titles_part.data.purchasedTitlesRetrieve.pageInfo.offset;
          itemCount = titles_part.data.purchasedTitlesRetrieve.pageInfo.totalCount;
        } while (offset + pageRequestLimit < itemCount);


      }

      return titles;
    }

    public async Task<List<PlayedTitlesMobile.PlayedTitleMobile>> GetPlayedTitlesMobile()
    {
      await CheckAuthentication();

      var titles = new List<PlayedTitlesMobile.PlayedTitleMobile>();

      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        int? offset = 0;

        do
        {
          HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("get"), playedMobileListUrl.Format(offset));
          requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
          var resp = await httpClient.SendAsync(requestMessage);
          var strResponse = await resp.Content.ReadAsStringAsync();
          var titles_part = Serialization.FromJson<PlayedTitlesMobile>(strResponse);
          titles.AddRange(titles_part.titles);
          offset = titles_part.nextOffset;
        } while (offset != null);


      }

      return titles;
    }

    public async Task<List<TrophyTitleMobile>> GetTrohpiesMobile()
    {
      await CheckAuthentication();

      var titles = new List<TrophyTitleMobile>();

      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        int? offset = 0;

        do
        {
          HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("get"), trophiesMobileUrl.Format(offset));
          requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
          var resp = await httpClient.SendAsync(requestMessage);
          var strResponse = await resp.Content.ReadAsStringAsync();
          var titles_part = Serialization.FromJson<TrophyTitlesMobile>(strResponse);
          titles.AddRange(titles_part.trophyTitles);
          offset = titles_part.nextOffset;
        } while (offset != null);
      }

      return titles;
    }


    public async Task<List<TrophyTitlesWithIdsMobile.TrophyTitleWithIdsMobile>> GetTrohpiesWithIdsMobile(string[] titleIdsArray)
    {
      await CheckAuthentication();

      var titles = new List<TrophyTitlesWithIdsMobile.TrophyTitleWithIdsMobile>();

      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        int querySize = 5;
        int offset = 0;

        do
        {
          HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("get"), trophiesWithIdsMobileUrl.Format(string.Join(",", titleIdsArray.Skip(offset).Take(querySize))));
          requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
          var resp = await httpClient.SendAsync(requestMessage);
          var strResponse = await resp.Content.ReadAsStringAsync();
          var titles_part = Serialization.FromJson<TrophyTitlesWithIdsMobile>(strResponse);
          titles.AddRange(titles_part.titles);
          offset = offset + querySize;
        } while (offset < titleIdsArray.Length);
      }

      return titles;
    }

    private void TryRefreshCookies()
    {
      string address;
      using (var webView = api.WebViews.CreateOffscreenView())
      {
        webView.LoadingChanged += (s, e) =>
        {
          address = webView.GetCurrentAddress();
          webView.Close();
        };

        string npsso = library.SettingsViewModel.Settings.Npsso;
        Playnite.SDK.HttpCookie npssoCookie = new Playnite.SDK.HttpCookie();
        npssoCookie.Domain = "ca.account.sony.com";
        npssoCookie.Value = npsso;
        npssoCookie.Name = "npsso";
        npssoCookie.Path = "/";
        webView.SetCookies("https://ca.account.sony.com", npssoCookie);
        webView.NavigateAndWait(loginUrl);
      }

      dumpCookies();
    }

    public async Task<bool> GetIsUserLoggedIn()
    {
      string npsso = library.SettingsViewModel.Settings.Npsso;
      if (!File.Exists(tokenPath) && npsso == null)
      {
        return false;
      }

      try
      {
        var cookieContainer = ReadCookiesFromDisk();
        using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
        using (var httpClient = new HttpClient(handler))
        {

          var resp = httpClient.GetAsync(gameListUrl.Format(0, 24)).GetAwaiter().GetResult();
          var strResponse = await resp.Content.ReadAsStringAsync();
          if (Serialization.TryFromJson<AccountTitlesErrorResponse>(strResponse, out var error) && error.data.purchasedTitlesRetrieve == null)
          {
            return false;
          }

          if (Serialization.TryFromJson<AccountTitles>(strResponse, out var accountTitles) && accountTitles.data.purchasedTitlesRetrieve != null)
          {
            return true;
          }
        }
        return false;
      }
      catch (Exception e) when (!Debugger.IsAttached)
      {
        logger.Error(e, "Failed to check if user is authenticated into PSN.");
        return false;
      }
    }
  }
}
