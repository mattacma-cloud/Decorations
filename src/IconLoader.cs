using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyDecorations
{
    public static class IconLoader
    {
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static void Assign(Image image, string iconId, SimGameState sim)
        {
            if (image == null || string.IsNullOrEmpty(iconId))
                return;

            var sprite = GetSprite(iconId, sim);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = new Color(0.35f, 0.35f, 0.4f, 1f);
                Main.LogInfo("Icon not found: " + iconId);
            }
        }

        public static Sprite GetSprite(string iconId, SimGameState sim)
        {
            if (string.IsNullOrEmpty(iconId))
                return null;

            Sprite cached;
            if (Cache.TryGetValue(iconId, out cached) && cached != null)
                return cached;

            // 1) DataManager Sprite (ModTek manifest)
            try
            {
                if (sim != null && sim.DataManager != null &&
                    sim.DataManager.Exists(BattleTechResourceType.Sprite, iconId))
                {
                    var dmSprite = sim.DataManager.GetObjectOfType<Sprite>(iconId, BattleTechResourceType.Sprite);
                    if (dmSprite != null && dmSprite.texture != null)
                    {
                        Cache[iconId] = dmSprite;
                        return dmSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LogError("DataManager sprite failed for " + iconId);
                Main.LogException(ex);
            }

            // 2) Disk fallback from assets/icons
            try
            {
                var path = Path.Combine(Main.ModDir, "assets", "icons", iconId + ".png");
                if (!File.Exists(path))
                    path = Path.Combine(Main.ModDir, "assets", "icons", iconId + ".PNG");

                if (File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        var sprite = Sprite.Create(
                            tex,
                            new Rect(0f, 0f, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        sprite.name = iconId;
                        Cache[iconId] = sprite;
                        Main.LogInfo("Loaded icon from disk: " + path);
                        return sprite;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LogError("Disk icon load failed for " + iconId);
                Main.LogException(ex);
            }

            return null;
        }
    }
}
