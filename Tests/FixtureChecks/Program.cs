using System.IO;
using Playnite;
using PlayStationLibrary;

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
    }
}

using var fixture = new AchievementFixture();
var english = PlayStationLocales.ToTrophyAcceptLanguage("en-us", "pl_PL");
var french = PlayStationLocales.ToTrophyAcceptLanguage("fr-ca", "en_US");

// Fresh collection: two NPWR sets with overlapping trophy numbers, multiple categories and all tiers.
var imported = await fixture.Refresh(english, BuiltInCausations.LibraryUpdate);
Equal(1, imported.Count, "Fresh collection import");
Equal(8, imported[0].Achievements.Count, "All collection members imported");
Equal(8, imported[0].Achievements.Select(item => item.AchievementId).Distinct().Count(), "Qualified trophy IDs");
Equal(6, fixture.Types.Items.Count, "Every new category registered once");
Equal(4, fixture.Tiers.Items.Count, "Every new tier registered once");
Equal("BioShock Remastered — Base Game", fixture.Types.Items["psn_base_NPWR00001_00"].Name, "Initial category label");
Equal("Silver", fixture.Tiers.Items["psn_silver"].Name, "Initial tier label");
fixture.ImportNew(imported);
var originalIds = fixture.Achievements.Items.Keys.Order().ToArray();
var originalTypeIds = fixture.Types.Items.Keys.Order().ToArray();
var achievement = fixture.Achievements.Items.Values.First();
achievement.UnlockDate = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
achievement.Progress = 7;
achievement.ProgressMax = 10;
achievement.UnlockedIcon = "custom-achievement.png";
fixture.Tiers.Items["psn_silver"].Icon = "custom-silver.png";
var preserved = achievement.GetCopy();

fixture.Http.DefinitionRequests = 0;
Equal(0, (await fixture.Refresh(english, BuiltInCausations.LibraryUpdate)).Count, "Unchanged library update skipped");
Equal(0, (await fixture.Refresh(english, BuiltInCausations.GameStateChange)).Count, "Unchanged post-play update skipped");
Equal(0, fixture.Http.DefinitionRequests, "Routine updates retain cache");

// An explicit trophy locale must win over the unrelated Playnite UI locale.
imported = await fixture.Refresh(french, BuiltInCausations.LibraryUpdate);
Equal(1, imported.Count, "Locale change refreshes collection");
Equal(2, fixture.Http.DefinitionRequests, "Locale change reads both sets");
Equal("BioShock Remastered — Jeu de base", fixture.Types.Items["psn_base_NPWR00001_00"].Name, "Base category translated");
Equal("BioShock Remastered — Salles de défis", fixture.Types.Items["psn_dlc_NPWR00001_00_001"].Name, "Sony DLC category translated");
Equal("BioShock Remastered — Contenu additionnel", fixture.Types.Items["psn_dlc_NPWR00001_00_002"].Name, "Fallback category translated");
Equal("Argent", fixture.Tiers.Items["psn_silver"].Name, "Silver translated");
Equal("Or", fixture.Tiers.Items["psn_gold"].Name, "Gold translated");
Equal("Platine", fixture.Tiers.Items["psn_platinum"].Name, "Platinum translated");
Equal(true, originalIds.SequenceEqual(fixture.Achievements.Items.Keys.Order()), "Achievement IDs remain stable");
Equal(true, originalTypeIds.SequenceEqual(fixture.Types.Items.Keys.Order()), "Category IDs remain stable");
Equal("Trophée 0", fixture.Achievements.Items[preserved.Id].Name, "Existing trophy text translated");
Equal("Description française", fixture.Achievements.Items[preserved.Id].Description, "Description translated");
Equal(preserved.UnlockDate, fixture.Achievements.Items[preserved.Id].UnlockDate, "Unlock date preserved");
Equal(preserved.Progress, fixture.Achievements.Items[preserved.Id].Progress, "Progress preserved");
Equal(preserved.ProgressMax, fixture.Achievements.Items[preserved.Id].ProgressMax, "Progress maximum preserved");
Equal(preserved.UnlockedIcon, fixture.Achievements.Items[preserved.Id].UnlockedIcon, "Achievement icon preserved");
Equal("custom-silver.png", fixture.Tiers.Items["psn_silver"].Icon, "Tier icon preserved");
Equal(preserved.TypeId, fixture.Achievements.Items[preserved.Id].TypeId, "Category link preserved");
Equal(preserved.TrophyId, fixture.Achievements.Items[preserved.Id].TrophyId, "Tier link preserved");

// Manual refresh must fetch changes even when Sony reports exactly the same progress and locale.
fixture.Http.Revision = " revised";
fixture.Http.DefinitionRequests = 0;
var otherCache = fixture.Cache;
otherCache.Record(fixture.OtherGame.Id, fixture.OtherSet, french, "unchanged-other-game");
otherCache.Save();
imported = await fixture.Refresh(french, BuiltInCausations.ManualEdit);
Equal(2, fixture.Http.DefinitionRequests, "Manual refresh bypasses cache for every set");
Equal("Trophée 0 revised", fixture.Achievements.Items[preserved.Id].Name, "Manual refresh overwrites existing text");
Equal("BioShock Remastered — Salles de défis revised", fixture.Types.Items["psn_dlc_NPWR00001_00_001"].Name, "Manual refresh renames category");
Equal(true, fixture.Cache.IsUnchanged(fixture.OtherGame.Id, fixture.OtherSet, french, "unchanged-other-game"), "Unrequested game stays cached");

fixture.Http.MissingText = true;
await fixture.Refresh(french, BuiltInCausations.ManualEdit);
Equal("Trophée 0 revised", fixture.Achievements.Items[preserved.Id].Name, "Missing Sony name must not replace existing text with a numeric fallback");
Equal("Description française", fixture.Achievements.Items[preserved.Id].Description, "Missing description preserves existing text");
fixture.Http.MissingText = false;

// Progress-only updates leave user-edited text and labels alone.
fixture.Achievements.Items[preserved.Id].Name = "My trophy name";
fixture.Types.Items["psn_base_NPWR00001_00"].Name = "My category";
fixture.Tiers.Items["psn_silver"].Name = "My tier";
fixture.Http.Progress++;
await fixture.Refresh(french, BuiltInCausations.GameStateChange);
Equal("My trophy name", fixture.Achievements.Items[preserved.Id].Name, "Progress-only trophy text preserved");
Equal("My category", fixture.Types.Items["psn_base_NPWR00001_00"].Name, "Progress-only category preserved");
Equal("My tier", fixture.Tiers.Items["psn_silver"].Name, "Progress-only tier preserved");

// Failed collection member: no partial payload or partial metadata update, and retry all members.
fixture.Http.FailedSet = "NPWR00002_00";
imported = await fixture.Refresh(english, BuiltInCausations.LibraryUpdate);
Equal(0, imported.Count, "Incomplete collection is not returned");
Equal("My trophy name", fixture.Achievements.Items[preserved.Id].Name, "Failed collection keeps existing text");
Equal("My category", fixture.Types.Items["psn_base_NPWR00001_00"].Name, "Failed collection keeps existing labels");
Equal(false, fixture.Cache.HasAnyLocale(fixture.Game.Id, "NPWR00001_00"), "Successful sibling remains retryable");
fixture.Http.FailedSet = null;
fixture.Http.DefinitionRequests = 0;
Equal(1, (await fixture.Refresh(english, BuiltInCausations.LibraryUpdate)).Count, "Failed collection retries");
Equal(2, fixture.Http.DefinitionRequests, "Retry reads entire collection");

// Failure saving labels must not hide itself behind a successful progress cache.
fixture.Tiers.FailNextUpdate = true;
await fixture.Refresh(french, BuiltInCausations.LibraryUpdate);
Equal(false, fixture.Cache.HasAnyLocale(fixture.Game.Id, "NPWR00001_00"), "Failed label write not cached");
await fixture.Refresh(french, BuiltInCausations.LibraryUpdate);
Equal("Argent", fixture.Tiers.Items["psn_silver"].Name, "Failed label write retried");

fixture.Achievements.FailNextUpdate = true;
await fixture.Refresh(english, BuiltInCausations.LibraryUpdate);
Equal(false, fixture.Cache.HasAnyLocale(fixture.Game.Id, "NPWR00002_00"), "Failed trophy text write not cached");
await fixture.Refresh(english, BuiltInCausations.LibraryUpdate);
Equal("Trophy 0 revised", fixture.Achievements.Items[preserved.Id].Name, "Failed trophy text write retried");

// Group lookup failures and empty definition payloads must not erase good category names.
fixture.Http.FailGroups = true;
Equal(0, (await fixture.Refresh(french, BuiltInCausations.ManualEdit)).Count, "Failed groups rejected");
Equal("BioShock Remastered — Challenge Rooms revised", fixture.Types.Items["psn_dlc_NPWR00001_00_001"].Name, "Failed groups preserve names");
fixture.Http.FailGroups = false;
fixture.Http.EmptyDefinitions = true;
Equal(0, (await fixture.Refresh(french, BuiltInCausations.LibraryUpdate)).Count, "Empty definitions rejected");
Equal(false, fixture.Cache.HasAnyLocale(fixture.Game.Id, "NPWR00001_00"), "Empty definitions not cached");
fixture.Http.EmptyDefinitions = false;
await fixture.Refresh(french, BuiltInCausations.LibraryUpdate);

// An unplayed member absent from trophyTitles still converges in the progress cache.
fixture.Http.OmitSecondTitle = true;
await fixture.Refresh(french, BuiltInCausations.ManualEdit);
Equal(0, (await fixture.Refresh(french, BuiltInCausations.LibraryUpdate)).Count, "Unreported member can be cached");

// Cancellation must retain invalidation, including earlier games that will never reach the importer.
using var cancellation = new CancellationTokenSource();
fixture.Http.CancelOnOtherGame = cancellation;
try
{
    await fixture.Refresh(french, BuiltInCausations.ManualEdit, [fixture.Game, fixture.OtherGame], cancellation.Token);
    throw new InvalidOperationException("Expected cancellation.");
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Equal(false, fixture.Cache.HasAnyLocale(fixture.Game.Id, "NPWR00001_00"), "Earlier game not cached on cancelled batch");
    Equal(false, fixture.Cache.HasAnyLocale(fixture.OtherGame.Id, fixture.OtherSet), "Cancelled game not cached");
}
fixture.Http.CancelOnOtherGame = null;
imported = await fixture.Refresh(french, BuiltInCausations.LibraryUpdate, [fixture.OtherGame]);
Equal(1, imported.Count, "Direct PS5 trophy-list game imported through trophy2");

Equal("Srebro", PlayStationTrophyLabels.ForLocale("pl-PL, pl;q=0.9").Silver, "Polish trophy labels");
Equal("Silver", PlayStationTrophyLabels.ForLocale("unknown").Silver, "Unsupported language fallback");
Console.WriteLine("PASS: Alpha2 first ingest, collections, locale changes, manual refresh, normal cache, metadata preservation, failures, retries, cancellation, and PS5 service.");
