using System;
using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using HarmonyLib;
using HBS.Collections;

namespace CompanyDecorations.Patches
{
    [HarmonyPatch(typeof(SGRoomController_CptQuarters), "EnterRoom")]
    public static class CptQuarters_EnterRoom_Patch
    {
        public static void Postfix(SGRoomController_CptQuarters __instance)
        {
            if (!Main.Settings.HasEntryPoint("CaptainsQuarters") &&
                !Main.Settings.HasEntryPoint("CaptainQuarters") &&
                !Main.Settings.HasEntryPoint("CQ"))
                return;

            DecorationsButtonInjector.EnsureCaptainsQuartersEntry(__instance);
        }
    }

    [HarmonyPatch(typeof(SGRoomController_CptQuarters), "LeaveRoom")]
    public static class CptQuarters_LeaveRoom_Patch
    {
        public static void Prefix()
        {
            if (!Main.Settings.HasEntryPoint("CaptainsQuarters") &&
                !Main.Settings.HasEntryPoint("CaptainQuarters") &&
                !Main.Settings.HasEntryPoint("CQ"))
                return;

            DecorationsButtonInjector.Cleanup();
        }
    }

    [HarmonyPatch(typeof(SimGameState), "OnEventTriggered")]
    public static class SimGameState_OnEventTriggered_Patch
    {
        public static void Postfix(SimGameState __instance, SimGameEventDef eventDef)
        {
            AwardHelper.TryStoreCitationFromAwardEvent(__instance, eventDef);
        }
    }

    /// <summary>
    /// When lore-pack ForceEvents queue an award popup, mark it queued so Backfill does not double-queue.
    /// </summary>
    [HarmonyPatch(typeof(SimGameState), "AddSpecialEvent")]
    public static class SimGameState_AddSpecialEvent_Patch
    {
        public static void Postfix(SimGameState __instance, SimGameForcedEvent evt)
        {
            if (__instance == null || evt == null || string.IsNullOrEmpty(evt.EventID))
                return;

            var companyTag = AwardHelper.CompanyTagFromAwardEventId(evt.EventID);
            if (string.IsNullOrEmpty(companyTag) && Main.Catalog != null)
            {
                var entry = Main.Catalog.FindByAwardEventId(evt.EventID);
                if (entry != null)
                    companyTag = entry.companyTag;
            }

            if (string.IsNullOrEmpty(companyTag))
                return;

            AwardHelper.MarkAnnounceQueued(__instance, companyTag);
        }
    }

    [HarmonyPatch(typeof(SGRoomController_CptQuarters), "EnterRoom")]
    public static class CptQuarters_EnterRoom_Backfill_Patch
    {
        public static void Postfix(SGRoomController_CptQuarters __instance)
        {
            try
            {
                var sim = Traverse.Create(__instance).Field("simState").GetValue<SimGameState>();
                AwardHelper.BackfillMissingAwardAnnouncements(sim);
            }
            catch (Exception ex)
            {
                Main.LogError("BackfillMissingAwardAnnouncements failed.");
                Main.LogException(ex);
            }
        }
    }

    /// <summary>
    /// Stamp Date of Award when a decoration_* tag is added to CompanyTags (milestone path).
    /// </summary>
    [HarmonyPatch(typeof(TagSet), "Add", new Type[] { typeof(string) })]
    public static class TagSet_Add_Patch
    {
        public static void Postfix(TagSet __instance, string tag)
        {
            DecorationTagHooks.TryHandleDecorationTag(__instance, tag);
        }
    }

    [HarmonyPatch(typeof(TagSet), "AddRange", new Type[] { typeof(IEnumerable<string>) })]
    public static class TagSet_AddRange_Patch
    {
        public static void Postfix(TagSet __instance, IEnumerable<string> tags)
        {
            if (tags == null)
                return;
            foreach (var tag in tags)
                DecorationTagHooks.TryHandleDecorationTag(__instance, tag);
        }
    }

    [HarmonyPatch(typeof(TagSet), "AddRange", new Type[] { typeof(IEnumerable<string>), typeof(bool) })]
    public static class TagSet_AddRangeBool_Patch
    {
        public static void Postfix(TagSet __instance, IEnumerable<string> tags)
        {
            if (tags == null)
                return;
            foreach (var tag in tags)
                DecorationTagHooks.TryHandleDecorationTag(__instance, tag);
        }
    }

    internal static class DecorationTagHooks
    {
        internal static void TryHandleDecorationTag(TagSet tagSet, string tag)
        {
            if (tagSet == null || string.IsNullOrEmpty(tag))
                return;
            if (!tag.StartsWith(AwardHelper.TagPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var game = UnityGameInstance.BattleTechGame;
                if (game == null)
                    return;
                var sim = game.Simulation;
                if (sim == null || sim.CompanyTags == null)
                    return;
                if (!object.ReferenceEquals(sim.CompanyTags, tagSet))
                    return;

                AwardHelper.OnDecorationTagGranted(sim, tag);
            }
            catch (Exception ex)
            {
                Main.LogError("Decoration tag grant hook failed for " + tag);
                Main.LogException(ex);
            }
        }
    }
}
