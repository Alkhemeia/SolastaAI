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

        // --- GRANULAR FIGHTER SKILL TOGGLES ---
        /// <summary>
        /// Enables Second Wind / Durchschnaufen automation.
        /// </summary>
        public bool EnableFighterSecondWind = true;

        /// <summary>
        /// Enables Action Surge / Tatendrank automation.
        /// </summary>
        public bool EnableFighterActionSurge = true;

        /// <summary>
        /// Enables Opportunity Attack protection for Ranged Fighters.
        /// </summary>
        public bool EnableAvoidOpportunityAttacks = true;

        // --- GRANULAR DRUID SKILL TOGGLES ---
        /// <summary>
        /// Enables Wild Shape / Tiergestalt transformation.
        /// </summary>
        public bool EnableDruidWildShape = true;

        /// <summary>
        /// Enables Shillelagh / Zauberstock weapon enchantment.
        /// </summary>
        public bool EnableDruidShillelagh = true;

        /// <summary>
        /// Enables Guidance / Göttliche Führung self-buff when out of melee reach.
        /// </summary>
        public bool EnableDruidGuidance = true;

        /// <summary>
        /// Enables healing support for wounded allies by Druid spellcasters.
        /// </summary>
        public bool EnableDruidHealing = true;

        /// <summary>
        /// Enables Protection from Poison / Schutz vor Gift spell usage on allies.
        /// </summary>
        public bool EnableDruidProtectionFromPoison = true;

        /// <summary>
        /// Enables general buff & utility spellcasting for Druids.
        /// </summary>
        public bool EnableDruidBuffSpells = true;

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
        /// 5 = AI: Druid (Wild Shape & Support)
        /// 6 = AI: Druid (Shillelagh Melee)
        /// 7 = AI: Fighter (Melee)
        /// 8 = AI: Fighter (Ranged / Archer)
        /// 9 = AI: Mage Combat
        /// 10 = AI: Rogue Combat
        /// </summary>
        public static Dictionary<string, int> CharacterAIChoices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Track dropdown menu open states per character in the UMM UI.
        /// </summary>
        private static Dictionary<string, bool> DropdownOpenStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

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
            "AI: Druid (Wild Shape)",
            "AI: Druid (Shillelagh)",
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
        /// Renders the Unity Mod Manager Options UI panel for SolastaAI with dropdown selectors and dynamic mode-specific settings.
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

            GUILayout.Space(10);

            // --- SECTION 2: PARTY CHARACTER ARCHETYPE DROPDOWNS & DYNAMIC SUB-SETTINGS ---
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>👥 Party Character AI Archetype Selection & Skill Settings</b>");

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

                    string displayName = character.RulesetCharacter != null ? character.RulesetCharacter.Name : name;
                    string currentArchetypeName = (currentChoice >= 0 && currentChoice < AIPackageNames.Length) ? AIPackageNames[currentChoice] : AIPackageNames[0];

                    GUILayout.BeginVertical("box");

                    // 1. Character Dropdown Header
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{displayName}</b>", GUILayout.Width(180));

                    bool isOpen = DropdownOpenStates.TryGetValue(name, out bool open) && open;
                    if (GUILayout.Button($"<b>{currentArchetypeName}</b> ▼", GUILayout.Width(260)))
                    {
                        DropdownOpenStates[name] = !isOpen;
                    }
                    GUILayout.EndHorizontal();

                    // 2. Dropdown Options List (renders when expanded)
                    if (isOpen)
                    {
                        GUILayout.BeginVertical("box");
                        for (int i = 0; i < AIPackageNames.Length; i++)
                        {
                            bool isSelected = (i == currentChoice);
                            string prefix = isSelected ? "● " : "  ";
                            if (GUILayout.Button($"{prefix}{AIPackageNames[i]}", GUILayout.ExpandWidth(true)))
                            {
                                CharacterAIChoices[name] = i;
                                ApplyAIController(character, i);
                                SaveChoices();
                                DropdownOpenStates[name] = false;
                            }
                        }
                        GUILayout.EndVertical();
                    }

                    // 3. DYNAMIC GRANULAR SKILL SETTINGS (Displayed below dropdown based on selected mode!)
                    if (currentChoice == 5) // Druid (Wild Shape)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>⚙️ Individual Skill Controls for {displayName} ({currentArchetypeName}):</i>");

                        ModSettings.EnableDruidWildShape = GUILayout.Toggle(
                            ModSettings.EnableDruidWildShape,
                            "   └─ <b>Use Wild Shape / Tiergestalt</b> (Transform when threatened or HP < 75%)"
                        );

                        ModSettings.EnableDruidProtectionFromPoison = GUILayout.Toggle(
                            ModSettings.EnableDruidProtectionFromPoison,
                            "   └─ <b>Use Protection from Poison / Schutz vor Gift</b> (Casts poison protection on poisoned allies)"
                        );

                        ModSettings.EnableDruidBuffSpells = GUILayout.Toggle(
                            ModSettings.EnableDruidBuffSpells,
                            "   └─ <b>Use General Buff & Utility Spells</b> (Casts buff and utility spells during combat)"
                        );

                        ModSettings.EnableDruidHealing = GUILayout.Toggle(
                            ModSettings.EnableDruidHealing,
                            "   └─ <b>Use Ally Healing Spells</b> (Automatically heal wounded allies when HP < 50%)"
                        );

                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                            ModSettings.EnableAutoWeaponSwap,
                            "   └─ <b>Auto-Weapon Swap</b> (Equip ranged weapons when targets are out of reach)"
                        );

                        GUILayout.EndVertical();
                    }
                    else if (currentChoice == 6) // Druid (Shillelagh Melee)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>⚙️ Individual Skill Controls for {displayName} ({currentArchetypeName}):</i>");

                        ModSettings.EnableDruidShillelagh = GUILayout.Toggle(
                            ModSettings.EnableDruidShillelagh,
                            "   └─ <b>Use Shillelagh / Zauberstock</b> (Enchant melee weapon at turn start)"
                        );

                        ModSettings.EnableDruidGuidance = GUILayout.Toggle(
                            ModSettings.EnableDruidGuidance,
                            "   └─ <b>Use Guidance / Göttliche Führung</b> (Cast self-buff when out of melee reach)"
                        );

                        ModSettings.EnableDruidProtectionFromPoison = GUILayout.Toggle(
                            ModSettings.EnableDruidProtectionFromPoison,
                            "   └─ <b>Use Protection from Poison / Schutz vor Gift</b> (Casts poison protection on poisoned allies)"
                        );

                        ModSettings.EnableDruidBuffSpells = GUILayout.Toggle(
                            ModSettings.EnableDruidBuffSpells,
                            "   └─ <b>Use General Buff & Utility Spells</b> (Casts buff and utility spells during combat)"
                        );

                        ModSettings.EnableDruidHealing = GUILayout.Toggle(
                            ModSettings.EnableDruidHealing,
                            "   └─ <b>Use Ally Healing Spells</b> (Automatically heal wounded allies when HP < 50%)"
                        );

                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                            ModSettings.EnableAutoWeaponSwap,
                            "   └─ <b>Advance Towards Target + Ranged Cantrips</b> (Use ranged cantrip/bow while advancing into melee)"
                        );

                        GUILayout.EndVertical();
                    }
                    else if (currentChoice == 7 || currentChoice == 8) // Fighter (Melee) or Fighter (Ranged)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>⚙️ Individual Skill Controls for {displayName} ({currentArchetypeName}):</i>");

                        ModSettings.EnableFighterSecondWind = GUILayout.Toggle(
                            ModSettings.EnableFighterSecondWind,
                            "   └─ <b>Use Second Wind / Durchschnaufen</b> (Self-heal when HP < 60%)"
                        );

                        ModSettings.EnableFighterActionSurge = GUILayout.Toggle(
                            ModSettings.EnableFighterActionSurge,
                            "   └─ <b>Use Action Surge / Tatendrank</b> (Grant extra actions during combat)"
                        );

                        if (currentChoice == 8) // Fighter (Ranged)
                        {
                            ModSettings.EnableAvoidOpportunityAttacks = GUILayout.Toggle(
                                ModSettings.EnableAvoidOpportunityAttacks,
                                "   └─ <b>Avoid Opportunity Attacks</b> (Fight adjacent threats in melee first before retreating)"
                            );
                        }

                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                            ModSettings.EnableAutoWeaponSwap,
                            "   └─ <b>Auto-Weapon Swap</b> (Switch between Melee and Ranged weapon sets based on distance)"
                        );

                        GUILayout.EndVertical();
                    }
                    else if (currentChoice > 0)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>⚙️ Individual Skill Controls for {displayName} ({currentArchetypeName}):</i>");

                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(
                            ModSettings.EnableAutoWeaponSwap,
                            "   └─ <b>Auto-Weapon Swap</b> (Automatically equip ranged weapons when out of reach)"
                        );

                        GUILayout.EndVertical();
                    }

                    GUILayout.EndVertical();
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
                            case 5: package = decisionPackageDb.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break; // Druid (Wild Shape)
                            case 6: package = decisionPackageDb.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break; // Druid (Shillelagh Melee)
                            case 7: package = decisionPackageDb.GetElement("FighterCombatDecisions", true); break; // Fighter (Melee)
                            case 8: package = decisionPackageDb.GetElement("DefaultRangeWithBackupMeleeDecisions", true); break; // Fighter (Ranged / Archer)
                            case 9: package = decisionPackageDb.GetElement("CasterCombatDecisions", true); break;
                            case 10: package = decisionPackageDb.GetElement("RogueCombatDecisions", true); break;
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
        /// Executes Fighter class tactical skills (Second Wind / Durchschnaufen, Action Surge / Tatendrank) based on individual toggles.
        /// </summary>
        public static void ExecuteFighterTactics(GameLocationCharacter character, bool isRangedArchetype)
        {
            try
            {
                if (character == null || character.RulesetCharacter == null) return;
                
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.UsablePowers == null) return;

                // 1. Second Wind / Durchschnaufen (PowerFighterSecondWind) trigger when HP < 60% (if toggle enabled)
                if (ModSettings.EnableFighterSecondWind)
                {
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
                }

                // 2. Action Surge / Tatendrank (PowerFighterActionSurge) trigger in combat (if toggle enabled)
                if (ModSettings.EnableFighterActionSurge)
                {
                    var actionSurgePower = hero.UsablePowers.Find(p => p.PowerDefinition != null && 
                        (p.PowerDefinition.Name.IndexOf("ActionSurge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         p.PowerDefinition.Name.IndexOf("Tatendrank", StringComparison.OrdinalIgnoreCase) >= 0));

                    if (actionSurgePower != null && hero.GetRemainingUsesOfPower(actionSurgePower) > 0)
                    {
                        hero.UsePower(actionSurgePower);
                        ModEntry?.Logger.Log($"[SolastaAI] Fighter Skill: {character.Name} activated Action Surge / Tatendrank!");
                    }
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
        /// Executes Druid Wild Shape tactics (Wild Shape / Tiergestalt & Support Spells) based on individual toggles.
        /// </summary>
        public static void ExecuteDruidTactics(GameLocationCharacter character)
        {
            try
            {
                if (character == null || character.RulesetCharacter == null) return;
                
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.UsablePowers == null) return;

                // 1. Wild Shape / Tiergestalt trigger when HP < 75% (if toggle enabled)
                if (ModSettings.EnableDruidWildShape)
                {
                    int currentHp = hero.CurrentHitPoints;
                    int maxHp = currentHp + hero.MissingHitPoints;

                    var wildShapePower = hero.UsablePowers.Find(p => p.PowerDefinition != null && 
                        (p.PowerDefinition.Name.IndexOf("WildShape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         p.PowerDefinition.Name.IndexOf("Tiergestalt", StringComparison.OrdinalIgnoreCase) >= 0));

                    if (wildShapePower != null && hero.GetRemainingUsesOfPower(wildShapePower) > 0 && maxHp > 0 && ((float)currentHp / maxHp * 100f) < 75f)
                    {
                        hero.UsePower(wildShapePower);
                        ModEntry?.Logger.Log($"[SolastaAI] Druid Skill: {character.Name} activated Wild Shape / Tiergestalt! (HP: {currentHp}/{maxHp})");
                    }
                }

                // 2. Check Protection from Poison (if toggle enabled)
                if (ModSettings.EnableDruidProtectionFromPoison)
                {
                    CheckAndCastProtectionFromPoison(character);
                }

                // 3. Ally Healing Check (if toggle enabled)
                if (ModSettings.EnableDruidHealing)
                {
                    CheckAndHealAllies(character);
                }

                // 4. Auto-Weapon Swap for Druids
                CheckAndAutoSwapWeapons(character, isRangedArchetype: false);
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in ExecuteDruidTactics for {character?.Name}: {ex}");
            }
        }

        /// <summary>
        /// Executes Shillelagh Druid Melee tactics based on individual toggles.
        /// Programmatically instantiates and casts Shillelagh and advances towards melee target while using ranged cantrip/bow if out of reach.
        /// </summary>
        public static void ExecuteShillelaghDruidTactics(GameLocationCharacter character)
        {
            try
            {
                if (character == null || character.RulesetCharacter == null) return;
                
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null) return;

                // 1. Cast Shillelagh on melee weapon if enabled & available
                if (ModSettings.EnableDruidShillelagh && hero.SpellRepertoires != null)
                {
                    foreach (var repertoire in hero.SpellRepertoires)
                    {
                        if (repertoire == null) continue;
                        var shillelaghSpell = repertoire.KnownCantrips.Find(s => s != null && 
                            (s.Name.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             s.Name.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0));

                        if (shillelaghSpell != null && repertoire.CanCastSpell(shillelaghSpell, true))
                        {
                            var implService = ServiceRepository.GetService<IRulesetImplementationService>();
                            if (implService != null)
                            {
                                var effectSpell = implService.InstantiateEffectSpell(hero, repertoire, shillelaghSpell, 0, false);
                                if (effectSpell != null)
                                {
                                    hero.CastSpell(effectSpell, true, false);
                                    ModEntry?.Logger.Log($"[SolastaAI] Shillelagh Druid: {character.Name} successfully cast Shillelagh / Zauberstock!");
                                }
                            }
                            break;
                        }
                    }
                }

                // 2. Check Protection from Poison (if toggle enabled)
                if (ModSettings.EnableDruidProtectionFromPoison)
                {
                    CheckAndCastProtectionFromPoison(character);
                }

                // 3. Ally Healing Check (if toggle enabled)
                if (ModSettings.EnableDruidHealing)
                {
                    CheckAndHealAllies(character);
                }

                // 4. Enemy Distance & Guidance / Ranged Cantrip Advance Check
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService != null && battleService.IsBattleInProgress && battleService.Battle != null)
                {
                    int minDistance = int.MaxValue;
                    var enemies = (character.Side == RuleDefinitions.Side.Ally) ? battleService.Battle.EnemyContenders : battleService.Battle.PlayerContenders;
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

                    // If NO enemy is in melee reach (> 2 cells):
                    if (minDistance > 2)
                    {
                        bool castGuidance = false;
                        if (ModSettings.EnableDruidGuidance && hero.SpellRepertoires != null)
                        {
                            foreach (var repertoire in hero.SpellRepertoires)
                            {
                                if (repertoire == null) continue;
                                var guidanceSpell = repertoire.KnownCantrips.Find(s => s != null && 
                                    (s.Name.IndexOf("Guidance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     s.Name.IndexOf("GöttlicheFührung", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     s.Name.IndexOf("GoettlicheFuehrung", StringComparison.OrdinalIgnoreCase) >= 0));

                                if (guidanceSpell != null && repertoire.CanCastSpell(guidanceSpell, true))
                                {
                                    var implService = ServiceRepository.GetService<IRulesetImplementationService>();
                                    if (implService != null)
                                    {
                                        var effectSpell = implService.InstantiateEffectSpell(hero, repertoire, guidanceSpell, 0, false);
                                        if (effectSpell != null)
                                        {
                                            hero.CastSpell(effectSpell, true, false);
                                            ModEntry?.Logger.Log($"[SolastaAI] Shillelagh Druid: {character.Name} advancing towards melee target ({minDistance} cells) & cast Guidance / Göttliche Führung!");
                                            castGuidance = true;
                                        }
                                    }
                                    break;
                                }
                            }
                        }

                        // If Guidance not available, use Ranged Cantrip / Bow WHILE advancing towards melee target!
                        if (!castGuidance)
                        {
                            CheckAndAutoSwapWeapons(character, isRangedArchetype: true);
                        }
                    }
                    else
                    {
                        // Enemy IS in melee reach: equip Melee weapon set with Shillelagh!
                        CheckAndAutoSwapWeapons(character, isRangedArchetype: false);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in ExecuteShillelaghDruidTactics for {character?.Name}: {ex}");
            }
        }

        /// <summary>
        /// Checks party members for poison condition and casts Protection from Poison / Schutz vor Gift if enabled & available.
        /// </summary>
        public static void CheckAndCastProtectionFromPoison(GameLocationCharacter character)
        {
            try
            {
                if (!ModSettings.EnableDruidProtectionFromPoison || character == null || character.RulesetCharacter == null) return;

                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.SpellRepertoires == null) return;

                var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                if (charService == null || charService.PartyCharacters == null) return;

                foreach (var ally in charService.PartyCharacters)
                {
                    if (ally == null || ally.RulesetCharacter == null || ally.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth)
                        continue;

                    bool isPoisoned = ally.RulesetCharacter.HasConditionOfType("ConditionPoisoned");
                    if (isPoisoned)
                    {
                        foreach (var repertoire in hero.SpellRepertoires)
                        {
                            if (repertoire == null) continue;
                            var poisonSpell = repertoire.PreparedSpells.Find(s => s != null && 
                                (s.Name.IndexOf("ProtectionFromPoison", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("SchutzVorGift", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("Poison", StringComparison.OrdinalIgnoreCase) >= 0));

                            if (poisonSpell != null && repertoire.CanCastSpell(poisonSpell, true))
                            {
                                var implService = ServiceRepository.GetService<IRulesetImplementationService>();
                                if (implService != null)
                                {
                                    var effectSpell = implService.InstantiateEffectSpell(hero, repertoire, poisonSpell, 2, false);
                                    if (effectSpell != null)
                                    {
                                        hero.CastSpell(effectSpell, false, false);
                                        ModEntry?.Logger.Log($"[SolastaAI] Druid Protection: {character.Name} cast Protection from Poison on {ally.Name}!");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in CheckAndCastProtectionFromPoison: {ex}");
            }
        }

        /// <summary>
        /// Helper to check party member health and cast healing spells if any ally has HP < 50%.
        /// </summary>
        public static void CheckAndHealAllies(GameLocationCharacter character)
        {
            try
            {
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero == null || hero.SpellRepertoires == null) return;

                var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                if (charService == null || charService.PartyCharacters == null) return;

                foreach (var ally in charService.PartyCharacters)
                {
                    if (ally == null || ally.RulesetCharacter == null || ally.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth)
                        continue;

                    int allyHp = ally.RulesetCharacter.CurrentHitPoints;
                    int allyMaxHp = allyHp + ally.RulesetCharacter.MissingHitPoints;
                    if (allyMaxHp > 0 && ((float)allyHp / allyMaxHp * 100f) < 50f)
                    {
                        foreach (var repertoire in hero.SpellRepertoires)
                        {
                            if (repertoire == null) continue;
                            var healSpell = repertoire.PreparedSpells.Find(s => s != null && 
                                (s.Name.IndexOf("CureWounds", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("HealingWord", StringComparison.OrdinalIgnoreCase) >= 0));

                            if (healSpell != null && repertoire.CanCastSpell(healSpell, true))
                            {
                                var implService = ServiceRepository.GetService<IRulesetImplementationService>();
                                if (implService != null)
                                {
                                    var effectSpell = implService.InstantiateEffectSpell(hero, repertoire, healSpell, 1, false);
                                    if (effectSpell != null)
                                    {
                                        hero.CastSpell(effectSpell, false, false);
                                        ModEntry?.Logger.Log($"[SolastaAI] Druid Healing Support: {character.Name} healing ally {ally.Name} (HP: {allyHp}/{allyMaxHp})!");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[SolastaAI] Error in CheckAndHealAllies: {ex}");
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

                    // If AI control is active, check and execute Class Tactics or auto-weapon swap
                    if (choice == 5)
                    {
                        Main.ExecuteDruidTactics(__instance);
                    }
                    else if (choice == 6)
                    {
                        Main.ExecuteShillelaghDruidTactics(__instance);
                    }
                    else if (choice == 7)
                    {
                        Main.ExecuteFighterTactics(__instance, isRangedArchetype: false);
                    }
                    else if (choice == 8)
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
