using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CompanyDecorations
{
    public class CitationDef
    {
        public string companyTag;
        public string citation;
    }

    /// <summary>
    /// Loads unit citations from Lore-Packs\citations and Mods\*\citations.
    /// </summary>
    public static class CitationRegistry
    {
        private static readonly Dictionary<string, string> ByTag =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static int Count
        {
            get { return ByTag.Count; }
        }

        public static void LoadAll(string companyDecorationsModDir)
        {
            ByTag.Clear();

            // 1) Shared authoring folder next to Mods: Lore-Packs\citations
            try
            {
                var gameRoot = Path.GetFullPath(Path.Combine(companyDecorationsModDir, "..", ".."));
                LoadFolder(Path.Combine(gameRoot, "Lore-Packs", "citations"));
            }
            catch (Exception ex)
            {
                Main.LogError("Failed scanning Lore-Packs\\citations");
                Main.LogException(ex);
            }

            // 2) Each enabled mod may ship Mods\<Name>\citations\
            try
            {
                var modsRoot = Path.GetFullPath(Path.Combine(companyDecorationsModDir, ".."));
                if (Directory.Exists(modsRoot))
                {
                    foreach (var modDir in Directory.GetDirectories(modsRoot))
                    {
                        LoadFolder(Path.Combine(modDir, "citations"));
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LogError("Failed scanning Mods\\*\\citations");
                Main.LogException(ex);
            }

            Main.LogInfo(string.Format("CitationRegistry loaded {0} citation(s).", ByTag.Count));
        }

        public static string GetTemplate(string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag))
                return null;
            string citation;
            if (ByTag.TryGetValue(companyTag, out citation))
                return citation;
            return null;
        }

        private static void LoadFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var path = files[i];
                var name = Path.GetFileName(path);
                if (string.Equals(name, "README.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var json = File.ReadAllText(path);
                    var def = JsonConvert.DeserializeObject<CitationDef>(json);
                    if (def == null || string.IsNullOrEmpty(def.companyTag) || string.IsNullOrEmpty(def.citation))
                    {
                        Main.LogInfo("Skipping citation file (missing companyTag/citation): " + path);
                        continue;
                    }

                    ByTag[def.companyTag] = def.citation;
                    Main.LogInfo("Citation loaded: " + def.companyTag + " from " + path);
                }
                catch (Exception ex)
                {
                    Main.LogError("Failed to load citation file: " + path);
                    Main.LogException(ex);
                }
            }
        }
    }
}
