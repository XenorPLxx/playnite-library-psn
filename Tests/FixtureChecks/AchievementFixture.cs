using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Playnite;
using PlayStationLibrary;

internal sealed class AchievementFixture : IDisposable
{
    private readonly string cacheDirectory = Directory.CreateTempSubdirectory("psn-alpha2-fixtures-").FullName;
    private readonly IPlayniteApi api;
    public FakeCollection<Game> Games { get; } = FakeCollection<Game>.Create(item => item.GetCopy());
    public FakeCollection<GameAchievement> Achievements { get; } = FakeCollection<GameAchievement>.Create(item => item.GetCopy());
    public FakeCollection<GameAchievementType> Types { get; } = FakeCollection<GameAchievementType>.Create(item => item.GetCopy());
    public FakeCollection<GameAchievementTrophy> Tiers { get; } = FakeCollection<GameAchievementTrophy>.Create(item => item.GetCopy());
    public TrophyHttpHandler Http { get; } = new();
    public PlayStationTrophyProgress Cache => new(cacheDirectory);
    public string OtherSet => "NPWR00003_00";
    public Game Game { get; } = new()
    {
        Id = "collection-game",
        LibraryId = PlayStationLibraryPlugin.Id,
        LibraryGameId = "CUSA00001_00",
        Name = "BioShock: The Collection",
        ExternalIdentifiers =
        [
            new("psn_trophy_sets", "NPWR00001_00:trophy|BioShock%20Remastered"),
            new("psn_trophy_sets", "NPWR00002_00:trophy|BioShock%202%20Remastered")
        ]
    };
    public Game OtherGame { get; } = new()
    {
        Id = "other-game",
        LibraryId = PlayStationLibraryPlugin.Id,
        LibraryGameId = "NPWR00003_00",
        Name = "PS5 Game",
        ExternalIdentifiers = [new("psn_trophy_set", "NPWR00003_00:trophy2")]
    };

    public AchievementFixture()
    {
        Games.Items.Add(Game.Id, Game);
        Games.Items.Add(OtherGame.Id, OtherGame);
        var library = StubProxy.Create<ILibraryApi>((method, _) => method.Name switch
        {
            "get_Games" => Games.Api,
            "get_GameAchievements" => Achievements.Api,
            "get_GameAchievementTypes" => Types.Api,
            "get_GameAchievementTrophies" => Tiers.Api,
            _ => throw new NotSupportedException(method.Name)
        });
        api = StubProxy.Create<IPlayniteApi>((method, _) => method.Name switch
        {
            "get_Library" => library,
            "get_UserDataDir" => cacheDirectory,
            _ => throw new NotSupportedException(method.Name)
        });
    }

    public async Task<List<ImportableAchievements>> Refresh(
        string locale, string causation, IReadOnlyCollection<Game>? games = null, CancellationToken token = default)
    {
        Http.French = locale.StartsWith("fr", StringComparison.Ordinal);
        using var session = new PlayStationSession(new HttpClient(Http, disposeHandler: false));
        return await new PlayStationAchievements(api).GetAchievementsAsync(session, games ?? [Game], locale, causation, token);
    }

    public void ImportNew(List<ImportableAchievements> imports)
    {
        foreach (var game in imports)
        {
            foreach (var item in game.Achievements)
            {
                var achievement = new GameAchievement(game.PlayniteGameId, item.AchievementId)
                {
                    Name = item.Name,
                    Description = item.Description,
                    TypeId = item.AchievementType?.Id,
                    TrophyId = item.TrophyType?.Id
                };
                Achievements.Items.TryAdd(achievement.Id, achievement);
            }
        }
    }

    public void Dispose()
    {
        Http.Dispose();
        Directory.Delete(cacheDirectory, recursive: true);
    }
}

public class StubProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;

    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = Create<T, StubProxy>();
        ((StubProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
}

public class FakeCollection<T> : DispatchProxy where T : LibraryObject
{
    public ILibraryCollection<T> Api { get; private set; } = null!;
    public Dictionary<string, T> Items { get; } = new(StringComparer.Ordinal);
    public bool FailNextUpdate { get; set; }
    private Func<T, T> copy = null!;

    public static FakeCollection<T> Create(Func<T, T> copy)
    {
        var api = Create<ILibraryCollection<T>, FakeCollection<T>>();
        var collection = (FakeCollection<T>)(object)api;
        collection.Api = api;
        collection.copy = copy;
        return collection;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments)
    {
        var args = arguments ?? [];
        switch (method!.Name)
        {
            case "get_Count":
                return Items.Count;
            case "GetEnumerator":
                return Items.Values.Select(copy).ToList().GetEnumerator();
            case "Contains":
                return Items.ContainsKey((string)args[0]!);
            case "Get":
                return Items.TryGetValue((string)args[0]!, out var found) ? copy(found) : null;
            case "AddAsync":
                foreach (var item in (IEnumerable<T>)args[0]!)
                {
                    Items.Add(item.Id, copy(item));
                }
                return Task.CompletedTask;
            case "UpdateAsync":
                if (FailNextUpdate)
                {
                    FailNextUpdate = false;
                    throw new IOException("Simulated database write failure.");
                }

                if (args[0] is IEnumerable<string> ids && args[1] is Func<T, bool> update)
                {
                    foreach (var id in ids)
                    {
                        var item = copy(Items[id]);
                        if (update(item)) Items[id] = item;
                    }
                }
                else
                {
                    foreach (var item in (IEnumerable<T>)args[0]!) Items[item.Id] = copy(item);
                }
                return Task.FromResult(new List<CollectionItemUpdateData<T>>());
            default:
                throw new NotSupportedException(method.Name);
        }
    }
}

internal sealed class TrophyHttpHandler : HttpMessageHandler
{
    public bool French { get; set; }
    public int Progress { get; set; } = 10;
    public string Revision { get; set; } = "";
    public string? FailedSet { get; set; }
    public bool FailGroups { get; set; }
    public bool EmptyDefinitions { get; set; }
    public bool MissingText { get; set; }
    public bool OmitSecondTitle { get; set; }
    public CancellationTokenSource? CancelOnOtherGame { get; set; }
    public int DefinitionRequests { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        cancellationToken.ThrowIfCancellationRequested();
        if (url.Contains("/users/me/trophyTitles?", StringComparison.Ordinal))
        {
            var ids = OmitSecondTitle ? new[] { 1, 3 } : new[] { 1, 2, 3 };
            return Json(new
            {
                trophyTitles = ids.Select(id => new
                {
                    npCommunicationId = $"NPWR0000{id}_00",
                    npServiceName = id == 3 ? "trophy2" : "trophy",
                    trophyTitleName = id == 1 ? "BioShock Remastered" : id == 2 ? "BioShock 2 Remastered" : "PS5 Game",
                    progress = Progress,
                    lastUpdatedDateTime = "2026-08-16T12:00:00Z"
                }).ToArray()
            });
        }

        if (url.Contains("NPWR00003_00", StringComparison.Ordinal))
        {
            if (!url.Contains("npServiceName=trophy2", StringComparison.Ordinal))
                throw new InvalidOperationException("Direct PS5 title used the wrong service.");
            CancelOnOtherGame?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (FailedSet is not null && url.Contains(FailedSet, StringComparison.Ordinal))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        if (url.Contains("/users/me/npCommunicationIds/", StringComparison.Ordinal))
            return Json(new { trophies = Array.Empty<object>() });

        if (url.Contains("/trophyGroups/all/trophies?", StringComparison.Ordinal))
        {
            DefinitionRequests++;
            return Json(new
            {
                trophies = (EmptyDefinitions ? [] : new[] { "bronze", "silver", "gold", "platinum" })
                    .Select((type, id) => new
                    {
                        trophyId = id,
                        trophyName = MissingText ? null : (French ? "Trophée " : "Trophy ") + id + Revision,
                        trophyDetail = MissingText ? null : French ? "Description française" : "English description",
                        trophyType = type,
                        trophyGroupId = id == 1 ? "001" : id == 2 ? "002" : "default"
                    }).ToArray()
            });
        }

        if (url.Contains("/trophyGroups?", StringComparison.Ordinal))
        {
            if (FailGroups) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Json(new
            {
                trophyGroups = new[]
                {
                    new { trophyGroupId = "001", trophyGroupName = (French ? "Salles de défis" : "Challenge Rooms") + Revision }
                }
            });
        }

        throw new InvalidOperationException("Unexpected fixture request: " + url);
    }

    private static Task<HttpResponseMessage> Json(object payload) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload))
    });
}
