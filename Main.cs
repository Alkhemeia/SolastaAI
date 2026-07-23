using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace SolastaAI
{
    /// <summary>
    /// Configuration settings for SolastaAI character AI management and tactical automation.
    /// </summary>
    public class Settings : UnityModManager.ModSettings
    {
        /// <summary>
        /// When true, automatically reverts hero control back to Human (Player) if HP drops below the threshold.
        /// </summary>
        public bool EnableEmergencyLowHpFallback = true;

        /// <summary>
        /// Percentage threshold of Max HP below which Emergency Fallback triggers (default: 30%).
        /// </summary>
        public float EmergencyHpThresholdPercent = 30f;

        /// <summary>
        /// Enables the 'N' hotkey during combat to toggle active hero control mode.
        /// </summary>
        public bool EnableHotkeyToggle = true;

        /// <summary>
        /// KeyCode used to toggle active character control during combat.
        /// </summary>
        public KeyCode ToggleHotkey = KeyCode.N;

        /// <summary>
        /// Automatically swaps weapon sets to ranged if no enemy is reachable in melee range.
        /// </summary>
        public bool EnableAutoWeaponSwap = true;

        /// <summary>
        /// Automatically applies AI control for guest/companion characters.
        /// </summary>
        public bool AutoControlGuests = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    /// <summary>
    /// Core entry point and static manager for SolastaAI.
    /// </summary>
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings ModSettings { get; private set; }
        public static string SaveFilePath { get; private set; }
        
        /// <summary>
        /// Dictionary mapping Character Name to selected AI Choice Index:
        /// 0 = Human (Player)
        /// 1 = AI: Melee (Default)
        /// 2 = AI: Range (Backup Melee)
        /// 3 = AI: Caster (Backup Attacks)
        /// 4 = AI: Cleric Combat
        /// 5 = AI: Fighter Combat
        /// 6 = AI: Mage Combat
        /// 7 = AI: Rogue Combat
        /// </summary>
        public static Dictionary<string, int> CharacterAIChoices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Human-readable names for available AI Decision Packages in the UMM UI.
        /// </summary>
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

        /// <summary>
        /// Unity Mod Manager Load method called during mod initialization at boot.
        /// </summary>
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

        /// <summary>
        /// Renders the Unity Mod Manager Options UI panel for SolastaAI.
        /// </summary>
        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>SolastaAI Settings</b>", GUILayout.ExpandWidth(true));
            
            // Emergency Protection Toggle & Slider
            GUILayout.BeginHorizontal();
            ModSettings.EnableEmergencyLowHpFallback = GUILayout.Toggle(
                ModSettings.EnableEmergencyLowHpFallback, 
                " <b>Enable Emergency Protection</b> (Automatically revert to Human control on low HP)"
            );
            GUILayout.EndHorizontal();

            if (ModSettings.EnableEmergencyLowHpFallback)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"Emergency HP Threshold: <b>{Mathf.RoundToInt(ModSettings.EmergencyHpThresholdPercent)}% Max HP</b>", GUILayout.Width(280));
                ModSettings.EmergencyHpThresholdPercent = GUILayout.HorizontalSlider(ModSettings.EmergencyHpThresholdPercent, 5f, 50f, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                ModSettings.EnableAutoWeaponSwap,
                " <b>Enable Auto-Weapon Swap</b> (Automatically switch to ranged set if no target is in melee reach)"
            );

            ModSettings.EnableHotkeyToggle = GUILayout.Toggle(
                ModSettings.EnableHotkeyToggle, 
                " <b>Enable In-Combat Hotkey ('N')</b> (Toggles active hero control mode on the fly)"
            );
            
            ModSettings.AutoControlGuests = GUILayout.Toggle(
                ModSettings.AutoControlGuests, 
                " <b>Enable Auto AI for Guest/Companion Characters</b>"
            );
            
            GUILayout.Space(15);
            GUILayout.Label("<b>Active Party Character AI Controls:</b>");

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
                GUILayout.Label("<i>(No active party loaded. Load or start a campaign session to configure active heroes.)</i>");
            }

            GUILayout.EndVertical();
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            ModSettings.Save(modEntry);
            SaveChoices();
        }

        /// <summary>
        /// Frame update listener. Handles the 'N' hotkey during combat and guarantees 100% Player Control during exploration outside combat.
        /// </summary>
        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            // Outside of active combat, ensure all party members are under Player Control (movable & selectable)
            EnsureExplorationControl();

            if (!ModSettings.EnableHotkeyToggle) return;

            if (Input.GetKeyDown(ModSettings.ToggleHotkey))
            {
                ToggleActiveCharacterAI();
            }
        }

        /// <summary>
        /// Helper to retrieve the active Human Player Controller ID dynamically.
        /// </summary>
        public static int GetPlayerControllerId()
        {
            try
            {
                var activeController = Gui.ActivePlayerController;
                if (activeController != null)
                {
                    return activeController.ControllerId;
                }
            }
            catch {}
            return PlayerControllerManager.MainPlayerControllerId;
        }

        /// <summary>
        /// Restores party characters that were left in DM/AI mode back to the active Player Controller whenever outside of combat exploration.
        /// </summary>
        public static void EnsureExplorationControl()
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService == null || !battleService.IsBattleInProgress)
                {
                    var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                    if (charService != null && charService.PartyCharacters != null)
                    {
                        int humanId = GetPlayerControllerId();
                        bool dirtied = false;

                        foreach (var character in charService.PartyCharacters)
                        {
                            // ONLY reset characters that are currently set to DM/AI Controller ID (4242)
                            if (character != null && character.ControllerId == PlayerControllerManager.DmControllerId)
                            {
                                character.ControllerId = humanId;
                                dirtied = true;
                            }
                        }

                        if (dirtied)
                        {
                            var activePlayerController = Gui.ActivePlayerController;
                            if (activePlayerController != null)
                            {
                                activePlayerController.DirtyControlledCharacters();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in EnsureExplorationControl: {ex}");
            }
        }

        /// <summary>
        /// Toggles AI vs Human control for the active contender in combat when hotkey 'N' is pressed.
        /// </summary>
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

        /// <summary>
        /// Applies the requested Controller ID and Decision Package to the specified character.
        /// Controller ID 4242 (DM/AI) is ONLY applied when actively in combat.
        /// </summary>
        public static void ApplyAIController(GameLocationCharacter character, int choice)
        {
            if (character == null) return;
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                bool isInBattle = battleService != null && battleService.IsBattleInProgress;
                int humanId = GetPlayerControllerId();

                // Outside of battle, ALWAYS force Player Control so heroes can move and be selected in exploration!
                if (!isInBattle || choice <= 0)
                {
                    character.ControllerId = humanId;
                }
                else
                {
                    // AI / DM Computer Control (Controller ID = 4242) only during combat
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

        /// <summary>
        /// Evaluates distance to closest enemy contender and automatically switches hero weapon configuration between melee and ranged.
        /// </summary>
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

                // Find closest alive enemy contender on tactical grid
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

                // If no enemy is in melee reach (> 2 cells) and currently holding Melee weapon:
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

        /// <summary>
        /// Loads character AI choices from SavedAIControllers.json.
        /// </summary>
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

        /// <summary>
        /// Saves character AI choices to SavedAIControllers.json.
        /// </summary>
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

    /// <summary>
    /// Harmony Patch on GameLocationBattleManager.EndBattle to guarantee all party members revert to Player Control after combat ends.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationBattleManager), "EndBattle")]
    public static class GameLocationBattleManager_EndBattle_Patch
    {
        public static void Postfix()
        {
            try
            {
                Main.EnsureExplorationControl();
                Main.ModEntry?.Logger.Log("[SolastaAI] Combat ended. All party members restored to Player Control.");
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAI] Error in EndBattle Postfix: {ex}");
            }
        }
    }

    /// <summary>
    /// Harmony Patch on GameLocationCharacter.StartBattleTurn as a PREFIX so ControllerId and DecisionPackage are configured BEFORE turn initialization.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.StartBattleTurn))]
    public static class GameLocationCharacter_StartBattleTurn_Patch
    {
        public static bool Prefix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null) return true;
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return true;

                // Check Emergency Low HP Fallback (only if enabled)
                if (Main.ModSettings.EnableEmergencyLowHpFallback && __instance.RulesetCharacter != null)
                {
                    int currentHp = __instance.RulesetCharacter.CurrentHitPoints;
                    int maxHp = currentHp + __instance.RulesetCharacter.MissingHitPoints;
                    if (maxHp > 0 && ((float)currentHp / maxHp * 100f) < Main.ModSettings.EmergencyHpThresholdPercent)
                    {
                        Main.ModEntry?.Logger.Log($"[SolastaAI] Emergency Fallback triggered for {name} (HP: {currentHp}/{maxHp}). Switching to Human Control!");
                        Main.ApplyAIController(__instance, 0);
                        return true;
                    }
                }

                // Apply saved AI choice BEFORE StartBattleTurn body executes!
                if (Main.CharacterAIChoices.TryGetValue(name, out int choice))
                {
                    Main.ApplyAIController(__instance, choice);

                    // If AI control is active, check and perform auto-weapon swap if necessary
                    if (choice > 0)
                    {
                        Main.CheckAndAutoSwapWeapons(__instance);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAI] Error in StartBattleTurn Prefix: {ex}");
            }
            return true;
        }
    }

    /// <summary>
    /// Harmony Patch on GameLocationCharacter.DamageSustained to trigger real-time Emergency Fallback when taking damage in combat.
    /// </summary>
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
                            Main.CharacterAIChoices[name] = 0; // Revert choice to Human
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
