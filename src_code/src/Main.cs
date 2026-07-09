using BepInEx;
using UnityEngine;

namespace Deuterium
{
    [BepInPlugin("brokenbyteofcode.ukrlocale", "Ukrainian Localization", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        public void OnEnable()
        {
            LocaleManager.RegisterLanguages();
            OptionsMenuPatcher.Initialize();
            FontTheft.Initialize();
            LocalizationPatcher.Initialize();
            ModHooks.HookModProperties();
            ModManager_RefreshModsLists_Hook.Initialize();
        }
    }
}
