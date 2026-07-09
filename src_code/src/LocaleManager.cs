using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Deuterium
{
    public static class LocaleManager
    {
        private static string _ukrName = string.Empty;
        private static string _ukrDescription = string.Empty;
        private static string _belName = string.Empty;
        private static string _belDescription = string.Empty;
        private static bool _loaded;

        public static void RegisterLanguages()
        {
            foreach (var lang in Languages.All)
            {
                lang.LanguageID = new InGameTranslator.LanguageID(lang.DisplayName, register: true);
            }
        }

        public static bool IsCyrillicLanguage(InGameTranslator.LanguageID? lang)
        {
            var entry = Languages.FromLanguageID(lang);
            return entry != null && entry.UsesCyrillic;
        }

        public static bool IsReady
        {
            get
            {
                foreach (var lang in Languages.All)
                {
                    if (lang.LanguageID == null)
                    {
                        return false;
                    }
                }
                return Languages.All.Length > 0;
            }
        }

        public static string GetLocalizedModName(ModManager.Mod self, InGameTranslator.LanguageID? activeLang)
        {
            if (self.id == "brokenbyteofcode.ukrlocale")
            {
                EnsureMetadataLoaded();
                var entry = Languages.FromLanguageID(activeLang);
                if (entry == Languages.Ukrainian)
                {
                    return _ukrName;
                }
                if (entry == Languages.Belarusian)
                {
                    return _belName;
                }
            }
            return string.Empty;
        }

        public static string GetLocalizedModDescription(ModManager.Mod self, InGameTranslator.LanguageID? activeLang)
        {
            if (self.id == "brokenbyteofcode.ukrlocale")
            {
                EnsureMetadataLoaded();
                var entry = Languages.FromLanguageID(activeLang);
                if (entry == Languages.Ukrainian)
                {
                    return _ukrDescription;
                }
                if (entry == Languages.Belarusian)
                {
                    return _belDescription;
                }
            }
            return string.Empty;
        }

        private static void EnsureMetadataLoaded()
        {
            if (!_loaded)
            {
                LoadEmbeddedMetadata();
                _loaded = true;
            }
        }

        private static void LoadEmbeddedMetadata()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                const string resourceName = "Deuterium.assets.meta.json";

                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return;
                }

                using StreamReader reader = new StreamReader(stream);
                string jsonText = reader.ReadToEnd();

                var meta = JsonUtility.FromJson<ModMeta>(jsonText);
                _ukrName = meta.nameUKR;
                _belName = meta.nameBEL;
                _ukrDescription = meta.descriptionUKR;
                _belDescription = meta.descriptionBEL;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Deuterium] Error loading embedded meta.json: " + ex.Message);
            }
        }
    }

    [Serializable]
    public struct ModMeta
    {
        public string nameUKR;
        public string nameBEL;
        public string descriptionUKR;
        public string descriptionBEL;
    }
}
