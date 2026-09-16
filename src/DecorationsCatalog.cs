using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HBS.Collections;
using Newtonsoft.Json;

namespace CompanyDecorations
{
    public class DecorationsCatalog
    {
        public int version = 1;
        public List<DecorationEntry> decorations = new List<DecorationEntry>();

        public static DecorationsCatalog Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Decorations catalog not found", path);

            var json = File.ReadAllText(path);
            var catalog = JsonConvert.DeserializeObject<DecorationsCatalog>(json) ?? new DecorationsCatalog();
            if (catalog.decorations == null)
                catalog.decorations = new List<DecorationEntry>();
            return catalog;
        }

        public List<DecorationEntry> Earned(TagSet companyTags)
        {
            if (decorations == null || decorations.Count == 0)
                return new List<DecorationEntry>();

            return decorations
                .Where(d => d != null
                            && !string.IsNullOrEmpty(d.companyTag)
                            && companyTags != null
                            && companyTags.Contains(d.companyTag))
                .OrderBy(d => d.sortOrder)
                .ThenBy(d => d.title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public DecorationEntry FindByCompanyTag(string companyTag)
        {
            if (string.IsNullOrEmpty(companyTag) || decorations == null)
                return null;
            return decorations.FirstOrDefault(d =>
                d != null && string.Equals(d.companyTag, companyTag, StringComparison.OrdinalIgnoreCase));
        }

        public DecorationEntry FindByAwardEventId(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
                return null;

            // Convention: event_decoration_<Faction_AwardName> <-> decoration_<Faction_AwardName>
            if (eventId.StartsWith("event_decoration_", StringComparison.OrdinalIgnoreCase))
            {
                var tag = "decoration_" + eventId.Substring("event_decoration_".Length);
                return FindByCompanyTag(tag);
            }

            if (decorations == null)
                return null;
            return decorations.FirstOrDefault(d =>
                d != null && string.Equals(d.awardEventId, eventId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class DecorationEntry
    {
        public string id;
        public string companyTag;
        public string title;
        public string description;
        public string icon;
        public string loreId;
        public string category;
        public string awardedBy;
        public int sortOrder = 1000;
        public string awardEventId;
    }
}
