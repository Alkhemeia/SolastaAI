using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace SolastaAIPersistence
{
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static string SaveFilePath { get; private set; }
        public static Dictionary<string, int> SavedAIChoices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static Type PCCType;
        public static FieldInfo ControllersChoicesField;
        public static MethodInfo UpdatePartyControllerIdsMethod;
        public static Harmony HarmonyInstance;
        public static bool PatchedUB = false;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                ModEntry = modEntry;
                ModEntry.Logger.Log("[SolastaAIPersistence] Initializing Mod...");

                SaveFilePath = Path.Combine(modEntry.Path, "SavedAIControllers.json");
                LoadSavedChoices();

                HarmonyInstance = new Harmony(modEntry.Info.Id);

                // Patch GameManager.BindPostDatabase to defer UB patching until Solasta databases are ready
                var bindPostDbMethod = AccessTools.Method(typeof(GameManager), "BindPostDatabase");
                if (bindPostDbMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(GameManager_BindPostDatabase_Patch), nameof(GameManager_BindPostDatabase_Patch.Postfix));
                    HarmonyInstance.Patch(bindPostDbMethod, postfix: new HarmonyMethod(postfix));
                    ModEntry.Logger.Log("[SolastaAIPersistence] Patched GameManager.BindPostDatabase to defer initialization.");
                }

                // Patch GameLocationCharacter.StartBattleTurn
                var startBattleTurnMethod = AccessTools.Method(typeof(GameLocationCharacter), nameof(GameLocationCharacter.StartBattleTurn));
                if (startBattleTurnMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(GameLocationCharacter_StartBattleTurn_Patch), nameof(GameLocationCharacter_StartBattleTurn_Patch.Postfix));
                    HarmonyInstance.Patch(startBattleTurnMethod, postfix: new HarmonyMethod(postfix));
                    ModEntry.Logger.Log("[SolastaAIPersistence] Patched GameLocationCharacter.StartBattleTurn.");
                }

                ModEntry.Logger.Log("[SolastaAIPersistence] Mod loaded successfully!");
                return true;
            }
            catch (Exception ex)
            {
                modEntry?.Logger.Error($"[SolastaAIPersistence] Error in Load: {ex}");
                return true;
            }
        }

        public static void TryPatchUB()
        {
            if (PatchedUB) return;
            try
            {
                if (PCCType == null)
                {
                    PCCType = AccessTools.TypeByName("SolastaUnfinishedBusiness.Models.PlayerControllerContext");
                    if (PCCType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (asm.GetName().Name == "SolastaUnfinishedBusiness")
                            {
                                PCCType = asm.GetType("SolastaUnfinishedBusiness.Models.PlayerControllerContext");
                                if (PCCType != null) break;
                            }
                        }
                    }
                }

                if (PCCType != null)
                {
                    ControllersChoicesField = AccessTools.Field(PCCType, "ControllersChoices");
                    UpdatePartyControllerIdsMethod = AccessTools.Method(PCCType, "UpdatePartyControllerIds", new Type[] { typeof(bool) });

                    var refreshGuiStateMethod = AccessTools.Method(PCCType, "RefreshGuiState");
                    if (refreshGuiStateMethod != null)
                    {
                        var postfix = AccessTools.Method(typeof(PlayerControllerContext_RefreshGuiState_Patch), nameof(PlayerControllerContext_RefreshGuiState_Patch.Postfix));
                        HarmonyInstance.Patch(refreshGuiStateMethod, postfix: new HarmonyMethod(postfix));
                        ModEntry?.Logger.Log("[SolastaAIPersistence] Patched PlayerControllerContext.RefreshGuiState successfully.");
                    }

                    if (UpdatePartyControllerIdsMethod != null)
                    {
                        var postfix = AccessTools.Method(typeof(PlayerControllerContext_UpdatePartyControllerIds_Patch), nameof(PlayerControllerContext_UpdatePartyControllerIds_Patch.Postfix));
                        HarmonyInstance.Patch(UpdatePartyControllerIdsMethod, postfix: new HarmonyMethod(postfix));
                        ModEntry?.Logger.Log("[SolastaAIPersistence] Patched PlayerControllerContext.UpdatePartyControllerIds successfully.");
                    }

                    PatchedUB = true;
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAIPersistence] Error patching UB: {ex}");
            }
        }

        public static void LoadSavedChoices()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    SavedAIChoices.Clear();
                    
                    json = json.Trim('{', '}', ' ', '\r', '\n');
                    if (!string.IsNullOrEmpty(json))
                    {
                        var pairs = json.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pair in pairs)
                        {
                            var kv = pair.Split(new char[] { ':' }, 2);
                            if (kv.Length == 2)
                            {
                                string name = kv[0].Trim(' ', '"', '\r', '\n');
                                if (int.TryParse(kv[1].Trim(' ', '"', '\r', '\n'), out int choice))
                                {
                                    SavedAIChoices[name] = choice;
                                    ModEntry?.Logger.Log($"[SolastaAIPersistence] Loaded saved AI choice: {name} => {choice}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAIPersistence] Error loading saved AI choices: {ex}");
            }
        }

        public static void SaveChoices()
        {
            try
            {
                List<string> entries = new List<string>();
                foreach (var kvp in SavedAIChoices)
                {
                    entries.Add($"  \"{kvp.Key}\": {kvp.Value}");
                }
                string json = "{\n" + string.Join(",\n", entries.ToArray()) + "\n}";
                File.WriteAllText(SaveFilePath, json);
                ModEntry?.Logger.Log("[SolastaAIPersistence] Saved AI choices successfully.");
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAIPersistence] Error saving AI choices: {ex}");
            }
        }

        public static IDictionary GetControllersChoicesDict()
        {
            try
            {
                if (ControllersChoicesField == null && PCCType != null)
                {
                    ControllersChoicesField = AccessTools.Field(PCCType, "ControllersChoices");
                }
                return ControllersChoicesField?.GetValue(null) as IDictionary;
            }
            catch
            {
                return null;
            }
        }

        public static void InvokeUpdatePartyControllerIds(bool sideFlipped = false)
        {
            try
            {
                if (UpdatePartyControllerIdsMethod == null && PCCType != null)
                {
                    UpdatePartyControllerIdsMethod = AccessTools.Method(PCCType, "UpdatePartyControllerIds", new Type[] { typeof(bool) });
                }
                UpdatePartyControllerIdsMethod?.Invoke(null, new object[] { sideFlipped });
            }
            catch {}
        }
    }

    public static class GameManager_BindPostDatabase_Patch
    {
        public static void Postfix()
        {
            try
            {
                Main.TryPatchUB();
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAIPersistence] Error in BindPostDatabase Postfix: {ex}");
            }
        }
    }

    public static class PlayerControllerContext_RefreshGuiState_Patch
    {
        public static void Postfix()
        {
            try
            {
                var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                if (charService == null) return;

                var dict = Main.GetControllersChoicesDict();
                if (dict == null || dict.Count == 0) return;

                bool updatedAny = false;
                var keysList = new List<object>();
                foreach (var k in dict.Keys) keysList.Add(k);

                foreach (var characterObj in keysList)
                {
                    if (characterObj == null) continue;
                    
                    var nameProp = characterObj.GetType().GetProperty("Name");
                    string name = nameProp?.GetValue(characterObj, null) as string;

                    if (!string.IsNullOrEmpty(name) && Main.SavedAIChoices.TryGetValue(name, out int savedChoice))
                    {
                        int currentVal = Convert.ToInt32(dict[characterObj]);
                        if (currentVal != savedChoice)
                        {
                            dict[characterObj] = savedChoice;
                            updatedAny = true;
                            Main.ModEntry?.Logger.Log($"[SolastaAIPersistence] Applied persistent choice for {name} => {savedChoice}");
                        }
                    }
                }

                if (updatedAny)
                {
                    Main.InvokeUpdatePartyControllerIds(false);
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAIPersistence] Error in RefreshGuiState Postfix: {ex}");
            }
        }
    }

    public static class PlayerControllerContext_UpdatePartyControllerIds_Patch
    {
        public static void Postfix()
        {
            try
            {
                var dict = Main.GetControllersChoicesDict();
                if (dict == null) return;

                bool changed = false;
                foreach (DictionaryEntry entry in dict)
                {
                    var characterObj = entry.Key;
                    int choice = Convert.ToInt32(entry.Value);
                    if (characterObj == null) continue;

                    var nameProp = characterObj.GetType().GetProperty("Name");
                    string name = nameProp?.GetValue(characterObj, null) as string;

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!Main.SavedAIChoices.TryGetValue(name, out int current) || current != choice)
                        {
                            Main.SavedAIChoices[name] = choice;
                            changed = true;
                            Main.ModEntry?.Logger.Log($"[SolastaAIPersistence] Saved choice updated for {name} => {choice}");
                        }
                    }
                }

                if (changed)
                {
                    Main.SaveChoices();
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAIPersistence] Error in UpdatePartyControllerIds Postfix: {ex}");
            }
        }
    }

    public static class GameLocationCharacter_StartBattleTurn_Patch
    {
        public static void Postfix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null) return;
                string name = __instance.Name;
                if (!string.IsNullOrEmpty(name) && Main.SavedAIChoices.TryGetValue(name, out int savedChoice) && savedChoice > 0)
                {
                    var dict = Main.GetControllersChoicesDict();
                    if (dict != null && dict.Contains(__instance))
                    {
                        dict[__instance] = savedChoice;
                    }
                    Main.InvokeUpdatePartyControllerIds(false);
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAIPersistence] Error in StartBattleTurn Postfix: {ex}");
            }
        }
    }
}
