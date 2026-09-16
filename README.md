# CompanyDecorations 1.0.0

Company awards and decorations for HBS BattleTech (ModTek + Harmony).

Open **Captain’s Quarters** → **Awards** (lower-right). The screen title is **Decorations**. Select an award for icon, Awarded By, Date of Award, and **Award Details** (Description, Citation, Award Background).

## Requirements

- BattleTech (HBS)
- [ModTek](https://github.com/BattletechModders/ModTek)

Optional: lore packs that grant `decoration_*` company tags and ship citations / Darius events (e.g. Lore Pack 4SW).

## Install

1. Copy the `CompanyDecorations` folder into `BATTLETECH\Mods\`.
2. Ensure `"Enabled": true` in `mod.json`.
3. Start the game (ModTek loads the DLL).

Rebuild from source (no .NET SDK required; quit the game first if the DLL is locked):

```powershell
powershell -NoProfile -File ".\build.ps1"
```

## Contributing new decorations

If you have canon or fan created decorations you would like added to the main pack, please reach out to me and provide the png. and decoration information, which I will include in future releases.  

## Features

- Catalog-driven decorations (icon, title, description, lore, Awarded By)
- Shown when `CompanyTags` contains the entry’s `companyTag`
- Persisted citation (`decorationCite_*`) and award date (`decorationDate_*`)
- Darius-style announcement via `event_decoration_*` (ForceEvent or `AwardHelper`)
- Memorial Wall–style chrome; Awards button label unchanged

## Settings (`mod.json`)

| Setting | Default | Notes |
|--------|---------|--------|
| `EntryPoints` | `CaptainsQuarters` | Where the Awards button is injected |
| `ButtonLabel` | `Awards` | Corner button text |
| `ButtonIcon` | ribbon sprite id | ModTek Sprite / icon filename stem |
| `ButtonWidth` / `ButtonHeight` | 216 / 162 | Corner control size |
| `EmptyListMessage` | `No awards earned yet.` | Empty list copy |
| `DebugLog` | `false` | Extra Harmony/UI logging |

## Adding a decoration

**This mod (definition)**

1. Add a row to `decorations/catalog.json` (`companyTag`, `title`, `description`, `icon`, `loreId`, `awardedBy`, optional `awardEventId`).
2. Drop a PNG in `assets/icons\` named like the `icon` id (e.g. `decoration_Faction_AwardName.png`).
3. Add `baseDescriptions\LoreDecoration_….json` for Award Background / lore links.

**Granting pack (when it is earned)**

1. Add the `decoration_*` company tag (milestone `AddedTags`, event result, or `AwardHelper.Grant`).
2. Ship citation JSON under `Mods\<YourPack>\citations\` or `Lore-Packs\citations\`:

```json
{
  "companyTag": "decoration_Faction_AwardName",
  "citation": "House X awards {CompanyName} the …"
}
```

3. Ship `SimGameEventDef` id `event_decoration_Faction_AwardName` for the Darius popup.
4. On flashpoint end, ForceEvent with **`MinDaysWait` / `MaxDaysWait` ≥ 1** (0-day waits at FP complete are often dropped). Stagger multiple awards (e.g. day 1 and day 2).

### Tag / id conventions

| Piece | Format |
|--------|--------|
| Company tag | `decoration_Faction_AwardName` |
| Citation stat | `decorationCite_Faction_AwardName` |
| Date stat | `decorationDate_Faction_AwardName` |
| Award event | `event_decoration_Faction_AwardName` |

Tokens in citations / events: `{CompanyName}` (and common `{COMPANY.Name}` variants).

## Public API

```csharp
// Tag + citation + date; queues Darius announcement (wait 1 day) when the event def exists
CompanyDecorations.AwardHelper.Grant(sim, companyTag, citationText);

// Grant without queuing an announcement
CompanyDecorations.AwardHelper.Grant(sim, companyTag, citationText, announce: false);

CompanyDecorations.AwardHelper.QueueAwardAnnouncement(sim, companyTag, minDaysWait: 1, maxDaysWait: 1);
```

## Bundled examples

Three sample decorations ship in the catalog (Raging Wasp, Federated Suns Medal of Honor, Operation RAT Service Ribbon). FoT V grant wiring lives in Lore Pack 4SW, not in this mod’s milestone JSON.

## Do not regress

- CQ lower-right **Awards** button (fully on-screen); do not inject Barracks left-nav flyouts
- Screen title **Decorations**; details title **Award Details**; button/empty copy stay **Awards**
- List: icon + title; Details: Description → Citation → Award Background (inline lore, no third popup)
- Portrait stays in its frame; whole-row list select
- ForceEvent award waits ≥ 1 day after flashpoint completion
- Date of Award stamped when the `decoration_*` tag is added

## License / author

Author: **Blacknova**. Use and redistribute with your BattleTech mod packs as you see fit; keep attribution in `mod.json` unless you fork under your own name.
