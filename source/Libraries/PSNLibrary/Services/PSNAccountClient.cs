using Playnite.Common;
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
    public class PSNAccountClient
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
        private const string mobileCodeUrl = @"https://ca.account.sony.com/api/authz/v3/oauth/authorize?access_type=offline&client_id=ac8d161a-d966-4728-b0ea-ffec22f69edc&redirect_uri=com.playstation.PlayStationApp%3A%2F%2Fredirect&response_type=code&scope=psn%3Amobile.v1%20psn%3Aclientapp";
        private const string mobileTokenUrl = "https://ca.account.sony.com/api/authz/v3/oauth/token";
        private const string mobileTokenAuth = "YWM4ZDE2MWEtZDk2Ni00NzI4LWIwZWEtZmZlYzIyZjY5ZWRjOkRFaXhFcVhYQ2RYZHdqMHY=";
        private const string playedMobileListUrl = "https://m.np.playstation.net/api/gamelist/v2/users/me/titles?categories=ps4_game,ps5_native_game&limit=250&offset={0}";

        //private const string loginTokenUrl = @"https://ca.account.sony.com/api/v1/oauth/authorize?response_type=token&scope=capone:report_submission,kamaji:game_list,kamaji:get_account_hash,user:account.get,user:account.profile.get,kamaji:social_get_graph,kamaji:ugc:distributor,user:account.identityMapper,kamaji:music_views,kamaji:activity_feed_get_feed_privacy,kamaji:activity_feed_get_news_feed,kamaji:activity_feed_submit_feed_story,kamaji:activity_feed_internal_feed_submit_story,kamaji:account_link_token_web,kamaji:ugc:distributor_web,kamaji:url_preview&client_id=656ace0b-d627-47e6-915c-13b259cd06b2&redirect_uri=https://my.playstation.com/auth/response.html?requestID=iframe_request_ecd7cd01-27ad-4851-9c0d-0798c1a65e53&baseUrl=/&targetOrigin=https://my.playstation.com&prompt=none";
        //private const string tokenUrl = @"https://ca.account.sony.com/api/v1/oauth/authorize?response_type=token&scope=capone:report_submission,kamaji:game_list,kamaji:get_account_hash,user:account.get,user:account.profile.get,kamaji:social_get_graph,kamaji:ugc:distributor,user:account.identityMapper,kamaji:music_views,kamaji:activity_feed_get_feed_privacy,kamaji:activity_feed_get_news_feed,kamaji:activity_feed_submit_feed_story,kamaji:activity_feed_internal_feed_submit_story,kamaji:account_link_token_web,kamaji:ugc:distributor_web,kamaji:url_preview&client_id=656ace0b-d627-47e6-915c-13b259cd06b2&redirect_uri=https://my.playstation.com/auth/response.html?requestID=iframe_request_b0f09e04-8206-49be-8be6-b2cfe05249e2&baseUrl=/&targetOrigin=https://my.playstation.com&prompt=none";
        //private const string gameListUrl = @"https://gamelist.api.playstation.com/v1/users/me/titles?type=owned,played&app=richProfile&sort=-lastPlayedDate&iw=240&ih=240&fields=@default&limit={0}&offset={1}&npLanguage=en";
        //private const string trophiesUrl = @"https://us-tpy.np.community.playstation.net/trophy/v1/trophyTitles?fields=@default,trophyTitleSmallIconUrl&platform=PS3,PS4,PSVITA&limit={0}&offset={1}&npLanguage=en";
        //private const string profileUrl = @"https://us-prof.np.community.playstation.net/userProfile/v1/users/me/profile2";
        //private const string downloadListUrl = @"https://store.playstation.com/en/download/list";
        //private const string profileLandingUrl = @"https://my.playstation.com/whatsnew";

        public PSNAccountClient(PSNLibrary library, IPlayniteAPI api)
        {
            this.library = library;
            this.api = api;
            tokenPath = Path.Combine(library.GetPluginUserDataPath(), "token.json");
        }

        public void Login()
        {
            var loggedIn = false;
            

            using (var view = api.WebViews.CreateView(580, 700))
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

                view.DeleteDomainCookies(".playstation.com");
                view.Navigate(loginUrl);
                view.OpenDialog();
            }

            if (!loggedIn)
            {
                return;
            }
                  
            dumpCookies();

            //tryToken();

            return;
        }
  
        //private async void tryToken()
        //{
        //    await CheckAuthentication();

        //    var cookieContainer = ReadCookiesFromDisk();
        //    using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
        //    using (var httpClient = new HttpClient(handler))
        //    {
        //        //var resp = httpClient.GetAsync(@"https://m.np.playstation.net/api/trophy/v1/users/me/titles/trophyTitles?npTitleIds=PPSA01802_00").GetAwaiter().GetResult();
        //        //var resp2 = httpClient.GetAsync(@"https://ca.account.sony.com/api/v1/ssocookie").GetAwaiter().GetResult();
        //        var resp = httpClient.GetAsync(@"https://ca.account.sony.com/api/authz/v3/oauth/authorize?access_type=offline&client_id=ac8d161a-d966-4728-b0ea-ffec22f69edc&redirect_uri=com.playstation.PlayStationApp%3A%2F%2Fredirect&response_type=code&scope=psn%3Amobile.v1%20psn%3Aclientapp").GetAwaiter().GetResult();
        //        var code = HttpUtility.ParseQueryString(resp.Headers.Location.Query)["code"];

        //        HttpRequestMessage requestMessage2 = new HttpRequestMessage(new HttpMethod("post"), "https://ca.account.sony.com/api/authz/v3/oauth/token");
        //        var nvc2 = new List<KeyValuePair<string, string>>();
        //        nvc2.Add(new KeyValuePair<string, string>("code", code));
        //        nvc2.Add(new KeyValuePair<string, string>("redirect_uri", "com.playstation.PlayStationApp://redirect"));
        //        nvc2.Add(new KeyValuePair<string, string>("grant_type", "authorization_code"));
        //        nvc2.Add(new KeyValuePair<string, string>("token_format", "jwt"));

        //        requestMessage2.Content = new FormUrlEncodedContent(nvc2);
        //        requestMessage2.Headers.Authorization = new AuthenticationHeaderValue("Basic", "YWM4ZDE2MWEtZDk2Ni00NzI4LWIwZWEtZmZlYzIyZjY5ZWRjOkRFaXhFcVhYQ2RYZHdqMHY=");
        //        var cont = await requestMessage2.Content.ReadAsStringAsync();
        //        var resp2 = await httpClient.SendAsync(requestMessage2);
        //        var strResponse2 = await resp2.Content.ReadAsStringAsync();
        //        var tokens = Serialization.FromJson<MobileTokens>(strResponse2);

        //        // only communicationId for PS3 and PSVITA titles - also trophies can be shared between platforms
        //        HttpRequestMessage requestMessage3 = new HttpRequestMessage(new HttpMethod("get"), "https://m.np.playstation.net/api/trophy/v1/users/me/trophyTitles");
        //        requestMessage3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.access_token);
        //        var resp3 = await httpClient.SendAsync(requestMessage3);
        //        var strResponse3 = await resp3.Content.ReadAsStringAsync();

        //        // matching games to trophies for 1.0 migration
        //        HttpRequestMessage requestMessage4 = new HttpRequestMessage(new HttpMethod("get"), "https://m.np.playstation.net/api/trophy/v1/users/me/titles/trophyTitles?npTitleIds=PPSA01968_00,PPSA01802_00,PPSA01632_00");
        //        requestMessage4.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.access_token);
        //        var resp4 = await httpClient.SendAsync(requestMessage4);
        //        var strResponse4 = await resp4.Content.ReadAsStringAsync();

        //        // playtimes
        //        HttpRequestMessage requestMessage5 = new HttpRequestMessage(new HttpMethod("get"), "https://m.np.playstation.net/api/gamelist/v2/users/me/titles?categories=ps4_game,ps5_native_game&limit=250&offset=0");
        //        requestMessage5.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.access_token);
        //        var resp5 = await httpClient.SendAsync(requestMessage5);
        //        var strResponse5 = await resp5.Content.ReadAsStringAsync();

        //    }

        //}

        private IEnumerable<Playnite.SDK.HttpCookie> dumpCookies()
        {
            var view = api.WebViews.CreateView(1000, 800);

            var cookies = view.GetCookies(); //?.Where(x => x.Domain == ".playstation.com");


            var cookieContainer = new CookieContainer();
            foreach (var cookie in cookies)
            {
                if (cookie.Domain == ".playstation.com")
                {
                    cookieContainer.Add(new Uri("https://web.np.playstation.com"), new Cookie(cookie.Name, cookie.Value));
                }
                if (cookie.Domain == "ca.account.sony.com")                {

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

        private async Task getMobileToken()
        {
            var cookieContainer = ReadCookiesFromDisk();
            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (var httpClient = new HttpClient(handler))
            {
                var mobileCodeResponse = await httpClient.GetAsync(mobileCodeUrl);
                var mobileCode = HttpUtility.ParseQueryString(mobileCodeResponse.Headers.Location.Query)["code"];

                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("post"), mobileTokenUrl);
                var requestMessageForm = new List<KeyValuePair<string, string>>();
                requestMessageForm.Add(new KeyValuePair<string, string>("code", mobileCode));
                requestMessageForm.Add(new KeyValuePair<string, string>("redirect_uri", "com.playstation.PlayStationApp://redirect"));
                requestMessageForm.Add(new KeyValuePair<string, string>("grant_type", "authorization_code"));
                requestMessageForm.Add(new KeyValuePair<string, string>("token_format", "jwt"));
                requestMessage.Content = new FormUrlEncodedContent(requestMessageForm);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", mobileTokenAuth);

                var mobileTokenResponse = await httpClient.SendAsync(requestMessage);
                var strResponse = await mobileTokenResponse.Content.ReadAsStringAsync();
                mobileToken = Serialization.FromJson<MobileTokens>(strResponse);
            }
        }

        private async Task CheckAuthentication()
        {
            if (!File.Exists(tokenPath))
            {
                throw new Exception("User is not authenticated.");
            }
            else
            {
                if (!await GetIsUserLoggedIn())
                {
                    throw new Exception("User is not authenticated.");
                }
                else
                {
                    if (mobileToken == null)
                    {
                        await getMobileToken();
                    }
                }
            }
        }

        //public async Task<List<DownloadListEntitlement>> GetDownloadList()
        //{
        //    await CheckAuthentication();

        //    using (var webView = library.PlayniteApi.WebViews.CreateOffscreenView())
        //    {
        //        var loadComplete = new AutoResetEvent(false);
        //        var items = new List<DownloadListEntitlement>();
        //        var processingDownload = false;

        //        webView.LoadingChanged += async (_, e) =>
        //        {
        //            var address = webView.GetCurrentAddress();
        //            if (address?.EndsWith("download/list") == true && !e.IsLoading)
        //            {
        //                if (processingDownload)
        //                {
        //                    return;
        //                }

        //                processingDownload = true;
        //                var numberOfTries = 0;
        //                while (numberOfTries < 6)
        //                {
        //                    // Don't know how to reliable tell if the data are ready because they are laoded post page load
        //                    await Task.Delay(10000);
        //                    if (!webView.CanExecuteJavascriptInMainFrame)
        //                    {
        //                        logger.Warn("PSN JS execution not ready yet.");
        //                        continue;
        //                    }

        //                    // Need to use this hack since the data we need are stored in browser's local storage
        //                    // Based on https://github.com/RePod/psdle/blob/master/psdle.js
        //                    var res = await webView.EvaluateScriptAsync(@"JSON.stringify(Ember.Application.NAMESPACES_BY_ID['valkyrie-storefront'].__container__.lookup('service:macross-brain').macrossBrainInstance.getEntitlementStore().getAllEntitlements()._result)");
        //                    var strRes = (string)res.Result;
        //                    if (strRes.IsNullOrEmpty())
        //                    {
        //                        numberOfTries++;
        //                        continue;
        //                    }

        //                    try
        //                    {
        //                        items = Serialization.FromJson<List<DownloadListEntitlement>>(strRes);
        //                    }
        //                    catch (Exception exc)
        //                    {
        //                        logger.Error(exc, "Failed to deserialize PSN's download list.");
        //                        logger.Debug(strRes);
        //                    }

        //                    loadComplete.Set();
        //                    break;
        //                }
        //            }
        //        };

        //        webView.Navigate(downloadListUrl);
        //        loadComplete.WaitOne(60000);
        //        return items;
        //    }
        //}

        //private async Task<T> SendPageRequest<T>(HttpClient client, string url, int offset) where T : class, new()
        //{
        //    var strResponse = await client.GetStringAsync(url.Format(pageRequestLimit, offset));
        //    return Serialization.FromJson<T>(strResponse);
        //}

        //public async Task<List<TrophyTitles.TrophyTitle>> GetThropyTitles()
        //{
        //    await CheckAuthentication();
        //    var token = GetStoredToken();
        //    var titles = new List<TrophyTitles.TrophyTitle>();
        //    using (var client = new HttpClient())
        //    {
        //        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        //        var itemCount = 0;
        //        var offset = -pageRequestLimit;

        //        do
        //        {
        //            var response = await SendPageRequest<TrophyTitles>(client, trophiesUrl,  offset + pageRequestLimit);
        //            itemCount = response.totalResults;
        //            offset = response.offset;
        //            titles.AddRange(response.trophyTitles);
        //        }
        //        while (offset + pageRequestLimit < itemCount);
        //    }

        //    return titles;
        //}

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
                //HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("get"), playedMobileListUrl.Format(offset));
                //requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mobileToken.access_token);
                //var resp = await httpClient.SendAsync(requestMessage);
                //var strResponse = await resp.Content.ReadAsStringAsync();

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

        private string GetStoredToken()
        {
            var token = string.Empty;
            if (File.Exists(tokenPath))
            {
                token = File.ReadAllText(tokenPath);
            }

            return token;
        }

        //private string RefreshToken()
        //{
        //    logger.Debug("Trying to refresh PSN token.");
        //    if (File.Exists(tokenPath))
        //    {
        //        File.Delete(tokenPath);
        //    }

        //    var callbackUrl = string.Empty;
        //    using (var webView = library.PlayniteApi.WebViews.CreateOffscreenView())
        //    {
        //        webView.LoadingChanged += (_, __) =>
        //        {
        //            var address = webView.GetCurrentAddress();
        //            if (address.Contains("access_token="))
        //            {
        //                callbackUrl = address;
        //            }
        //        };

        //        webView.NavigateAndWait(profileLandingUrl);
        //        webView.NavigateAndWait(tokenUrl);

        //        if (!callbackUrl.IsNullOrEmpty())
        //        {
        //            var rediUri = new Uri(callbackUrl);
        //            var fragments = HttpUtility.ParseQueryString(rediUri.Fragment);
        //            var token = fragments["#access_token"];
        //            FileSystem.WriteStringToFile(tokenPath, token);
        //            return token;
        //        }
        //    }

        //    return string.Empty;
        //}

        public async Task<bool> GetIsUserLoggedIn()
        {
            //return await new Task<bool>(() => false);

            if (!File.Exists(tokenPath))
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
                //    var token = GetStoredToken();
                //    using (var client = new HttpClient())
                //    {
                //        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                //        var response = await client.GetAsync(profileUrl);
                //        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                //        {
                //            return true;
                //        }
                //        else
                //        {
                //            token = RefreshToken();
                //            if (token.IsNullOrEmpty())
                //            {
                //                return false;
                //            }

                //            client.DefaultRequestHeaders.Remove("Authorization");
                //            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                //            response = await client.GetAsync(profileUrl);
                //            return response.StatusCode == System.Net.HttpStatusCode.OK;
                //        }
                //    }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, "Failed to check if user is authenticated into PSN.");
                return false;
            }
        }
    }
}
