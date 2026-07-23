using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace SolastaAIPersistence
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableEmergencyLowHpFallback = true;
        public float EmergencyHpThresholdPercent = 30f;
        public bool EnableHotkeyToggle = true;
        public KeyCode ToggleHotkey = KeyCode.N;
        public bool EnableAutoWeaponSwap = true;
        public bool AutoControlGuests = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings ModSettings { get; private set; }
        public static string SaveFilePath { get; private set; }
        
        // Character Name -> AI Choice Index (0 = Human/Player, 1 = Melee, 2 = Range, 3 = Caster, 4 = Cleric, 5 = Fighter, 6 = Mage, 7 = Rogue)
        public static Dictionary<string, int> CharacterAIChoices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static readonly string[] AIPackageNames = new string[]
        {
            "Human (Player)",
            "AI: Melee (Default)",
            "AI: Range (Backup Melee)",
            "AI: Caster (Backup Attacks)",
            "AI: Cleric Combat",
            "AI: Fighter Combat",
            "AI: Mage Combat",
            "AI: Rogue Combat"
        };

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                ModEntry = modEntry;
                ModSettings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                SaveFilePath = Path.Combine(modEntry.Path, "SavedAIControllers.json");

                LoadSavedChoices();

                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
                modEntry.OnUpdate = OnUpdate;

                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                modEntry.Logger.Log("[SolastaAI] Standalone Mod loaded successfully!");
                return true;
            }
            catch (Exception ex)
            {
                modEntry?.Logger.Error($"[SolastaAI] Critical Error during Load: {ex}");
                return true;
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Solasta AI & AI Persistence Settings</b>", GUILayout.ExpandWidth(true));
            
            // Emergency Protection Toggle & Slider
            GUILayout.BeginHorizontal();
            ModSettings.EnableEmergencyLowHpFallback = GUILayout.Toggle(
                ModSettings.EnableEmergencyLowHpFallback, 
                " <b>Notfall-Schutz aktivieren</b> (Bei niedrigen TP automatisch auf manuelle Steuerung zurückschalten)"
            );
            GUILayout.EndHorizontal();

            if (ModSettings.EnableEmergencyLowHpFallback)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"TP-Schwelle für Notfall-Schutz: <b>{Mathf.RoundToInt(ModSettings.EmergencyHpThresholdPercent)}% TP</b>", GUILayout.Width(280));
                ModSettings.EmergencyHpThresholdPercent = GUILayout.HorizontalSlider(ModSettings.EmergencyHpThresholdPercent, 5f, 50f, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                ModSettings.EnableAutoWeaponSwap,
                " <b>Automatischer Waffenwechsel</b> (Wechselt auf Fernkampf, wenn kein Nahkampfziel erreichbar ist)"
            );

            ModSettings.EnableHotkeyToggle = GUILayout.Toggle(
                ModSettings.EnableHotkeyToggle, 
                " <b>Hotkey 'N' im Kampf aktivieren</b> (Schaltet den aktiven Helden zwischen KI und Spieler um)"
            );
            
            ModSettings.AutoControlGuests = GUILayout.Toggle(
                ModSettings.AutoControlGuests, 
                " <b>Automatische KI für Gast-Charaktere</b>"
            );
            
            GUILayout.Space(15);
            GUILayout.Label("<b>Aktive Helden-Steuerung (Party AI Controls):</b>");

            var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
            if (charService != null && charService.PartyCharacters != null && charService.PartyCharacters.Count > 0)
            {
                foreach (var character in charService.PartyCharacters)
                {
                    if (character == null) continue;
                    string name = character.Name;
                    if (string.IsNullOrEmpty(name)) continue;

                    if (!CharacterAIChoices.TryGetValue(name, out int currentChoice))
                    {
                        currentChoice = 0; // Default Human
                    }

                    GUILayout.BeginHorizontal("box");
                    string displayName = character.RulesetCharacter != null ? character.RulesetCharacter.Name : name;
                    GUILayout.Label($"<b>{displayName}</b>", GUILayout.Width(200));
                    
                    int newChoice = GUILayout.SelectionGrid(currentChoice, AIPackageNames, 4, GUILayout.ExpandWidth(true));
                    if (newChoice != currentChoice)
                    {
                        CharacterAIChoices[name] = newChoice;
                        ApplyAIController(character, newChoice);
                        SaveChoices();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("<i>(Keine aktive Gruppe geladen. Starte oder lade einen Spielstand, um Helden zu konfigurieren.)</i>");
            }

            GUILayout.EndVertical();
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            ModSettings.Save(modEntry);
            SaveChoices();
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            if (!ModSettings.EnableHotkeyToggle) return;

            if (Input.GetKeyDown(ModSettings.ToggleHotkey))
            {
                ToggleActiveCharacterAI();
            }
        }

        public static void ToggleActiveCharacterAI()
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService != null && battleService.IsBattleInProgress)
                {
                    var activeBattle = battleService.Battle;
                    var activeContender = activeBattle?.ActiveContender;
                    if (activeContender != null)
                    {
                        string name = activeContender.Name;
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!CharacterAIChoices.TryGetValue(name, out int currentChoice))
                            {
                                currentChoice = 0;
                            }

                            int newChoice = (currentChoice == 0) ? 1 : 0;
                            CharacterAIChoices[name] = newChoice;
                            ApplyAIController(activeContender, newChoice);
                            SaveChoices();

                            string modeStr = newChoice > 0 ? "AI CONTROL" : "HUMAN CONTROL";
                            ModEntry.Logger.Log($"[SolastaAI] Hotkey 'N' pressed: {name} set to {modeStr}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger.Error($"[SolastaAI] Error handling Hotkey toggle: {ex}");
            }
        }

        public static void ApplyAIController(GameLocationCharacter character, int choice)
        {
            if (character == null) return;
            try
            {
                if (choice <= 0)
                {
                    character.ControllerId = PlayerControllerManager.MainPlayerControllerId;
                }
                else
                {
                    character.ControllerId = PlayerControllerManager.DmControllerId;

                    var decisionPackageDb = DatabaseRepository.GetDatabase<TA.AI.DecisionPackageDefinition>();
                    if (decisionPackageDb != null)
                    {
                        TA.AI.DecisionPackageDefinition package = null;
                        switch (choice)
                        {
                            case 1: package = decisionPackageDb.GetElement("DefaultMeleeWithBackupRangeDecisions", true); break;
                            case 2: package = decisionPackageDb.GetElement("DefaultRangeWithBackupMeleeDecisions", true); break;
                            case 3: package = decisionPackageDb.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break;
                            case 4: package = decisionPackageDb.GetElement("ClericCombatDecisions", true); break;
                            case 5: package = decisionPackageDb.GetElement("FighterCombatDecisions", true); break;
                            case 6: package = decisionPackageDb.GetElement("CasterCombatDecisions", true); break;
                            case 7: package = decisionPackageDb.GetElement("RogueCombatDecisions", true); break;
                            default: package = decisionPackageDb.GetElement("DefaultMeleeWithBackupRangeDecisions", true); break;
                        }

                        if (package != null)
                        {
                            if (character.BehaviourPackage == null)
                            {
                                var newPkg = new GameLocationBehaviourPackage();
                                newPkg.BattleStartBehavior = GameLocationBehaviourPackage.BattleStartBehaviorType.RaisesAlarm;
                                character.BehaviourPackage = newPkg;
                            }
                            character.BehaviourPackage.DecisionPackageDefinition = package;
                        }
                    }
                }

                var activePlayerController = Gui.ActivePlayerController;
                if (activePlayerController != null)
                {
                    activePlayerController.DirtyControlledCharacters();
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger.Error($"[SolastaAI] Error applying AI controller to {character?.Name}: {ex}");
            }
        }

        public static void CheckAndAutoSwapWeapons(GameLocationCharacter character)
        {
            try
            {
                if (!ModSettings.EnableAutoWeaponSwap || character == null || character.RulesetCharacter == null) return;

                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.CharacterInventory == null) return;

                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService == null || !battleService.IsBattleInProgress) return;

                var battle = battleService.Battle;
                if (battle == null) return;

                // Find closest alive enemy contender
                int minDistance = int.MaxValue;
                var enemies = (character.Side == RuleDefinitions.Side.Ally) ? battle.EnemyContenders : battle.PlayerContenders;
                if (enemies == null || enemies.Count == 0) return;

                var posA = character.LocationPosition;
                foreach (var enemy in enemies)
                {
                    if (enemy == null || enemy.RulesetCharacter == null || enemy.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth)
                        continue;

                    var posB = enemy.LocationPosition;
                    int dx = Math.Abs(posA.x - posB.x);
                    int dz = Math.Abs(posA.z - posB.z);
                    int dy = Math.Abs(posA.y - posB.y);
                    int dist = Math.Max(dx, Math.Max(dy, dz));

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                    }
                }

                if (minDistance == int.MaxValue) return;

                var inventory = hero.CharacterInventory;
                int currentConfig = inventory.CurrentConfiguration;
                int otherConfig = (currentConfig == 0) ? 1 : 0;

                bool currentlyRanged = hero.IsWieldingRangedWeapon();

                // If no enemy in melee reach (> 2 cells) and currently holding Melee weapon:
                if (minDistance > 2 && !currentlyRanged)
                {
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (hero.IsWieldingRangedWeapon())
                    {
                        ModEntry?.Logger.Log($"[SolastaAI] Auto-Weapon Swap: {character.Name} switched to Ranged Weapon Set (Config {otherConfig}) - Target distance: {minDistance} cells.");
                    }
                    else
                    {
                        // Secondary set is not ranged, revert back
                        inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                    }
                }
                // If enemy IS in melee reach (<= 2 cells) and currently holding Ranged weapon:
                else if (minDistance <= 2 && currentlyRanged)
                {
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (!hero.IsWieldingRangedWeapon())
                    {
                        ModEntry?.Logger.Log($"[SolastaAI] Auto-Weapon Swap: {character.Name} switched to Melee Weapon Set (Config {otherConfig}) - Target in melee reach ({minDistance} cells).");
                    }
                    else
                    {
                        // Secondary set was also ranged, revert back
                        inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in CheckAndAutoSwapWeapons: {ex}");
            }
        }

        public static void LoadSavedChoices()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    CharacterAIChoices.Clear();
                    
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
                                    CharacterAIChoices[name] = choice;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error loading saved choices: {ex}");
            }
        }

        public static void SaveChoices()
        {
            try
            {
                List<string> entries = new List<string>();
                foreach (var kvp in CharacterAIChoices)
                {
                    entries.Add($"  \"{kvp.Key}\": {kvp.Value}");
                }
                string json = "{\n" + string.Join(",\n", entries.ToArray()) + "\n}";
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error saving choices: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.StartBattleTurn))]
    public static class GameLocationCharacter_StartBattleTurn_Patch
    {
        public static void Postfix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null) return;
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return;

                // Check Emergency Low HP Fallback (only if enabled)
                if (Main.ModSettings.EnableEmergencyLowHpFallback && __instance.RulesetCharacter != null)
                {
                    int currentHp = __instance.RulesetCharacter.CurrentHitPoints;
                    int maxHp = currentHp + __instance.RulesetCharacter.MissingHitPoints;
                    if (maxHp > 0 && ((float)currentHp / maxHp * 100f) < Main.ModSettings.EmergencyHpThresholdPercent)
                    {
                        Main.ModEntry?.Logger.Log($"[SolastaAI] Emergency Fallback triggered for {name} (HP: {currentHp}/{maxHp}). Switching to Human Control!");
                        Main.ApplyAIController(__instance, 0);
                        return;
                    }
                }

                // Apply saved choice
                if (Main.CharacterAIChoices.TryGetValue(name, out int choice))
                {
                    Main.ApplyAIController(__instance, choice);

                    // If AI is active, check and auto-swap weapon if needed
                    if (choice > 0)
                    {
                        Main.CheckAndAutoSwapWeapons(__instance);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAI] Error in StartBattleTurn Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.DamageSustained))]
    public static class GameLocationCharacter_DamageSustained_Patch
    {
        public static void Postfix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null || !Main.ModSettings.EnableEmergencyLowHpFallback) return;
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return;

                if (Main.CharacterAIChoices.TryGetValue(name, out int choice) && choice > 0)
                {
                    if (__instance.RulesetCharacter != null)
                    {
                        int currentHp = __instance.RulesetCharacter.CurrentHitPoints;
                        int maxHp = currentHp + __instance.RulesetCharacter.MissingHitPoints;
                        if (maxHp > 0 && ((float)currentHp / maxHp * 100f) < Main.ModSettings.EmergencyHpThresholdPercent)
                        {
                            Main.ModEntry?.Logger.Log($"[SolastaAI] Damage Sustained! Emergency Fallback triggered for {name} (HP: {currentHp}/{maxHp}). Reverting to Human Control!");
                            Main.CharacterAIChoices[name] = 0; // Revert choice
                            Main.ApplyAIController(__instance, 0);
                            Main.SaveChoices();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAI] Error in DamageSustained Postfix: {ex}");
            }
        }
    }
}
