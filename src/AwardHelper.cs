using System;
using BattleTech;
using HBS.Collections;

namespace CompanyDecorations
{
    /// <summary>
    /// Public grant API for lore packs / other mods.
    /// Citation company stat: decorationCite_&lt;Faction_AwardName&gt;
    /// </summary>
    public static class AwardHelper
    {
        public const string TagPrefix = "decoration_";
        public const string CitePrefix = "decorationCite_";
        public const string DatePrefix = "decorationDate_";
        public const string AnnouncedPrefix = "decorationAnnounced_";
        public const string QueuedPrefix = "decorationAnnounceQueued_";
        public const string AwardEventPrefix = "event_decoration_";

        public static string CitationStatName(string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;
            if (companyTag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                return CitePrefix + companyTag.Substring(TagPrefix.Length);
            return CitePrefix + companyTag;
        }

        public static string DateStatName(string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;
            if (companyTag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                return DatePrefix + companyTag.Substring(TagPrefix.Length);
            return DatePrefix + companyTag;
        }

        public static string CompanyTagFromAwardEventId(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
                return null;
            if (eventId.StartsWith(AwardEventPrefix, StringComparison.OrdinalIgnoreCase))
                return TagPrefix + eventId.Substring(AwardEventPrefix.Length);
            return null;
        }

        public static void Grant(SimGameState sim, string companyTag, string citationText)
        {
            Grant(sim, companyTag, citationText, true);
        }

        /// <summary>
        /// Add tag + citation (+ date). When announce is true, queue the Darius award event
        /// for the next travel day (same pattern as lore-pack ForceEvents with MinDaysWait 1).
        /// </summary>
        public static void Grant(SimGameState sim, string companyTag, string citationText, bool announce)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag))
                return;

            try
            {
                if (sim.CompanyTags == null)
                    return;

                if (!sim.CompanyTags.Contains(companyTag))
                    sim.CompanyTags.Add(companyTag);

                SetCitation(sim, companyTag, citationText);
                EnsureAwardDate(sim, companyTag);
                if (announce)
                    QueueAwardAnnouncement(sim, companyTag, 1, 1);
                Main.LogInfo("AwardHelper.Grant: " + companyTag + " announce=" + announce);
            }
            catch (Exception ex)
            {
                Main.LogError("AwardHelper.Grant failed for " + companyTag);
                Main.LogException(ex);
            }
        }

        /// <summary>
        /// Queue a Darius-style SimGameEventDef via AddSpecialEvent.
        /// Prefer MinDaysWait &gt;= 1 after flashpoint completion — 0-day waits are often lost.
        /// </summary>
        public static void QueueAwardAnnouncement(SimGameState sim, string companyTag, int minDaysWait, int maxDaysWait)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag))
                return;

            if (WasAnnounced(sim, companyTag) || WasAnnounceQueued(sim, companyTag))
            {
                Main.LogInfo("Skip award announcement (already announced/queued): " + companyTag);
                return;
            }

            var eventId = ResolveAwardEventId(companyTag);
            if (string.IsNullOrEmpty(eventId))
            {
                Main.LogError("No award event id for " + companyTag);
                return;
            }

            if (sim.DataManager == null ||
                !sim.DataManager.Exists(BattleTechResourceType.SimGameEventDef, eventId))
            {
                Main.LogInfo("Award event def not loaded; skip announce: " + eventId);
                return;
            }

            if (minDaysWait < 0)
                minDaysWait = 0;
            if (maxDaysWait < minDaysWait)
                maxDaysWait = minDaysWait;

            try
            {
                var forced = new SimGameForcedEvent
                {
                    Scope = EventScope.Company,
                    EventID = eventId,
                    MinDaysWait = minDaysWait,
                    MaxDaysWait = maxDaysWait,
                    Probability = 100,
                    RetainPilot = false
                };
                sim.AddSpecialEvent(forced, null);
                SetAnnounceQueued(sim, companyTag);
                Main.LogInfo(string.Format(
                    "Queued award announcement {0} in {1}-{2} day(s)",
                    eventId, minDaysWait, maxDaysWait));
            }
            catch (Exception ex)
            {
                Main.LogError("QueueAwardAnnouncement failed for " + eventId);
                Main.LogException(ex);
            }
        }

        public static string ResolveAwardEventId(string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;

            if (Main.Catalog != null)
            {
                var entry = Main.Catalog.FindByCompanyTag(companyTag);
                if (entry != null && !string.IsNullOrEmpty(entry.awardEventId))
                    return entry.awardEventId;
            }

            if (companyTag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                return AwardEventPrefix + companyTag.Substring(TagPrefix.Length);
            return AwardEventPrefix + companyTag;
        }

        public static string AnnouncedStatName(string companyTag)
        {
            return StatNameFromTag(AnnouncedPrefix, companyTag);
        }

        public static string AnnounceQueuedStatName(string companyTag)
        {
            return StatNameFromTag(QueuedPrefix, companyTag);
        }

        private static string StatNameFromTag(string prefix, string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;
            if (companyTag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                return prefix + companyTag.Substring(TagPrefix.Length);
            return prefix + companyTag;
        }

        public static bool WasAnnounced(SimGameState sim, string companyTag)
        {
            return HasCompanyStringStat(sim, AnnouncedStatName(companyTag));
        }

        public static bool WasAnnounceQueued(SimGameState sim, string companyTag)
        {
            return HasCompanyStringStat(sim, AnnounceQueuedStatName(companyTag));
        }

        public static void MarkAnnounced(SimGameState sim, string companyTag)
        {
            SetCompanyStringStat(sim, AnnouncedStatName(companyTag), "1");
        }

        public static void MarkAnnounceQueued(SimGameState sim, string companyTag)
        {
            SetAnnounceQueued(sim, companyTag);
        }

        private static void SetAnnounceQueued(SimGameState sim, string companyTag)
        {
            SetCompanyStringStat(sim, AnnounceQueuedStatName(companyTag), "1");
        }

        private static bool HasCompanyStringStat(SimGameState sim, string statName)
        {
            if (sim == null || sim.CompanyStats == null || string.IsNullOrEmpty(statName))
                return false;
            if (!sim.CompanyStats.ContainsStatistic(statName))
                return false;
            try
            {
                var stat = sim.CompanyStats.GetStatistic(statName);
                return stat != null && !string.IsNullOrEmpty(stat.Value<string>());
            }
            catch
            {
                return false;
            }
        }

        private static void SetCompanyStringStat(SimGameState sim, string statName, string value)
        {
            if (sim == null || string.IsNullOrEmpty(statName) || value == null)
                return;
            try
            {
                sim.SetCompanyStat(statName, value);
            }
            catch (Exception ex)
            {
                Main.LogError("Failed to set company stat " + statName);
                Main.LogException(ex);
            }
        }

        public static void SetCitation(SimGameState sim, string companyTag, string citationText)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag) || citationText == null)
                return;

            var resolved = ResolveTokens(sim, citationText);
            var statName = CitationStatName(companyTag);
            if (string.IsNullOrEmpty(statName))
                return;

            try
            {
                sim.SetCompanyStat(statName, resolved);
                Main.LogInfo("Set citation stat " + statName);
            }
            catch (Exception ex)
            {
                Main.LogError("Failed to set citation stat " + statName);
                Main.LogException(ex);
            }
        }

        public static string GetCitation(SimGameState sim, string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;

            if (sim != null && sim.CompanyStats != null)
            {
                var statName = CitationStatName(companyTag);
                if (!string.IsNullOrEmpty(statName) && sim.CompanyStats.ContainsStatistic(statName))
                {
                    try
                    {
                        var stat = sim.CompanyStats.GetStatistic(statName);
                        if (stat != null)
                        {
                            var stored = stat.Value<string>();
                            if (!string.IsNullOrEmpty(stored))
                                return stored;
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.LogError("Failed to read citation stat " + statName);
                        Main.LogException(ex);
                    }
                }
            }

            // Fallback: unresolved template from citations folder (e.g. save-edited tag)
            var template = CitationRegistry.GetTemplate(companyTag);
            if (string.IsNullOrEmpty(template))
                return null;
            return ResolveTokens(sim, template);
        }

        public static string ResolveTokens(SimGameState sim, string text)
        {
            if (string.IsNullOrEmpty(text) || sim == null)
                return text;

            var companyName = sim.CompanyName;
            if (string.IsNullOrEmpty(companyName))
                companyName = "your company";

            return text
                .Replace("{CompanyName}", companyName)
                .Replace("{COMPANY.Name}", companyName)
                .Replace("{Company.Name}", companyName)
                .Replace("{company.name}", companyName)
                .Replace("{company.Name}", companyName);
        }

        public static void TryStoreCitationFromAwardEvent(SimGameState sim, SimGameEventDef eventDef)
        {
            if (sim == null || eventDef == null || eventDef.Description == null)
                return;

            var eventId = eventDef.Description.Id;
            var companyTag = CompanyTagFromAwardEventId(eventId);
            if (string.IsNullOrEmpty(companyTag) && Main.Catalog != null)
            {
                var entry = Main.Catalog.FindByAwardEventId(eventId);
                if (entry != null)
                    companyTag = entry.companyTag;
            }

            if (string.IsNullOrEmpty(companyTag))
                return;

            // Prefer Lore-Packs/citations (or Mods/*/citations); fall back to event Details
            var citation = CitationRegistry.GetTemplate(companyTag);
            if (string.IsNullOrEmpty(citation))
                citation = eventDef.Description.Details;
            if (string.IsNullOrEmpty(citation))
                return;

            if (sim.CompanyTags != null && !sim.CompanyTags.Contains(companyTag))
                sim.CompanyTags.Add(companyTag);

            SetCitation(sim, companyTag, citation);
            EnsureAwardDate(sim, companyTag);
            MarkAnnounced(sim, companyTag);
        }

        /// <summary>
        /// Called when a decoration_* company tag is first applied (milestone AddedTags, Grant, etc.).
        /// Stamps Date of Award immediately — do not wait for the Darius popup.
        /// </summary>
        public static void OnDecorationTagGranted(SimGameState sim, string companyTag)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag))
                return;
            if (!companyTag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            EnsureAwardDate(sim, companyTag);
        }

        /// <summary>
        /// Stamp Date of Award for any earned decoration that still shows Unknown.
        /// Uses CurrentDate when the original grant day was never recorded (e.g. prior FoT V run).
        /// </summary>
        public static void BackfillMissingAwardDates(SimGameState sim)
        {
            if (sim == null || sim.CompanyTags == null || Main.Catalog == null)
                return;

            var earned = Main.Catalog.Earned(sim.CompanyTags);
            for (var i = 0; i < earned.Count; i++)
            {
                var entry = earned[i];
                if (entry == null || string.IsNullOrEmpty(entry.companyTag))
                    continue;
                EnsureAwardDate(sim, entry.companyTag);
            }
        }

        /// <summary>
        /// One-shot recovery: if a decoration tag exists but Darius never fired (common when
        /// ForceEvents used MinDaysWait 0 during flashpoint end), queue the award event.
        /// Skips tags already announced or queued by this mod.
        /// </summary>
        public static void BackfillMissingAwardAnnouncements(SimGameState sim)
        {
            if (sim == null || sim.CompanyTags == null || Main.Catalog == null)
                return;

            BackfillMissingAwardDates(sim);

            var earned = Main.Catalog.Earned(sim.CompanyTags);
            for (var i = 0; i < earned.Count; i++)
            {
                var entry = earned[i];
                if (entry == null || string.IsNullOrEmpty(entry.companyTag))
                    continue;
                if (WasAnnounced(sim, entry.companyTag) || WasAnnounceQueued(sim, entry.companyTag))
                    continue;

                var eventId = ResolveAwardEventId(entry.companyTag);
                if (string.IsNullOrEmpty(eventId) ||
                    sim.DataManager == null ||
                    !sim.DataManager.Exists(BattleTechResourceType.SimGameEventDef, eventId))
                    continue;

                // Stagger so multiple awards do not collide on the same interrupt
                QueueAwardAnnouncement(sim, entry.companyTag, 1 + i, 1 + i);
            }
        }

        public static void EnsureAwardDate(SimGameState sim, string companyTag)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag))
                return;

            var existing = GetAwardDateRaw(sim, companyTag);
            if (!string.IsNullOrEmpty(existing))
                return;

            try
            {
                SetAwardDate(sim, companyTag, sim.CurrentDate);
            }
            catch (Exception ex)
            {
                Main.LogError("EnsureAwardDate failed for " + companyTag);
                Main.LogException(ex);
            }
        }

        public static void SetAwardDate(SimGameState sim, string companyTag, DateTime date)
        {
            if (sim == null || string.IsNullOrEmpty(companyTag))
                return;

            var statName = DateStatName(companyTag);
            if (string.IsNullOrEmpty(statName))
                return;

            // Store ISO-ish for stability; display formatter reads it back
            var stored = date.ToString("yyyy-MM-dd");
            sim.SetCompanyStat(statName, stored);
            Main.LogInfo("Set award date stat " + statName + " = " + stored);
        }

        public static string GetAwardDateDisplay(SimGameState sim, string companyTag)
        {
            var raw = GetAwardDateRaw(sim, companyTag);
            if (string.IsNullOrEmpty(raw))
                return "Unknown";

            DateTime parsed;
            if (DateTime.TryParse(raw, out parsed))
                return parsed.ToString("dd MMM yyyy");
            return raw;
        }

        public static string GetAwardDateRaw(SimGameState sim, string companyTag)
        {
            if (sim == null || sim.CompanyStats == null || string.IsNullOrEmpty(companyTag))
                return null;

            var statName = DateStatName(companyTag);
            if (string.IsNullOrEmpty(statName) || !sim.CompanyStats.ContainsStatistic(statName))
                return null;

            try
            {
                var stat = sim.CompanyStats.GetStatistic(statName);
                if (stat == null)
                    return null;
                return stat.Value<string>();
            }
            catch (Exception ex)
            {
                Main.LogError("Failed to read award date " + statName);
                Main.LogException(ex);
                return null;
            }
        }

        public static string GetAwardedBy(DecorationEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(entry.awardedBy))
                return entry.awardedBy;
            if (!string.IsNullOrEmpty(entry.category))
                return entry.category;
            return string.Empty;
        }
    }
}
