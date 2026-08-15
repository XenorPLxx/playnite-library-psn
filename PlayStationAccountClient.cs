using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml;
using Playnite;

namespace PlayStationLibrary;

public sealed class PlayStationAccountClient
{
    // The NPSSO cookie is exchanged through the OAuth flow of the official PlayStation mobile app.
    // Sony's web SSO endpoint (web.np.playstation.com/api/session/v1/signin) no longer turns an
    // NPSSO cookie into a browser session and answers with error=login_required instead, so the
    // mobile flow is the only one that still works. It also needs no browser at all.
    private const string AuthorizeUrl = "https://ca.account.sony.com/api/authz/v3/oauth/authorize" +
        "?access_type=offline" +
        "&client_id=09515159-7237-4370-9b40-3806e67c0891" +
        "&redirect_uri=com.scee.psxandroid.scecompcall%3A%2F%2Fredirect" +
        "&response_type=code" +
        "&scope=psn%3Amobile.v2.core%20psn%3Aclientapp";
    private const string TokenUrl = "https://ca.account.sony.com/api/authz/v3/oauth/token";
    private const string RedirectUri = "com.scee.psxandroid.scecompcall://redirect";
    private const string TokenScope = "psn:mobile.v2.core psn:clientapp";
    private const string ClientAuthorization = "MDk1MTUxNTktNzIzNy00MzcwLTliNDAtMzgwNmU2N2MwODkxOnVjUGprYTV0bnRCMktxc1A=";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";

    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly IPlayniteApi playniteApi;
    private readonly string tokenStorePath;

    public PlayStationAccountClient(IPlayniteApi playniteApi)
    {
        this.playniteApi = playniteApi;
        tokenStorePath = Path.Combine(playniteApi.UserDataDir, "auth.json");
    }

    public static bool TryGetNpsso(string? value, out string npsso, out string error)
    {
        npsso = string.Empty;
        error = string.Empty;
        var trimmedValue = value?.Trim();
        if (string.IsNullOrEmpty(trimmedValue))
        {
            return true;
        }

        if (!trimmedValue.StartsWith('{'))
        {
            npsso = trimmedValue;
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmedValue);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("npsso", out var npssoElement) ||
                npssoElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(npssoElement.GetString()))
            {
                error = "The ssocookie JSON must contain a non-empty string property named 'npsso'.";
                return false;
            }

            npsso = npssoElement.GetString()!.Trim();
            return true;
        }
        catch (JsonException)
        {
            error = "The NPSSO input is not valid JSON. Paste the raw NPSSO value or a JSON object such as {\"npsso\":\"...\"}.";
            return false;
        }
    }

    public void ClearAuthentication()
    {
        try
        {
            File.Delete(tokenStorePath);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to delete the stored PlayStation refresh token.");
        }
    }

    /// <summary>
    /// Reads the stored token so it can be put back if a check that cleared it turns out to fail.
    /// Returns null when nothing was stored.
    /// </summary>
    public string? TakeAuthentication()
    {
        try
        {
            if (!File.Exists(tokenStorePath))
            {
                return null;
            }

            var stored = File.ReadAllText(tokenStorePath);
            File.Delete(tokenStorePath);
            return stored;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to read the stored PlayStation refresh token.");
            return null;
        }
    }

    /// <summary>Puts back a token taken by <see cref="TakeAuthentication"/>.</summary>
    public void RestoreAuthentication(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        try
        {
            File.WriteAllText(tokenStorePath, stored);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to restore the stored PlayStation refresh token.");
        }
    }

    public async Task<bool> GetIsUserLoggedInAsync(string? npsso, CancellationToken cancellationToken)
    {
        try
        {
            // A usable bearer token is the authentication boundary. Do not make a particular
            // library endpoint the test: Sony may reject one source (for example purchases) while
            // the same valid mobile session can still read play history and trophies.
            using var session = await CreateSessionAsync(npsso, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e) when (!playniteApi.AppInfo.ThrowAllErrors)
        {
            logger.Error(e, "PlayStation authentication check failed.");
            return false;
        }
    }

    /// <summary>
    /// Authenticates once and returns a session that can serve every PlayStation API.
    /// </summary>
    public async Task<PlayStationSession> CreateSessionAsync(string? npsso, CancellationToken cancellationToken, string? locale = null)
    {
        var client = CreateHttpClient();
        try
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept-Language", PlayStationLocales.ToTrophyAcceptLanguage(locale, playniteApi.Settings.Language));
            var accessToken = await GetAccessTokenAsync(client, npsso, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return new PlayStationSession(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        // The authorization step answers with a redirect to a custom scheme that HttpClient cannot
        // follow, so the redirect has to be inspected manually.
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private async Task<string> GetAccessTokenAsync(HttpClient client, string? npsso, CancellationToken cancellationToken)
    {
        if (!TryGetNpsso(npsso, out var parsedNpsso, out var npssoError))
        {
            throw new InvalidOperationException(npssoError);
        }

        var storedTokens = ReadStoredTokens();
        if (!string.IsNullOrWhiteSpace(storedTokens?.RefreshToken) && storedTokens.RefreshTokenExpires > DateTimeOffset.UtcNow)
        {
            try
            {
                return await RedeemTokenAsync(client, cancellationToken, new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = storedTokens.RefreshToken!,
                    ["scope"] = TokenScope,
                    ["token_format"] = "jwt"
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.Info($"The stored PlayStation refresh token was rejected ({e.Message}). Falling back to the NPSSO cookie.");
            }
        }

        if (string.IsNullOrWhiteSpace(parsedNpsso))
        {
            throw new InvalidOperationException(
                "PlayStation is not authenticated. Enter an NPSSO value in the PlayStation library settings.");
        }

        var code = await GetAuthorizationCodeAsync(client, parsedNpsso, cancellationToken);
        return await RedeemTokenAsync(client, cancellationToken, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["token_format"] = "jwt"
        });
    }

    private static async Task<string> GetAuthorizationCodeAsync(HttpClient client, string npsso, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthorizeUrl);
        request.Headers.TryAddWithoutValidation("Cookie", "npsso=" + npsso);
        using var response = await client.SendAsync(request, cancellationToken);

        var location = response.Headers.Location?.OriginalString;
        if (string.IsNullOrEmpty(location))
        {
            throw new InvalidOperationException(
                $"The PlayStation authorization request returned {(int)response.StatusCode} without a redirect.");
        }

        var code = GetQueryValue(location, "code");
        if (!string.IsNullOrEmpty(code))
        {
            return code;
        }

        // Sony reports an expired or rejected NPSSO as error=login_required (error_code 4165).
        var error = GetQueryValue(location, "error");
        if (string.Equals(error, "login_required", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PlayStation rejected the NPSSO value. It has expired or belongs to a signed-out session. " +
                "Sign in at playstation.com again and paste a fresh NPSSO into the settings.");
        }

        throw new InvalidOperationException(
            "The PlayStation authorization response did not contain a code" +
            (string.IsNullOrEmpty(error) ? "." : $" (error: {error})."));
    }

    private async Task<string> RedeemTokenAsync(HttpClient client, CancellationToken cancellationToken, Dictionary<string, string> parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ClientAuthorization);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"The PlayStation token request returned {(int)response.StatusCode} ({response.ReasonPhrase}){PlayStationJson.Describe(responseContent)}.");
        }

        using var document = JsonDocument.Parse(responseContent);
        var accessToken = PlayStationJson.GetString(document.RootElement, "access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("The PlayStation token response did not contain an access token.");
        }

        var refreshToken = PlayStationJson.GetString(document.RootElement, "refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
        {
            WriteStoredTokens(new StoredTokens
            {
                RefreshToken = refreshToken,
                RefreshTokenExpires = DateTimeOffset.UtcNow.AddSeconds(PlayStationJson.GetSeconds(document.RootElement, "refresh_token_expires_in") ?? 0)
            });
        }

        return accessToken;
    }

    private StoredTokens? ReadStoredTokens()
    {
        if (!File.Exists(tokenStorePath))
        {
            return null;
        }

        try
        {
            var protectedContent = File.ReadAllText(tokenStorePath);
            var content = PlayStationSecrets.Unprotect(protectedContent);
            return content == null ? null : JsonSerializer.Deserialize<StoredTokens>(content);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to read the stored PlayStation refresh token.");
            return null;
        }
    }

    private void WriteStoredTokens(StoredTokens tokens)
    {
        try
        {
            var protectedContent = PlayStationSecrets.Protect(JsonSerializer.Serialize(tokens));
            if (protectedContent != null)
            {
                File.WriteAllText(tokenStorePath, protectedContent);
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to store the PlayStation refresh token.");
        }
    }

    private static string? GetQueryValue(string url, string name)
    {
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
        {
            return null;
        }

        foreach (var pair in url[(queryStart + 1)..].Split('&'))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0 && pair[..separator] == name)
            {
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }

    private sealed class StoredTokens
    {
        public string? RefreshToken { get; set; }
        public DateTimeOffset RefreshTokenExpires { get; set; }
    }
}

/// <summary>
/// An authenticated PlayStation session. Each method maps to one of the four independent APIs the
/// library import draws from; callers are expected to tolerate any single one of them failing.
/// </summary>
public sealed class PlayStationSession : IDisposable
{
    private const string GraphQlUrl = "https://web.np.playstation.com/api/graphql/v1/op";
    private const string PurchasedGamesQueryHash = "827a423f6a8ddca4107ac01395af2ec0eafd8396fc7fa204aaf9b7ed2eefa168";
    private const string PlayedGamesQueryHash = "e780a6d8b921ef0c59ec01ea5c5255671272ca0d819edb61320914cf7a78b3ae";
    private const string MobilePlayedTitlesUrl = "https://m.np.playstation.com/api/gamelist/v2/users/me/titles?categories=ps4_game,ps5_native_game&limit={0}&offset={1}";
    private const string MobileTrophyTitlesUrl = "https://m.np.playstation.com/api/trophy/v1/users/me/trophyTitles?limit={0}&offset={1}";
    private const string TrophyMappingUrl = "https://m.np.playstation.com/api/trophy/v1/users/me/titles/trophyTitles?npTitleIds={0}";
    private const string TrophyDefinitionsUrl = "https://m.np.playstation.com/api/trophy/v1/npCommunicationIds/{0}/trophyGroups/all/trophies?npServiceName={1}";
    private const string TrophyProgressUrl = "https://m.np.playstation.com/api/trophy/v1/users/me/npCommunicationIds/{0}/trophyGroups/all/trophies?npServiceName={1}";
    private const string TrophyGroupsUrl = "https://m.np.playstation.com/api/trophy/v1/npCommunicationIds/{0}/trophyGroups?npServiceName={1}";
    /// <summary>Sony rejects more than five ids per mapping request with HTTP 400.</summary>
    public const int TrophyMappingBatchSize = 5;

    /// <summary>Spacing between mapping requests, which are issued five ids at a time.</summary>
    private static readonly TimeSpan TrophyMappingDelay = TimeSpan.FromMilliseconds(200);
    private const int PurchasedPageSize = 100;
    private const int MobilePageSize = 200;
    private const int TrophyPageSize = 250;

    private readonly HttpClient client;

    internal PlayStationSession(HttpClient client)
    {
        this.client = client;
    }

    public void Dispose()
    {
        client.Dispose();
    }

    /// <summary>API 1 of 4: everything the account owns. Supplies the canonical game names.</summary>
    public async Task<List<PlayStationPurchasedTitle>> GetPurchasedTitlesAsync(CancellationToken cancellationToken, int? pageSize = null)
    {
        var titles = new List<PlayStationPurchasedTitle>();
        var size = pageSize ?? PurchasedPageSize;
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var variables = JsonSerializer.Serialize(new
            {
                isActive = true,
                platform = new[] { "ps3", "ps4", "ps5" },
                start = offset,
                size,
                sortBy = "ACTIVE_DATE",
                sortDirection = "desc"
            });

            using var document = await GetGraphQlAsync("getPurchasedGameList", variables, PurchasedGamesQueryHash, cancellationToken);
            if (!TryGetGraphQlPayload(document, "purchasedTitlesRetrieve", out var payload) ||
                !payload.TryGetProperty("games", out var games) ||
                games.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("The PlayStation purchased games response was incomplete.");
            }

            var pageCount = 0;
            foreach (var game in games.EnumerateArray())
            {
                pageCount++;
                titles.Add(new PlayStationPurchasedTitle
                {
                    TitleId = PlayStationJson.GetString(game, "titleId"),
                    Name = PlayStationJson.GetString(game, "name"),
                    Platform = PlayStationJson.GetString(game, "platform"),
                    // Older responses carry the entitlement under subscriptionService instead.
                    Membership = PlayStationJson.GetString(game, "membership")
                        ?? PlayStationJson.GetString(game, "subscriptionService")
                });
            }

            // Treating a missing pageInfo as the last page would silently import only the first
            // page as a success, so an unreadable one is an error like a missing games array.
            if (!payload.TryGetProperty("pageInfo", out var pageInfo) ||
                !pageInfo.TryGetProperty("isLast", out var isLastElement) ||
                (isLastElement.ValueKind != JsonValueKind.True && isLastElement.ValueKind != JsonValueKind.False))
            {
                throw new InvalidOperationException("The PlayStation purchased games response did not report paging information.");
            }

            var isLast = isLastElement.GetBoolean();
            if (isLast || pageCount == 0)
            {
                return titles;
            }

            offset += size;
        }
    }

    /// <summary>API 2 of 4: recently played titles from the web API. Supplies last-played dates.</summary>
    public async Task<List<PlayStationPlayedTitle>> GetPlayedTitlesAsync(CancellationToken cancellationToken)
    {
        var variables = JsonSerializer.Serialize(new { limit = 100, categories = "ps4_game,ps5_native_game" });
        using var document = await GetGraphQlAsync("getUserGameList", variables, PlayedGamesQueryHash, cancellationToken);
        if (!TryGetGraphQlPayload(document, "gameLibraryTitlesRetrieve", out var payload) ||
            !payload.TryGetProperty("games", out var games) ||
            games.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The PlayStation played games response was incomplete.");
        }

        var titles = new List<PlayStationPlayedTitle>();
        foreach (var game in games.EnumerateArray())
        {
            titles.Add(new PlayStationPlayedTitle
            {
                TitleId = PlayStationJson.GetString(game, "titleId"),
                Name = PlayStationJson.GetString(game, "name"),
                Platform = PlayStationJson.GetString(game, "platform"),
                LastPlayed = PlayStationJson.GetDate(game, "lastPlayedDateTime")
            });
        }

        return titles;
    }

    /// <summary>API 3 of 4: the mobile play history. The only source of play time.</summary>
    public async Task<List<PlayStationPlayedTitle>> GetMobilePlayedTitlesAsync(CancellationToken cancellationToken)
    {
        var titles = new List<PlayStationPlayedTitle>();
        await ForEachMobilePageAsync(MobilePlayedTitlesUrl, MobilePageSize, "titles", cancellationToken, title =>
        {
            titles.Add(new PlayStationPlayedTitle
            {
                TitleId = PlayStationJson.GetString(title, "titleId"),
                Name = PlayStationJson.GetString(title, "name"),
                Category = PlayStationJson.GetString(title, "category"),
                LastPlayed = PlayStationJson.GetDate(title, "lastPlayedDateTime"),
                PlayTime = ParsePlayDuration(PlayStationJson.GetString(title, "playDuration"))
            });
        });

        return titles;
    }

    /// <summary>
    /// API 4 of 4: trophy titles. This is the only source that covers PS3, PSP, Vita and PC, so it
    /// is what makes those platforms importable at all.
    /// </summary>
    public async Task<List<PlayStationTrophyTitle>> GetTrophyTitlesAsync(CancellationToken cancellationToken)
    {
        var titles = new List<PlayStationTrophyTitle>();
        await ForEachMobilePageAsync(MobileTrophyTitlesUrl, TrophyPageSize, "trophyTitles", cancellationToken, title =>
        {
            titles.Add(new PlayStationTrophyTitle
            {
                NpCommunicationId = PlayStationJson.GetString(title, "npCommunicationId"),
                Name = PlayStationJson.GetString(title, "trophyTitleName"),
                Platform = PlayStationJson.GetString(title, "trophyTitlePlatform"),
                ServiceName = PlayStationJson.GetString(title, "npServiceName"),
                Progress = title.TryGetProperty("progress", out var titleProgress) && titleProgress.TryGetInt32(out var progressValue) ? progressValue : 0,
                LastPlayed = PlayStationJson.GetDate(title, "lastUpdatedDateTime")
            });
        });

        return titles;
    }

    /// <summary>
    /// Resolves game ids to their trophy sets. A single store title can represent a collection and
    /// therefore have more than one set. Sony caps this at five ids per request, so the result is
    /// worth caching rather than repeating.
    /// </summary>
    public async Task<Dictionary<string, List<PlayStationTrophySet>>> GetTrophySetsAsync(IEnumerable<string> titleIds, CancellationToken cancellationToken)
    {
        var mappings = new Dictionary<string, List<PlayStationTrophySet>>(StringComparer.Ordinal);
        var first = true;
        foreach (var batch in titleIds.Distinct(StringComparer.Ordinal).Chunk(TrophyMappingBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!first)
            {
                // Five ids per request means a large library issues many of these in a row.
                await Task.Delay(TrophyMappingDelay, cancellationToken);
            }

            first = false;
            using var document = await GetJsonAsync(string.Format(TrophyMappingUrl, string.Join(',', batch)), cancellationToken);
            if (!document.RootElement.TryGetProperty("titles", out var titles) || titles.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var title in titles.EnumerateArray())
            {
                var titleId = PlayStationJson.GetString(title, "npTitleId");
                if (string.IsNullOrEmpty(titleId) ||
                    !title.TryGetProperty("trophyTitles", out var trophyTitles) ||
                    trophyTitles.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var sets = new List<PlayStationTrophySet>();
                foreach (var trophyTitle in trophyTitles.EnumerateArray())
                {
                    var communicationId = PlayStationJson.GetString(trophyTitle, "npCommunicationId");
                    if (!string.IsNullOrEmpty(communicationId))
                    {
                        var set = new PlayStationTrophySet(
                            communicationId,
                            PlayStationJson.GetString(trophyTitle, "npServiceName") ?? "trophy",
                            PlayStationJson.GetString(trophyTitle, "trophyTitleName"));
                        if (!sets.Any(existing =>
                                string.Equals(existing.CommunicationId, set.CommunicationId, StringComparison.Ordinal) &&
                                string.Equals(existing.ServiceName, set.ServiceName, StringComparison.Ordinal)))
                        {
                            sets.Add(set);
                        }
                    }
                }

                if (sets.Count > 0)
                {
                    mappings[titleId] = sets;
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Reads one game's trophies. Definitions and earned state come from two endpoints that are
    /// joined on trophyId; the service name must match the title or Sony answers 404.
    /// </summary>
    public async Task<List<PlayStationTrophy>> GetTrophiesAsync(PlayStationTrophySet trophySet, CancellationToken cancellationToken)
    {
        using var definitionsDocument = await GetJsonAsync(
            string.Format(TrophyDefinitionsUrl, trophySet.CommunicationId, trophySet.ServiceName), cancellationToken);
        using var progressDocument = await GetJsonAsync(
            string.Format(TrophyProgressUrl, trophySet.CommunicationId, trophySet.ServiceName), cancellationToken);

        var progress = new Dictionary<int, JsonElement>();
        if (progressDocument.RootElement.TryGetProperty("trophies", out var progressTrophies) &&
            progressTrophies.ValueKind == JsonValueKind.Array)
        {
            foreach (var trophy in progressTrophies.EnumerateArray())
            {
                if (trophy.TryGetProperty("trophyId", out var id) && id.TryGetInt32(out var trophyId))
                {
                    progress[trophyId] = trophy;
                }
            }
        }

        var trophies = new List<PlayStationTrophy>();
        if (!definitionsDocument.RootElement.TryGetProperty("trophies", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array)
        {
            return trophies;
        }

        foreach (var definition in definitions.EnumerateArray())
        {
            if (!definition.TryGetProperty("trophyId", out var idElement) || !idElement.TryGetInt32(out var trophyId))
            {
                continue;
            }

            progress.TryGetValue(trophyId, out var earnedState);
            trophies.Add(new PlayStationTrophy
            {
                Id = trophyId,
                Name = PlayStationJson.GetString(definition, "trophyName"),
                Detail = PlayStationJson.GetString(definition, "trophyDetail"),
                IconUrl = PlayStationJson.GetString(definition, "trophyIconUrl"),
                Type = PlayStationJson.GetString(definition, "trophyType"),
                GroupId = PlayStationJson.GetString(definition, "trophyGroupId"),
                Hidden = definition.TryGetProperty("trophyHidden", out var hidden) && hidden.ValueKind == JsonValueKind.True,
                Earned = earnedState.ValueKind == JsonValueKind.Object &&
                         earnedState.TryGetProperty("earned", out var earned) && earned.ValueKind == JsonValueKind.True,
                EarnedDate = earnedState.ValueKind == JsonValueKind.Object ? PlayStationJson.GetDate(earnedState, "earnedDateTime") : null,
                // Sony reports this as a percentage string, e.g. "1.3".
                EarnedRate = earnedState.ValueKind == JsonValueKind.Object
                    ? PlayStationJson.GetSeconds(earnedState, "trophyEarnedRate")
                    : null
            });
        }

        return trophies;
    }

    /// <summary>Maps trophy group ids to their names, e.g. "001" to "The Frozen Wilds".</summary>
    public async Task<Dictionary<string, string>> GetTrophyGroupNamesAsync(PlayStationTrophySet trophySet, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = await GetJsonAsync(
            string.Format(TrophyGroupsUrl, trophySet.CommunicationId, trophySet.ServiceName), cancellationToken);
        if (!document.RootElement.TryGetProperty("trophyGroups", out var groups) || groups.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var group in groups.EnumerateArray())
        {
            var id = PlayStationJson.GetString(group, "trophyGroupId");
            var name = PlayStationJson.GetString(group, "trophyGroupName");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                names[id] = name;
            }
        }

        return names;
    }

    private async Task ForEachMobilePageAsync(string urlFormat, int pageSize, string arrayName, CancellationToken cancellationToken, Action<JsonElement> onItem)
    {
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = await GetJsonAsync(string.Format(urlFormat, pageSize, offset), cancellationToken);
            if (!document.RootElement.TryGetProperty(arrayName, out var items) || items.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"The PlayStation response did not contain a '{arrayName}' collection.");
            }

            foreach (var item in items.EnumerateArray())
            {
                onItem(item);
            }

            // The last page either omits nextOffset entirely or returns it as null.
            if (!document.RootElement.TryGetProperty("nextOffset", out var nextOffset) ||
                nextOffset.ValueKind != JsonValueKind.Number ||
                !nextOffset.TryGetInt32(out var next) ||
                next <= offset)
            {
                return;
            }

            offset = next;
        }
    }

    private async Task<JsonDocument> GetGraphQlAsync(string operationName, string variables, string queryHash, CancellationToken cancellationToken)
    {
        var extensions = JsonSerializer.Serialize(new
        {
            persistedQuery = new { version = 1, sha256Hash = queryHash }
        });
        var url = GraphQlUrl +
            "?operationName=" + operationName +
            "&variables=" + Uri.EscapeDataString(variables) +
            "&extensions=" + Uri.EscapeDataString(extensions);
        return await GetJsonAsync(url, cancellationToken, "x-apollo-operation-name", "pn_psn");
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken, string? headerName = null, string? headerValue = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (headerName != null)
        {
            request.Headers.TryAddWithoutValidation(headerName, headerValue);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"PlayStation returned {(int)response.StatusCode} ({response.ReasonPhrase}){PlayStationJson.Describe(responseContent)}.");
        }

        return JsonDocument.Parse(responseContent);
    }

    private static bool TryGetGraphQlPayload(JsonDocument document, string propertyName, out JsonElement payload)
    {
        if (document.RootElement.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            var message = PlayStationJson.GetString(errors[0], "message");
            throw new InvalidOperationException(
                "The PlayStation request failed" + (string.IsNullOrWhiteSpace(message) ? "." : ": " + message));
        }

        payload = default;
        return document.RootElement.TryGetProperty("data", out var data) &&
               data.TryGetProperty(propertyName, out payload);
    }

    private static uint ParsePlayDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 0;
        }

        try
        {
            // ISO-8601 duration, e.g. PT15H20M23S. XmlConvert also handles a day component, which a
            // hand-rolled H/M/S parser would silently drop.
            var parsed = XmlConvert.ToTimeSpan(duration);
            return parsed > TimeSpan.Zero ? (uint)parsed.TotalSeconds : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }
}

internal static class PlayStationJson
{
    public static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static double? GetSeconds(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        // Sony returns these lifetimes as a number in some responses and as a string in others.
        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetDouble(),
            JsonValueKind.String => double.TryParse(
                property.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) ? parsed : null,
            _ => null
        };
    }

    public static DateTimeOffset? GetDate(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        // The current culture may default to a non-Gregorian calendar, which fails on these values.
        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Compacts a failed response body so it can be shown in an exception message.</summary>
    public static string Describe(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var compacted = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();
        return ": " + (compacted.Length > 300 ? compacted[..300] + "…" : compacted);
    }
}

public sealed class PlayStationPurchasedTitle
{
    public string? TitleId { get; init; }
    public string? Name { get; init; }
    public string? Platform { get; init; }
    public string? Membership { get; init; }
}

public sealed class PlayStationPlayedTitle
{
    public string? TitleId { get; init; }
    public string? Name { get; init; }
    public string? Platform { get; init; }
    public string? Category { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }
    public uint PlayTime { get; init; }
}

public sealed class PlayStationTrophyTitle
{
    public string? NpCommunicationId { get; init; }
    public string? Name { get; init; }
    public string? Platform { get; init; }
    public string? ServiceName { get; init; }
    /// <summary>Completion percentage, usable as a cheap "has anything changed" signal.</summary>
    public int Progress { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }
}

/// <summary>Identifies a trophy set. The first two fields are required to read its trophies.</summary>
public sealed record PlayStationTrophySet(string CommunicationId, string ServiceName, string? Name = null);

public sealed class PlayStationTrophy
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Detail { get; init; }
    public string? IconUrl { get; init; }
    public string? Type { get; init; }
    public string? GroupId { get; init; }
    public bool Hidden { get; init; }
    public bool Earned { get; init; }
    public DateTimeOffset? EarnedDate { get; init; }
    public double? EarnedRate { get; init; }
}
