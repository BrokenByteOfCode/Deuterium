using System.Linq;

namespace Deuterium
{
    public class LanguageEntry
    {
        public string ShortName { get; }
        public string DisplayName { get; }
        public string ButtonLabel { get; }
        public float ButtonPos { get; }
        public int ButtonId { get; }
        public bool UsesCyrillic { get; }
        public bool Enabled { get; set; }
        public InGameTranslator.LanguageID? LanguageID { get; set; }

        public LanguageEntry(string shortName, string displayName, string buttonLabel, float buttonPos, int buttonId, bool usesCyrillic = true)
        {
            ShortName = shortName;
            DisplayName = displayName;
            ButtonLabel = buttonLabel;
            ButtonPos = buttonPos;
            ButtonId = buttonId;
            UsesCyrillic = usesCyrillic;
            Enabled = true;
        }
    }

    public static class Languages
    {
        public static readonly LanguageEntry Ukrainian  = new("ukrainian",  "Ukrainian",  "UKRAINIAN",  5.0f, 10);
        public static readonly LanguageEntry Belarusian = new("belarusian", "Belarusian", "BELARUSIAN", 5.5f, 11) {Enabled = false};

        public static readonly LanguageEntry[] All = { Ukrainian, Belarusian };

        public static LanguageEntry? FromShortName(string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
            {
                return null;
            }
            string lower = shortName.ToLowerInvariant();
            return All.FirstOrDefault(l => l.ShortName == lower);
        }

        public static LanguageEntry? FromLanguageID(InGameTranslator.LanguageID? id)
        {
            if (id == null)
            {
                return null;
            }
            return All.FirstOrDefault(l => l.LanguageID != null && l.LanguageID.value == id.value);
        }
    }
}
