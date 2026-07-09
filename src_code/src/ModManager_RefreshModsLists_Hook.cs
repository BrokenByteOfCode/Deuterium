using System;

namespace Deuterium
{
    internal static class ModManager_RefreshModsLists_Hook
    {
        public static void Initialize()
        {
            On.ModManager.RefreshModsLists += ModManager_RefreshModsLists;
        }

        private static void ModManager_RefreshModsLists(On.ModManager.orig_RefreshModsLists orig, RainWorld rainWorld)
        {
            orig(rainWorld);
            DeuteriumOI.Register();
        }
    }
}
