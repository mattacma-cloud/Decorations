using BattleTech;
using BattleTech.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyDecorations
{
    /// <summary>
    /// Captain's Quarters entry via lower-right Awards image button (fully on-screen).
    /// </summary>
    public static class DecorationsButtonInjector
    {
        private const string CornerButtonName = "CompanyDecorations_AwardsButton";
        private const string LegacyBarracksButtonName = "CompanyDecorations_BarracksButton";
        private static GameObject _cornerButton;
        private static SimGameState _sim;

        public static void EnsureCaptainsQuartersEntry(SGRoomController_CptQuarters room)
        {
            if (room == null)
                return;

            try
            {
                _sim = Traverse.Create(room).Field("simState").GetValue<SimGameState>();
                EnsureCornerButton();
                SetCornerVisible(true);
            }
            catch (System.Exception ex)
            {
                Main.LogError("EnsureCaptainsQuartersEntry failed.");
                Main.LogException(ex);
            }
        }

        public static void SetCornerVisible(bool visible)
        {
            if (_cornerButton != null)
                _cornerButton.SetActive(visible);
        }

        public static void Cleanup()
        {
            DecorationsScreen.Release();
            SetCornerVisible(false);
        }

        private static void EnsureCornerButton()
        {
            var ui = UIManager.Instance;
            if (ui == null || ui.UIRoot == null)
            {
                Main.LogError("UIManager.UIRoot not ready for Awards button.");
                return;
            }

            DestroyLegacyBarracksButton(ui.UIRoot.transform);

            if (_cornerButton == null)
                CreateCornerButton(ui.UIRoot.transform);
            else
            {
                ApplyButtonLayout();
                RefreshButtonArt();
            }
        }

        private static void DestroyLegacyBarracksButton(Transform uiRoot)
        {
            if (uiRoot == null)
                return;
            var legacy = uiRoot.Find(LegacyBarracksButtonName);
            if (legacy != null)
                Object.Destroy(legacy.gameObject);
        }

        private static void CreateCornerButton(Transform parent)
        {
            _cornerButton = new GameObject(CornerButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            _cornerButton.transform.SetParent(parent, false);

            // Soft frame behind icon
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(_cornerButton.transform, false);
            frame.transform.SetAsFirstSibling();
            var frameRt = frame.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = new Vector2(-6f, -6f);
            frameRt.offsetMax = new Vector2(6f, 6f);
            frame.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
            frame.GetComponent<Image>().raycastTarget = false;

            // Label inside button bounds so lower-right placement stays fully visible
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(_cornerButton.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.sizeDelta = new Vector2(0f, 44f);
            labelRt.anchoredPosition = new Vector2(0f, 6f);

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = Main.Settings.ButtonLabel ?? "Awards";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
            foreach (var o in fonts)
            {
                var font = (TMP_FontAsset)o;
                if (font != null && font.name == "UnitedSansReg-Medium SDF")
                {
                    tmp.font = font;
                    break;
                }
            }

            ApplyButtonLayout();
            RefreshButtonArt();
            _cornerButton.GetComponent<Button>().onClick.AddListener(OnCornerClicked);
            Main.LogInfo("Created Captain's Quarters Awards button (lower-right).");
        }

        private static void ApplyButtonLayout()
        {
            if (_cornerButton == null)
                return;

            var width = Main.Settings.ButtonWidth > 16f ? Main.Settings.ButtonWidth : 216f;
            var height = Main.Settings.ButtonHeight > 16f ? Main.Settings.ButtonHeight : 162f;
            // Keep whole control (incl. frame padding) inside the safe lower-right corner
            const float marginRight = 28f;
            const float marginBottom = 28f;

            var rt = _cornerButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-marginRight, marginBottom);

            var img = _cornerButton.GetComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = true;

            var label = _cornerButton.transform.Find("Label");
            if (label != null)
            {
                var tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = Main.Settings.ButtonLabel ?? "Awards";
            }
        }

        private static void RefreshButtonArt()
        {
            if (_cornerButton == null)
                return;

            var img = _cornerButton.GetComponent<Image>();
            var iconId = Main.Settings.ButtonIcon;
            if (string.IsNullOrEmpty(iconId))
                iconId = "decoration_Davion_OperationRATServiceRibbon";

            IconLoader.Assign(img, iconId, _sim);
        }

        private static void OnCornerClicked()
        {
            if (_sim != null)
                DecorationsScreen.Show(_sim);
            else
                Main.LogError("Awards button clicked but SimGameState is null.");
        }
    }
}
