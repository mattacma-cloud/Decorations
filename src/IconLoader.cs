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
                image.overrideSprite = null;
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

            // Disk first. Each medal gets its own texture. The game sprite
            // cache can hand back another Sunburst when the ids share a stem.
            var fromDisk = LoadFromDisk(iconId);
            if (fromDisk != null)
            {
                Cache[iconId] = fromDisk;
                return fromDisk;
            }

            try
            {
                if (sim != null && sim.DataManager != null &&
                    sim.DataManager.Exists(BattleTechResourceType.Sprite, iconId))
                {
                    var dmSprite = sim.DataManager.GetObjectOfType<Sprite>(iconId, BattleTechResourceType.Sprite);
                    if (dmSprite != null && dmSprite.texture != null &&
                        SpriteNameMatches(dmSprite, iconId))
                    {
                        var copy = CopySprite(dmSprite, iconId);
                        Cache[iconId] = copy;
                        return copy;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LogError("DataManager sprite failed for " + iconId);
                Main.LogException(ex);
            }

            return null;
        }

        static bool SpriteNameMatches(Sprite sprite, string iconId)
        {
            if (sprite == null || string.IsNullOrEmpty(iconId))
                return false;
            var name = sprite.name ?? string.Empty;
            if (name.Length == 0)
                return true;
            return string.Equals(name, iconId, StringComparison.OrdinalIgnoreCase);
        }

        static Sprite CopySprite(Sprite source, string iconId)
        {
            var tex = source.texture;
            var copyTex = new Texture2D(tex.width, tex.height, tex.format, false);
            copyTex.name = iconId;
            Graphics.CopyTexture(tex, copyTex);
            var sprite = Sprite.Create(
                copyTex,
                new Rect(0f, 0f, copyTex.width, copyTex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = iconId;
            return sprite;
        }

        static Sprite LoadFromDisk(string iconId)
        {
            try
            {
                if (string.IsNullOrEmpty(Main.ModDir))
                    return null;

                var path = Path.Combine(Main.ModDir, "assets", "icons", iconId + ".png");
                if (!File.Exists(path))
                    path = Path.Combine(Main.ModDir, "assets", "icons", iconId + ".PNG");
                if (!File.Exists(path))
                    return null;

                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.name = iconId;
                if (!tex.LoadImage(bytes))
                    return null;

                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                var sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = iconId;
                Main.LogInfo("Loaded icon from disk: " + path);
                return sprite;
            }
            catch (Exception ex)
            {
                Main.LogError("Disk icon load failed for " + iconId);
                Main.LogException(ex);
                return null;
            }
        }
    }
}
