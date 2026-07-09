using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace Deuterium
{
    public static class LocalizationPatcher
    {
        private const string TextFolderMarker = "text/text_";
        private const int StringsXorKey = 12467;
        private const int NewLanguageEncryptIndex = 8;

        private delegate string orig_ResolveFilePath(string path, bool skipMergedMods, bool skipConsoleFiles);
        private delegate int orig_EncryptIndex(InGameTranslator.LanguageID lang);
        private delegate string orig_ChatlogDecryptResult(string result, string path);
        private delegate void orig_ConversationLoadEventsFromFile(int fileName, SlugcatStats.Name saveFile, bool oneRandomLine, int randomSeed);
        private delegate string[] orig_ListDirectory3(string path, bool directories, bool includeAll);
        private delegate string[] orig_ListDirectory4(string path, bool directories, bool includeAll, bool moddedOnly);

        private static Hook? _resolveFilePathHook;
        private static Hook? _encryptIndexHook;
        private static Hook? _chatlogDecryptResultHook;
        private static Hook? _conversationLoadEventsFromFileHook;
        private static Hook? _listDirectoryHook3;
        private static Hook? _listDirectoryHook4;

        public static void Initialize()
        {
            On.LocalizationTranslator.LangShort += LocalizationTranslator_LangShort;
            On.InGameTranslator.LoadShortStrings += InGameTranslator_LoadShortStrings;

            _resolveFilePathHook = TryMakeHook(
                () => typeof(AssetManager).GetMethod(
                    nameof(AssetManager.ResolveFilePath),
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(bool), typeof(bool) },
                    null),
                nameof(ResolveFilePath_Hook));

            _encryptIndexHook = TryMakeHook(
                () => typeof(InGameTranslator.LanguageID).GetMethod(
                    "EncryptIndex",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(InGameTranslator.LanguageID) },
                    null),
                nameof(LanguageID_EncryptIndex_Hook));

            _chatlogDecryptResultHook = TryMakeHook(
                () => typeof(ChatlogData).GetMethod(
                    "DecryptResult",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null),
                nameof(ChatlogData_DecryptResult_Hook));

            _conversationLoadEventsFromFileHook = TryMakeHook(
                () => typeof(Conversation).GetMethod(
                    "LoadEventsFromFile",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int), typeof(SlugcatStats.Name), typeof(bool), typeof(int) },
                    null),
                nameof(Conversation_LoadEventsFromFile_Hook));

            _listDirectoryHook3 = TryMakeHook(
                () => typeof(AssetManager).GetMethod(
                    "ListDirectory",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(bool), typeof(bool) },
                    null),
                nameof(ListDirectory_Hook3));

            if (_listDirectoryHook3 != null)
            {
                Debug.LogWarning("[Deuterium] ListDirectoryHook3 installed.");
            }

            _listDirectoryHook4 = TryMakeHook(
                () => typeof(AssetManager).GetMethod(
                    "ListDirectory",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool) },
                    null),
                nameof(ListDirectory_Hook4));

            if (_listDirectoryHook4 != null)
            {
                Debug.LogWarning("[Deuterium] ListDirectoryHook4 installed.");
            }
        }

        private static Hook? TryMakeHook(Func<MethodInfo?> targetResolver, string hookName)
        {
            try
            {
                return MakeHook(targetResolver(), hookName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deuterium] Hook '{hookName}' failed to install: {ex}");
                return null;
            }
        }

        private static Hook MakeHook(MethodInfo? target, string hookName)
        {
            if (target == null)
            {
                Debug.LogError($"[Deuterium] Could not locate target method for hook '{hookName}'.");
                throw new InvalidOperationException($"[Deuterium] Missing target for hook '{hookName}'.");
            }

            MethodInfo? hook = typeof(LocalizationPatcher).GetMethod(hookName, BindingFlags.NonPublic | BindingFlags.Static);
            if (hook == null)
            {
                Debug.LogError($"[Deuterium] Could not locate own hook method '{hookName}'.");
                throw new InvalidOperationException($"[Deuterium] Missing own hook method '{hookName}'.");
            }

            return new Hook(target, hook);
        }

        private static string LocalizationTranslator_LangShort(On.LocalizationTranslator.orig_LangShort orig, InGameTranslator.LanguageID lang)
        {
            var entry = Languages.FromLanguageID(lang);
            if (entry != null)
            {
                return entry.ShortName;
            }
            return orig(lang);
        }

        private static void InGameTranslator_LoadShortStrings(On.InGameTranslator.orig_LoadShortStrings orig, InGameTranslator self)
        {
            orig(self);

            try
            {
                LoadSubfolderShortStrings(self);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deuterium] Failed to load subfolder short strings: " + ex.Message);
            }
        }

        private static string ResolveFilePath_Hook(orig_ResolveFilePath orig, string path, bool skipMergedMods, bool skipConsoleFiles)
        {
            string result = orig(path, skipMergedMods, skipConsoleFiles);
            bool origExists = File.Exists(result);
            Debug.LogWarning("[Deuterium] ResolveFilePath_Hook: path=" + path + " origExists=" + origExists + " origResult=" + result);

            if (origExists)
            {
                Debug.LogWarning("[Deuterium] ResolveFilePath_Hook: returning orig because exists.");
                return result;
            }

            string? subfolderPath = TryResolveInSubfolders(path);
            Debug.LogWarning("[Deuterium] ResolveFilePath_Hook: subfolderPath=" + subfolderPath);
            return subfolderPath ?? result;
        }

        private static bool TryParseOurLanguageTextPath(string path, out string langShort, out string fileName)
        {
            langShort = string.Empty;
            fileName = string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: path is null/empty");
                return false;
            }

            string normalized = path.Replace('\\', '/').ToLowerInvariant();
            int markerIndex = normalized.IndexOf(TextFolderMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: marker not found in " + normalized);
                return false;
            }

            int afterMarker = markerIndex + TextFolderMarker.Length;
            int slashIndex = normalized.IndexOf('/', afterMarker);
            if (slashIndex < 0)
            {
                Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: slash not found after marker in " + normalized);
                return false;
            }

            langShort = normalized.Substring(afterMarker, slashIndex - afterMarker);
            if (Languages.FromShortName(langShort) == null)
            {
                Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: langShort '" + langShort + "' not ours");
                return false;
            }

            fileName = normalized.Substring(slashIndex + 1);
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: fileName is empty");
                return false;
            }

            Debug.LogWarning("[Deuterium] TryParseOurLanguageTextPath: parsed lang=" + langShort + " file=" + fileName);
            return true;
        }

        private static string? TryResolveInSubfolders(string path)
        {
            if (!TryParseOurLanguageTextPath(path, out string langShort, out string fileName))
            {
                return null;
            }

            Debug.LogWarning("[Deuterium] TryResolveInSubfolders: searching for " + fileName + " in lang " + langShort);

            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                ModManager.Mod mod = ModManager.ActiveMods[i];
                string baseDir = Path.Combine(mod.path, "text", "text_" + langShort);
                if (!Directory.Exists(baseDir))
                {
                    continue;
                }

                Debug.LogWarning("[Deuterium] TryResolveInSubfolders: searching baseDir=" + baseDir);
                string[] matches = Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    Debug.LogWarning("[Deuterium] TryResolveInSubfolders: found " + matches.Length + " matches, returning " + matches[0]);
                    return matches[0];
                }
                else
                {
                    Debug.LogWarning("[Deuterium] TryResolveInSubfolders: no matches in " + baseDir);
                }
            }

            Debug.LogWarning("[Deuterium] TryResolveInSubfolders: nothing found for " + fileName);
            return null;
        }

        private static void LoadSubfolderShortStrings(InGameTranslator translator)
        {
            if (translator == null)
            {
                return;
            }

            InGameTranslator.LanguageID current = translator.currentLanguage;
            if (current == null)
            {
                return;
            }

            string langShort = LocalizationTranslator.LangShort(current);
            if (string.IsNullOrEmpty(langShort))
            {
                return;
            }

            foreach (ModManager.Mod mod in ModManager.ActiveMods)
            {
                string baseDir = Path.Combine(mod.path, "text", "text_" + langShort);
                if (!Directory.Exists(baseDir))
                {
                    continue;
                }

                string[] stringsFiles = Directory.GetFiles(baseDir, "strings.txt", SearchOption.AllDirectories);
                foreach (string file in stringsFiles)
                {
                    MergeShortStringsFile(translator.shortStrings, file);
                }

                string metaFile = Path.Combine(baseDir, "deuterium_oi.txt");
                if (File.Exists(metaFile))
                {
                    MergeShortStringsFile(translator.shortStrings, metaFile);
                }
            }
        }

        private static void MergeShortStringsFile(Dictionary<string, string> dict, string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text.Length == 0)
            {
                return;
            }

            if (text[0] == '1')
            {
                text = Custom.xorEncrypt(text, StringsXorKey);
            }
            else if (text[0] == '0')
            {
                text = text.Remove(0, 1);
            }

            string[] lines = Regex.Split(text, "\r?\n");
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains("///"))
                {
                    line = line.Split('/')[0].TrimEnd();
                }

                string[] parts = line.Split('|');
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                {
                    dict[parts[0]] = parts[1];
                }
            }
        }

        private static int LanguageID_EncryptIndex_Hook(orig_EncryptIndex orig, InGameTranslator.LanguageID lang)
        {
            if (Languages.FromLanguageID(lang) != null)
            {
                return NewLanguageEncryptIndex;
            }

            return orig(lang);
        }

        private static string ChatlogData_DecryptResult_Hook(
            orig_ChatlogDecryptResult orig, string result, string path)
        {
            if (File.Exists(path))
            {
                result = File.ReadAllText(path, Encoding.UTF8);
            }

            if (!string.IsNullOrEmpty(result) && result[0] == '0')
            {
                return result;
            }

            int num = 0;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < fileNameWithoutExtension.Length; i++)
            {
                num += fileNameWithoutExtension[i] - 48;
            }

            int langFactor = InGameTranslator.LanguageID.EncryptIndex(
                Custom.rainWorld.inGameTranslator.currentLanguage);
            return Custom.xorEncrypt(result, 54 + num + langFactor * 7);
        }

        private static void Conversation_LoadEventsFromFile_Hook(
            orig_ConversationLoadEventsFromFile orig,
            Conversation self,
            int fileName,
            SlugcatStats.Name saveFile,
            bool oneRandomLine,
            int randomSeed)
        {
            Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: fileName=" + fileName + " saveFile=" + (saveFile != null ? saveFile.value : "null"));
            InGameTranslator.LanguageID languageID = Custom.rainWorld.inGameTranslator.currentLanguage;
            string text;
            while (true)
            {
                text = AssetManager.ResolveFilePath(Custom.rainWorld.inGameTranslator.SpecificTextFolderDirectory(languageID) + Path.DirectorySeparatorChar + fileName + ".txt");
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: resolved base text=" + text);
                if (saveFile != null)
                {
                    string text2 = text;
                    text = AssetManager.ResolveFilePath(Custom.rainWorld.inGameTranslator.SpecificTextFolderDirectory(languageID) + Path.DirectorySeparatorChar + fileName + "-" + saveFile.value + ".txt");
                    Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: resolved save text=" + text);
                    if (!File.Exists(text))
                    {
                        text = text2;
                        Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: save text missing, fallback to base");
                    }
                }
                if (File.Exists(text))
                {
                    Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: FILE FOUND " + text);
                    break;
                }
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: NOT FOUND " + text);
                if (languageID != InGameTranslator.LanguageID.English)
                {
                    Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: retry with English");
                    Custom.LogImportant("RETRY WITH ENGLISH");
                    languageID = InGameTranslator.LanguageID.English;
                    continue;
                }
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: English also not found, returning");
                return;
            }
            string text3 = File.ReadAllText(text, Encoding.UTF8);
            Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: read " + (text3 != null ? text3.Length : -1) + " chars");
            if (string.IsNullOrEmpty(text3))
            {
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: file is empty or unreadable, returning");
                return;
            }
            if (text3[0] != '0')
            {
                int langFactor = InGameTranslator.LanguageID.EncryptIndex(Custom.rainWorld.inGameTranslator.currentLanguage);
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: decrypting with langFactor=" + langFactor + " key=" + (54 + fileName + langFactor * 7));
                text3 = Custom.xorEncrypt(text3, 54 + fileName + langFactor * 7);
            }
            string[] array = Regex.Split(text3, "\r?\n");
            if (array.Length == 0 || string.IsNullOrEmpty(array[0]))
            {
                Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: invalid header line, returning");
                return;
            }
            try
            {
                string[] headerParts = Regex.Split(array[0], "-");
                if (headerParts.Length < 2 || headerParts[1] != fileName.ToString())
                {
                    Debug.LogWarning("[Deuterium] Conversation_LoadEventsFromFile_Hook: HEADER MISMATCH! array[0]=" + array[0]);
                    return;
                }
                if (oneRandomLine)
                {
                    List<Conversation.TextEvent> list = new List<Conversation.TextEvent>();
                    for (int i = 1; i < array.Length; i++)
                    {
                        string[] array2 = LocalizationTranslator.ConsolidateLineInstructions(array[i]);
                        if (array2.Length == 3)
                        {
                            list.Add(new Conversation.TextEvent(self, int.Parse(array2[0], NumberStyles.Any, CultureInfo.InvariantCulture), array2[2], int.Parse(array2[1], NumberStyles.Any, CultureInfo.InvariantCulture)));
                        }
                        else if (array2.Length == 1 && array2[0].Length > 0)
                        {
                            list.Add(new Conversation.TextEvent(self, 0, array2[0], 0));
                        }
                    }
                    if (list.Count > 0)
                    {
                        UnityEngine.Random.State state = UnityEngine.Random.state;
                        UnityEngine.Random.InitState(randomSeed);
                        Conversation.TextEvent item = list[UnityEngine.Random.Range(0, list.Count)];
                        UnityEngine.Random.state = state;
                        self.events.Add(item);
                    }
                    return;
                }
                for (int j = 1; j < array.Length; j++)
                {
                    string[] array3 = LocalizationTranslator.ConsolidateLineInstructions(array[j]);
                    if (array3.Length == 3)
                    {
                        if (ModManager.MSC && !int.TryParse(array3[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var _) && int.TryParse(array3[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var _))
                        {
                            self.events.Add(new Conversation.TextEvent(self, int.Parse(array3[0], NumberStyles.Any, CultureInfo.InvariantCulture), array3[1], int.Parse(array3[2], NumberStyles.Any, CultureInfo.InvariantCulture)));
                        }
                        else
                        {
                            self.events.Add(new Conversation.TextEvent(self, int.Parse(array3[0], NumberStyles.Any, CultureInfo.InvariantCulture), array3[2], int.Parse(array3[1], NumberStyles.Any, CultureInfo.InvariantCulture)));
                        }
                    }
                    else if (array3.Length == 2)
                    {
                        if (array3[0] == "SPECEVENT")
                        {
                            self.events.Add(new Conversation.SpecialEvent(self, 0, array3[1]));
                        }
                        else if (array3[0] == "PEBBLESWAIT")
                        {
                            self.events.Add(new SSOracleBehavior.PebblesConversation.PauseAndWaitForStillEvent(self, null, int.Parse(array3[1], NumberStyles.Any, CultureInfo.InvariantCulture)));
                        }
                    }
                    else if (array3.Length == 1 && array3[0].Length > 0)
                    {
                        self.events.Add(new Conversation.TextEvent(self, 0, array3[0], 0));
                    }
                }
            }
            catch (Exception ex)
            {
                Custom.LogWarning("TEXT ERROR " + ex.StackTrace);
                self.events.Add(new Conversation.TextEvent(self, 0, "TEXT ERROR", 100));
            }
        }

        private static string[] ListDirectory_Hook3(orig_ListDirectory3 orig, string path, bool directories, bool includeAll)
        {
            Debug.LogWarning("[Deuterium] ListDirectory_Hook3 called: " + path + " dirs=" + directories + " includeAll=" + includeAll);
            string[] result = ListDirectoryCore(orig(path, directories, includeAll), path, directories, includeAll);
            Debug.LogWarning("[Deuterium] ListDirectory_Hook3 result count=" + (result != null ? result.Length : -1));
            return result;
        }

        private static string[] ListDirectory_Hook4(orig_ListDirectory4 orig, string path, bool directories, bool includeAll, bool moddedOnly)
        {
            Debug.LogWarning("[Deuterium] ListDirectory_Hook4 called: " + path + " dirs=" + directories + " includeAll=" + includeAll + " moddedOnly=" + moddedOnly);
            string[] result = ListDirectoryCore(orig(path, directories, includeAll, moddedOnly), path, directories, includeAll);
            Debug.LogWarning("[Deuterium] ListDirectory_Hook4 result count=" + (result != null ? result.Length : -1));
            return result;
        }

        private static string[] ListDirectoryCore(string[] result, string path, bool directories, bool includeAll)
        {
            string normalizedPath = path.Replace('\\', '/').ToLowerInvariant().TrimEnd('/');
            Debug.LogWarning("[Deuterium] ListDirectoryCore normalizedPath=" + normalizedPath + " origResultCount=" + (result != null ? result.Length : -1));

            bool isOurLangDir = false;
            foreach (var lang in Languages.All)
            {
                if (normalizedPath.EndsWith("text/text_" + lang.ShortName))
                {
                    isOurLangDir = true;
                    break;
                }
            }

            if (isOurLangDir)
            {
                HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in result)
                {
                    existingNames.Add(Path.GetFileName(file));
                }

                List<string> additional = new List<string>();
                foreach (ModManager.Mod mod in ModManager.ActiveMods)
                {
                    string baseDir = Path.Combine(mod.path, normalizedPath);
                    if (!Directory.Exists(baseDir))
                    {
                        Debug.LogWarning("[Deuterium] ListDirectoryCore baseDir missing: " + baseDir);
                        continue;
                    }

                    string[] subdirs = Directory.GetDirectories(baseDir);
                    Debug.LogWarning("[Deuterium] ListDirectoryCore subdirs count=" + (subdirs != null ? subdirs.Length : -1) + " in " + baseDir);
                    foreach (string subdir in subdirs)
                    {
                        string[] items = directories ? Directory.GetDirectories(subdir) : Directory.GetFiles(subdir);
                        Debug.LogWarning("[Deuterium] ListDirectoryCore items count=" + (items != null ? items.Length : -1) + " in " + subdir);
                        foreach (string item in items)
                        {
                            string fileName = Path.GetFileName(item);
                            if (!existingNames.Contains(fileName) || includeAll)
                            {
                                additional.Add(item);
                                if (!includeAll)
                                {
                                    existingNames.Add(fileName);
                                }
                            }
                        }
                    }
                }

                Debug.LogWarning("[Deuterium] ListDirectoryCore additional count=" + additional.Count);
                if (additional.Count > 0)
                {
                    List<string> final = new List<string>(result);
                    final.AddRange(additional);
                    return final.ToArray();
                }
            }

            return result;
        }
    }
}