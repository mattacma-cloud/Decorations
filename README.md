# Decorations
<img width="1920" height="1080" alt="20260910133307_1" src="https://github.com/user-attachments/assets/2e289eb1-5501-440a-942f-7079f184e687" />

**How to Use**

Open **Captain’s Quarters** then **Awards** in lower-right of the screen. This will bring up any awards you have. To test, use the BattleTech Save Editor to give yourself an award tag. In the award/decorations screen you can view the award description, citation, and award background.

**Requirements**
- BattleTech (HBS)
- [ModTek](https://github.com/BattletechModders/ModTek)

Optional: lore packs or flashpoints that grant `decoration_*` company tags and ship citations / Darius events (e.g. Lore Pack 4SW).

**Installation**
1. Copy the `CompanyDecorations` folder into `BATTLETECH\Mods\`.
2. Ensure `"Enabled": true` in `mod.json`.
3. Start the game (ModTek loads the DLL).

**Features**
- Catalog-driven decorations (icon, title, description, lore, Awarded By)
- Shown when `CompanyTags` contains the entry’s `companyTag`
- Persisted citation (`decorationCite_*`) and award date (`decorationDate_*`)
- Darius-style announcement via `event_decoration_*` (ForceEvent or `AwardHelper`)
- Memorial Wall–style chrome; Awards button label unchanged

**Adding a decoration**

***This mod (definition)***

1. Add a row to `decorations/catalog.json` (`companyTag`, `title`, `description`, `icon`, `loreId`, `awardedBy`, optional `awardEventId`).
2. Drop a PNG in `assets/icons\` named like the `icon` id (e.g. `decoration_Faction_AwardName.png`). 512x512 .png
3. Add `baseDescriptions\LoreDecoration_….json` for Award Background / lore links.

***Granting pack (when it is earned)***

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

**Tag / id conventions**
Company tag: `decoration_Faction_AwardName`
Citation stat: `decorationCite_Faction_AwardName`
Date stat: `decorationDate_Faction_AwardName`
Award event: `event_decoration_Faction_AwardName`

Tokens in citations / events: `{CompanyName}` (and common `{COMPANY.Name}` variants).

**Bundled examples**

Three sample decorations ship in the catalog (Raging Wasp, Federated Suns Medal of Honor, Operation RAT Service Ribbon). Fox's of Tikonov V (in the Lore-Pack 4th Succession War) grant the latter two, not this mod’s milestone JSON.

**License / author**
Author: **Blacknova**. Use and redistribute with your BattleTech mod packs as you see fit; keep attribution in `mod.json` unless you fork under your own name.
