# PlayStation Library Integration

Bring your PlayStation library, play activity, and trophies into Playnite.

## Features

- Imports purchased and played PlayStation 4 and PlayStation 5 games, including platform, last-played date, and reported play time.
- Imports PlayStation 3, PlayStation Portable, and PlayStation Vita games from the trophy list, with a separate toggle for each legacy platform.
- Records a play session for the play time gained between imports, so PlayStation play activity builds up over time.
- Imports trophies as Playnite achievements, including earned status and date, rarity, hidden trophies, trophy tier, and named DLC trophy groups.
- Recognises disc and other trophy-only games that PlayStation does not report as purchases. This import is optional and off by default, because such games can only be matched to your existing library by name.
- Marks games included with PlayStation Plus, and can place those games under a dedicated **PlayStation Plus** source instead of the main PlayStation source.
- Requests trophy names and descriptions in your Playnite language, or in a language chosen in settings.
- Caches trophy-set mappings and completion data so later achievement refreshes only read the games that actually changed.

## Getting started

1. Sign in to PlayStation in your normal browser and choose **Trust this Browser**.
2. Open [https://ca.account.sony.com/api/v1/ssocookie](https://ca.account.sony.com/api/v1/ssocookie) in that browser.
3. On the **Settings** tab, paste either the NPSSO value or the complete `{"npsso":"…"}` response, then select **Check authentication** and save.

Run a library update to import the collection. The NPSSO is stored encrypted for your Windows user account, and is exchanged for a token that the extension refreshes on its own until the NPSSO expires.

## Notes

Whether play time and play sessions are applied to games you already have is Playnite's decision, on the **Library Settings** tab.

PlayStation only links a game to its trophies once the account has trophy progress for it, so achievements appear for a game the first time you play it, not when you buy it. Play time, play sessions, and trophies are reported by PlayStation and are not available for games it does not track.
