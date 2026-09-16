using System;
using System.Text;
using System.Text.RegularExpressions;
using BattleTech;

namespace CompanyDecorations
{
    public static class CitationTextUtil
    {
        // [[DM.BaseDescriptionDefs[Id], Display Name]]
        private static readonly Regex LoreLinkRegex = new Regex(
            @"\[\[DM\.BaseDescriptionDefs\[([^\]]+)\],\s*([^\]]+)\]\]",
            RegexOptions.Compiled);

        public static string ToDisplayRichText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            var text = raw.Replace("\r\n", "\n");
            text = LoreLinkRegex.Replace(text, delegate(Match m)
            {
                var id = m.Groups[1].Value.Trim();
                var display = m.Groups[2].Value.Trim();
                // TMP link id carries the BaseDescriptionDef Id
                return string.Format(
                    "<link=\"bdd:{0}\"><color=#7EB8FF><u>{1}</u></color></link>",
                    id,
                    display);
            });
            return text;
        }

        public static string ToPlainDisplayText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            var text = raw.Replace("\r\n", "\n");
            text = LoreLinkRegex.Replace(text, "$2");
            return text;
        }

        public static bool TryGetBaseDescription(SimGameState sim, string id, out BaseDescriptionDef def)
        {
            def = null;
            if (sim == null || sim.DataManager == null || string.IsNullOrEmpty(id))
                return false;

            try
            {
                if (sim.DataManager.BaseDescriptionDefs != null &&
                    sim.DataManager.BaseDescriptionDefs.TryGet(id, out def) &&
                    def != null)
                    return true;
            }
            catch (Exception ex)
            {
                Main.LogError("TryGet BaseDescriptionDef failed for " + id);
                Main.LogException(ex);
            }

            try
            {
                if (sim.DataManager.Exists(BattleTechResourceType.BaseDescriptionDef, id))
                {
                    def = sim.DataManager.GetObjectOfType<BaseDescriptionDef>(id, BattleTechResourceType.BaseDescriptionDef);
                    return def != null;
                }
            }
            catch (Exception ex)
            {
                Main.LogError("GetObjectOfType BaseDescriptionDef failed for " + id);
                Main.LogException(ex);
            }

            return false;
        }

        public static string FormatLorePopupBody(BaseDescriptionDef def)
        {
            if (def == null)
                return string.Empty;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(def.Details))
                sb.Append(def.Details.Replace("\r\n", "\n"));
            return sb.ToString();
        }
    }
}
