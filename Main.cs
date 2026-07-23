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
        /// Enables Fighter class skill automation (Second Wind / Durchschnaufen, Action Surge / Tatendrank).
        /// </summary>
        public bool EnableFighterTactics = true;

        /// <summary>
        /// Enables Opportunity Attack protection for Ranged Fighters (eliminating adjacent threats in melee first).
        /// </summary>
        public bool EnableAvoidOpportunityAttacks = true;

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
        /// 5 = AI: Fighter (Melee)
        /// 6 = AI: Fighter (Ranged / Archer)
        /// 7 = AI: Mage Combat
        /// 8 = AI: Rogue Combat
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
            "AI: Fighter (Melee)",
            "AI: Fighter (Ranged)",
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
        /// Renders the Unity Mod Manager Options UI panel for SolastaAI with structured sections.
        /// </summary>
        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b><size=14>SolastaAI - Advanced AI & Tactical Settings</size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            // --- SECTION 1: GLOBAL SAFETY & HOTKEY SETTINGS ---
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>🛡️ Global Safety & Hotkey Settings</b>");
            
            ModSettings.EnableEmergencyLowHpFallback = GUILayout.Toggle(
                ModSettings.EnableEmergencyLowHpFallback, 
                " <b>Enable Emergency Low HP Protection</b> (Reverts hero to Human control when HP drops below threshold)"
            );

            if (ModSettings.EnableEmergencyLowHpFallback)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"Emergency HP Threshold: <b>{Mathf.RoundToInt(ModSettings.EmergencyHpThresholdPercent)}% Max HP</b>", GUILayout.Width(280));
                ModSettings.EmergencyHpThresholdPercent = GUILayout.HorizontalSlider(ModSettings.EmergencyHpThresholdPercent, 5f, 50f, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            ModSettings.EnableHotkeyToggle = GUILayout.Toggle(
                ModSettings.EnableHotkeyToggle, 
                " <b>Enable In-Combat Quick Hotkey ('N')</b> (Toggles active hero AI/Human control on the fly)"
            );
            
            ModSettings.AutoControlGuests = GUILayout.Toggle(
                ModSettings.AutoControlGuests, 
                " <b>Enable Auto AI for Guest & Companion Characters</b>"
            );
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // --- SECTION 2: CLASS & COMBAT TACTICS ---
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>⚔️ Class Tactics & Combat Intelligence</b>");

            ModSettings.EnableFighterTactics = GUILayout.Toggle(
                ModSettings.EnableFighterTactics,
                " <b>Enable Fighter Skill Automation</b> (Automatically executes Second Wind / Durchschnaufen & Action Surge / Tatendrank)"
            );

            ModSettings.EnableAvoidOpportunityAttacks = GUILayout.Toggle(
                ModSettings.EnableAvoidOpportunityAttacks,
                " <b>Enable Opportunity Attack Protection for Ranged Fighters</b> (Fights adjacent threats in melee first before retreating safely)"
            );

            ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                ModSettings.EnableAutoWeaponSwap,
                " <b>Enable Auto-Weapon Swap</b> (Swaps between Melee & Ranged weapon sets based on enemy target distance)"
            );
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // --- SECTION 3: PARTY CHARACTER ARCHETYPE SELECTION ---
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>👥 Party Character AI Archetype Selection</b>");

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
                    GUILayout.Label($"<b>{displayName}</b>", GUILayout.Width(180));
                    
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
                GUILayout.Label("<i>(No active party loaded. Start or load a campaign session to configure active heroes.)</i>");
            }
            GUILayout.EndVertical();

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
                            case 5: package = decisionPackageDb.GetElement("FighterCombatDecisions", true); break; // Fighter (Melee)
                            case 6: package = decisionPackageDb.GetElement("DefaultRangeWithBackupMeleeDecisions", true); break; // Fighter (Ranged / Archer)
                            case 7: package = decisionPackageDb.GetElement("CasterCombatDecisions", true); break;
                            case 8: package = decisionPackageDb.GetElement("RogueCombatDecisions", true); break;
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
        /// Executes Fighter class tactical skills (Second Wind / Durchschnaufen, Action Surge / Tatendrank) and handles archetype-specific weapon positioning.
        /// </summary>
        public static void ExecuteFighterTactics(GameLocationCharacter character, bool isRangedArchetype)
        {
            try
            {
                if (!ModSettings.EnableFighterTactics || character == null || character.RulesetCharacter == null) return;
                
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.UsablePowers == null) return;

                // 1. Second Wind / Durchschnaufen (PowerFighterSecondWind) trigger when HP < 60%
                int currentHp = hero.CurrentHitPoints;
                int maxHp = currentHp + hero.MissingHitPoints;
                if (maxHp > 0 && ((float)currentHp / maxHp * 100f) < 60f)
                {
                    var secondWindPower = hero.UsablePowers.Find(p => p.PowerDefinition != null && 
                        (p.PowerDefinition.Name.IndexOf("SecondWind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         p.PowerDefinition.Name.IndexOf("Durchschnaufen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         p.PowerDefinition.Name.IndexOf("CatchBreath", StringComparison.OrdinalIgnoreCase) >= 0));

                    if (secondWindPower != null && hero.GetRemainingUsesOfPower(secondWindPower) > 0)
                    {
                        hero.UsePower(secondWindPower);
                        ModEntry?.Logger.Log($"[SolastaAI] Fighter Skill: {character.Name} used Second Wind / Durchschnaufen! (HP: {currentHp}/{maxHp})");
                    }
                }

                // 2. Action Surge / Tatendrank (PowerFighterActionSurge) trigger in combat
                var actionSurgePower = hero.UsablePowers.Find(p => p.PowerDefinition != null && 
                    (p.PowerDefinition.Name.IndexOf("ActionSurge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     p.PowerDefinition.Name.IndexOf("Tatendrank", StringComparison.OrdinalIgnoreCase) >= 0));

                if (actionSurgePower != null && hero.GetRemainingUsesOfPower(actionSurgePower) > 0)
                {
                    hero.UsePower(actionSurgePower);
                    ModEntry?.Logger.Log($"[SolastaAI] Fighter Skill: {character.Name} activated Action Surge / Tatendrank!");
                }

                // 3. Auto-Weapon Swap tailored specifically to Fighter archetype (Melee vs Ranged)
                CheckAndAutoSwapWeapons(character, isRangedArchetype);
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in ExecuteFighterTactics for {character?.Name}: {ex}");
            }
        }

        /// <summary>
        /// Evaluates distance to closest enemy contender and automatically switches hero weapon configuration between melee and ranged.
        /// Respects EnableAvoidOpportunityAttacks setting for Ranged Fighters.
        /// </summary>
        public static void CheckAndAutoSwapWeapons(GameLocationCharacter character, bool isRangedArchetype = false)
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

                var inventory = hero.CharacterInventory;
                int currentConfig = inventory.CurrentConfiguration;
                int otherConfig = (currentConfig == 0) ? 1 : 0;
                bool currentlyRanged = hero.IsWieldingRangedWeapon();

                // Calculate distance to closest alive enemy contender on tactical grid
                int minDistance = int.MaxValue;
                var enemies = (character.Side == RuleDefinitions.Side.Ally) ? battle.EnemyContenders : battle.PlayerContenders;
                if (enemies != null && enemies.Count > 0)
                {
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
                }

                // RANGED FIGHTER TACTICAL OPPORTUNITY ATTACK PREVENTION:
                if (isRangedArchetype)
                {
                    // If Opportunity Attack Protection is enabled AND an enemy is in immediate melee reach (dist <= 1 cell):
                    if (ModSettings.EnableAvoidOpportunityAttacks && minDistance <= 1)
                    {
                        if (currentlyRanged)
                        {
                            inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                            if (!hero.IsWieldingRangedWeapon())
                            {
                                ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter Safety: {character.Name} threatened in melee (dist: {minDistance}). Switched to Melee Set to safely eliminate threat without provoking opportunity attacks.");
                            }
                            else
                            {
                                inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                            }
                        }
                    }
                    // Otherwise (Safe distance or protection toggle disabled):
                    else
                    {
                        if (!currentlyRanged)
                        {
                            inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                            if (hero.IsWieldingRangedWeapon())
                            {
                                ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter Safety: {character.Name} safe from opportunity attacks (dist: {minDistance}). Equipping Ranged Set!");
                            }
                            else
                            {
                                inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                            }
                        }
                    }
                    return;
                }

                // GENERAL / MELEE AUTO-WEAPON SWAP LOGIC:
                if (minDistance == int.MaxValue) return;

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
    /// Harmony Patch on GameLocationBattleManager.TriggerBattleEnd to guarantee all party members revert to Player Control after combat ends.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationBattleManager), "TriggerBattleEnd")]
    public static class GameLocationBattleManager_TriggerBattleEnd_Patch
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
                Main.ModEntry?.Logger.Error($"[SolastaAI] Error in TriggerBattleEnd Postfix: {ex}");
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

                    // If AI control is active, check and execute Fighter Tactics or auto-weapon swap
                    if (choice == 5)
                    {
                        Main.ExecuteFighterTactics(__instance, isRangedArchetype: false);
                    }
                    else if (choice == 6)
                    {
                        Main.ExecuteFighterTactics(__instance, isRangedArchetype: true);
                    }
                    else if (choice > 0)
                    {
                        Main.CheckAndAutoSwapWeapons(__instance, isRangedArchetype: false);
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
