using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RWCustom;

namespace Deuterium
{
	public static class ModHooks
	{
		private delegate string orig_LocalizedName(ModManager.Mod self);
		private delegate string orig_LocalizedDescription(ModManager.Mod self);

		public static void HookModProperties()
		{
			try
			{
				var nameProp = typeof(ModManager.Mod).GetProperty("LocalizedName");
				var descProp = typeof(ModManager.Mod).GetProperty("LocalizedDescription");

				if (nameProp != null && descProp != null)
				{
					var nameGetMethod = nameProp.GetGetMethod();
					var descGetMethod = descProp.GetGetMethod();

					if (nameGetMethod != null && descGetMethod != null)
					{
						new Hook(nameGetMethod, typeof(ModHooks).GetMethod("Mod_get_LocalizedName", BindingFlags.NonPublic | BindingFlags.Static)!);
						new Hook(descGetMethod, typeof(ModHooks).GetMethod("Mod_get_LocalizedDescription", BindingFlags.NonPublic | BindingFlags.Static)!);
						UnityEngine.Debug.LogWarning("[Deuterium] Successfully detoured Mod properties for UA and BE!");
					}
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[Deuterium] Failed to detour Mod properties: " + ex.Message);
			}
		}

		private static string GetLocalizedProperty(ModManager.Mod self, Func<ModManager.Mod, InGameTranslator.LanguageID?, string> getLocalizedValue, Func<ModManager.Mod, string> orig)
		{
			if (self.id == "brokenbyteofcode.ukrlocale" && Custom.rainWorld != null)
			{
				string localized = getLocalizedValue(self, Custom.rainWorld.inGameTranslator.currentLanguage);
				if (!string.IsNullOrEmpty(localized))
				{
					return localized;
				}
			}
			return orig(self);
		}

		private static string Mod_get_LocalizedName(orig_LocalizedName orig, ModManager.Mod self)
		{
			return GetLocalizedProperty(self, LocaleManager.GetLocalizedModName, orig.Invoke);
		}

		private static string Mod_get_LocalizedDescription(orig_LocalizedDescription orig, ModManager.Mod self)
		{
			return GetLocalizedProperty(self, LocaleManager.GetLocalizedModDescription, orig.Invoke);
		}
	}
}