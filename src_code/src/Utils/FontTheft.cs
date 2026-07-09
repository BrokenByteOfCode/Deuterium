using System;
using System.Collections.Generic;
using System.Reflection;
using Menu;
using RWCustom;
using UnityEngine;

namespace Deuterium
{
	public static class FontTheft
	{
		private static bool _aliasCreated;

		public static void Initialize()
		{
			On.RWCustom.Custom.GetFont += Custom_GetFont;
			On.RWCustom.Custom.GetDisplayFont += Custom_GetDisplayFont;
			On.InGameTranslator.LoadFonts += InGameTranslator_LoadFonts;
		}

		private static string Custom_GetFont(On.RWCustom.Custom.orig_GetFont orig)
		{
			if (IsCyrillicLanguage() && Futile.atlasManager.DoesContainFontWithName("fontCyr"))
			{
				return "fontCyr";
			}
			return orig();
		}

		private static string Custom_GetDisplayFont(On.RWCustom.Custom.orig_GetDisplayFont orig)
		{
			if (IsCyrillicLanguage() && Futile.atlasManager.DoesContainFontWithName("DisplayFontCyr"))
			{
				return "DisplayFontCyr";
			}
			return orig();
		}

		private static void InGameTranslator_LoadFonts(On.InGameTranslator.orig_LoadFonts orig, InGameTranslator.LanguageID lang, Menu.Menu menu)
		{
			if (LocaleManager.IsCyrillicLanguage(lang))
			{
				orig(InGameTranslator.LanguageID.Russian, menu);
				AliasFontInManager("fontRus", "fontCyr");
				AliasFontInManager("DisplayFontRus", "DisplayFontCyr");
				_aliasCreated = true;
				return;
			}

			orig(lang, menu);

			if (!_aliasCreated)
			{
				_aliasCreated = true;
				orig(InGameTranslator.LanguageID.Russian, menu);
				AliasFontInManager("fontRus", "fontCyr");
				AliasFontInManager("DisplayFontRus", "DisplayFontCyr");
			}
		}

		private static bool IsCyrillicLanguage()
		{
			return Custom.rainWorld != null && LocaleManager.IsCyrillicLanguage(Custom.rainWorld.inGameTranslator.currentLanguage);
		}

		private static void AliasFontInManager(string originalName, string aliasName)
		{
			try
			{
				var field = typeof(FAtlasManager).GetField("_fontsByName", BindingFlags.NonPublic | BindingFlags.Instance);
				if (field != null)
				{
					var dict = field.GetValue(Futile.atlasManager) as Dictionary<string, FFont>;
					if (dict != null && dict.ContainsKey(originalName))
					{
						dict[aliasName] = dict[originalName];
					}
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[Deuterium] Font aliasing failed: " + ex.Message);
			}
		}
	}
}
