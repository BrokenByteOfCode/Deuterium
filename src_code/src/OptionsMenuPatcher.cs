using System.Collections.Generic;
using Menu;
using UnityEngine;

namespace Deuterium
{
    public static class OptionsMenuPatcher
    {
        public static void Initialize()
        {
            On.Menu.OptionsMenu.ctor += OptionsMenu_ctor;
            On.Menu.OptionsMenu.GetSaveSlotButtonWidth += OptionsMenu_GetSaveSlotButtonWidth;
        }

        private static void OptionsMenu_ctor(On.Menu.OptionsMenu.orig_ctor orig, OptionsMenu self, ProcessManager manager)
        {
            orig(self, manager);

            if (!LocaleManager.IsReady)
            {
                return;
            }

            var langList = new List<InGameTranslator.LanguageID>(self.languageOrder);
            var btnList = new List<SelectOneButton>(self.languageButtons);

            foreach (var lang in Languages.All)
            {
                if (lang.LanguageID != null && lang.Enabled)
                {
                    AddLanguageButton(self, langList, btnList, lang.LanguageID, lang.ButtonLabel, lang.ButtonPos, lang.ButtonId);
                }
            }

            self.languageOrder = langList.ToArray();
            self.languageButtons = btnList.ToArray();
        }

        private static void AddLanguageButton(OptionsMenu self, List<InGameTranslator.LanguageID> langList, List<SelectOneButton> btnList, InGameTranslator.LanguageID id, string label, float posIndex, int buttonId)
        {
            if (!listContains(langList, id))
            {
                langList.Add(id);
            }

            Vector2 basePosition = new Vector2(891f, 620f);
            Vector2 pos = basePosition + new Vector2(55f + (posIndex - Mathf.Floor(posIndex)) * 220f, Mathf.Floor(posIndex) * -40f);
            var button = new SelectOneButton(self, self.pages[0], self.Translate(label), "Language", pos, new Vector2(self.normalLanguageButtonWidth, 30f), self.languageButtons, buttonId);
            btnList.Add(button);
            self.pages[0].subObjects.Add(button);
        }

        private static bool listContains(List<InGameTranslator.LanguageID> list, InGameTranslator.LanguageID id)
        {
            foreach (var item in list)
            {
                if (item.value == id.value)
                {
                    return true;
                }
            }
            return false;
        }

        private static float OptionsMenu_GetSaveSlotButtonWidth(On.Menu.OptionsMenu.orig_GetSaveSlotButtonWidth orig, InGameTranslator.LanguageID lang)
        {
            if (LocaleManager.IsCyrillicLanguage(lang))
            {
                return 170f;
            }

            return orig(lang);
        }
    }
}
