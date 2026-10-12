# Offline achievement checks

Run on Windows with the .NET 10 SDK:

```powershell
dotnet run -c Release --project Tests/FixtureChecks
```

The checks exercise the production achievement reader with synthetic HTTP responses and in-memory
Playnite collections. They require no PSN credentials or running Playnite instance and use a temporary
progress cache that is deleted afterwards.

Coverage includes first ingest with multiple trophy sets and all tiers, stable IDs on language changes,
manual versus automatic causation, preserving progress and icons, failed set/group/metadata requests,
retry behavior, cancellation, unreported collection members, and the PS5 `trophy2` service.

Label checks cover every selectable locale, all six labels, case/underscore/header normalization,
Chinese script precedence and region-only tags, Automatic language selection, and English fallback.
The achievement fixtures also exercise Japanese first ingest, manual refresh of formerly English
labels, and Traditional/Simplified Chinese switching without changing category or tier IDs.

For a host check on Alpha2, import a collection in English, change trophy language to French and sync,
then use the game's **Update achievements** command with unchanged progress. Check both the trophy
text and the shared category/tier records. The fixture importer only creates initial trophies; it does
not simulate all Playnite database, UI, or normal import behavior.
