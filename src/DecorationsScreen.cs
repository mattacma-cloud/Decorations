using System;
using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CompanyDecorations
{
    /// <summary>
    /// Awards UI: prefers Memorial Wall chrome (list + detail slots), procedural fallback.
    /// </summary>
    public static class DecorationsScreen
    {
        private static GameObject _root;
        private static Transform _listContent;
        private static TextMeshProUGUI _emptyLabel;
        private static GameObject _detailRoot;
        private static Image _detailIcon;
        private static TextMeshProUGUI _detailTitle;
        private static TextMeshProUGUI _detailBody;
        private static RectTransform _detailBodyContentRt;
        private static RectTransform _detailBodyTextRt;
        private static GameObject _loreRoot;
        private static Image _loreIcon;
        private static TextMeshProUGUI _loreTitle;
        private static TextMeshProUGUI _loreBody;
        private static RectTransform _loreBodyTextRt;
        private static string _currentAwardIconId;
        private static SimGameState _sim;
        private static TMP_FontAsset _fontMedium;
        private static TMP_FontAsset _fontBlack;
        private static MemorialAwardsUi.BoundChrome _memorial;
        private static bool _usingMemorial;
        private static DecorationEntry _selectedAward;

        public static bool IsOpen
        {
            get { return _root != null && (_usingMemorial ? (_memorial != null && _memorial.Widget != null && _memorial.Widget.Visible) : _root.activeSelf); }
        }

        public static bool IsLoreOpen
        {
            get { return _loreRoot != null && _loreRoot.activeSelf; }
        }

        public static bool IsDetailOpen
        {
            get { return _detailRoot != null && _detailRoot.activeSelf; }
        }

        public static void Show(SimGameState sim)
        {
            if (sim == null)
                return;

            _sim = sim;
            MemorialAwardsUi.EnsurePrefabReady(sim, delegate
            {
                try
                {
                    EnsureUi();
                    HideLore();
                    HideDetail();
                    Populate(Main.Catalog.Earned(sim.CompanyTags));
                    if (_usingMemorial && _memorial != null)
                        MemorialAwardsUi.SetVisible(_memorial, true);
                    else if (_root != null)
                    {
                        _root.SetActive(true);
                        _root.transform.SetAsLastSibling();
                    }
                    Main.LogInfo("DecorationsScreen opened. memorial=" + _usingMemorial);
                }
                catch (Exception ex)
                {
                    Main.LogError("DecorationsScreen.Show failed.");
                    Main.LogException(ex);
                }
            });
        }

        public static void Dismiss()
        {
            MemorialAwardsUi.CloseAwardAdditionalDetails();
            HideLore();
            HideDetail();
            if (_usingMemorial && _memorial != null)
                MemorialAwardsUi.SetVisible(_memorial, false);
            else if (_root != null)
                _root.SetActive(false);
            Main.LogInfo("DecorationsScreen closed.");
        }

        /// <summary>Called when leaving CQ so Memorial clone can return to the pool.</summary>
        public static void Release()
        {
            Dismiss();
            if (_memorial != null)
            {
                MemorialAwardsUi.Release(_memorial, _sim);
                _memorial = null;
            }
            if (_root != null && !_usingMemorial)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _listContent = null;
            _emptyLabel = null;
            _detailRoot = null;
            _loreRoot = null;
            _usingMemorial = false;
        }

        public static void HandleEscape()
        {
            if (MemorialAwardsUi.IsAwardAdditionalDetailsOpen)
            {
                MemorialAwardsUi.CloseAwardAdditionalDetails();
                return;
            }
            if (IsLoreOpen)
            {
                HideLore();
                return;
            }
            if (IsDetailOpen)
            {
                HideDetail();
                return;
            }
            Dismiss();
        }

        public static void SetSelectedAward(DecorationEntry entry)
        {
            _selectedAward = entry;
            if (entry != null)
                _currentAwardIconId = entry.icon;
        }

        public static void OpenSelectedAwardDetails()
        {
            if (_selectedAward == null)
                return;

            if (_usingMemorial && _memorial != null)
            {
                MemorialAwardsUi.OpenAwardAdditionalDetails(_memorial, _selectedAward, _sim);
                return;
            }

            // Procedural fallback
            var citation = AwardHelper.GetCitation(_sim, _selectedAward.companyTag);
            var body = BuildDetailBody(_selectedAward.description, citation);
            ShowDetail(_selectedAward, body);
        }

        public static void ShowLoreFromLink(string linkId)
        {
            if (string.IsNullOrEmpty(linkId) || _sim == null)
                return;

            var id = linkId;
            if (id.StartsWith("bdd:", StringComparison.OrdinalIgnoreCase))
                id = id.Substring(4);

            BaseDescriptionDef def;
            if (!CitationTextUtil.TryGetBaseDescription(_sim, id, out def))
            {
                Main.LogError("Lore link BaseDescriptionDef not found: " + id);
                return;
            }

            EnsureUi();
            if (_loreRoot == null || _loreTitle == null || _loreBody == null)
                return;

            _loreTitle.text = def.Name ?? id;
            _loreBody.text = CitationTextUtil.FormatLorePopupBody(def);
            if (_loreIcon != null)
            {
                IconLoader.Assign(_loreIcon, _currentAwardIconId, _sim);
                _loreIcon.gameObject.SetActive(true);
            }
            _loreRoot.SetActive(true);
            _loreRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        public static string BuildDetailBodyPublic(string description, string citationRichOrPlain)
        {
            return BuildDetailBody(description, citationRichOrPlain);
        }

        public static void ShowDetailPublic(DecorationEntry entry, string body)
        {
            ShowDetail(entry, body);
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            CacheFonts();

            var ui = UIManager.Instance;
            if (ui == null || ui.PopupRoot == null)
                throw new InvalidOperationException("UIManager.PopupRoot not ready");

            _memorial = MemorialAwardsUi.Create(_sim);
            if (_memorial != null && _memorial.Root != null && _memorial.ListContent != null)
            {
                _usingMemorial = true;
                _root = _memorial.Root;
                _listContent = _memorial.ListContent;
                BuildDetailOverlay(_root.transform);
                BuildLoreOverlay(_root.transform);
                if (_detailRoot != null)
                    _detailRoot.SetActive(false);
                if (_loreRoot != null)
                    _loreRoot.SetActive(false);
                if (_root.GetComponent<DecorationsScreenEscape>() == null)
                    _root.AddComponent<DecorationsScreenEscape>();
                MemorialAwardsUi.SetVisible(_memorial, false);
                Main.LogInfo("Awards UI using Memorial Wall chrome (UIManager).");
                return;
            }

            if (_memorial != null)
            {
                MemorialAwardsUi.Release(_memorial, _sim);
                _memorial = null;
            }
            Main.LogInfo("Memorial chrome incomplete; falling back to procedural Awards UI.");

            EnsureProceduralUi(ui);
        }

        private static void EnsureProceduralUi(UIManager ui)
        {
            _usingMemorial = false;
            _root = new GameObject("CompanyDecorations_Screen", typeof(RectTransform));
            _root.transform.SetParent(ui.PopupRoot.transform, false);
            StretchFull(_root.GetComponent<RectTransform>());

            var backdrop = CreatePanel("Backdrop", _root.transform, new Color(0f, 0f, 0f, 0.65f));
            StretchFull(backdrop.GetComponent<RectTransform>());
            backdrop.AddComponent<Button>().onClick.AddListener(Dismiss);

            var panel = CreatePanel("Panel", _root.transform, new Color(0.08f, 0.09f, 0.11f, 0.96f));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1200f, 860f);
            panelRt.anchoredPosition = Vector2.zero;

            var titleGo = CreateTmp("Title", panel.transform, 38f, TextAlignmentOptions.Center, _fontBlack, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-40f, 60f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleGo.GetComponent<TextMeshProUGUI>().text = "Awards";

            var closeGo = CreatePanel("CloseButton", panel.transform, new Color(0.35f, 0.12f, 0.12f, 1f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(180f, 64f);
            closeRt.anchoredPosition = new Vector2(-16f, -16f);
            closeGo.AddComponent<Button>().onClick.AddListener(Dismiss);
            var closeLabel = CreateTmp("Label", closeGo.transform, 24f, TextAlignmentOptions.Center, _fontMedium, false);
            StretchFull(closeLabel.GetComponent<RectTransform>());
            closeLabel.GetComponent<TextMeshProUGUI>().text = "Close";

            var hintGo = CreateTmp("Hint", panel.transform, 24f, TextAlignmentOptions.Center, _fontMedium, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.sizeDelta = new Vector2(-40f, 40f);
            hintRt.anchoredPosition = new Vector2(0f, -90f);
            hintGo.GetComponent<TextMeshProUGUI>().text = "Click an award for details. Blue links open lore entries.";
            hintGo.GetComponent<TextMeshProUGUI>().color = new Color(0.75f, 0.75f, 0.78f, 1f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(24f, 24f);
            scrollRt.offsetMax = new Vector2(-24f, -140f);
            scrollGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.5f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            _listContent = content.transform;

            var emptyGo = CreateTmp("Empty", panel.transform, 24f, TextAlignmentOptions.Center, _fontMedium, false);
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0.1f, 0.4f);
            emptyRt.anchorMax = new Vector2(0.9f, 0.6f);
            emptyRt.offsetMin = Vector2.zero;
            emptyRt.offsetMax = Vector2.zero;
            _emptyLabel = emptyGo.GetComponent<TextMeshProUGUI>();
            _emptyLabel.text = Main.Settings.EmptyListMessage;

            BuildDetailOverlay(panel.transform);
            BuildLoreOverlay(panel.transform);

            _root.AddComponent<DecorationsScreenEscape>();
            _root.SetActive(false);
        }

        private static void BuildDetailOverlay(Transform panelParent)
        {
            _detailRoot = CreatePanel("DetailOverlay", panelParent, new Color(0.05f, 0.06f, 0.08f, 0.98f));
            StretchFull(_detailRoot.GetComponent<RectTransform>());

            var backGo = CreatePanel("BackButton", _detailRoot.transform, new Color(0.2f, 0.25f, 0.35f, 1f));
            var backRt = backGo.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.sizeDelta = new Vector2(180f, 64f);
            backRt.anchoredPosition = new Vector2(16f, -16f);
            backGo.AddComponent<Button>().onClick.AddListener(HideDetail);
            var backLabel = CreateTmp("Label", backGo.transform, 40f, TextAlignmentOptions.Center, _fontMedium, false);
            StretchFull(backLabel.GetComponent<RectTransform>());
            backLabel.GetComponent<TextMeshProUGUI>().text = "Back";

            // Header strip: large icon + title
            var iconGo = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_detailRoot.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 1f);
            iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.sizeDelta = new Vector2(320f, 320f);
            iconRt.anchoredPosition = new Vector2(24f, -90f);
            _detailIcon = iconGo.GetComponent<Image>();
            _detailIcon.preserveAspect = true;
            _detailIcon.color = Color.white;

            var titleGo = CreateTmp("DetailTitle", _detailRoot.transform, 38f, TextAlignmentOptions.TopLeft, _fontBlack, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(364f, -220f);
            titleRt.offsetMax = new Vector2(-24f, -96f);
            _detailTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _detailTitle.enableWordWrapping = true;
            _detailTitle.overflowMode = TextOverflowModes.Ellipsis;

            var tipGo = CreateTmp("DetailTip", _detailRoot.transform, 24f, TextAlignmentOptions.TopLeft, _fontMedium, false);
            var tipRt = tipGo.GetComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(0f, 1f);
            tipRt.anchorMax = new Vector2(1f, 1f);
            tipRt.pivot = new Vector2(0f, 1f);
            tipRt.offsetMin = new Vector2(364f, -300f);
            tipRt.offsetMax = new Vector2(-24f, -230f);
            tipGo.GetComponent<TextMeshProUGUI>().text = "Click blue underlined lore links for background entries.";
            tipGo.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.75f, 0.85f, 1f);

            // Body scroll below header (leaves room for 320px icon)
            var bodyScroll = new GameObject("DetailScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            bodyScroll.transform.SetParent(_detailRoot.transform, false);
            var bodyScrollRt = bodyScroll.GetComponent<RectTransform>();
            bodyScrollRt.anchorMin = new Vector2(0f, 0f);
            bodyScrollRt.anchorMax = new Vector2(1f, 1f);
            bodyScrollRt.offsetMin = new Vector2(24f, 24f);
            bodyScrollRt.offsetMax = new Vector2(-24f, -430f);
            bodyScroll.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.13f, 0.9f);

            var bodyViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            bodyViewport.transform.SetParent(bodyScroll.transform, false);
            StretchFull(bodyViewport.GetComponent<RectTransform>());
            bodyViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            var bodyContent = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            bodyContent.transform.SetParent(bodyViewport.transform, false);
            _detailBodyContentRt = bodyContent.GetComponent<RectTransform>();
            _detailBodyContentRt.anchorMin = new Vector2(0f, 1f);
            _detailBodyContentRt.anchorMax = new Vector2(1f, 1f);
            _detailBodyContentRt.pivot = new Vector2(0.5f, 1f);
            _detailBodyContentRt.anchoredPosition = Vector2.zero;
            _detailBodyContentRt.sizeDelta = new Vector2(0f, 0f);
            bodyContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyContent.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var bodyTextGo = CreateTmp("Body", bodyContent.transform, 24f, TextAlignmentOptions.TopLeft, _fontMedium, false);
            _detailBodyTextRt = bodyTextGo.GetComponent<RectTransform>();
            _detailBodyTextRt.anchorMin = new Vector2(0f, 1f);
            _detailBodyTextRt.anchorMax = new Vector2(1f, 1f);
            _detailBodyTextRt.pivot = new Vector2(0.5f, 1f);
            _detailBodyTextRt.offsetMin = new Vector2(16f, 0f);
            _detailBodyTextRt.offsetMax = new Vector2(-16f, -12f);
            _detailBody = bodyTextGo.GetComponent<TextMeshProUGUI>();
            _detailBody.enableWordWrapping = true;
            _detailBody.overflowMode = TextOverflowModes.Overflow;
            _detailBody.raycastTarget = true;
            bodyTextGo.AddComponent<LayoutElement>();
            bodyTextGo.AddComponent<TmpPreferredHeightDriver>();
            bodyTextGo.AddComponent<TmpLoreLinkClickHandler>();

            var scroll = bodyScroll.GetComponent<ScrollRect>();
            scroll.viewport = bodyViewport.GetComponent<RectTransform>();
            scroll.content = _detailBodyContentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            _detailRoot.SetActive(false);
        }

        private static void BuildLoreOverlay(Transform panelParent)
        {
            _loreRoot = CreatePanel("LoreOverlay", panelParent, new Color(0.04f, 0.05f, 0.07f, 0.98f));
            StretchFull(_loreRoot.GetComponent<RectTransform>());

            var backGo = CreatePanel("BackButton", _loreRoot.transform, new Color(0.2f, 0.25f, 0.35f, 1f));
            var backRt = backGo.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.sizeDelta = new Vector2(220f, 64f);
            backRt.anchoredPosition = new Vector2(16f, -16f);
            backGo.AddComponent<Button>().onClick.AddListener(HideLore);
            var backLabel = CreateTmp("Label", backGo.transform, 40f, TextAlignmentOptions.Center, _fontMedium, false);
            StretchFull(backLabel.GetComponent<RectTransform>());
            backLabel.GetComponent<TextMeshProUGUI>().text = "Back";

            // Same award image scale as detail (320x320)
            var iconGo = new GameObject("LoreIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_loreRoot.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 1f);
            iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.sizeDelta = new Vector2(320f, 320f);
            iconRt.anchoredPosition = new Vector2(24f, -90f);
            _loreIcon = iconGo.GetComponent<Image>();
            _loreIcon.preserveAspect = true;
            _loreIcon.color = Color.white;

            var titleGo = CreateTmp("LoreTitle", _loreRoot.transform, 38f, TextAlignmentOptions.TopLeft, _fontBlack, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(364f, -220f);
            titleRt.offsetMax = new Vector2(-24f, -96f);
            _loreTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _loreTitle.enableWordWrapping = true;

            var bodyScroll = new GameObject("LoreScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            bodyScroll.transform.SetParent(_loreRoot.transform, false);
            var bodyScrollRt = bodyScroll.GetComponent<RectTransform>();
            bodyScrollRt.anchorMin = new Vector2(0f, 0f);
            bodyScrollRt.anchorMax = new Vector2(1f, 1f);
            bodyScrollRt.offsetMin = new Vector2(24f, 24f);
            bodyScrollRt.offsetMax = new Vector2(-24f, -430f);
            bodyScroll.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.13f, 0.9f);

            var bodyViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            bodyViewport.transform.SetParent(bodyScroll.transform, false);
            StretchFull(bodyViewport.GetComponent<RectTransform>());
            bodyViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            var bodyContent = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            bodyContent.transform.SetParent(bodyViewport.transform, false);
            var bodyContentRt = bodyContent.GetComponent<RectTransform>();
            bodyContentRt.anchorMin = new Vector2(0f, 1f);
            bodyContentRt.anchorMax = new Vector2(1f, 1f);
            bodyContentRt.pivot = new Vector2(0.5f, 1f);
            bodyContentRt.sizeDelta = new Vector2(0f, 0f);
            bodyContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyContent.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var bodyTextGo = CreateTmp("Body", bodyContent.transform, 24f, TextAlignmentOptions.TopLeft, _fontMedium, false);
            _loreBodyTextRt = bodyTextGo.GetComponent<RectTransform>();
            _loreBodyTextRt.anchorMin = new Vector2(0f, 1f);
            _loreBodyTextRt.anchorMax = new Vector2(1f, 1f);
            _loreBodyTextRt.pivot = new Vector2(0.5f, 1f);
            _loreBodyTextRt.offsetMin = new Vector2(16f, 0f);
            _loreBodyTextRt.offsetMax = new Vector2(-16f, -12f);
            _loreBody = bodyTextGo.GetComponent<TextMeshProUGUI>();
            _loreBody.enableWordWrapping = true;
            _loreBody.overflowMode = TextOverflowModes.Overflow;
            bodyTextGo.AddComponent<LayoutElement>();
            bodyTextGo.AddComponent<TmpPreferredHeightDriver>();

            var scroll = bodyScroll.GetComponent<ScrollRect>();
            scroll.viewport = bodyViewport.GetComponent<RectTransform>();
            scroll.content = bodyContentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            _loreRoot.SetActive(false);
        }

        private static void Populate(List<DecorationEntry> earned)
        {
            if (_listContent == null)
                return;

            for (var i = _listContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_listContent.GetChild(i).gameObject);

            var hasRows = earned != null && earned.Count > 0;
            if (_emptyLabel != null)
                _emptyLabel.gameObject.SetActive(!hasRows);

            if (!hasRows)
            {
                if (_usingMemorial && _memorial != null)
                {
                    MemorialAwardsUi.SetHeader(_memorial, 0);
                    MemorialAwardsUi.ClearDetail(_memorial);
                }
                return;
            }

            if (_usingMemorial && _memorial != null)
                MemorialAwardsUi.SetHeader(_memorial, earned.Count);

            DecorationEntry first = null;
            foreach (var entry in earned)
            {
                if (first == null)
                    first = entry;
                CreateRow(entry);
            }

            if (_usingMemorial && _memorial != null && first != null)
                SelectAward(first);
        }

        private static void SelectAward(DecorationEntry entry)
        {
            if (entry == null)
                return;

            SetSelectedAward(entry);
            var citation = AwardHelper.GetCitation(_sim, entry.companyTag);
            string citePlain = null;
            string citeRich = null;
            if (!string.IsNullOrEmpty(citation))
            {
                citeRich = CitationTextUtil.ToDisplayRichText(citation);
                citePlain = CitationTextUtil.ToPlainDisplayText(citation);
            }

            if (_usingMemorial && _memorial != null)
                MemorialAwardsUi.BindDetail(_memorial, entry, citePlain, citeRich, _sim);
            else
            {
                // Procedural: open full detail immediately
                ShowDetail(entry, BuildDetailBody(entry.description, citeRich ?? citePlain));
            }
        }

        private static void CreateRow(DecorationEntry entry)
        {
            if (_usingMemorial)
            {
                CreateMemorialListRow(entry);
                return;
            }

            // Compact award row with lore-matched text sizes (title 38 / body 24)
            const float rowHeight = 120f;
            const float iconSize = 96f;
            const float textWidthFallback = 640f;

            var row = CreatePanel("Row_" + entry.companyTag, _listContent, new Color(0.14f, 0.15f, 0.18f, 0.95f));
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = rowHeight;
            rowLe.preferredHeight = rowHeight;
            rowLe.flexibleWidth = 1f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            var iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.minWidth = iconSize;
            iconLe.preferredWidth = iconSize;
            iconLe.minHeight = iconSize;
            iconLe.preferredHeight = iconSize;
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            IconLoader.Assign(iconImage, entry.icon, _sim);

            var textScrollGo = new GameObject("TextScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            textScrollGo.transform.SetParent(row.transform, false);
            var textScrollImg = textScrollGo.GetComponent<Image>();
            textScrollImg.color = new Color(0f, 0f, 0f, 0.2f);
            textScrollImg.raycastTarget = true;
            var textScrollLe = textScrollGo.GetComponent<LayoutElement>();
            textScrollLe.flexibleWidth = 1f;
            textScrollLe.minWidth = 200f;
            textScrollLe.minHeight = rowHeight - 12f;
            textScrollLe.preferredHeight = rowHeight - 12f;

            var textViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            textViewport.transform.SetParent(textScrollGo.transform, false);
            StretchFull(textViewport.GetComponent<RectTransform>());
            var vpImg = textViewport.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = true;

            var textContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            textContent.transform.SetParent(textViewport.transform, false);
            var textContentRt = textContent.GetComponent<RectTransform>();
            textContentRt.anchorMin = new Vector2(0f, 1f);
            textContentRt.anchorMax = new Vector2(1f, 1f);
            textContentRt.pivot = new Vector2(0.5f, 1f);
            textContentRt.anchoredPosition = Vector2.zero;
            textContentRt.sizeDelta = new Vector2(0f, 0f);

            var tv = textContent.GetComponent<VerticalLayoutGroup>();
            tv.padding = new RectOffset(4, 4, 2, 2);
            tv.spacing = 2f;
            tv.childAlignment = TextAnchor.UpperLeft;
            tv.childControlHeight = true;
            tv.childControlWidth = true;
            tv.childForceExpandHeight = false;
            tv.childForceExpandWidth = true;

            var textFitter = textContent.GetComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = textScrollGo.GetComponent<ScrollRect>();
            scroll.viewport = textViewport.GetComponent<RectTransform>();
            scroll.content = textContentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.inertia = false;

            var title = CreateTmp("Title", textContent.transform, 38f, TextAlignmentOptions.TopLeft, _fontBlack, true);
            var titleTmp = title.GetComponent<TextMeshProUGUI>();
            titleTmp.text = entry.title ?? entry.companyTag;
            titleTmp.enableWordWrapping = true;
            titleTmp.overflowMode = TextOverflowModes.Overflow;
            SizeTmpForLayout(title, textWidthFallback);

            var descText = entry.description ?? string.Empty;
            var desc = CreateTmp("Description", textContent.transform, 24f, TextAlignmentOptions.TopLeft, _fontMedium, true);
            var descTmp = desc.GetComponent<TextMeshProUGUI>();
            descTmp.text = CitationTextUtil.ToPlainDisplayText(descText);
            descTmp.enableWordWrapping = true;
            SizeTmpForLayout(desc, textWidthFallback);

            var citation = AwardHelper.GetCitation(_sim, entry.companyTag);
            string citePlain = null;
            string citeRich = null;
            if (!string.IsNullOrEmpty(citation))
            {
                citeRich = CitationTextUtil.ToDisplayRichText(citation);
                citePlain = CitationTextUtil.ToPlainDisplayText(citation);
                var cite = CreateTmp("Citation", textContent.transform, 24f, TextAlignmentOptions.TopLeft, _fontMedium, true);
                var citeTmp = cite.GetComponent<TextMeshProUGUI>();
                citeTmp.text = "<i>" + citePlain + "</i>";
                citeTmp.color = new Color(0.85f, 0.8f, 0.65f, 1f);
                citeTmp.enableWordWrapping = true;
                SizeTmpForLayout(cite, textWidthFallback);
            }

            var tipGo = CreateTmp("Tip", textContent.transform, 20f, TextAlignmentOptions.TopLeft, _fontMedium, true);
            var tipTmp = tipGo.GetComponent<TextMeshProUGUI>();
            tipTmp.text = "Click row for details";
            tipTmp.color = new Color(0.55f, 0.7f, 0.9f, 1f);
            SizeTmpForLayout(tipGo, textWidthFallback);

            // Defer width-accurate remeasure once parent layout has a real width
            var rem = textScrollGo.AddComponent<RowTextScrollRemeasure>();
            rem.Content = textContentRt;
            rem.FallbackWidth = textWidthFallback;

            var captured = entry;
            var btn = row.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = row.GetComponent<Image>();
            btn.onClick.AddListener(delegate
            {
                Main.LogInfo("Award row clicked: " + captured.companyTag);
                SelectAward(captured);
            });

            var hover = row.AddComponent<DecorationRowHover>();
            hover.TargetImage = row.GetComponent<Image>();
            hover.Normal = new Color(0.14f, 0.15f, 0.18f, 0.95f);
            hover.Highlighted = new Color(0.2f, 0.24f, 0.32f, 0.98f);
        }

        private static void CreateMemorialListRow(DecorationEntry entry)
        {
            const float rowHeight = 88f;
            const float iconSize = 72f;

            var row = CreatePanel("Row_" + entry.companyTag, _listContent, new Color(0.14f, 0.15f, 0.18f, 0.95f));
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = rowHeight;
            rowLe.preferredHeight = rowHeight;
            rowLe.flexibleWidth = 1f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            var iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.minWidth = iconSize;
            iconLe.preferredWidth = iconSize;
            iconLe.minHeight = iconSize;
            iconLe.preferredHeight = iconSize;
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            IconLoader.Assign(iconImage, entry.icon, _sim);

            var title = CreateTmp("Title", row.transform, 28f, TextAlignmentOptions.Left, _fontBlack, true);
            var titleLe = title.GetComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.minWidth = 120f;
            var titleTmp = title.GetComponent<TextMeshProUGUI>();
            titleTmp.text = entry.title ?? entry.companyTag;
            titleTmp.enableWordWrapping = true;
            titleTmp.overflowMode = TextOverflowModes.Ellipsis;

            var captured = entry;
            var btn = row.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = row.GetComponent<Image>();
            btn.onClick.AddListener(delegate
            {
                Main.LogInfo("Award row clicked: " + captured.companyTag);
                SelectAward(captured);
            });

            var hover = row.AddComponent<DecorationRowHover>();
            hover.TargetImage = row.GetComponent<Image>();
            hover.Normal = new Color(0.14f, 0.15f, 0.18f, 0.95f);
            hover.Highlighted = new Color(0.2f, 0.24f, 0.32f, 0.98f);
        }

        /// <summary>
        /// Size TMP LayoutElement height immediately so ContentSizeFitter has non-zero children
        /// (TmpPreferredHeightDriver alone failed inside nested ScrollRects with width 0).
        /// </summary>
        private static void SizeTmpForLayout(GameObject go, float width)
        {
            if (go == null)
                return;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            var le = go.GetComponent<LayoutElement>();
            var rt = go.GetComponent<RectTransform>();
            if (tmp == null || le == null || rt == null)
                return;

            width = Mathf.Max(80f, width);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            tmp.ForceMeshUpdate();
            var pref = tmp.GetPreferredValues(tmp.text ?? string.Empty, width, 0f);
            var h = Mathf.Max(14f, pref.y + 4f);
            le.minHeight = h;
            le.preferredHeight = h;
            le.flexibleWidth = 1f;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        }

        private static string BuildDetailBody(string description, string citationRichOrPlain)
        {
            var body = string.Empty;
            if (!string.IsNullOrEmpty(description))
                body += "<b>Background</b>\n" + CitationTextUtil.ToDisplayRichText(description);
            if (!string.IsNullOrEmpty(citationRichOrPlain))
            {
                if (body.Length > 0)
                    body += "\n\n";
                // citation may already be rich-converted
                var cite = citationRichOrPlain.Contains("<link=")
                    ? citationRichOrPlain
                    : CitationTextUtil.ToDisplayRichText(citationRichOrPlain);
                body += "<b>Citation</b>\n" + cite;
            }
            return body;
        }

        private static void ShowDetail(DecorationEntry entry, string body)
        {
            if (_detailRoot == null || entry == null)
                return;

            HideLore();
            _currentAwardIconId = entry.icon;
            _detailTitle.text = entry.title ?? entry.companyTag;
            _detailBody.text = body ?? string.Empty;
            IconLoader.Assign(_detailIcon, entry.icon, _sim);
            if (_detailIcon != null)
                _detailIcon.gameObject.SetActive(true);

            _detailRoot.SetActive(true);
            _detailRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            Main.LogInfo("Detail opened for " + entry.companyTag + " icon=" + entry.icon);
        }

        private static void HideDetail()
        {
            HideLore();
            _currentAwardIconId = null;
            if (_detailRoot != null)
                _detailRoot.SetActive(false);
        }

        private static void HideLore()
        {
            if (_loreRoot != null)
                _loreRoot.SetActive(false);
        }

        private static void CacheFonts()
        {
            if (_fontMedium != null && _fontBlack != null)
                return;

            var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
            foreach (var o in fonts)
            {
                var font = (TMP_FontAsset)o;
                if (font == null)
                    continue;
                if (font.name == "UnitedSansReg-Black SDF")
                    _fontBlack = font;
                if (font.name == "UnitedSansReg-Medium SDF")
                    _fontMedium = font;
            }

            if (_fontMedium == null && fonts != null && fonts.Length > 0)
                _fontMedium = fonts[0] as TMP_FontAsset;
            if (_fontBlack == null)
                _fontBlack = _fontMedium;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateTmp(string name, Transform parent, float size, TextAlignmentOptions align, TMP_FontAsset font, bool withLayout)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (withLayout)
                go.AddComponent<LayoutElement>();
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (font != null)
                tmp.font = font;
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    public class DecorationsScreenEscape : MonoBehaviour
    {
        private void Update()
        {
            if (!DecorationsScreen.IsOpen)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
                DecorationsScreen.HandleEscape();
        }
    }

    /// <summary>
    /// After the first layout pass, remeasure row TMP heights against the real scroll viewport width.
    /// </summary>
    public class RowTextScrollRemeasure : MonoBehaviour
    {
        public RectTransform Content;
        public float FallbackWidth = 640f;
        private bool _done;

        private void LateUpdate()
        {
            if (_done || Content == null)
                return;

            var width = Content.rect.width;
            if (width < 40f)
                width = FallbackWidth;
            if (width < 40f)
                return;

            for (var i = 0; i < Content.childCount; i++)
            {
                var child = Content.GetChild(i).gameObject;
                var tmp = child.GetComponent<TextMeshProUGUI>();
                var le = child.GetComponent<LayoutElement>();
                var rt = child.GetComponent<RectTransform>();
                if (tmp == null || le == null || rt == null)
                    continue;

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                tmp.ForceMeshUpdate();
                var pref = tmp.GetPreferredValues(tmp.text ?? string.Empty, width, 0f);
                var h = Mathf.Max(14f, pref.y + 4f);
                le.minHeight = h;
                le.preferredHeight = h;
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            _done = true;
        }
    }

    public class TmpPreferredHeightDriver : MonoBehaviour
    {
        private TextMeshProUGUI _tmp;
        private LayoutElement _le;
        private RectTransform _rt;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshProUGUI>();
            _le = GetComponent<LayoutElement>();
            _rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_tmp == null || _rt == null)
                return;

            // Force text rect to parent's full width so wrapping uses the box, not infinite width
            var parentRt = _rt.parent as RectTransform;
            var parentWidth = parentRt != null ? parentRt.rect.width : 0f;
            if (parentWidth > 1f)
            {
                var pad = Mathf.Abs(_rt.offsetMin.x) + Mathf.Abs(_rt.offsetMax.x);
                if (pad < 1f)
                    pad = 32f;
                var width = Mathf.Max(50f, parentWidth - pad);
                _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

                var pref = _tmp.GetPreferredValues(_tmp.text, width, 0f);
                var h = Mathf.Max(20f, pref.y + 8f);
                if (_le != null)
                {
                    _le.preferredHeight = h;
                    _le.minHeight = h;
                }
                _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }
        }
    }

    public class TmpLoreLinkClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            var tmp = GetComponent<TextMeshProUGUI>();
            if (tmp == null)
                return;

            var linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.position, eventData.pressEventCamera);
            if (linkIndex < 0)
                return;

            var linkInfo = tmp.textInfo.linkInfo[linkIndex];
            var linkId = linkInfo.GetLinkID();
            DecorationsScreen.ShowLoreFromLink(linkId);
        }
    }

    public class DecorationRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image TargetImage;
        public Color Normal;
        public Color Highlighted;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TargetImage != null)
                TargetImage.color = Highlighted;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TargetImage != null)
                TargetImage.color = Normal;
        }
    }
}
