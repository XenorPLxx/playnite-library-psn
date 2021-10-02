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
        private readonly PSNLibrary library;
        private readonly string tokenPath;
        private const int pageRequestLimit = 100;
        private const string loginUrl = @"https://web.np.playstation.com/api/session/v1/signin?redirect_uri=https://io.playstation.com/central/auth/login%3FpostSignInURL=https://www.playstation.com/home%26cancelURL=https://www.playstation.com/home&smcid=web:pdc";

        private const string loginTokenUrl = @"https://ca.account.sony.com/api/v1/oauth/authorize?response_type=token&scope=capone:report_submission,kamaji:game_list,kamaji:get_account_hash,user:account.get,user:account.profile.get,kamaji:social_get_graph,kamaji:ugc:distributor,user:account.identityMapper,kamaji:music_views,kamaji:activity_feed_get_feed_privacy,kamaji:activity_feed_get_news_feed,kamaji:activity_feed_submit_feed_story,kamaji:activity_feed_internal_feed_submit_story,kamaji:account_link_token_web,kamaji:ugc:distributor_web,kamaji:url_preview&client_id=656ace0b-d627-47e6-915c-13b259cd06b2&redirect_uri=https://my.playstation.com/auth/response.html?requestID=iframe_request_ecd7cd01-27ad-4851-9c0d-0798c1a65e53&baseUrl=/&targetOrigin=https://my.playstation.com&prompt=none";
        private const string tokenUrl = @"https://ca.account.sony.com/api/v1/oauth/authorize?response_type=token&scope=capone:report_submission,kamaji:game_list,kamaji:get_account_hash,user:account.get,user:account.profile.get,kamaji:social_get_graph,kamaji:ugc:distributor,user:account.identityMapper,kamaji:music_views,kamaji:activity_feed_get_feed_privacy,kamaji:activity_feed_get_news_feed,kamaji:activity_feed_submit_feed_story,kamaji:activity_feed_internal_feed_submit_story,kamaji:account_link_token_web,kamaji:ugc:distributor_web,kamaji:url_preview&client_id=656ace0b-d627-47e6-915c-13b259cd06b2&redirect_uri=https://my.playstation.com/auth/response.html?requestID=iframe_request_b0f09e04-8206-49be-8be6-b2cfe05249e2&baseUrl=/&targetOrigin=https://my.playstation.com&prompt=none";
        private const string gameListUrl = @"https://gamelist.api.playstation.com/v1/users/me/titles?type=owned,played&app=richProfile&sort=-lastPlayedDate&iw=240&ih=240&fields=@default&limit={0}&offset={1}&npLanguage=en";
        private const string trophiesUrl = @"https://us-tpy.np.community.playstation.net/trophy/v1/trophyTitles?fields=@default,trophyTitleSmallIconUrl&platform=PS3,PS4,PSVITA&limit={0}&offset={1}&npLanguage=en";
        private const string profileUrl = @"https://us-prof.np.community.playstation.net/userProfile/v1/users/me/profile2";
        private const string downloadListUrl = @"https://store.playstation.com/en/download/list";
        private const string profileLandingUrl = @"https://my.playstation.com/whatsnew";

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

            //var cookies = getCookies();

            //if (cookies.Count() == 0)
            //{
            //    logger.Error("Failed to get cookies for Playstation Services Network.");
            //}

            dumpCookies();

            return;
            
            //getExcahngeToken(cookies);

            //if (apiRedirectContent.IsNullOrEmpty())
            //{
            //    return;
            //}

            //var sid = Serialization.FromJson<ApiRedirectResponse>(apiRedirectContent).sid;
            //FileSystem.DeleteFile(tokenPath);
            //var exchangeKey = getExcahngeToken(sid);

            //using (var httpClient = new HttpClient())
            //{
            //    httpClient.DefaultRequestHeaders.Clear();
            //    httpClient.DefaultRequestHeaders.Add("Authorization", "basic " + authEcodedString);
            //    using (var content = new StringContent($"grant_type=exchange_code&exchange_code={exchangeKey}&token_type=eg1"))
            //    {
            //        content.Headers.Clear();
            //        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            //        var response = httpClient.PostAsync(oauthUrl, content).GetAwaiter().GetResult();
            //        var respContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            //        FileSystem.CreateDirectory(Path.GetDirectoryName(tokenPath));
            //        Encryption.EncryptToFile(
            //            tokenPath,
            //            respContent,
            //            Encoding.UTF8,
            //            WindowsIdentity.GetCurrent().User.Value);
            //    }
            //}
        }

        private IEnumerable<Playnite.SDK.HttpCookie> dumpCookies()
        {
            var view = api.WebViews.CreateView(1000, 800);
 
            var cookies = view.GetCookies()?.Where(x => x.Domain == ".playstation.com");


            var cookieContainer = new CookieContainer();
            foreach (var cookie in cookies)
            {
                if (cookie.Domain == ".playstation.com")
                    cookieContainer.Add(new Uri("https://web.np.playstation.com"), new Cookie(cookie.Name, cookie.Value));
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

        private string getExcahngeToken(IEnumerable<Playnite.SDK.HttpCookie> cookies)
        {
            var cookieContainer = ReadCookiesFromDisk();
            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (var httpClient = new HttpClient(handler))
            {
                foreach (var cookie in cookies)
                {
                    if (cookie.Domain == ".playstation.com")
                        cookieContainer.Add(new Uri("https://web.np.playstation.com"), new Cookie(cookie.Name, cookie.Value));
                }
                //httpClient.DefaultRequestHeaders.Clear();
                //httpClient.DefaultRequestHeaders.Add("X-Epic-Event-Action", "login");
                //httpClient.DefaultRequestHeaders.Add("X-Epic-Event-Category", "login");
                //httpClient.DefaultRequestHeaders.Add("X-Epic-Strategy-Flags", "");
                //httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                //httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0");

                //httpClient.GetAsync(@"https://www.epicgames.com/id/api/set-sid?sid=" + sid).GetAwaiter().GetResult();
                string gameList = "https://web.np.playstation.com/api/graphql/v1/op" +
                "?operationName=getPurchasedGameList" +
                "&variables={\"isActive\":true,\"platform\":[\"ps3\",\"ps4\",\"ps5\"],\"start\":0,\"size\":100,\"subscriptionService\":\"NONE\"}" +
                "&extensions={\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2\"}}";

                //"https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables=%7B%22isActive%22%3Atrue%2C%22platform%22%3A%5B%22ps4%22%2C%22ps5%22%5D%2C%22size%22%3A24%2C%22start%22%3A0%2C%22sortBy%22%3A%22ACTIVE_DATE%22%2C%22sortDirection%22%3A%22desc%22%2C%22subscriptionService%22%3A%22NONE%22%7D&extensions=%7B%22persistedQuery%22%3A%7B%22version%22%3A1%2C%22sha256Hash%22%3A%222c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2%22%7D%7D"

                //"https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList%22&variables=%7B%7B%22isActive%22:true,%22platform%22:[%22ps3%22,%22ps4%22,%22ps5%22],%22start%22:0,%22size%22:100,%22subscriptionService%22:%22NONE%22%7D%7D&extensions=%7B%7B%22persistedQuery%22:%7B%7B%22version%22:1,%22sha256Hash%22:%222c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2%22%7D%7D%7D%7D"

                //https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={"isActive":true,"platform":["ps4","ps5"],"size":24,"start":0,"sortBy":"ACTIVE_DATE","sortDirection":"desc","subscriptionService":"NONE"}&extensions={"persistedQuery":{"version":1,"sha256Hash":"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2"}}

                //https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={"isActive":true,"platform":["ps4","ps5"],"start":0,"size":100,"subscriptionService":"NONE"}}&extensions=                                            {"persistedQuery":{"version":1,"sha256Hash":"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2"}}

                var resp = httpClient.GetAsync(@gameList).GetAwaiter().GetResult();
                var respSteam = resp.Content.ReadAsStringAsync();
                //var cookies = resp.Headers.Single(header => header.Key == "Set-Cookie").Value;
                //if (cookies != null)
                //{
                //    //var match = Regex.Match(cookies.First(), @"=(.+);");
                //    //var xsrf = match.Groups[1].Value;
                //    //httpClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", xsrf);
                //    //var country = "US";
                //    //try
                //    //{
                //    //    country = System.Globalization.CultureInfo.CurrentCulture.Name.Split(new char[] { '-' }).Last();
                //    //}
                //    //catch (Exception e)
                //    //{
                //    //    logger.Error(e, $"Failed to get country for auth request.");
                //    //}

                //    //cookieContainer.Add(new Uri("https://www.epicgames.com"), new Cookie("EPIC_COUNTRY", country));
                //    //resp = httpClient.PostAsync("https://www.epicgames.com/id/api/exchange/generate", null).GetAwaiter().GetResult();
                //    //var respContent = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();                  
                //    return "cookies";
                //}
                //else
                //{
                    return null;
                //}
            }
        }

        //public void Login()
        //{
        //    using (var webView = library.PlayniteApi.WebViews.CreateView(540, 670))
        //    {
        //        var callbackUrl = string.Empty;
        //        webView.LoadingChanged += (_, __) =>
        //        {
        //            var address = webView.GetCurrentAddress();
        //            if (address == profileLandingUrl)
        //            {
        //                webView.Navigate(tokenUrl);
        //            }
        //            else if (address.Contains("access_token="))
        //            {
        //                callbackUrl = address;
        //                webView.Close();
        //            }
        //            else if (address.EndsWith("/friends"))
        //            {
        //                webView.Navigate(loginTokenUrl);
        //            }
        //        };

        //        if (File.Exists(tokenPath))
        //        {
        //            File.Delete(tokenPath);
        //        }

        //        webView.DeleteDomainCookies(".playstation.com");
        //        webView.DeleteDomainCookies(".sonyentertainmentnetwork.com");
        //        webView.DeleteDomainCookies(".sony.com");
        //        webView.Navigate(loginUrl);
        //        webView.OpenDialog();

        //        if (!callbackUrl.IsNullOrEmpty())
        //        {
        //            var rediUri = new Uri(callbackUrl);
        //            var fragments = HttpUtility.ParseQueryString(rediUri.Fragment);
        //            var token = fragments["#access_token"];
        //            FileSystem.WriteStringToFile(tokenPath, token);
        //        }
        //    }
        //}

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
            }
        }

        public async Task<List<DownloadListEntitlement>> GetDownloadList()
        {
            await CheckAuthentication();

            using (var webView = library.PlayniteApi.WebViews.CreateOffscreenView())
            {
                var loadComplete = new AutoResetEvent(false);
                var items = new List<DownloadListEntitlement>();
                var processingDownload = false;

                webView.LoadingChanged += async (_, e) =>
                {
                    var address = webView.GetCurrentAddress();
                    if (address?.EndsWith("download/list") == true && !e.IsLoading)
                    {
                        if (processingDownload)
                        {
                            return;
                        }

                        processingDownload = true;
                        var numberOfTries = 0;
                        while (numberOfTries < 6)
                        {
                            // Don't know how to reliable tell if the data are ready because they are laoded post page load
                            await Task.Delay(10000);
                            if (!webView.CanExecuteJavascriptInMainFrame)
                            {
                                logger.Warn("PSN JS execution not ready yet.");
                                continue;
                            }

                            // Need to use this hack since the data we need are stored in browser's local storage
                            // Based on https://github.com/RePod/psdle/blob/master/psdle.js
                            var res = await webView.EvaluateScriptAsync(@"JSON.stringify(Ember.Application.NAMESPACES_BY_ID['valkyrie-storefront'].__container__.lookup('service:macross-brain').macrossBrainInstance.getEntitlementStore().getAllEntitlements()._result)");
                            var strRes = (string)res.Result;
                            if (strRes.IsNullOrEmpty())
                            {
                                numberOfTries++;
                                continue;
                            }

                            try
                            {
                                items = Serialization.FromJson<List<DownloadListEntitlement>>(strRes);
                            }
                            catch (Exception exc)
                            {
                                logger.Error(exc, "Failed to deserialize PSN's download list.");
                                logger.Debug(strRes);
                            }

                            loadComplete.Set();
                            break;
                        }
                    }
                };

                webView.Navigate(downloadListUrl);
                loadComplete.WaitOne(60000);
                return items;
            }
        }

        private async Task<T> SendPageRequest<T>(HttpClient client, string url, int offset) where T : class, new()
        {
            var strResponse = await client.GetStringAsync(url.Format(pageRequestLimit, offset));
            return Serialization.FromJson<T>(strResponse);
        }

        public async Task<List<TrophyTitles.TrophyTitle>> GetThropyTitles()
        {
            await CheckAuthentication();
            var token = GetStoredToken();
            var titles = new List<TrophyTitles.TrophyTitle>();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var itemCount = 0;
                var offset = -pageRequestLimit;

                do
                {
                    var response = await SendPageRequest<TrophyTitles>(client, trophiesUrl,  offset + pageRequestLimit);
                    itemCount = response.totalResults;
                    offset = response.offset;
                    titles.AddRange(response.trophyTitles);
                }
                while (offset + pageRequestLimit < itemCount);
            }

            return titles;
        }

        public async Task<List<AccountTitles.Data.TitlesRetrieve.Title>> GetAccountTitles()
        {
            var titles = new List<AccountTitles.Data.TitlesRetrieve.Title>();

            var cookieContainer = ReadCookiesFromDisk();
            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (var httpClient = new HttpClient(handler))
            {
                //string gameList = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={\"isActive\":true,\"platform\":[\"ps3\",\"ps4\",\"ps5\"],\"start\":{0},\"size\":{1},\"subscriptionService\":\"NONE\"}&extensions={\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2\"}}";
                string gameList = "https://web.np.playstation.com/api/graphql/v1/op?operationName=getPurchasedGameList&variables={{\"isActive\":true,\"platform\":[\"ps3\",\"ps4\",\"ps5\"],\"start\":{0},\"size\":{1},\"subscriptionService\":\"NONE\"}}&extensions={{\"persistedQuery\":{{\"version\":1,\"sha256Hash\":\"2c045408b0a4d0264bb5a3edfed4efd49fb4749cf8d216be9043768adff905e2\"}}}}";

                var itemCount = 0;
                var offset = -pageRequestLimit;

                do
                {
                    object[] args = { offset, pageRequestLimit };
                    var resp = httpClient.GetAsync(gameList.Format(offset + pageRequestLimit, pageRequestLimit)).GetAwaiter().GetResult();
                    var strResponse = await resp.Content.ReadAsStringAsync();
                    var titles_part = Serialization.FromJson<AccountTitles>(strResponse);
                    titles.AddRange(titles_part.data.purchasedTitlesRetrieve.games);
                    offset = titles_part.data.purchasedTitlesRetrieve.pageInfo.offset;
                    itemCount = titles_part.data.purchasedTitlesRetrieve.pageInfo.totalCount;
                } while (offset + pageRequestLimit < itemCount);


            }

            //await CheckAuthentication();
            //var token = GetStoredToken();
            //using (var client = new HttpClient())
            //{
            //    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            //    var itemCount = 0;
            //    var offset = -pageRequestLimit;

            //    do
            //    {
            //        var response = await SendPageRequest<AccountTitles>(client, gameListUrl, offset + pageRequestLimit);
            //        itemCount = response.totalResults;
            //        offset = response.start;
            //        titles.AddRange(response.titles);
            //    }
            //    while (offset + pageRequestLimit < itemCount);
            //}

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

        private string RefreshToken()
        {
            logger.Debug("Trying to refresh PSN token.");
            if (File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
            }

            var callbackUrl = string.Empty;
            using (var webView = library.PlayniteApi.WebViews.CreateOffscreenView())
            {
                webView.LoadingChanged += (_, __) =>
                {
                    var address = webView.GetCurrentAddress();
                    if (address.Contains("access_token="))
                    {
                        callbackUrl = address;
                    }
                };

                webView.NavigateAndWait(profileLandingUrl);
                webView.NavigateAndWait(tokenUrl);

                if (!callbackUrl.IsNullOrEmpty())
                {
                    var rediUri = new Uri(callbackUrl);
                    var fragments = HttpUtility.ParseQueryString(rediUri.Fragment);
                    var token = fragments["#access_token"];
                    FileSystem.WriteStringToFile(tokenPath, token);
                    return token;
                }
            }

            return string.Empty;
        }

        public async Task<bool> GetIsUserLoggedIn()
        {
            //var cookies = getCookies();

            //if (cookies.Count() > 0)
            //{
            //    return true;
            //}

            return await new Task<bool>(() => false);

            //if (!File.Exists(tokenPath))
            //{
            //    return false;
            //}

            //try
            //{
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
            //}
            //catch (Exception e) when (!Debugger.IsAttached)
            //{
            //    logger.Error(e, "Failed to check if user is authenticated into PSN.");
            //    return false;
            //}
        }
    }
}
