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
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

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
    private static readonly Uri[] cookieUris =
    {
      new Uri("https://web.np.playstation.com"),
      new Uri("https://ca.account.sony.com")
    };
    private static readonly byte[] cookieEncryptionEntropy = Encoding.UTF8.GetBytes("PSNLibrary.CookieStore.v1");
    private readonly IPlayniteAPI api;
    private MobileTokens mobileToken;
    private readonly PSNLibrary psnLibrary;
    private readonly string cookiesPath;
    private readonly string legacyTokenPath;
    private const int pageRequestLimit = 100;
    private const string loginUrl = @"https://web.np.playstation.com/api/session/v1/signin?redirect_uri=https://io.playstation.com/central/auth/login%3FpostSignInURL=https://www.playstation.com/home%26cancelURL=https://www.playstation.com/home&smcid=web:pdc";
    private const string gameListUrl = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={{\"isActive\":true,\"platform\":[\"ps3\",\"ps4\",\"ps5\"],\"start\":{0},\"size\":{1},\"sortBy\":\"ACTIVE_DATE\",\"sortDirection\":\"desc\"}}&extensions={{\"persistedQuery\":{{\"version\":1,\"sha256Hash\":\"827a423f6a8ddca4107ac01395af2ec0eafd8396fc7fa204aaf9b7ed2eefa168\"}}}}";
    private const string playedListUrl = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getUserGameList&variables=%7B%22limit%22%3A100%2C%22categories%22%3A%22ps4_game%2Cps5_native_game%22%7D&extensions=%7B%22persistedQuery%22%3A%7B%22version%22%3A1%2C%22sha256Hash%22%3A%22e780a6d8b921ef0c59ec01ea5c5255671272ca0d819edb61320914cf7a78b3ae%22%7D%7D";
    private const string mobileCodeUrl = "https://ca.account.sony.com/api/authz/v3/oauth/authorize?access_type=offline&client_id=09515159-7237-4370-9b40-3806e67c0891&redirect_uri=com.scee.psxandroid.scecompcall%3A%2F%2Fredirect&response_type=code&scope=psn%3Amobile.v2.core%20psn%3Aclientapp";
    private const string mobileTokenUrl = "https://ca.account.sony.com/api/authz/v3/oauth/token";
    private const string mobileTokenAuth = "MDk1MTUxNTktNzIzNy00MzcwLTliNDAtMzgwNmU2N2MwODkxOnVjUGprYTV0bnRCMktxc1A=";
    private const string playedMobileListUrl = "https://m.np.playstation.com/api/gamelist/v2/users/me/titles?categories=ps4_game,ps5_native_game&limit=200&offset={0}";
    private const string trophiesMobileUrl = @"https://m.np.playstation.com/api/trophy/v1/users/me/trophyTitles?limit=250&offset={0}";
    private const string trophiesWithIdsMobileUrl = @"https://m.np.playstation.com/api/trophy/v1/users/me/titles/trophyTitles?npTitleIds={0}";

    public PSNClient(PSNLibrary psnLibrary)
    {
      this.psnLibrary = psnLibrary;
      api = psnLibrary.PlayniteApi;
      cookiesPath = Path.Combine(psnLibrary.GetPluginUserDataPath(), "cookies.dat");
      legacyTokenPath = Path.Combine(psnLibrary.GetPluginUserDataPath(), "token.json");
    }

    public static bool TryGetNpsso(string value, out string npsso, out string error)
    {
      npsso = string.Empty;
      error = null;
      var trimmedValue = value?.Trim();
      if (string.IsNullOrEmpty(trimmedValue))
      {
        return true;
      }

      if (!trimmedValue.StartsWith("{", StringComparison.Ordinal))
      {
        npsso = trimmedValue;
        return true;
      }

      try
      {
        var cookie = new JavaScriptSerializer().DeserializeObject(trimmedValue) as Dictionary<string, object>;
        if (cookie == null || !cookie.TryGetValue("npsso", out var npssoValue) || !(npssoValue is string cookieNpsso) || string.IsNullOrWhiteSpace(cookieNpsso))
        {
          error = "The ssocookie JSON must contain a non-empty string property named 'npsso'.";
          return false;
        }

        npsso = cookieNpsso.Trim();
        return true;
      }
      catch (ArgumentException)
      {
        error = "The NPSSO input is not valid JSON. Paste the raw NPSSO value or a JSON object such as {\"npsso\":\"...\"}.";
        return false;
      }
    }

    private bool DumpCookies(IEnumerable<Playnite.SDK.HttpCookie> cookies)
    {
      var cookieContainer = new CookieContainer();
      foreach (var cookie in cookies)
      {
        if (cookie.Domain == ".playstation.com")
        {
          cookieContainer.Add(new Uri("https://web.np.playstation.com"), new Cookie(cookie.Name, cookie.Value));
        }
        if (cookie.Domain == ".ca.account.sony.com" || cookie.Domain == "ca.account.sony.com" || cookie.Domain == ".sony.com")
        {
          cookieContainer.Add(new Uri("https://ca.account.sony.com"), new Cookie(cookie.Name, cookie.Value));
        }
      }

      var cookiesSaved = WriteCookiesToDisk(cookieContainer);
      if (cookiesSaved && File.Exists(legacyTokenPath))
      {
        File.Delete(legacyTokenPath);
      }

      return cookiesSaved;
    }

    private bool WriteCookiesToDisk(CookieContainer cookieJar)
    {
      var temporaryCookiesPath = cookiesPath + ".tmp";
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(cookiesPath));
        var encryptedCookies = ProtectedData.Protect(
          Encoding.UTF8.GetBytes(Serialization.ToJson(GetStoredCookies(cookieJar))),
          cookieEncryptionEntropy,
          DataProtectionScope.CurrentUser);
        File.WriteAllBytes(temporaryCookiesPath, encryptedCookies);
        File.Copy(temporaryCookiesPath, cookiesPath, true);
        return true;
      }
      catch (Exception e)
      {
        logger.Error(e, "Failed to save PlayStation authentication cookies.");
        return false;
      }
      finally
      {
        if (File.Exists(temporaryCookiesPath))
        {
          File.Delete(temporaryCookiesPath);
        }
      }
    }

    private CookieContainer ReadCookiesFromDisk()
    {
      if (File.Exists(cookiesPath))
      {
        try
        {
          var decryptedCookies = ProtectedData.Unprotect(
            File.ReadAllBytes(cookiesPath),
            cookieEncryptionEntropy,
            DataProtectionScope.CurrentUser);
          return CreateCookieContainer(Serialization.FromJson<List<StoredCookie>>(Encoding.UTF8.GetString(decryptedCookies)));
        }
        catch (Exception e)
        {
          logger.Error(e, "Failed to load saved PlayStation authentication cookies.");
        }
      }

      var legacyCookies = ReadLegacyCookiesFromDisk();
      if (legacyCookies != null)
      {
        if (WriteCookiesToDisk(legacyCookies))
        {
          File.Delete(legacyTokenPath);
        }

        return legacyCookies;
      }

      return new CookieContainer();
    }

    private CookieContainer ReadLegacyCookiesFromDisk()
    {
      if (!File.Exists(legacyTokenPath))
      {
        return null;
      }

      try
      {
        using (var stream = File.Open(legacyTokenPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          return new BinaryFormatter().Deserialize(stream) as CookieContainer;
        }
      }
      catch (Exception e)
      {
        logger.Error(e, "Failed to import legacy PlayStation authentication cookies.");
        return null;
      }
    }

    private static List<StoredCookie> GetStoredCookies(CookieContainer cookieJar)
    {
      var cookies = new List<StoredCookie>();
      var cookieKeys = new HashSet<string>(StringComparer.Ordinal);

      foreach (var uri in cookieUris)
      {
        foreach (Cookie cookie in cookieJar.GetCookies(uri))
        {
          var key = string.Join("\n", cookie.Domain, cookie.Path, cookie.Name);
          if (!cookieKeys.Add(key))
          {
            continue;
          }

          cookies.Add(new StoredCookie
          {
            Domain = cookie.Domain,
            Path = cookie.Path,
            Name = cookie.Name,
            Value = cookie.Value,
            Expires = cookie.Expires == DateTime.MinValue ? null : (DateTime?)cookie.Expires,
            Secure = cookie.Secure,
            HttpOnly = cookie.HttpOnly
          });
        }
      }

      return cookies;
    }

    private static CookieContainer CreateCookieContainer(IEnumerable<StoredCookie> storedCookies)
    {
      var cookieContainer = new CookieContainer();
      if (storedCookies == null)
      {
        return cookieContainer;
      }

      foreach (var storedCookie in storedCookies)
      {
        if (string.IsNullOrEmpty(storedCookie?.Name) || string.IsNullOrEmpty(storedCookie.Domain))
        {
          continue;
        }

        try
        {
          var cookie = new Cookie(
            storedCookie.Name,
            storedCookie.Value ?? string.Empty,
            string.IsNullOrEmpty(storedCookie.Path) ? "/" : storedCookie.Path,
            storedCookie.Domain)
          {
            Secure = storedCookie.Secure,
            HttpOnly = storedCookie.HttpOnly
          };
          if (storedCookie.Expires.HasValue)
          {
            cookie.Expires = storedCookie.Expires.Value;
          }

          cookieContainer.Add(cookie);
        }
        catch (CookieException e)
        {
          logger.Warn(e, "Skipping an invalid saved PlayStation authentication cookie.");
        }
      }

      return cookieContainer;
    }

    private bool HasSavedCookies()
    {
      return File.Exists(cookiesPath) || File.Exists(legacyTokenPath);
    }

    private class StoredCookie
    {
      public string Domain { get; set; }
      public string Path { get; set; }
      public string Name { get; set; }
      public string Value { get; set; }
      public DateTime? Expires { get; set; }
      public bool Secure { get; set; }
      public bool HttpOnly { get; set; }
    }

    private async Task<bool> GetMobileToken(CancellationToken cancellationToken = default(CancellationToken))
    {
      cancellationToken.ThrowIfCancellationRequested();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        string mobileCode;
        try
        {
          mobileCode = await GetMobileAuthorizationCode(httpClient, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception e)
        {
          logger.Info(e, "Failed to obtain a PlayStation mobile authorization code. Trying to refresh cookies from NPSSO.");
          if (!TryRefreshCookies())
          {
            return false;
          }
          CopyCookies(ReadCookiesFromDisk(), cookieContainer);

          try
          {
            mobileCode = await GetMobileAuthorizationCode(httpClient, cancellationToken);
          }
          catch (OperationCanceledException)
          {
            throw;
          }
          catch (Exception retryException)
          {
            logger.Warn(retryException, "Failed to obtain a PlayStation mobile authorization code after refreshing cookies from NPSSO.");
            return false;
          }
        }

        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, mobileTokenUrl))
        {
          requestMessage.Content = new FormUrlEncodedContent(new[]
          {
            new KeyValuePair<string, string>("code", mobileCode),
            new KeyValuePair<string, string>("redirect_uri", "com.scee.psxandroid.scecompcall://redirect"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("token_format", "jwt")
          });
          requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", mobileTokenAuth);

          using (var mobileTokenResponse = await httpClient.SendAsync(requestMessage, cancellationToken))
          {
            var strResponse = await GetSuccessfulResponseContent(mobileTokenResponse, "the mobile token request", cancellationToken);
            mobileToken = Serialization.FromJson<MobileTokens>(strResponse);
            if (string.IsNullOrEmpty(mobileToken?.access_token))
            {
              throw new InvalidDataException("The PlayStation mobile token response did not contain an access token.");
            }
          }
        }

        return true;
      }
    }

    private static async Task<string> GetMobileAuthorizationCode(HttpClient httpClient, CancellationToken cancellationToken)
    {
      using (var response = await httpClient.GetAsync(mobileCodeUrl, cancellationToken))
      {
        var mobileCode = HttpUtility.ParseQueryString(response.Headers.Location?.Query)["code"];
        if (!string.IsNullOrEmpty(mobileCode))
        {
          return mobileCode;
        }

        var responseContent = await ReadResponseContent(response, cancellationToken);
        throw new InvalidDataException("The PlayStation mobile authorization response did not contain a redirect code" + GetResponseDetails(responseContent) + ".");
      }
    }

    public void ClearAuthentication()
    {
      mobileToken = null;
      DeleteCookieStore(cookiesPath);
      DeleteCookieStore(legacyTokenPath);
    }

    public async Task CheckAuthentication(CancellationToken cancellationToken = default(CancellationToken))
    {
      cancellationToken.ThrowIfCancellationRequested();
      var npsso = psnLibrary.SettingsViewModel.Settings.Npsso;
      if (!HasSavedCookies() && string.IsNullOrWhiteSpace(npsso))
      {
        throw new Exception("User is not authenticated: no saved cookies or NPSSO token found.");
      }

      if (!await GetIsUserLoggedIn(cancellationToken))
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryRefreshCookies() || !await GetIsUserLoggedIn(cancellationToken))
        {
          throw new Exception("User is not authenticated.");
        }
      }

      if (mobileToken == null && !await GetMobileToken(cancellationToken))
      {
        throw new Exception("User is not authenticated.");
      }
    }

    public async Task<List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title>> GetPlayedTitles(CancellationToken cancellationToken = default(CancellationToken))
    {
      cancellationToken.ThrowIfCancellationRequested();
      var titles = new List<PlayedTitlesResponseData.PlayedTitlesRetrieve.Title>();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-apollo-operation-name", "pn_psn");
        using (var response = await httpClient.GetAsync(playedListUrl, cancellationToken))
        {
          var strResponse = await GetSuccessfulResponseContent(response, "the recently played games request", cancellationToken);
          var titlesPart = Serialization.FromJson<PlayedTitles>(strResponse);
          var games = titlesPart?.data?.gameLibraryTitlesRetrieve?.games;
          if (games == null)
          {
            throw new InvalidDataException("The PlayStation recently played games response did not contain a games list.");
          }

          titles.AddRange(games);
        }
      }

      return titles;
    }

    public async Task<List<AccountTitlesResponseData.AccountTitlesRetrieve.Title>> GetAccountTitles(CancellationToken cancellationToken = default(CancellationToken))
    {
      var titles = new List<AccountTitlesResponseData.AccountTitlesRetrieve.Title>();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-apollo-operation-name", "pn_psn");
        var offset = 0;
        while (true)
        {
          cancellationToken.ThrowIfCancellationRequested();
          using (var response = await httpClient.GetAsync(gameListUrl.Format(offset, pageRequestLimit), cancellationToken))
          {
            var strResponse = await GetSuccessfulResponseContent(response, "the purchased games request", cancellationToken);
            var titlesPart = Serialization.FromJson<AccountTitles>(strResponse);
            var purchasedTitles = titlesPart?.data?.purchasedTitlesRetrieve;
            if (purchasedTitles?.games == null || purchasedTitles.pageInfo == null)
            {
              throw new InvalidDataException("The PlayStation purchased games response did not contain paging information.");
            }

            titles.AddRange(purchasedTitles.games);
            if (purchasedTitles.pageInfo.isLast)
            {
              break;
            }

            var nextOffset = purchasedTitles.pageInfo.offset + purchasedTitles.pageInfo.size;
            if (nextOffset <= offset)
            {
              throw new InvalidDataException("The PlayStation purchased games response did not advance its page offset.");
            }

            offset = nextOffset;
          }
        }
      }

      return titles;
    }

    public async Task<List<PlayedTitlesMobile.PlayedTitleMobile>> GetPlayedTitlesMobile(CancellationToken cancellationToken = default(CancellationToken))
    {
      var titles = new List<PlayedTitlesMobile.PlayedTitleMobile>();
      EnsureMobileToken();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        int? offset = 0;
        do
        {
          cancellationToken.ThrowIfCancellationRequested();
          using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, playedMobileListUrl.Format(offset)))
          {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
            using (var response = await httpClient.SendAsync(requestMessage, cancellationToken))
            {
              var strResponse = await GetSuccessfulResponseContent(response, "the mobile recently played games request", cancellationToken);
              var titlesPart = Serialization.FromJson<PlayedTitlesMobile>(strResponse);
              if (titlesPart?.titles == null)
              {
                throw new InvalidDataException("The PlayStation mobile recently played games response did not contain a games list.");
              }

              titles.AddRange(titlesPart.titles);
              offset = titlesPart.nextOffset;
            }
          }
        } while (offset != null);
      }

      return titles;
    }

    public async Task<List<TrophyTitleMobile>> GetTrohpiesMobile(CancellationToken cancellationToken = default(CancellationToken))
    {
      var titles = new List<TrophyTitleMobile>();
      EnsureMobileToken();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        int? offset = 0;
        do
        {
          cancellationToken.ThrowIfCancellationRequested();
          using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, trophiesMobileUrl.Format(offset)))
          {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
            using (var response = await httpClient.SendAsync(requestMessage, cancellationToken))
            {
              var strResponse = await GetSuccessfulResponseContent(response, "the trophy list request", cancellationToken);
              var titlesPart = Serialization.FromJson<TrophyTitlesMobile>(strResponse);
              if (titlesPart?.trophyTitles == null)
              {
                throw new InvalidDataException("The PlayStation trophy list response did not contain trophy titles.");
              }

              titles.AddRange(titlesPart.trophyTitles);
              offset = titlesPart.nextOffset;
            }
          }
        } while (offset != null);
      }

      return titles;
    }

    public async Task<List<TrophyTitlesWithIdsMobile.TrophyTitleWithIdsMobile>> GetTrohpiesWithIdsMobile(string[] titleIdsArray, CancellationToken cancellationToken = default(CancellationToken))
    {
      var titles = new List<TrophyTitlesWithIdsMobile.TrophyTitleWithIdsMobile>();
      if (titleIdsArray == null || titleIdsArray.Length == 0)
      {
        return titles;
      }

      EnsureMobileToken();
      var cookieContainer = ReadCookiesFromDisk();
      using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
      using (var httpClient = new HttpClient(handler))
      {
        const int querySize = 5;
        var offset = 0;
        do
        {
          cancellationToken.ThrowIfCancellationRequested();
          using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, trophiesWithIdsMobileUrl.Format(string.Join(",", titleIdsArray.Skip(offset).Take(querySize)))))
          {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
            using (var response = await httpClient.SendAsync(requestMessage, cancellationToken))
            {
              var strResponse = await GetSuccessfulResponseContent(response, "the trophy details request", cancellationToken);
              var titlesPart = Serialization.FromJson<TrophyTitlesWithIdsMobile>(strResponse);
              if (titlesPart?.titles == null)
              {
                throw new InvalidDataException("The PlayStation trophy details response did not contain trophy titles.");
              }

              titles.AddRange(titlesPart.titles);
              offset += querySize;
            }
          }
        } while (offset < titleIdsArray.Length);
      }

      return titles;
    }

    private bool TryRefreshCookies()
    {
      if (!TryGetNpsso(psnLibrary.SettingsViewModel.Settings.Npsso, out var npsso, out var error))
      {
        logger.Warn($"The configured NPSSO input is invalid: {error}");
        return false;
      }

      if (string.IsNullOrWhiteSpace(npsso))
      {
        return false;
      }

      try
      {
        using (var webView = api.WebViews.CreateOffscreenView())
        {
          webView.SetCookies("https://ca.account.sony.com", new Playnite.SDK.HttpCookie
          {
            Domain = "ca.account.sony.com",
            Value = npsso,
            Name = "npsso",
            Path = "/"
          });
          webView.NavigateAndWait(loginUrl);
          return DumpCookies(webView.GetCookies());
        }
      }
      catch (Exception e)
      {
        logger.Error(e, "Failed to refresh PlayStation authentication cookies from NPSSO.");
        return false;
      }
    }

    public async Task<bool> GetIsUserLoggedIn(CancellationToken cancellationToken = default(CancellationToken))
    {
      var npsso = psnLibrary.SettingsViewModel.Settings.Npsso;
      if (!HasSavedCookies() && string.IsNullOrWhiteSpace(npsso))
      {
        return false;
      }

      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        var cookieContainer = ReadCookiesFromDisk();
        using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
        using (var httpClient = new HttpClient(handler))
        {
          httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-apollo-operation-name", "pn_psn");
          using (var response = await httpClient.GetAsync(gameListUrl.Format(0, 24), cancellationToken))
          {
            if (!response.IsSuccessStatusCode)
            {
              return false;
            }

            var strResponse = await ReadResponseContent(response, cancellationToken);
            if (Serialization.TryFromJson<AccountTitlesErrorResponse>(strResponse, out var error) && error?.data?.purchasedTitlesRetrieve == null)
            {
              return false;
            }

            return Serialization.TryFromJson<AccountTitles>(strResponse, out var accountTitles) && accountTitles?.data?.purchasedTitlesRetrieve != null;
          }
        }
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception e) when (!Debugger.IsAttached)
      {
        logger.Error(e, "Failed to check if user is authenticated into PSN.");
        return false;
      }
    }

    private void EnsureMobileToken()
    {
      if (string.IsNullOrEmpty(mobileToken?.access_token))
      {
        throw new InvalidOperationException("A PlayStation mobile token is required before loading mobile game data.");
      }
    }

    private static async Task<string> GetSuccessfulResponseContent(HttpResponseMessage response, string requestDescription, CancellationToken cancellationToken)
    {
      var content = await ReadResponseContent(response, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        throw new HttpRequestException("PlayStation returned " + (int)response.StatusCode + " (" + response.ReasonPhrase + ") for " + requestDescription + GetResponseDetails(content) + ".");
      }

      return content;
    }

    private static async Task<string> ReadResponseContent(HttpResponseMessage response, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var content = response?.Content == null ? string.Empty : await response.Content.ReadAsStringAsync();
      cancellationToken.ThrowIfCancellationRequested();
      return content;
    }

    private static string GetResponseDetails(string content)
    {
      if (string.IsNullOrWhiteSpace(content))
      {
        return string.Empty;
      }

      var compactContent = string.Join(" ", content.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
      return ": " + compactContent.Substring(0, Math.Min(compactContent.Length, 300));
    }

    private static void DeleteCookieStore(string path)
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }

    private static void CopyCookies(CookieContainer source, CookieContainer target)
    {
      foreach (var storedCookie in GetStoredCookies(source))
      {
        try
        {
          target.Add(new Cookie(
            storedCookie.Name,
            storedCookie.Value ?? string.Empty,
            string.IsNullOrEmpty(storedCookie.Path) ? "/" : storedCookie.Path,
            storedCookie.Domain)
          {
            Secure = storedCookie.Secure,
            HttpOnly = storedCookie.HttpOnly,
            Expires = storedCookie.Expires ?? DateTime.MinValue
          });
        }
        catch (CookieException e)
        {
          logger.Warn(e, "Skipping an invalid refreshed PlayStation authentication cookie.");
        }
      }
    }
  }
}
