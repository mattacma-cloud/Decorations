using System;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CompanyDecorations
{
    /// <summary>
    /// Awards chrome via UIManager UIModule APIs using the Memorial Wall prefab.
    /// Uses CreatePopupModule so Barracks' UIRoot Memorial singleton is not stolen.
    /// </summary>
    public static class MemorialAwardsUi
    {
        public const string PrefabId = "uixPrfPanl_SIM_MemorialWall-Screen";

        public sealed class BoundChrome
        {
            public GameObject Root;
            public SGMemorialWallWidget Widget;
            public HBSPilotMemorialListView ListView;
            public SGMemorialWallDetailPanel DetailPanel;
            public LocalizableText HeaderText;
            public HBSDOTweenButton BackButton;
            public HBSDOTweenButton DetailsButton;
            public RectTransform ListContent;
            public GameObject NobodyDeadOverlay;
            public GameObject LoadingNotification;
            public Image DetailPortrait;
            public LocalizableText DetailCallsign;
            public LocalizableText DetailFirstName;
            public LocalizableText DetailLastName;
            public LocalizableText DetailExpertise;
            public LocalizableText DetailTimeServed;
            public LocalizableText DetailCauseOfDeath;
            public LocalizableText DetailDeployments;
            public LocalizableText DetailInjuries;
            public LocalizableText DetailMechKills;
            public LocalizableText DetailOtherKills;
            public LocalizableText DetailEjections;
        }

        /// <summary>UIManager loads the prefab; no DataManager preload required.</summary>
        public static void EnsurePrefabReady(SimGameState sim, Action onReady)
        {
            if (onReady != null)
                onReady();
        }

        public static BoundChrome Create(SimGameState sim)
        {
            var ui = UIManager.Instance;
            if (ui == null || sim == null)
            {
                Main.LogError("Memorial Awards: UIManager or SimGameState missing.");
                return null;
            }

            SGMemorialWallWidget widget = null;
            try
            {
                Main.LogInfo("Creating Memorial Awards chrome via CreatePopupModule (" + PrefabId + ").");
                widget = ui.CreatePopupModule<SGMemorialWallWidget>("", true);
            }
            catch (Exception ex)
            {
                Main.LogError("CreatePopupModule<SGMemorialWallWidget> failed.");
                Main.LogException(ex);
            }

            if (widget == null)
            {
                try
                {
                    Main.LogInfo("CreatePopupModule returned null; trying CreateUIModule.");
                    widget = ui.CreateUIModule<SGMemorialWallWidget>("", true);
                }
                catch (Exception ex)
                {
                    Main.LogError("CreateUIModule<SGMemorialWallWidget> failed.");
                    Main.LogException(ex);
                }
            }

            if (widget == null)
            {
                Main.LogError("Could not create SGMemorialWallWidget for Awards.");
                return null;
            }

            if (IsBarracksMemorialInstance(sim, widget))
            {
                Main.LogError("UIManager returned Barracks Memorial singleton — refusing to repurpose it. Procedural fallback.");
                return null;
            }

            try
            {
                widget.Initialize(sim);
            }
            catch (Exception ex)
            {
                Main.LogError("SGMemorialWallWidget.Initialize failed (continuing with chrome bind).");
                Main.LogException(ex);
            }

            try
            {
                widget.Visible = true;
            }
            catch (Exception ex)
            {
                Main.LogException(ex);
                if (widget.gameObject != null)
                    widget.gameObject.SetActive(true);
            }

            widget.gameObject.name = "CompanyDecorations_AwardsMemorial";

            var bound = Bind(widget);
            if (bound == null)
                return null;

            if (bound.ListContent == null)
            {
                Main.LogError("Memorial list content RectTransform not found after Initialize.");
                // Keep chrome if we can still show detail; list parent fallback below
                bound.ListContent = FindFallbackListParent(widget);
            }

            if (bound.ListContent == null)
            {
                Main.LogError("No usable list parent on Memorial chrome.");
                SafeHide(widget);
                return null;
            }

            Sanitize(bound);
            WireBackButton(bound);
            ApplyChromeTitles(bound.Root);
            widget.Visible = false;
            Main.LogInfo("Awards Memorial chrome ready (UIManager). listContent=" + bound.ListContent.name);
            return bound;
        }

        public static void SetVisible(BoundChrome bound, bool visible)
        {
            if (bound == null || bound.Widget == null)
                return;
            try
            {
                bound.Widget.Visible = visible;
            }
            catch
            {
                if (bound.Root != null)
                    bound.Root.SetActive(visible);
            }

            if (visible && bound.Root != null)
                bound.Root.transform.SetAsLastSibling();
        }

        public static void Release(BoundChrome bound, SimGameState sim)
        {
            if (bound == null || bound.Widget == null)
                return;

            if (IsBarracksMemorialInstance(sim, bound.Widget))
            {
                Main.LogError("Release skipped — widget is Barracks Memorial singleton.");
                return;
            }

            try
            {
                SafeHide(bound.Widget);
                // Prefer UIModule.Pool — UIManager.PoolModule is not always publicly callable
                bound.Widget.Pool(true);
            }
            catch (Exception ex)
            {
                Main.LogException(ex);
                if (bound.Root != null)
                    UnityEngine.Object.Destroy(bound.Root);
            }
        }

        private static bool IsBarracksMemorialInstance(SimGameState sim, SGMemorialWallWidget widget)
        {
            if (sim == null || widget == null || sim.RoomManager == null)
                return false;
            try
            {
                var barracks = sim.RoomManager.BarracksRoom;
                if (barracks == null)
                    return false;
                var existing = Traverse.Create(barracks).Field("memorialWidget").GetValue<SGMemorialWallWidget>();
                return existing != null && ReferenceEquals(existing, widget);
            }
            catch
            {
                return false;
            }
        }

        private static void SafeHide(SGMemorialWallWidget widget)
        {
            if (widget == null)
                return;
            try
            {
                widget.Visible = false;
            }
            catch
            {
                if (widget.gameObject != null)
                    widget.gameObject.SetActive(false);
            }
        }

        private static BoundChrome Bind(SGMemorialWallWidget widget)
        {
            if (widget == null)
                return null;

            var tr = Traverse.Create(widget);
            var bound = new BoundChrome
            {
                Root = widget.gameObject,
                Widget = widget,
                ListView = tr.Field("listView").GetValue<HBSPilotMemorialListView>(),
                DetailPanel = tr.Field("detailPanel").GetValue<SGMemorialWallDetailPanel>(),
                HeaderText = tr.Field("totalCasualtiesText").GetValue<LocalizableText>(),
                BackButton = tr.Field("backBtn").GetValue<HBSDOTweenButton>(),
                LoadingNotification = tr.Field("loadingNotification").GetValue<GameObject>()
            };

            bound.ListContent = ResolveListContent(bound.ListView);

            if (bound.DetailPanel != null)
            {
                var d = Traverse.Create(bound.DetailPanel);
                bound.DetailsButton = d.Field("detailsButton").GetValue<HBSDOTweenButton>();
                bound.NobodyDeadOverlay = d.Field("nobodyDeadOverlay").GetValue<GameObject>();
                bound.DetailPortrait = d.Field("portrait").GetValue<Image>();
                bound.DetailCallsign = d.Field("callsignText").GetValue<LocalizableText>();
                bound.DetailFirstName = d.Field("firstNameText").GetValue<LocalizableText>();
                bound.DetailLastName = d.Field("lastNameText").GetValue<LocalizableText>();
                bound.DetailExpertise = d.Field("expertiseText").GetValue<LocalizableText>();
                bound.DetailTimeServed = d.Field("timeServedText").GetValue<LocalizableText>();
                bound.DetailCauseOfDeath = d.Field("causeOfDeathText").GetValue<LocalizableText>();
                bound.DetailDeployments = d.Field("deploymentsText").GetValue<LocalizableText>();
                bound.DetailInjuries = d.Field("injuriesText").GetValue<LocalizableText>();
                bound.DetailMechKills = d.Field("mechKillsText").GetValue<LocalizableText>();
                bound.DetailOtherKills = d.Field("otherKillsText").GetValue<LocalizableText>();
                bound.DetailEjections = d.Field("ejectionsText").GetValue<LocalizableText>();
            }

            Main.LogInfo(string.Format(
                "Memorial bind: listView={0} detail={1} header={2} back={3} listContent={4}",
                bound.ListView != null,
                bound.DetailPanel != null,
                bound.HeaderText != null,
                bound.BackButton != null,
                bound.ListContent != null ? bound.ListContent.name : "null"));

            return bound;
        }

        private static RectTransform ResolveListContent(HBSPilotMemorialListView listView)
        {
            if (listView == null)
                return null;

            var content = Traverse.Create(listView).Field("_content").GetValue<RectTransform>();
            if (content != null)
                return content;

            var scroll = listView.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null)
                return scroll.content;

            return null;
        }

        private static RectTransform FindFallbackListParent(SGMemorialWallWidget widget)
        {
            if (widget == null)
                return null;

            var rects = widget.GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < rects.Length; i++)
            {
                var n = rects[i].name ?? string.Empty;
                if (n.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                    n.IndexOf("ListContent", StringComparison.OrdinalIgnoreCase) >= 0)
                    return rects[i];
            }

            // Last resort: viewport under listView
            if (widget != null)
            {
                var listView = Traverse.Create(widget).Field("listView").GetValue<HBSPilotMemorialListView>();
                if (listView != null)
                {
                    var vp = Traverse.Create(listView).Field("_viewPort").GetValue<RectTransform>();
                    if (vp != null)
                        return vp;
                }
            }

            return null;
        }

        private static void Sanitize(BoundChrome bound)
        {
            // Disable Memorial pilot logic; keep GameObjects for chrome visuals.
            SetBehaviourEnabled(bound.Widget, false);
            SetBehaviourEnabled(bound.ListView, false);
            SetBehaviourEnabled(bound.DetailPanel, false);

            if (bound.LoadingNotification != null)
                bound.LoadingNotification.SetActive(false);

            if (bound.ListContent != null)
            {
                for (var i = bound.ListContent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(bound.ListContent.GetChild(i).gameObject);

                var vlg = bound.ListContent.GetComponent<VerticalLayoutGroup>();
                if (vlg == null)
                    vlg = bound.ListContent.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.spacing = 6f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;

                var fitter = bound.ListContent.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = bound.ListContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            DeclutterMainDetailPanel(bound);
            WireDetailsButton(bound);
        }

        private static void WireBackButton(BoundChrome bound)
        {
            if (bound.BackButton == null || bound.BackButton.OnClicked == null)
                return;

            bound.BackButton.OnClicked.RemoveAllListeners();
            bound.BackButton.OnClicked.AddListener(new UnityAction(DecorationsScreen.Dismiss));
        }

        private static void WireDetailsButton(BoundChrome bound)
        {
            if (bound == null || bound.DetailsButton == null || bound.DetailsButton.OnClicked == null)
                return;

            // Strip vanilla Memorial "Details" (opens MW dossier) so only Awards handler runs.
            bound.DetailsButton.OnClicked.RemoveAllListeners();
            bound.DetailsButton.OnClicked.AddListener(new UnityAction(DecorationsScreen.OpenSelectedAwardDetails));
        }

        private static void SetBehaviourEnabled(Behaviour b, bool enabled)
        {
            if (b != null)
                b.enabled = enabled;
        }

        public static void SetHeader(BoundChrome bound, int awardCount)
        {
            if (bound == null)
                return;

            ApplyChromeTitles(bound.Root);

            if (bound.HeaderText == null)
                return;
            // Keep count line; main screen title is set to "Decorations" via ApplyChromeTitles
            bound.HeaderText.SetText("Company Awards: {0}", awardCount);
        }

        public static void ClearDetail(BoundChrome bound)
        {
            if (bound == null)
                return;

            DeclutterMainDetailPanel(bound);

            if (bound.NobodyDeadOverlay != null)
                bound.NobodyDeadOverlay.SetActive(true);

            SetText(bound.DetailCallsign, string.Empty);
            SetText(bound.DetailFirstName, string.Empty);
            SetText(bound.DetailLastName, string.Empty);
            if (bound.DetailPortrait != null)
            {
                bound.DetailPortrait.sprite = null;
                bound.DetailPortrait.color = new Color(0.2f, 0.2f, 0.22f, 1f);
            }
        }

        public static void BindDetail(BoundChrome bound, DecorationEntry entry, string citationPlain, string citationRich, SimGameState sim)
        {
            if (bound == null || entry == null)
                return;

            DeclutterMainDetailPanel(bound);
            WireDetailsButton(bound);

            if (bound.NobodyDeadOverlay != null)
                bound.NobodyDeadOverlay.SetActive(false);

            // Title (expand Memorial callsign box so long names can wrap above the portrait)
            SetText(bound.DetailCallsign, entry.title ?? entry.companyTag);
            ExpandDetailTitleArea(bound.DetailCallsign);
            // First Name slot → Awarded By
            SetText(bound.DetailFirstName, AwardHelper.GetAwardedBy(entry));
            // Last Name slot → Date of Award
            SetText(bound.DetailLastName, AwardHelper.GetAwardDateDisplay(sim, entry.companyTag));

            if (bound.DetailPortrait != null)
                IconLoader.Assign(bound.DetailPortrait, entry.icon, sim);

            DecorationsScreen.SetSelectedAward(entry);
        }

        /// <summary>
        /// Opens vanilla Memorial Additional Details popup, retargeted for award description/citation/lore.
        /// </summary>
        public static void OpenAwardAdditionalDetails(BoundChrome bound, DecorationEntry entry, SimGameState sim)
        {
            if (entry == null || sim == null)
                return;

            var ui = UIManager.Instance;
            if (ui == null)
                return;

            SGMemorialWallAdditionalDetailsPanel popup = null;
            try
            {
                popup = ui.GetOrCreatePopupModule<SGMemorialWallAdditionalDetailsPanel>("", true);
            }
            catch (Exception ex)
            {
                Main.LogError("GetOrCreatePopupModule AdditionalDetails failed.");
                Main.LogException(ex);
                return;
            }

            if (popup == null)
            {
                Main.LogError("AdditionalDetails popup was null.");
                return;
            }

            try
            {
                if (bound != null && bound.Widget != null)
                    popup.Initialize(sim, bound.Widget);
            }
            catch (Exception ex)
            {
                Main.LogInfo("AdditionalDetails.Initialize: " + ex.Message);
            }

            DeclutterAdditionalDetailsPanel(popup);
            FillAdditionalDetailsPanel(popup, entry, sim);
            ApplyAwardDetailsTitles(popup.gameObject);

            var tr = Traverse.Create(popup);
            tr.Field("DoneCB").SetValue(new UnityAction(delegate
            {
                CloseAwardAdditionalDetails();
            }));

            try
            {
                popup.ShowAdditionalDetails();
            }
            catch
            {
                popup.Visible = true;
            }

            _awardDetailsPopup = popup;
            Main.LogInfo("Opened Memorial Additional Details for award " + entry.companyTag);
        }

        public static bool IsAwardAdditionalDetailsOpen
        {
            get
            {
                return _awardDetailsPopup != null &&
                       _awardDetailsPopup.gameObject != null &&
                       _awardDetailsPopup.Visible;
            }
        }

        public static void CloseAwardAdditionalDetails()
        {
            if (_awardDetailsPopup == null)
                return;

            try
            {
                _awardDetailsPopup.Close();
            }
            catch
            {
                try { _awardDetailsPopup.Visible = false; }
                catch { /* ignore */ }
            }

            _awardDetailsPopup = null;
        }

        private static SGMemorialWallAdditionalDetailsPanel _awardDetailsPopup;

        private static void DeclutterMainDetailPanel(BoundChrome bound)
        {
            if (bound == null || bound.DetailPanel == null)
                return;

            var d = Traverse.Create(bound.DetailPanel);

            // Upper-right style icons (veteran / ronin)
            HideGo(d.Field("veteranIcon").GetValue<Component>());
            HideGo(d.Field("roninIcon").GetValue<Component>());

            // Lower-left ability icon placeholders / frames
            HideComponentList(d.Field("abilities").GetValue<object>());
            HideComponentList(d.Field("abilityFrames").GetValue<object>());

            // Bold specialty line under portrait
            HideGo(bound.DetailExpertise);

            // Battle stats values
            HideGo(bound.DetailTimeServed);
            HideGo(bound.DetailCauseOfDeath);
            HideGo(bound.DetailDeployments);
            HideGo(bound.DetailInjuries);
            HideGo(bound.DetailMechKills);
            HideGo(bound.DetailOtherKills);
            HideGo(bound.DetailEjections);

            RelabelAndHideStaticLabels(bound.DetailPanel.gameObject, true);
        }

        private static void DeclutterAdditionalDetailsPanel(SGMemorialWallAdditionalDetailsPanel popup)
        {
            if (popup == null)
                return;

            var d = Traverse.Create(popup);
            HideGo(d.Field("veteranIcon").GetValue<Component>());
            HideGo(d.Field("roninIcon").GetValue<Component>());
            HideComponentList(d.Field("abilities").GetValue<object>());
            HideComponentList(d.Field("abilityFrames").GetValue<object>());
            HideComponentList(d.Field("emptyAbilityObjs").GetValue<object>());
            HideComponentList(d.Field("activeAbilityObjs").GetValue<object>());

            HideGo(d.Field("gunneryText").GetValue<LocalizableText>());
            HideGo(d.Field("pilotingText").GetValue<LocalizableText>());
            HideGo(d.Field("tacticsText").GetValue<LocalizableText>());
            HideGo(d.Field("gutsText").GetValue<LocalizableText>());
            HideGo(d.Field("specializationText").GetValue<LocalizableText>());
            HideGo(d.Field("diedInText").GetValue<LocalizableText>());

            // Hide parent rows that still show GUN/PLT/GUT/TAC chrome
            HideNamedChildren(popup.gameObject, new[]
            {
                "gunnery", "piloting", "tactics", "guts", "skills", "skill", "lost", "died"
            });

            var tagViewer = d.Field("tagViewer").GetValue<Component>();
            if (tagViewer != null)
                tagViewer.gameObject.SetActive(false);

            RelabelAndHideStaticLabels(popup.gameObject, false);
            ApplyAwardDetailsTitles(popup.gameObject);
        }

        private static void FillAdditionalDetailsPanel(SGMemorialWallAdditionalDetailsPanel popup, DecorationEntry entry, SimGameState sim)
        {
            if (popup == null || entry == null)
                return;

            var d = Traverse.Create(popup);
            var portrait = d.Field("portrait").GetValue<Image>();
            var callsign = d.Field("callsignText").GetValue<LocalizableText>();
            var first = d.Field("firstNameText").GetValue<LocalizableText>();
            var last = d.Field("lastNameText").GetValue<LocalizableText>();
            var bio = d.Field("biographyText").GetValue<LocalizableText>();

            if (portrait != null)
                IconLoader.Assign(portrait, entry.icon, sim);

            SetText(callsign, entry.title ?? entry.companyTag);
            SetText(first, AwardHelper.GetAwardedBy(entry));
            SetText(last, AwardHelper.GetAwardDateDisplay(sim, entry.companyTag));

            var citation = AwardHelper.GetCitation(sim, entry.companyTag);
            var sections = new System.Collections.Generic.List<string>(3);

            // Catalog description (separate block)
            if (!string.IsNullOrEmpty(entry.description))
            {
                sections.Add("<b>Description</b>\n" +
                    TrimSection(CitationTextUtil.ToPlainDisplayText(entry.description)));
            }

            // Citation (templates often end with \n\n — trim so section join is a single break)
            if (!string.IsNullOrEmpty(citation))
            {
                sections.Add("<b>Citation</b>\n" +
                    TrimSection(CitationTextUtil.ToPlainDisplayText(citation)));
            }

            // Lore BaseDescriptionDef inline — no third lore popup
            if (!string.IsNullOrEmpty(entry.loreId))
            {
                BaseDescriptionDef loreDef;
                if (CitationTextUtil.TryGetBaseDescription(sim, entry.loreId, out loreDef) && loreDef != null)
                {
                    var loreBody = TrimSection(CitationTextUtil.ToPlainDisplayText(
                        CitationTextUtil.FormatLorePopupBody(loreDef)));
                    if (!string.IsNullOrEmpty(loreBody))
                        sections.Add("<b>Award Background</b>\n" + loreBody);
                }
                else
                    Main.LogInfo("Award Background loreId not found: " + entry.loreId);
            }

            SetText(bio, string.Join("\n\n", sections.ToArray()));

            // Do not attach lore-link click handler — background is inline only
            if (bio != null)
            {
                var linkHandler = bio.GetComponent<TmpLoreLinkClickHandler>();
                if (linkHandler != null)
                    UnityEngine.Object.Destroy(linkHandler);
                var tmp = bio.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                    tmp.raycastTarget = false;
            }
        }

        private static void ApplyChromeTitles(GameObject root)
        {
            ReplaceTitleTexts(root, "Memorial Wall", "Decorations");
        }

        private static void ApplyAwardDetailsTitles(GameObject root)
        {
            ReplaceTitleTexts(root, "MechWarriors Details", "Award Details");
            ReplaceTitleTexts(root, "MechWarrior Details", "Award Details");
            ReplaceTitleTexts(root, "MechWarrior Detail", "Award Details");
        }

        private static void ReplaceTitleTexts(GameObject root, string from, string to)
        {
            if (root == null || string.IsNullOrEmpty(from))
                return;

            var texts = root.GetComponentsInChildren<LocalizableText>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var lt = texts[i];
                if (lt == null)
                    continue;
                var raw = GetLabelProbe(lt);
                if (string.IsNullOrEmpty(raw))
                    continue;
                if (ContainsInsensitive(raw, from))
                    lt.SetText(to);
            }

            // Some titles are plain TMP without LocalizableText
            var tmps = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            for (var i = 0; i < tmps.Length; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || string.IsNullOrEmpty(tmp.text))
                    continue;
                if (ContainsInsensitive(tmp.text, from))
                    tmp.text = to;
            }
        }

        private static void HideNamedChildren(GameObject root, string[] nameParts)
        {
            if (root == null || nameParts == null)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.gameObject == root)
                    continue;
                var n = t.name ?? string.Empty;
                for (var p = 0; p < nameParts.Length; p++)
                {
                    if (n.IndexOf(nameParts[p], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        t.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        private static void RelabelAndHideStaticLabels(GameObject root, bool mainPanel)
        {
            if (root == null)
                return;

            var texts = root.GetComponentsInChildren<LocalizableText>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var lt = texts[i];
                if (lt == null)
                    continue;

                var raw = GetLabelProbe(lt);
                if (string.IsNullOrEmpty(raw))
                    continue;

                if (ContainsInsensitive(raw, "First Name"))
                {
                    lt.SetText("Awarded By");
                    continue;
                }
                if (ContainsInsensitive(raw, "Last Name"))
                {
                    lt.SetText("Date of Award");
                    continue;
                }

                if (ShouldHideMemorialLabel(raw, mainPanel))
                    lt.gameObject.SetActive(false);
            }
        }

        private static bool ShouldHideMemorialLabel(string raw, bool mainPanel)
        {
            string[] hide =
            {
                "Battle Stats", "Deployments", "Injuries", "Mech Kills", "Other Kills", "Ejections",
                "Time Served", "Cause of Death", "Died In", "Died in", "Lost In", "Lost in",
                "Specialization", "Gunnery", "Piloting", "Tactics", "Guts", "Expertise",
                "Biography", "Abilities"
            };
            for (var i = 0; i < hide.Length; i++)
            {
                if (ContainsInsensitive(raw, hide[i]))
                    return true;
            }

            // Skill abbreviation strip (exact-ish tokens)
            var trimmed = raw.Trim();
            if (trimmed.Equals("GUN", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("PLT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("GUT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("TAC", StringComparison.OrdinalIgnoreCase) ||
                (ContainsInsensitive(trimmed, "GUN") && ContainsInsensitive(trimmed, "PLT")))
                return true;

            return false;
        }

        private static string GetLabelProbe(LocalizableText lt)
        {
            try
            {
                if (!string.IsNullOrEmpty(lt.text))
                    return lt.text;
            }
            catch { /* ignore */ }

            try
            {
                var nonLoc = Traverse.Create(lt).Field("m_nonLocalizedText").GetValue<string>();
                if (!string.IsNullOrEmpty(nonLoc))
                    return nonLoc;
            }
            catch { /* ignore */ }

            return lt.gameObject != null ? lt.gameObject.name : string.Empty;
        }

        private static bool ContainsInsensitive(string hay, string needle)
        {
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void HideComponentList(object listObj)
        {
            if (listObj == null)
                return;

            var list = listObj as System.Collections.IList;
            if (list == null)
                return;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                    continue;
                var comp = item as Component;
                if (comp != null)
                {
                    comp.gameObject.SetActive(false);
                    continue;
                }
                var go = item as GameObject;
                if (go != null)
                    go.SetActive(false);
            }
        }

        private static void HideGo(Component c)
        {
            if (c != null)
                c.gameObject.SetActive(false);
        }

        private static void SetText(LocalizableText text, string value)
        {
            if (text == null)
                return;
            text.gameObject.SetActive(true);
            text.SetText(value ?? string.Empty);
        }

        private static string TrimSection(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
        }

        /// <summary>
        /// Memorial callsign slot is short; long decoration titles wrap into the portrait.
        /// Widen/tall-en the box and allow wrap overflow. Portrait stays in its vanilla frame.
        /// </summary>
        private static void ExpandDetailTitleArea(LocalizableText callsign)
        {
            if (callsign == null)
                return;

            try
            {
                var tmp = callsign.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.enableWordWrapping = true;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.alignment = TextAlignmentOptions.Top;
                    // Slightly smaller than MW callsign so two lines clear the image more often
                    if (tmp.fontSize > 34f)
                        tmp.fontSize = 34f;
                }

                var rt = callsign.rectTransform;
                if (rt != null)
                {
                    // Prefer height growth for a second line; also widen when not stretch-anchored
                    var size = rt.sizeDelta;
                    if (Mathf.Abs(rt.anchorMin.x - rt.anchorMax.x) < 0.01f && size.x > 0f && size.x < 520f)
                        size.x = 520f;
                    if (size.y > 0f && size.y < 96f)
                        size.y = 96f;
                    else if (size.y <= 0f)
                    {
                        // Stretch anchors: grow by lowering offsetMin (more height downward)
                        var min = rt.offsetMin;
                        var max = rt.offsetMax;
                        var height = max.y - min.y;
                        if (height > 0f && height < 96f)
                        {
                            var grow = 96f - height;
                            rt.offsetMin = new Vector2(min.x, min.y - grow);
                        }
                        // Widen stretched box toward the right if there is spare margin
                        if (rt.offsetMax.x > -40f)
                            rt.offsetMax = new Vector2(-24f, rt.offsetMax.y);
                        if (rt.offsetMin.x < 40f)
                            rt.offsetMin = new Vector2(Mathf.Min(rt.offsetMin.x, 8f), rt.offsetMin.y);
                    }
                    rt.sizeDelta = size;

                    // Lift title a little so the second line sits above the portrait
                    var pos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(pos.x, pos.y + 8f);
                }
            }
            catch (Exception ex)
            {
                Main.LogInfo("ExpandDetailTitleArea: " + ex.Message);
            }
        }
    }
}
