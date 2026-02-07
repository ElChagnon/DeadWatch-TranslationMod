using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppYarn.Unity;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppYarn;

[assembly: MelonInfo(typeof(DeadWatchTranslationMod.DeadWatchTranslationMod), "DeadWatch-TranslationMod", "1.0.2", "ElChagnon")]
[assembly: MelonGame(null, null)]

namespace DeadWatchTranslationMod
{
    public partial class DeadWatchTranslationMod : MelonMod
    {
        public static DeadWatchTranslationMod Instance;

        // Définie les noms des fichiers CSV pour chaque langue
        private const string CSV_FILENAME_FR = "french.csv";
        private const string CSV_FILENAME_IT = "italian.csv";
        private const string CSV_FILENAME_ES = "spanish.csv";
        private const string CSV_FILENAME_PT = "portuguese.csv";
        private const string CSV_FILENAME_RU = "russian.csv";
        private const string CSV_FILENAME_UA = "ukrainian.csv";
        private const string CSV_FILENAME_DE = "german.csv";
        private const string CSV_FILENAME_PL = "polish.csv";
        private const string CSV_FILENAME_TR = "turkish.csv";
        private const string CSV_FILENAME_CN = "chinese.csv";
        private const string CSV_FILENAME_JP = "japanese.csv";

        private const string TARGET_SCENE_NAME = "StoryGameModescene";
        private const string UPDATE_URL = "https://raw.githubusercontent.com/ElChagnon/DeadWatch-TranslationMod/main/version.txt";

        // Pour récup les textes pas définit de base et les mettre en cache histoire que le jeu ne crash pas
        private static string CACHE_FILE_PATH => Path.Combine(MelonEnvironment.UserDataDirectory, "DeadWatch_TranslationCache.txt");

        public const string CURRENT_VERSION = "1.1.8";
        public static bool ShowUpdatePopup = false;

        // Menu activé par la touche K
        private bool _showDebugMenu = false;

        private float _uiUpdateTimer = 0f;
        private const float UI_UPDATE_INTERVAL = 3.0f; // 3s pour éviter les freeze

        public static HashSet<string> LoggedLocKeys = new HashSet<string>();

        // pour pas faire crash 
        public static Dictionary<IntPtr, string> OriginalTextCache = new Dictionary<IntPtr, string>();
        public static HashSet<IntPtr> _translatedObjects = new HashSet<IntPtr>();

        // Sauvegarde les textes sur le disque pour les traduire plus vite
        public static HashSet<string> _cachedHierarchyPaths = new HashSet<string>();

        // Liste des chemins à ignorer (objets qui changent tout le temps)
        private static readonly HashSet<string> _excludedPaths = new HashSet<string>
        {
            "Canvas layer 1/Inspection Screen UI/Container/Text/Super Text",
            "Canvas layer 1/Dialogue UI Router/Bottom dialogue UI/Text holder/Text/Name/Name",
            "Canvas layer 3/Pause menu/Menu List/Report a bug/Animator/Super Text",
            "Canvas layer 3/Pause menu/Menu List/Settings/Animator/Super Text",
            "Canvas layer 3/Pause menu/Menu List/Resume/Animator/Super Text",
            "Canvas layer 3/Pause menu/Menu List/Exit/Animator/Super Text",
            "Rooms/Main hall/Shop/Shop board/Shop desc text",
            "Rooms/Main hall/Shop/Shop board/Shop text",
            "Rooms/Hallways/Hallways 2/Sign/New Super Text",
            "Canvas layer 1/Dialogue UI Router/Options Dialogue View/2 Options/Option B/Super Text",
            "Canvas layer 1/Dialogue UI Router/Options Dialogue View/2 Options/Option A/Super Text",
            "Rooms/Main hall/Shop/Shop closed window/Preopen",
            "Canvas layer 1/Dialogue UI Router/Options Dialogue View/4 Options/Option D/Super Text",
            "Canvas layer 1/Screen Message System/Super Text"
        };

        // Liste des langues possibles
        public enum LanguageEnum
        {
            English, French, Italian, Spanish, Portuguese, Russian, Ukrainian, German, Polish, Turkish, Chinese, Japanese
        }

        public static LanguageEnum CurrentLanguage = LanguageEnum.French;
        public static List<LanguageEnum> AvailableLanguages = new List<LanguageEnum>();

        public static Dictionary<string, string> AllTranslations = new Dictionary<string, string>();

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonLogger.Msg($"Mod v{CURRENT_VERSION} (K-Menu Mode) loaded.");

            LoadHierarchyCache();

            DetectAvailableLanguages(); // Check les langues dispo dans le dossier

            // J'ai mis le français mais faut juste le changer pour la langue par défaut que vous voulez
            if (AvailableLanguages.Contains(LanguageEnum.French))
                CurrentLanguage = LanguageEnum.French;
            else if (AvailableLanguages.Count > 0)
                CurrentLanguage = AvailableLanguages[0];

            LoadAllTranslations(); // Charge les textes traduits
            TryPatchSuperTextMesh();

            if (!string.IsNullOrEmpty(UPDATE_URL)) MelonCoroutines.Start(CheckForUpdateRoutine()); // check les mises à jour
        }

        private void DetectAvailableLanguages()
        {
            AvailableLanguages.Clear();
            AvailableLanguages.Add(LanguageEnum.English);

            string gameDir = Directory.GetCurrentDirectory();
            string langDir = Path.Combine(gameDir, "Language");
            if (!Directory.Exists(langDir)) langDir = Path.Combine(gameDir, "Mods", "Language");

            // Vérifie quels fichiers CSV existent et ajoute la langue correspondante
            if (Directory.Exists(langDir))
            {
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_FR))) AvailableLanguages.Add(LanguageEnum.French);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_IT))) AvailableLanguages.Add(LanguageEnum.Italian);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_ES))) AvailableLanguages.Add(LanguageEnum.Spanish);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_PT))) AvailableLanguages.Add(LanguageEnum.Portuguese);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_RU))) AvailableLanguages.Add(LanguageEnum.Russian);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_UA))) AvailableLanguages.Add(LanguageEnum.Ukrainian);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_DE))) AvailableLanguages.Add(LanguageEnum.German);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_PL))) AvailableLanguages.Add(LanguageEnum.Polish);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_TR))) AvailableLanguages.Add(LanguageEnum.Turkish);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_CN))) AvailableLanguages.Add(LanguageEnum.Chinese);
                if (File.Exists(Path.Combine(langDir, CSV_FILENAME_JP))) AvailableLanguages.Add(LanguageEnum.Japanese);
            }

        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) // S'active quand on change de scène 
        {
            OriginalTextCache.Clear();
            _translatedObjects.Clear();

            if (sceneName == TARGET_SCENE_NAME) // supp et le jeu crash
            {
                ApplyCachedHierarchyTranslations();
                ApplyUITranslationsNative();
            }
        }

        public override void OnGUI() // Supp si vous voulez ne plus avoir le message de mise à jour
        {
            if (ShowUpdatePopup) // Si y'a une maj, affiche une boite au milieu de l'écran
            {
                float centerX = Screen.width / 2f;
                float centerY = Screen.height / 2f;
                string title = "Update Available";
                string content = "The mod is outdated.\nPlease check the mod channel or Github.";
                if (CurrentLanguage == LanguageEnum.French) { title = "Mise à jour disponible"; content = "Le mod n'est pas à jour.\nVeuillez vérifier le Github."; }
                GUI.Box(new Rect(centerX - 250, centerY - 75, 500, 150), title);
                GUI.Label(new Rect(centerX - 230, centerY - 25, 460, 60), content);
                if (GUI.Button(new Rect(centerX - 50, centerY + 45, 100, 30), "OK")) ShowUpdatePopup = false;
            }

            // MENU K
            if (_showDebugMenu)
            {
                // Fond du menu
                GUI.Box(new Rect(10, 10, 220, 130), "Traduction (Menu 'K')");

                string langText = "Language : English";
                switch (CurrentLanguage)
                {
                    case LanguageEnum.French: langText = "Langue : Français"; break;
                    case LanguageEnum.Italian: langText = "Lingua : Italiano"; break;
                    default: langText = $"Lang: {CurrentLanguage}"; break;
                }

                // Bouton changer langue
                if (GUI.Button(new Rect(30, 40, 180, 30), langText))
                {
                    ToggleLanguage();
                }

                // Bouton recharger
                if (GUI.Button(new Rect(30, 80, 180, 30), "Reload Translations (F5)"))
                {
                    DetectAvailableLanguages();
                    LoadAllTranslations();
                    LoadHierarchyCache();
                    MelonLogger.Msg("Translations reloaded!");
                    LoggedLocKeys.Clear();
                    ApplyUITranslationsNative();
                }
            }
        }

        public override void OnLateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                _showDebugMenu = !_showDebugMenu;
            }

            if (Input.GetKeyDown(KeyCode.F5)) // f5 pour recharger le cvs actuel
            {
                DetectAvailableLanguages();
                LoadAllTranslations();
                LoadHierarchyCache(); // Recharge le fichier cache
                MelonLogger.Msg("Translations reloaded!");
                LoggedLocKeys.Clear();
                ApplyUITranslationsNative();
            }

            // 
            bool isEnglish = CurrentLanguage == LanguageEnum.English;
            if (!isEnglish || _translatedObjects.Count > 0)
            {
                _uiUpdateTimer += Time.deltaTime;
                if (_uiUpdateTimer >= UI_UPDATE_INTERVAL)
                {
                    ApplyUITranslationsNative();
                    _uiUpdateTimer = 0f;
                }
            }
        }

        /* Toujours pas la v2 faite pas attention car flm de le commenter vu qu'il est juste là pour traduire des textes a la volée et je deviens fou*/
        private void LoadHierarchyCache() // Lit le fichier texte du cache
        {
            _cachedHierarchyPaths.Clear();
            if (File.Exists(CACHE_FILE_PATH))
            {
                try
                {
                    var lines = File.ReadAllLines(CACHE_FILE_PATH);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line)) _cachedHierarchyPaths.Add(line.Trim());
                    }
                }
                catch (Exception e) { }
            }
        }

        private void AddToHierarchyCache(GameObject go)
        {
            if (go == null) return;
            string path = GetGameObjectPath(go);

            if (_excludedPaths.Contains(path)) return;

            if (!_cachedHierarchyPaths.Contains(path))
            {
                _cachedHierarchyPaths.Add(path);
                try
                {
                    File.AppendAllText(CACHE_FILE_PATH, path + Environment.NewLine);
                }
                catch (Exception e) { }
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path.Substring(1);
        }

        private void ApplyCachedHierarchyTranslations() // Traduit seulement les objets connus du cache
        {
            if (CurrentLanguage == LanguageEnum.English) return;

            foreach (string path in _cachedHierarchyPaths)
            {
                GameObject go = GameObject.Find(path);
                if (go != null)
                {
                    ProcessSingleObject(go);
                }
            }
        }

        // Si vous voulez réutilisez mon code checker ça c'est vraiment le seul truc qui permet d'avoir un truc fonctionnel et prévu par le dev
        private void TryPatchSuperTextMesh()
        {
            try
            {
                var stmType = AccessTools.TypeByName("SuperTextMesh");
                if (stmType == null) stmType = AccessTools.TypeByName("Il2Cpp.SuperTextMesh");

                if (stmType != null)
                {
                    var onEnableMethod = AccessTools.Method(stmType, "OnEnable");
                    if (onEnableMethod != null)
                    {
                        HarmonyInstance.Patch(onEnableMethod, postfix: new HarmonyMethod(typeof(DeadWatchTranslationMod).GetMethod(nameof(OnSuperTextMeshEnable), BindingFlags.Static | BindingFlags.NonPublic)));
                    }
                }
            }
            catch (Exception e) { }
        }

        private static void OnSuperTextMeshEnable(Component __instance)
        {
            if (Instance == null || CurrentLanguage == LanguageEnum.English) return;
            Instance.ProcessSingleObject(__instance.gameObject);
        }

        // v2

        private void ProcessSingleObject(GameObject go) // Regarde si un objet contient du texte
        {
            if (go == null || !go.activeInHierarchy) return;

            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                IntPtr ptr = comp.Pointer;
                IntPtr klass = IL2CPP.il2cpp_object_get_class(ptr);
                IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
                string nativeClassName = Marshal.PtrToStringAnsi(namePtr);

                // Si le composant s'appelle Text ou Super ou TMP, on essaie de le traduire
                if (nativeClassName.Contains("Text") || nativeClassName.Contains("Super") || nativeClassName.Contains("TMP"))
                {
                    ApplyTranslationLogic(ptr, klass, go, nativeClassName);
                }
            }
        }

        private void ApplyTranslationLogic(IntPtr ptr, IntPtr klass, GameObject go, string className)
        {
            bool revertToEnglish = CurrentLanguage == LanguageEnum.English;

            // Lit le texte actuel directement dans la mémoire du jeu
            string currentText = ReadNativeString(ptr, klass, "text");
            if (string.IsNullOrEmpty(currentText)) currentText = ReadNativeString(ptr, klass, "m_Text");
            if (string.IsNullOrEmpty(currentText)) currentText = ReadNativeString(ptr, klass, "_text");

            if (currentText == null) return;

            string cleanCurrent = NormalizeString(currentText);

            // Sauvegarde le texte original s'il n'est pas connu
            if (AllTranslations.ContainsKey(cleanCurrent)) OriginalTextCache[ptr] = currentText;
            if (!OriginalTextCache.ContainsKey(ptr)) OriginalTextCache[ptr] = currentText;

            string originalText = OriginalTextCache[ptr];

            if (revertToEnglish) // Si on remet en anglais
            {
                if (_translatedObjects.Contains(ptr))
                {
                    ApplyTranslation(ptr, klass, originalText, go.name); // Remet le texte original
                    _translatedObjects.Remove(ptr);
                }
                return;
            }

            string cleanKey = NormalizeString(originalText);
            // Cherche la traduction dans le dictionnaire
            if (AllTranslations.TryGetValue(cleanKey, out string trad))
            {
                if (NormalizeString(currentText) == NormalizeString(trad))
                {
                    if (!_translatedObjects.Contains(ptr)) _translatedObjects.Add(ptr);
                    return;
                }

                // Applique la traduction
                if (ApplyTranslation(ptr, klass, trad, go.name, cleanKey))
                {
                    _translatedObjects.Add(ptr);
                    //On ajoute ce GameObject au cache pour le retrouver vite la prochaine fois
                    AddToHierarchyCache(go);
                }
            }
        }

        private void ApplyUITranslationsNative() // Scanne tous les objets du jeu pour les traduire
        {
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var go in allObjects)
            {
                ProcessSingleObject(go);
            }
        }

        // pour la v2 encore une fois c'est juste pour traduire des textes à la volée et trouver les clés de traduction plus facilement ça sert à rien pour la v1 du mod
        private bool ApplyTranslation(IntPtr ptr, IntPtr klass, string textToSet, string goName, string keyForLog = null)
        {
            // Essaie d'écrire le nouveau texte dans la variable "text"
            bool success = SetNativeProperty(ptr, klass, "text", textToSet);
            if (!success) success = SetNativeProperty(ptr, klass, "Text", textToSet);

            if (!success)
            {
                TryTranslateNativeField(ptr, klass, "text", textToSet);
                TryTranslateNativeField(ptr, klass, "m_Text", textToSet);
                TryTranslateNativeField(ptr, klass, "_text", textToSet);
                success = true;
            }
            return success;
        }

        private string NormalizeString(string input) // Nettoie le texte (enlève les espaces vides)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\r\n", "\n").Replace("\r", "").Trim();
        }

        private unsafe string ReadNativeString(IntPtr objPtr, IntPtr klass, string fieldName) // Lit une chaine de caractères en mémoire
        {
            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(klass, fieldName);
            if (field == IntPtr.Zero) return null;
            IntPtr stringPtr = IntPtr.Zero;
            IL2CPP.il2cpp_field_get_value(objPtr, field, (void*)&stringPtr);
            if (stringPtr != IntPtr.Zero) return IL2CPP.Il2CppStringToManaged(stringPtr);
            return null;
        }

        private unsafe bool SetNativeProperty(IntPtr objPtr, IntPtr klass, string propName, string newValue) // Modifie une propriété en mémoire
        {
            IntPtr propNamePtr = Marshal.StringToHGlobalAnsi(propName);
            IntPtr prop = IL2CPP.il2cpp_class_get_property_from_name(klass, propNamePtr);
            Marshal.FreeHGlobal(propNamePtr);

            if (prop == IntPtr.Zero) return false;
            IntPtr setMethod = IL2CPP.il2cpp_property_get_set_method(prop);
            if (setMethod == IntPtr.Zero) return false;

            IntPtr il2cppString = IL2CPP.ManagedStringToIl2Cpp(newValue);
            void** args = stackalloc void*[1];
            args[0] = (void*)il2cppString;

            IntPtr exc = IntPtr.Zero;
            IL2CPP.il2cpp_runtime_invoke(setMethod, objPtr, args, ref exc);
            return exc == IntPtr.Zero;
        }

        private unsafe void TryTranslateNativeField(IntPtr objPtr, IntPtr klass, string fieldName, string newValue) // Modifie un champ en mémoire
        {
            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(klass, fieldName);
            if (field == IntPtr.Zero) return;
            IntPtr newStringPtr = IL2CPP.ManagedStringToIl2Cpp(newValue);
            IL2CPP.il2cpp_field_set_value(objPtr, field, (void*)newStringPtr);
        }

        // pour charger la langue 
        public void ToggleLanguage() // Change la langue quand on appuie sur le bouton
        {
            if (AvailableLanguages.Count <= 1) return;

            int currentIndex = AvailableLanguages.IndexOf(CurrentLanguage);
            int nextIndex = currentIndex + 1;
            if (nextIndex >= AvailableLanguages.Count) nextIndex = 0;

            CurrentLanguage = AvailableLanguages[nextIndex];
            LoadAllTranslations(); // Recharge les textes
            // UpdateTextLogic(); // Supprimé car on n'a plus de bouton in-game à mettre à jour
            ApplyUITranslationsNative(); // Applique partout

        }

        private void LoadAllTranslations() // Charge le bon fichier CSV selon la langue
        {
            AllTranslations.Clear();
            if (CurrentLanguage == LanguageEnum.English) return;

            string filename = "";
            switch (CurrentLanguage)
            {
                case LanguageEnum.French: filename = CSV_FILENAME_FR; break;
                case LanguageEnum.Italian: filename = CSV_FILENAME_IT; break;
                case LanguageEnum.Spanish: filename = CSV_FILENAME_ES; break;

            }
            if (string.IsNullOrEmpty(filename)) filename = $"{CurrentLanguage.ToString().ToLower()}.csv";

            if (!string.IsNullOrEmpty(filename))
            {
                LoadCSV(filename);
                LoadCSV("ui_" + filename, isUIFile: true); // Charge aussi le cvs ui_ s'il existe mais la fonction pour l'appliquer est désac donc sert a rien
            }
        }

        private void LoadCSV(string filename, bool isUIFile = false)
        {
            string gameDir = Directory.GetCurrentDirectory();
            string path = Path.Combine(gameDir, "Language", filename);
            if (!File.Exists(path)) path = Path.Combine(gameDir, "Mods", "Language", filename);

            if (!File.Exists(path)) return;

            try
            {
                Encoding encodingToUse;
                bool useLegacyFix = false;
                //  au cas ou si le cvs est mal encodé 
                if (CurrentLanguage == LanguageEnum.French || CurrentLanguage == LanguageEnum.Italian)
                {
                    encodingToUse = Encoding.GetEncoding("iso-8859-1");
                    useLegacyFix = true;
                }
                else
                {
                    encodingToUse = Encoding.UTF8;
                    useLegacyFix = false;
                }

                var lines = File.ReadAllLines(path, encodingToUse);
                char separator = ',';
                if (lines.Length > 0 && lines[0].Contains(";") && !lines[0].Contains(",")) separator = ';';

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Type")) continue; // Saute les lignes vides ou titres
                    var parts = CsvParser.ParseLine(line, separator);
                    if (parts.Count >= 3)
                    {
                        string rawKey = CleanText(parts[0], useLegacyFix);
                        string key = NormalizeString(rawKey);
                        string value = CleanText(parts[2], useLegacyFix);
                        if (!string.IsNullOrEmpty(key)) AllTranslations[key] = value.Normalize();
                    }
                }

            }
            catch (Exception e) { }
        }

        private string CleanText(string input, bool useLegacyEncodingFix)
        {
            string t = input;
            if (t.StartsWith("\"") && t.EndsWith("\"") && t.Length > 1) t = t.Substring(1, t.Length - 2);
            t = t.Replace("\"\"", "\"").Replace("\\n", "\n");
            if (useLegacyEncodingFix) return FixEncoding(t);
            return t;
        }

        private string FixEncoding(string text) // au cas ou si le cvs est mal encodé encore une fois merci chatgpt
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Contains("Ã©") || text.Contains("Ã¨") || text.Contains("Ã§") || text.Contains("Ã"))
            {
                byte[] bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(text);
                return Encoding.UTF8.GetString(bytes);
            }
            return text;
        }

        public static string SwapCharacters(string input) // Echange é et è merci shinobi pour LA POLICE ECRITURE DE MERDE 
        {
            if (string.IsNullOrEmpty(input)) return input;
            char[] chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == 'é') chars[i] = 'è';
                else if (chars[i] == 'è') chars[i] = 'é';
                else if (chars[i] == 'É') chars[i] = 'È';
                else if (chars[i] == 'È') chars[i] = 'É';
            }
            return new string(chars);
        }

        private IEnumerator CheckForUpdateRoutine() // ça check mon github pour voir si y'a une update si ça gene vraiment vous pouvez supp
        {
            UnityWebRequest www = UnityWebRequest.Get(UPDATE_URL);
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string onlineVersion = www.downloadHandler.text.Trim();
                if (onlineVersion.Length < 10 && !onlineVersion.Contains("<") && onlineVersion != CURRENT_VERSION) ShowUpdatePopup = true;
            }
        }
    }

    public static class CsvParser
    {
        public static List<string> ParseLine(string line, char separator) // merci chatgpt 
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder currentValue = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"') { currentValue.Append('\"'); i++; }
                    else { inQuotes = !inQuotes; }
                }
                else if (c == separator && !inQuotes) { result.Add(currentValue.ToString()); currentValue.Clear(); }
                else { currentValue.Append(c); }
            }
            result.Add(currentValue.ToString());
            return result;
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.GetLocalizedString))]
    public static class Localization_GetLocalizedString_Patch
    {
        // Le savais tu que c'est plus simple de faire un patch quand le dev le met dans le jeu  je vais pas pas commenter plus c'est juste j'utilise yarn et je regarde aussi le nom de certains perso pour appliquer une correction dans une fonction plus au vu que shinobi utilise des polices écriture BUGGER EN FRANCAIS SAYER
        public static bool Prefix(Localization __instance, string key, ref string __result)
        {
            if (DeadWatchTranslationMod.CurrentLanguage == DeadWatchTranslationMod.LanguageEnum.English) return true;
            if (DeadWatchTranslationMod.AllTranslations.TryGetValue(key, out string translatedText))
            {
                if (translatedText.Contains("StockPhotoMan") || translatedText.Contains("Lumi") || translatedText.Contains("Flumi"))
                    translatedText = DeadWatchTranslationMod.SwapCharacters(translatedText);
                __result = translatedText;
                return false;
            }
            if (key.StartsWith("line:"))
            {
                string shortKey = key.Substring(5);
                if (DeadWatchTranslationMod.AllTranslations.TryGetValue(shortKey, out string translatedShort))
                {
                    if (translatedShort.Contains("StockPhotoMan") || translatedShort.Contains("Lumi") || translatedShort.Contains("Flumi"))
                        translatedShort = DeadWatchTranslationMod.SwapCharacters(translatedShort);
                    __result = translatedShort;
                    return false;
                }
            }
            return true;
        }
    }
}
