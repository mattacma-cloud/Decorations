# CompanyDecorations — changelog

## 1.0.1 — 2026-09-28

### Fixed
- Federated Suns Medal of Honor (and RAT Service Ribbon) could be awarded at random in campaigns that never ran Fox's of Tikonov. In 0.1.9 the award events were marked `PUBLISHED` + `NORMAL` with no requirement tags, which put them in the random event pool, and the DLL then added the tag when the event fired.
- DLL: an `event_decoration_*` event no longer grants its tag. It only stores the citation/date when the company already has the tag (granted by milestone `AddedTags` or `AwardHelper.Grant`).
- Lore_Pack_4SW: all award events set to `"EventType": "UNSELECTABLE"` (vanilla forced-only pattern), so they cannot roll randomly.
- Citations using the game token `{COMPANY.CompanyName}` now resolve in Award Details (previously shown literally).

### Added
- Diamond Sunburst (`decoration_Davion_SunburstDiamond`) and the Charge of the Guards Operation RAT Service Ribbon (`decoration_Davion_OperationRAT_ChargeGuard`), granted at Charge of the Guards IV End (success) / fail Complete (ribbon only).

### Affected saves
Remove tag `decoration_Davion_FederatedSunsMedalofHonor` and stats `decorationCite_` / `decorationDate_` / `decorationAnnounced_` / `decorationAnnounceQueued_Davion_FederatedSunsMedalofHonor` with a save editor (same pattern for the Ribbon if it appeared).

---

## 1.0.0 — 2026-09-16 (publication)

First stable release.

- Captain’s Quarters **Awards** → **Decorations** screen (Memorial chrome)
- Catalog + icons + lore; citations from pack / `Lore-Packs\citations`
- Award Details: Description → Citation → Award Background
- Awarded By / Date of Award; date stamped on `decoration_*` tag grant
- Darius announcements via `event_decoration_*` (ForceEvent wait ≥ 1; CQ backfill for missed queues)
- Public `AwardHelper.Grant` / `QueueAwardAnnouncement`
- `DebugLog` default **false**; temp reflect scripts removed

### Bundled sample decorations

- `decoration_Mercenary_RagingWasp`
- `decoration_Davion_FederatedSunsMedalofHonor`
- `decoration_Davion_OperationRATServiceRibbon`

---

## 0.1.10 — Date of Award on tag grant

- Stamp `decorationDate_*` when a decoration company tag is added; CQ backfill for Unknown dates.

## 0.1.9 — Darius award announcements

- FoT V ForceEvent waits fixed (1+ days); announce queue / backfill / announced stats.

## 0.1.8 — Portrait position

- Reverted portrait nudge; title box expansion kept.

## 0.1.7 — Title box + citation spacing

- Longer titles; single break between Citation and Award Background.

## 0.1.6 — Titles + inline Award Background

- Decorations / Award Details titles; inline lore; hide Lost In / skill strip.

## 0.1.5 — Memorial declutter + Details dossier

- List icon+title; Awarded By / Date; Details → Additional Details panel.

## Earlier (0.1.0–0.1.4)

- Initial CQ entry, Memorial UIManager chrome, catalog / icons / citations scaffolding.
