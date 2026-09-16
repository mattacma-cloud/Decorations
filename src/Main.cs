using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using HBS.Logging;
using Newtonsoft.Json;

namespace CompanyDecorations
{
    public static class Main
    {
        public const string HarmonyId = "com.blacknova.companydecorations";

        internal static ModSettings Settings = new ModSettings();
        internal static string ModDir;
        internal static DecorationsCatalog Catalog = new DecorationsCatalog();
        internal static ILog Log;

        public static void Init(string directory, string settingsJSON)
        {
            ModDir = directory;
            Log = Logger.GetLogger("CompanyDecorations");

            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(settingsJSON) ?? new ModSettings();
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to parse settings; using defaults.");
                Log.LogException(ex);
                Settings = new ModSettings();
            }

            try
            {
                var catalogPath = Path.Combine(directory, Settings.CatalogRelativePath);
                Catalog = DecorationsCatalog.Load(catalogPath);
                LogInfo(string.Format("Loaded catalog v{0} with {1} decoration(s) from {2}",
                    Catalog.version, Catalog.decorations.Count, catalogPath));
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to load decorations catalog.");
                Log.LogException(ex);
                Catalog = new DecorationsCatalog();
            }

            try
            {
                CitationRegistry.LoadAll(directory);
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to load citation registry.");
                Log.LogException(ex);
            }

            try
            {
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), HarmonyId);
                LogInfo("Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.LogError("Harmony patch failed.");
                Log.LogException(ex);
            }
        }

        internal static void LogInfo(string message)
        {
            if (Settings != null && Settings.DebugLog && Log != null)
                Log.Log(message);
        }

        internal static void LogError(string message)
        {
            if (Log != null)
                Log.LogError(message);
        }

        internal static void LogException(Exception ex)
        {
            if (Log != null)
                Log.LogException(ex);
        }
    }

    public class ModSettings
    {
        public string[] EntryPoints = new string[] { "CaptainsQuarters" };
        public string CatalogRelativePath = "decorations/catalog.json";
        public string ButtonLabel = "Awards";
        public string ButtonIcon = "decoration_Davion_OperationRATServiceRibbon";
        public float ButtonSize = 216f;
        public float ButtonWidth = 216f;
        public float ButtonHeight = 162f;
        public string EmptyListMessage = "No awards earned yet.";
        public bool DebugLog = false;

        public bool HasEntryPoint(string name)
        {
            if (EntryPoints == null || EntryPoints.Length == 0)
                return false;
            for (var i = 0; i < EntryPoints.Length; i++)
            {
                if (string.Equals(EntryPoints[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
