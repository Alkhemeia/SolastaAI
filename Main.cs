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
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableEmergencyLowHpFallback = true;
        public float EmergencyHpThresholdPercent = 30f;
        public bool EnableHotkeyToggle = true;
        public KeyCode ToggleHotkey = KeyCode.N;
        public bool EnableAutoWeaponSwap = true;

        // Fighter Toggles
        public bool EnableFighterSecondWind = true;
        public bool EnableFighterActionSurge = true;
        public bool EnableFighterIndomitable = true;
        public bool EnableFighterPushingAttack = true;
        public bool EnableFighterTripAttack = true;
        public bool EnableFighterRiposte = true;
        public bool EnableFighterPrecisionAttack = true;
        public bool EnableAvoidOpportunityAttacks = true;

        // Druid Toggles
        public bool EnableDruidWildShape = true;
        public bool EnableSpellShillelagh = true;
        public bool EnableSpellGuidance = true;
        public bool EnableSpellProduceFlame = true;
        public bool EnableSpellThornWhip = true;
        public bool EnableSpellPoisonSpray = true;
        public bool EnableSpellChillTouch = true;
        public bool EnableSpellCureWounds = true;
        public bool EnableSpellHealingWord = true;
        public bool EnableSpellEntangle = true;
        public bool EnableSpellFaerieFire = true;
        public bool EnableSpellFogCloud = true;
        public bool EnableSpellGoodberry = true;
        public bool EnableSpellJump = true;
        public bool EnableSpellLongstrider = true;
        public bool EnableSpellProtectionFromPoison = true;
        public bool EnableSpellBarkskin = true;
        public bool EnableSpellFlamingSphere = true;
        public bool EnableSpellHoldPerson = true;
        public bool EnableSpellLesserRestoration = true;
        public bool EnableSpellMoonbeam = true;
        public bool EnableSpellSpikeGrowth = true;
        public bool EnableSpellPassWithoutTrace = true;
        public bool EnableSpellCallLightning = true;
        public bool EnableSpellDispelMagic = true;
        public bool EnableSpellSleetStorm = true;
        public bool EnableSpellWindWall = true;
        public bool EnableSpellDaylight = true;

        public bool AutoControlGuests = false;

        public override void Save(UnityModManager.ModEntry modEntry) { Save(this, modEntry); }
    }

    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings ModSettings { get; private set; }
        public static string SaveFilePath { get; private set; }

        // Tracks the name of the character whose turn is currently being evaluated by the AI engine.
        // Updated in StartBattleTurn so mode-specific blocking is context-aware.
        public static string CurrentTurnCharacterName = "";

        public static Dictionary<string, int> CharacterAIChoices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, bool> DropdownOpenStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Mode constants
        public const int MODE_HUMAN         = 0;
        public const int MODE_MELEE         = 1;
        public const int MODE_RANGE_BACKUP  = 2;
        public const int MODE_CASTER        = 3;
        public const int MODE_CLERIC        = 4;
        public const int MODE_DRUID_WILD    = 5;
        public const int MODE_DRUID_SHILLELAGH = 6;
        public const int MODE_FIGHTER_MELEE = 7;
        public const int MODE_FIGHTER_RANGED = 8;
        public const int MODE_MAGE          = 9;
        public const int MODE_ROGUE         = 10;

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
                modEntry.Logger.Log("[SolastaAI] Mod loaded successfully!");
                return true;
            }
            catch (Exception ex)
            {
                modEntry?.Logger.Error($"[SolastaAI] Critical Error during Load: {ex}");
                return true;
            }
        }

        /// <summary>
        /// Gets the current AI mode for the character currently taking their turn.
        /// Used by patches to make mode-specific decisions.
        /// </summary>
        public static int GetCurrentTurnCharacterMode()
        {
            if (string.IsNullOrEmpty(CurrentTurnCharacterName)) return MODE_HUMAN;
            if (CharacterAIChoices.TryGetValue(CurrentTurnCharacterName, out int mode)) return mode;
            return MODE_HUMAN;
        }

        /// <summary>
        /// Returns true if a spell should be allowed for the AI engine to use,
        /// considering both the user toggle AND the current character's mode.
        /// Shillelagh Druid: ranged cantrips are blocked from AI selection (forces melee advance).
        /// </summary>
        public static bool IsSpellEnabledForAI(string spellName)
        {
            if (string.IsNullOrEmpty(spellName)) return true;

            // Check user-controlled spell toggles FIRST - disabled spells must ALWAYS be blocked
            if (spellName.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellShillelagh) return false;
            if (spellName.IndexOf("Guidance", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("GöttlicheFührung", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellGuidance) return false;
            if (spellName.IndexOf("ProduceFlame", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellProduceFlame) return false;
            if (spellName.IndexOf("ThornWhip", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellThornWhip) return false;
            if (spellName.IndexOf("PoisonSpray", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellPoisonSpray) return false;
            if (spellName.IndexOf("ChillTouch", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("KalteHand", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellChillTouch) return false;
            if (spellName.IndexOf("CureWounds", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellCureWounds) return false;
            if (spellName.IndexOf("HealingWord", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellHealingWord) return false;
            if (spellName.IndexOf("Entangle", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellEntangle) return false;
            if (spellName.IndexOf("FaerieFire", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellFaerieFire) return false;
            if (spellName.IndexOf("FogCloud", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellFogCloud) return false;
            if (spellName.IndexOf("Goodberry", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellGoodberry) return false;
            if (spellName.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellJump) return false;
            if (spellName.IndexOf("Longstrider", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellLongstrider) return false;
            if (spellName.IndexOf("ProtectionFromPoison", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("SchutzVorGift", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellProtectionFromPoison) return false;
            if (spellName.IndexOf("Barkskin", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellBarkskin) return false;
            if (spellName.IndexOf("FlamingSphere", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellFlamingSphere) return false;
            if (spellName.IndexOf("HoldPerson", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellHoldPerson) return false;
            if (spellName.IndexOf("LesserRestoration", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellLesserRestoration) return false;
            if (spellName.IndexOf("Moonbeam", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellMoonbeam) return false;
            if (spellName.IndexOf("SpikeGrowth", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellSpikeGrowth) return false;
            if (spellName.IndexOf("PassWithoutTrace", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellPassWithoutTrace) return false;
            if (spellName.IndexOf("CallLightning", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellCallLightning) return false;
            if (spellName.IndexOf("DispelMagic", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellDispelMagic) return false;
            if (spellName.IndexOf("SleetStorm", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellSleetStorm) return false;
            if (spellName.IndexOf("WindWall", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellWindWall) return false;
            if (spellName.IndexOf("Daylight", StringComparison.OrdinalIgnoreCase) >= 0)
                if (!ModSettings.EnableSpellDaylight) return false;

            int mode = GetCurrentTurnCharacterMode();

            // For Shillelagh Druid: block ranged cantrips & non-essential spells from AI engine
            // so it is FORCED to advance to melee with its Shillelagh weapon.
            if (mode == MODE_DRUID_SHILLELAGH)
            {
                if (spellName.IndexOf("ProduceFlame", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("ThornWhip", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("PoisonSpray", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("ChillTouch", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("KalteHand", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("Entangle", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (spellName.IndexOf("FaerieFire", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }

            // User-controlled spell toggles
            if (spellName.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellShillelagh;
            if (spellName.IndexOf("Guidance", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("GöttlicheFührung", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellGuidance;
            if (spellName.IndexOf("ProduceFlame", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellProduceFlame;
            if (spellName.IndexOf("ThornWhip", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellThornWhip;
            if (spellName.IndexOf("PoisonSpray", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellPoisonSpray;
            if (spellName.IndexOf("ChillTouch", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("KalteHand", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellChillTouch;
            if (spellName.IndexOf("CureWounds", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellCureWounds;
            if (spellName.IndexOf("HealingWord", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellHealingWord;
            if (spellName.IndexOf("Entangle", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellEntangle;
            if (spellName.IndexOf("FaerieFire", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellFaerieFire;
            if (spellName.IndexOf("FogCloud", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellFogCloud;
            if (spellName.IndexOf("Goodberry", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellGoodberry;
            if (spellName.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellJump;
            if (spellName.IndexOf("Longstrider", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellLongstrider;
            if (spellName.IndexOf("ProtectionFromPoison", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("SchutzVorGift", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellProtectionFromPoison;
            if (spellName.IndexOf("Barkskin", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellBarkskin;
            if (spellName.IndexOf("FlamingSphere", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellFlamingSphere;
            if (spellName.IndexOf("HoldPerson", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellHoldPerson;
            if (spellName.IndexOf("LesserRestoration", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellLesserRestoration;
            if (spellName.IndexOf("Moonbeam", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellMoonbeam;
            if (spellName.IndexOf("SpikeGrowth", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellSpikeGrowth;
            if (spellName.IndexOf("PassWithoutTrace", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellPassWithoutTrace;
            if (spellName.IndexOf("CallLightning", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellCallLightning;
            if (spellName.IndexOf("DispelMagic", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellDispelMagic;
            if (spellName.IndexOf("SleetStorm", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellSleetStorm;
            if (spellName.IndexOf("WindWall", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellWindWall;
            if (spellName.IndexOf("Daylight", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellDaylight;

            return true;
        }

        /// <summary>
        /// Returns true if a power should be allowed for the AI engine to use.
        /// </summary>
        public static bool IsPowerEnabledForAI(string powerName)
        {
            if (string.IsNullOrEmpty(powerName)) return true;

            if (powerName.IndexOf("WildShape", StringComparison.OrdinalIgnoreCase) >= 0 || powerName.IndexOf("Tiergestalt", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableDruidWildShape;
            if (powerName.IndexOf("SecondWind", StringComparison.OrdinalIgnoreCase) >= 0 || powerName.IndexOf("Durchschnaufen", StringComparison.OrdinalIgnoreCase) >= 0 || powerName.IndexOf("CatchBreath", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterSecondWind;
            if (powerName.IndexOf("ActionSurge", StringComparison.OrdinalIgnoreCase) >= 0 || powerName.IndexOf("Tatendrank", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterActionSurge;
            if (powerName.IndexOf("Indomitable", StringComparison.OrdinalIgnoreCase) >= 0 || powerName.IndexOf("Unbeugsam", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterIndomitable;
            if (powerName.IndexOf("PushingAttack", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterPushingAttack;
            if (powerName.IndexOf("TripAttack", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterTripAttack;
            if (powerName.IndexOf("Riposte", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterRiposte;
            if (powerName.IndexOf("PrecisionAttack", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableFighterPrecisionAttack;

            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b><size=14>SolastaAI - Advanced AI & Tactical Settings</size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>🛡️ Global Safety & Hotkey Settings</b>");
            ModSettings.EnableEmergencyLowHpFallback = GUILayout.Toggle(ModSettings.EnableEmergencyLowHpFallback,
                " <b>Enable Emergency Low HP Protection</b> (Reverts hero to Human control when HP drops below threshold)");
            if (ModSettings.EnableEmergencyLowHpFallback)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"Emergency HP Threshold: <b>{Mathf.RoundToInt(ModSettings.EmergencyHpThresholdPercent)}% Max HP</b>", GUILayout.Width(280));
                ModSettings.EmergencyHpThresholdPercent = GUILayout.HorizontalSlider(ModSettings.EmergencyHpThresholdPercent, 5f, 50f, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }
            ModSettings.EnableHotkeyToggle = GUILayout.Toggle(ModSettings.EnableHotkeyToggle,
                " <b>Enable In-Combat Quick Hotkey ('N')</b> (Toggles active hero AI/Human control on the fly)");
            ModSettings.AutoControlGuests = GUILayout.Toggle(ModSettings.AutoControlGuests,
                " <b>Enable Auto AI for Guest & Companion Characters</b>");
            GUILayout.EndVertical();

            GUILayout.Space(10);

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

                    if (!CharacterAIChoices.TryGetValue(name, out int currentChoice)) currentChoice = 0;

                    string displayName = character.RulesetCharacter != null ? character.RulesetCharacter.Name : name;
                    string currentArchetypeName = (currentChoice >= 0 && currentChoice < AIPackageNames.Length) ? AIPackageNames[currentChoice] : AIPackageNames[0];

                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{displayName}</b>", GUILayout.Width(180));
                    bool isOpen = DropdownOpenStates.TryGetValue(name, out bool open) && open;
                    if (GUILayout.Button($"<b>{currentArchetypeName}</b> ▼", GUILayout.Width(260)))
                        DropdownOpenStates[name] = !isOpen;
                    GUILayout.EndHorizontal();

                    if (isOpen)
                    {
                        GUILayout.BeginVertical("box");
                        for (int i = 0; i < AIPackageNames.Length; i++)
                        {
                            bool isSelected = (i == currentChoice);
                            if (GUILayout.Button($"{(isSelected ? "● " : "  ")}{AIPackageNames[i]}", GUILayout.ExpandWidth(true)))
                            {
                                CharacterAIChoices[name] = i;
                                ApplyAIController(character, i);
                                SaveChoices();
                                DropdownOpenStates[name] = false;
                            }
                        }
                        GUILayout.EndVertical();
                    }

                    if (currentChoice == MODE_DRUID_WILD || currentChoice == MODE_DRUID_SHILLELAGH)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>✨ Druid Spell Controls for {displayName}:</i>");

                        if (currentChoice == MODE_DRUID_WILD)
                            ModSettings.EnableDruidWildShape = GUILayout.Toggle(ModSettings.EnableDruidWildShape, "   └─ <b>Wild Shape / Tiergestalt</b>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>💚 Healing & Restoration:</b>");
                        ModSettings.EnableSpellCureWounds = GUILayout.Toggle(ModSettings.EnableSpellCureWounds, "     └─ <b>Cure Wounds</b>");
                        ModSettings.EnableSpellHealingWord = GUILayout.Toggle(ModSettings.EnableSpellHealingWord, "     └─ <b>Healing Word</b>");
                        ModSettings.EnableSpellLesserRestoration = GUILayout.Toggle(ModSettings.EnableSpellLesserRestoration, "     └─ <b>Lesser Restoration</b>");
                        ModSettings.EnableSpellGoodberry = GUILayout.Toggle(ModSettings.EnableSpellGoodberry, "     └─ <b>Goodberry</b>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>🛡️ Protection & Buffs:</b>");
                        if (currentChoice == MODE_DRUID_SHILLELAGH)
                        {
                            ModSettings.EnableSpellShillelagh = GUILayout.Toggle(ModSettings.EnableSpellShillelagh, "     └─ <b>Shillelagh / Zauberstock</b>");
                            ModSettings.EnableSpellGuidance = GUILayout.Toggle(ModSettings.EnableSpellGuidance, "     └─ <b>Guidance / Göttliche Führung</b>");
                        }
                        ModSettings.EnableSpellProtectionFromPoison = GUILayout.Toggle(ModSettings.EnableSpellProtectionFromPoison, "     └─ <b>Protection from Poison</b>");
                        ModSettings.EnableSpellBarkskin = GUILayout.Toggle(ModSettings.EnableSpellBarkskin, "     └─ <b>Barkskin</b>");
                        ModSettings.EnableSpellLongstrider = GUILayout.Toggle(ModSettings.EnableSpellLongstrider, "     └─ <b>Longstrider</b>");
                        ModSettings.EnableSpellPassWithoutTrace = GUILayout.Toggle(ModSettings.EnableSpellPassWithoutTrace, "     └─ <b>Pass Without Trace</b>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>⚔️ Attack & Crowd Control:</b>");
                        ModSettings.EnableSpellProduceFlame = GUILayout.Toggle(ModSettings.EnableSpellProduceFlame, "     └─ <b>Produce Flame</b>");
                        ModSettings.EnableSpellThornWhip = GUILayout.Toggle(ModSettings.EnableSpellThornWhip, "     └─ <b>Thorn Whip</b>");
                        ModSettings.EnableSpellPoisonSpray = GUILayout.Toggle(ModSettings.EnableSpellPoisonSpray, "     └─ <b>Poison Spray</b>");
                        ModSettings.EnableSpellChillTouch = GUILayout.Toggle(ModSettings.EnableSpellChillTouch, "     └─ <b>Chill Touch / Kalte Hand</b>");
                        ModSettings.EnableSpellEntangle = GUILayout.Toggle(ModSettings.EnableSpellEntangle, "     └─ <b>Entangle</b>");
                        ModSettings.EnableSpellFaerieFire = GUILayout.Toggle(ModSettings.EnableSpellFaerieFire, "     └─ <b>Faerie Fire</b>");
                        ModSettings.EnableSpellFlamingSphere = GUILayout.Toggle(ModSettings.EnableSpellFlamingSphere, "     └─ <b>Flaming Sphere</b>");
                        ModSettings.EnableSpellHoldPerson = GUILayout.Toggle(ModSettings.EnableSpellHoldPerson, "     └─ <b>Hold Person</b>");
                        ModSettings.EnableSpellMoonbeam = GUILayout.Toggle(ModSettings.EnableSpellMoonbeam, "     └─ <b>Moonbeam</b>");
                        ModSettings.EnableSpellSpikeGrowth = GUILayout.Toggle(ModSettings.EnableSpellSpikeGrowth, "     └─ <b>Spike Growth</b>");
                        ModSettings.EnableSpellCallLightning = GUILayout.Toggle(ModSettings.EnableSpellCallLightning, "     └─ <b>Call Lightning</b>");

                        GUILayout.Space(3);
                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(ModSettings.EnableAutoWeaponSwap, "   └─ <b>Auto-Weapon Swap / Cantrip Positioning</b>");
                        GUILayout.EndVertical();
                    }
                    else if (currentChoice == MODE_FIGHTER_MELEE || currentChoice == MODE_FIGHTER_RANGED)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>✨ Fighter Skill Controls for {displayName}:</i>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>🛡️ Defense & Recovery:</b>");
                        ModSettings.EnableFighterSecondWind = GUILayout.Toggle(ModSettings.EnableFighterSecondWind, "     └─ <b>Second Wind / Durchschnaufen</b>");
                        ModSettings.EnableFighterIndomitable = GUILayout.Toggle(ModSettings.EnableFighterIndomitable, "     └─ <b>Indomitable / Unbeugsam</b>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>⚔️ Offensive Skills & Maneuvers:</b>");
                        ModSettings.EnableFighterActionSurge = GUILayout.Toggle(ModSettings.EnableFighterActionSurge, "     └─ <b>Action Surge / Tatendrank</b>");
                        ModSettings.EnableFighterPushingAttack = GUILayout.Toggle(ModSettings.EnableFighterPushingAttack, "     └─ <b>Pushing Attack / Stoßangriff</b>");
                        ModSettings.EnableFighterTripAttack = GUILayout.Toggle(ModSettings.EnableFighterTripAttack, "     └─ <b>Trip Attack / Beinstellen</b>");
                        ModSettings.EnableFighterRiposte = GUILayout.Toggle(ModSettings.EnableFighterRiposte, "     └─ <b>Riposte</b>");
                        ModSettings.EnableFighterPrecisionAttack = GUILayout.Toggle(ModSettings.EnableFighterPrecisionAttack, "     └─ <b>Precision Attack / Präzisionsangriff</b>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>🎯 Movement & Positioning:</b>");
                        if (currentChoice == MODE_FIGHTER_RANGED)
                            ModSettings.EnableAvoidOpportunityAttacks = GUILayout.Toggle(ModSettings.EnableAvoidOpportunityAttacks, "     └─ <b>Avoid Opportunity Attacks</b>");
                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(ModSettings.EnableAutoWeaponSwap, "     └─ <b>Auto-Weapon Swap</b>");
                        GUILayout.EndVertical();
                    }
                    else if (currentChoice > 0)
                    {
                        GUILayout.BeginVertical("box");
                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(ModSettings.EnableAutoWeaponSwap, "   └─ <b>Auto-Weapon Swap</b>");
                        GUILayout.EndVertical();
                    }

                    GUILayout.EndVertical();
                }
            }
            else
            {
                GUILayout.Label("<i>(No active party loaded. Start or load a campaign to configure heroes.)</i>");
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            ModSettings.Save(modEntry);
            SaveChoices();
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            EnsureExplorationControl();
            if (!ModSettings.EnableHotkeyToggle) return;
            if (Input.GetKeyDown(ModSettings.ToggleHotkey)) ToggleActiveCharacterAI();
        }

        public static int GetPlayerControllerId()
        {
            try { var c = Gui.ActivePlayerController; if (c != null) return c.ControllerId; } catch {}
            return PlayerControllerManager.MainPlayerControllerId;
        }

        public static void EnsureExplorationControl()
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService == null || !battleService.IsBattleInProgress)
                {
                    var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                    if (charService?.PartyCharacters == null) return;
                    int humanId = GetPlayerControllerId();
                    bool dirtied = false;
                    foreach (var character in charService.PartyCharacters)
                    {
                        if (character != null && character.ControllerId == PlayerControllerManager.DmControllerId)
                        {
                            character.ControllerId = humanId;
                            dirtied = true;
                        }
                    }
                    if (dirtied) Gui.ActivePlayerController?.DirtyControlledCharacters();
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] EnsureExplorationControl: {ex}"); }
        }

        public static void ToggleActiveCharacterAI()
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService?.IsBattleInProgress != true) return;
                var contender = battleService.Battle?.ActiveContender;
                if (contender == null) return;
                string name = contender.Name;
                if (string.IsNullOrEmpty(name)) return;
                if (!CharacterAIChoices.TryGetValue(name, out int currentChoice)) currentChoice = 0;
                int newChoice = (currentChoice == 0) ? 1 : 0;
                CharacterAIChoices[name] = newChoice;
                ApplyAIController(contender, newChoice);
                SaveChoices();
                ModEntry.Logger.Log($"[SolastaAI] Hotkey: {name} -> {AIPackageNames[newChoice]}");
            }
            catch (Exception ex) { ModEntry.Logger.Error($"[SolastaAI] ToggleActiveCharacterAI: {ex}"); }
        }

        public static void ApplyAIController(GameLocationCharacter character, int choice)
        {
            if (character == null) return;
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                bool isInBattle = battleService?.IsBattleInProgress == true;
                int humanId = GetPlayerControllerId();

                if (!isInBattle || choice <= 0)
                {
                    character.ControllerId = humanId;
                }
                else
                {
                    character.ControllerId = PlayerControllerManager.DmControllerId;

                    var db = DatabaseRepository.GetDatabase<TA.AI.DecisionPackageDefinition>();
                    if (db != null)
                    {
                        TA.AI.DecisionPackageDefinition pkg = null;
                        switch (choice)
                        {
                            case MODE_MELEE:          pkg = db.GetElement("DefaultMeleeWithBackupRangeDecisions", true); break;
                            case MODE_RANGE_BACKUP:   pkg = db.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break;
                            case MODE_CASTER:         pkg = db.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break;
                            case MODE_CLERIC:         pkg = db.GetElement("ClericCombatDecisions", true); break;
                            case MODE_DRUID_WILD:     pkg = db.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break;
                            // Shillelagh Druid: Melee package so AI pathfinding advances to melee range.
                            // Ranged cantrips are blocked by IsSpellEnabledForAI so AI is FORCED to advance.
                            case MODE_DRUID_SHILLELAGH: pkg = db.GetElement("DefaultMeleeWithBackupRangeDecisions", true); break;
                            case MODE_FIGHTER_MELEE:  pkg = db.GetElement("FighterCombatDecisions", true); break;
                            // Ranged Fighter: CasterCombatDecisions keeps maximum distance from enemies.
                            case MODE_FIGHTER_RANGED: pkg = db.GetElement("CasterCombatDecisions", true); break;
                            case MODE_MAGE:           pkg = db.GetElement("CasterCombatDecisions", true); break;
                            case MODE_ROGUE:          pkg = db.GetElement("RogueCombatDecisions", true); break;
                            default:                  pkg = db.GetElement("DefaultMeleeWithBackupRangeDecisions", true); break;
                        }

                        if (pkg != null)
                        {
                            if (character.BehaviourPackage == null)
                            {
                                var newPkg = new GameLocationBehaviourPackage();
                                newPkg.BattleStartBehavior = GameLocationBehaviourPackage.BattleStartBehaviorType.RaisesAlarm;
                                character.BehaviourPackage = newPkg;
                            }
                            character.BehaviourPackage.DecisionPackageDefinition = pkg;
                        }
                    }
                }

                Gui.ActivePlayerController?.DirtyControlledCharacters();
            }
            catch (Exception ex) { ModEntry.Logger.Error($"[SolastaAI] ApplyAIController {character?.Name}: {ex}"); }
        }

        public static void ExecuteFighterTactics(GameLocationCharacter character, bool isRangedArchetype)
        {
            try
            {
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.UsablePowers == null) return;

                if (ModSettings.EnableFighterSecondWind)
                {
                    int currentHp = hero.CurrentHitPoints;
                    int maxHp = currentHp + hero.MissingHitPoints;
                    if (maxHp > 0 && ((float)currentHp / maxHp * 100f) < 60f)
                    {
                        var power = hero.UsablePowers.Find(p => p.PowerDefinition != null &&
                            (p.PowerDefinition.Name.IndexOf("SecondWind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             p.PowerDefinition.Name.IndexOf("CatchBreath", StringComparison.OrdinalIgnoreCase) >= 0));
                        if (power != null && hero.GetRemainingUsesOfPower(power) > 0)
                        {
                            hero.UsePower(power);
                            ModEntry?.Logger.Log($"[SolastaAI] {character.Name} used Second Wind!");
                        }
                    }
                }

                if (ModSettings.EnableFighterActionSurge)
                {
                    var power = hero.UsablePowers.Find(p => p.PowerDefinition != null &&
                        p.PowerDefinition.Name.IndexOf("ActionSurge", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (power != null && hero.GetRemainingUsesOfPower(power) > 0)
                    {
                        hero.UsePower(power);
                        ModEntry?.Logger.Log($"[SolastaAI] {character.Name} used Action Surge!");
                    }
                }

                CheckAndAutoSwapWeapons(character, isRangedArchetype);
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] ExecuteFighterTactics: {ex}"); }
        }

        public static void ExecuteDruidTactics(GameLocationCharacter character)
        {
            try
            {
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.UsablePowers == null) return;

                if (ModSettings.EnableDruidWildShape)
                {
                    int currentHp = hero.CurrentHitPoints;
                    int maxHp = currentHp + hero.MissingHitPoints;
                    var power = hero.UsablePowers.Find(p => p.PowerDefinition != null &&
                        (p.PowerDefinition.Name.IndexOf("WildShape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         p.PowerDefinition.Name.IndexOf("Tiergestalt", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (power != null && hero.GetRemainingUsesOfPower(power) > 0 && maxHp > 0 && ((float)currentHp / maxHp * 100f) < 75f)
                    {
                        hero.UsePower(power);
                        ModEntry?.Logger.Log($"[SolastaAI] {character.Name} used Wild Shape!");
                    }
                }

                CheckAndCastProtectionFromPoison(character);
                CheckAndHealAllies(character);
                CheckAndAutoSwapWeapons(character, false);
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] ExecuteDruidTactics: {ex}"); }
        }

        public static void ExecuteShillelaghDruidTactics(GameLocationCharacter character)
        {
            try
            {
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero == null) return;

                // NOTE: Shillelagh bonus action is cast in the StartBattleTurn POSTFIX
                // (after turn/action-economy is fully initialized) via TryCastShillelaghInPostfix().

                CheckAndCastProtectionFromPoison(character);
                CheckAndHealAllies(character);

                // Shillelagh Druid ALWAYS stays on melee weapon so he advances into melee
                CheckAndAutoSwapWeapons(character, false);
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] ExecuteShillelaghDruidTactics: {ex}"); }
        }

        public static int GetMinDistanceToEnemy(GameLocationCharacter character)
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService?.IsBattleInProgress != true || battleService.Battle == null) return int.MaxValue;
                var enemies = (character.Side == RuleDefinitions.Side.Ally)
                    ? battleService.Battle.EnemyContenders
                    : battleService.Battle.PlayerContenders;
                if (enemies == null || enemies.Count == 0) return int.MaxValue;
                int min = int.MaxValue;
                var posA = character.LocationPosition;
                foreach (var enemy in enemies)
                {
                    if (enemy?.RulesetCharacter == null || enemy.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth) continue;
                    var posB = enemy.LocationPosition;
                    int dist = Math.Max(Math.Abs(posA.x - posB.x), Math.Max(Math.Abs(posA.y - posB.y), Math.Abs(posA.z - posB.z)));
                    if (dist < min) min = dist;
                }
                return min;
            }
            catch { return int.MaxValue; }
        }

        public static void CheckAndCastProtectionFromPoison(GameLocationCharacter character)
        {
            try
            {
                if (!ModSettings.EnableSpellProtectionFromPoison) return;
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.SpellRepertoires == null) return;
                var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                if (charService?.PartyCharacters == null) return;
                foreach (var ally in charService.PartyCharacters)
                {
                    if (ally?.RulesetCharacter == null || ally.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth) continue;
                    if (!ally.RulesetCharacter.HasConditionOfType("ConditionPoisoned")) continue;
                    foreach (var rep in hero.SpellRepertoires)
                    {
                        if (rep == null) continue;
                        var spell = rep.PreparedSpells.Find(s => s != null && s.Name.IndexOf("ProtectionFromPoison", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (spell == null || !rep.CanCastSpell(spell, true)) continue;
                        var impl = ServiceRepository.GetService<IRulesetImplementationService>();
                        if (impl == null) continue;
                        var effect = impl.InstantiateEffectSpell(hero, rep, spell, 2, false);
                        if (effect != null) { hero.CastSpell(effect, false, false); return; }
                    }
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] CheckAndCastProtectionFromPoison: {ex}"); }
        }

        public static void CheckAndHealAllies(GameLocationCharacter character)
        {
            try
            {
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.SpellRepertoires == null) return;
                var charService = ServiceRepository.GetService<IGameLocationCharacterService>();
                if (charService?.PartyCharacters == null) return;
                foreach (var ally in charService.PartyCharacters)
                {
                    if (ally?.RulesetCharacter == null || ally.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth) continue;
                    int allyHp = ally.RulesetCharacter.CurrentHitPoints;
                    int allyMax = allyHp + ally.RulesetCharacter.MissingHitPoints;
                    if (allyMax <= 0 || ((float)allyHp / allyMax * 100f) >= 50f) continue;
                    foreach (var rep in hero.SpellRepertoires)
                    {
                        if (rep == null) continue;
                        var spell = rep.PreparedSpells.Find(s => s != null &&
                            ((ModSettings.EnableSpellCureWounds && s.Name.IndexOf("CureWounds", StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (ModSettings.EnableSpellHealingWord && s.Name.IndexOf("HealingWord", StringComparison.OrdinalIgnoreCase) >= 0)));
                        if (spell == null || !rep.CanCastSpell(spell, true)) continue;
                        var impl = ServiceRepository.GetService<IRulesetImplementationService>();
                        if (impl == null) continue;
                        var effect = impl.InstantiateEffectSpell(hero, rep, spell, 1, false);
                        if (effect != null) { hero.CastSpell(effect, false, false); break; }
                    }
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] CheckAndHealAllies: {ex}"); }
        }

        public static void CheckAndAutoSwapWeapons(GameLocationCharacter character, bool isRangedArchetype = false)
        {
            try
            {
                if (!ModSettings.EnableAutoWeaponSwap) return;
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.CharacterInventory == null) return;
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService?.IsBattleInProgress != true) return;

                var inventory = hero.CharacterInventory;
                int currentConfig = inventory.CurrentConfiguration;
                int otherConfig = currentConfig == 0 ? 1 : 0;
                bool currentlyRanged = hero.IsWieldingRangedWeapon();
                int minDist = GetMinDistanceToEnemy(character);

                if (isRangedArchetype)
                {
                    if (minDist <= 2 && ModSettings.EnableAvoidOpportunityAttacks && currentlyRanged)
                    {
                        inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                        if (hero.IsWieldingRangedWeapon()) inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                        else ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: melee threat at {minDist} cells, switched to melee set.");
                    }
                    else if (minDist > 2 && !currentlyRanged)
                    {
                        inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                        if (!hero.IsWieldingRangedWeapon()) inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                        else ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: at range {minDist} cells, switched to ranged set.");
                    }
                    return;
                }

                if (minDist > 2 && !currentlyRanged)
                {
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (!hero.IsWieldingRangedWeapon()) inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                }
                else if (minDist <= 2 && currentlyRanged)
                {
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (hero.IsWieldingRangedWeapon()) inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] CheckAndAutoSwapWeapons: {ex}"); }
        }

        public static void LoadSavedChoices()
        {
            try
            {
                if (!File.Exists(SaveFilePath)) return;
                string json = File.ReadAllText(SaveFilePath).Trim('{', '}', ' ', '\r', '\n');
                CharacterAIChoices.Clear();
                if (string.IsNullOrEmpty(json)) return;
                foreach (var pair in json.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split(new char[] { ':' }, 2);
                    if (kv.Length == 2)
                    {
                        string n = kv[0].Trim(' ', '"', '\r', '\n');
                        if (int.TryParse(kv[1].Trim(' ', '"', '\r', '\n'), out int c)) CharacterAIChoices[n] = c;
                    }
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] LoadSavedChoices: {ex}"); }
        }

        public static void SaveChoices()
        {
            try
            {
                var entries = new List<string>();
                foreach (var kvp in CharacterAIChoices) entries.Add($"  \"{kvp.Key}\": {kvp.Value}");
                File.WriteAllText(SaveFilePath, "{\n" + string.Join(",\n", entries.ToArray()) + "\n}");
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] SaveChoices: {ex}"); }
        }

        /// <summary>
        /// Cast Shillelagh as a proper Bonus Action in the POSTFIX of StartBattleTurn,
        /// after action economy is initialized. Uses ExecuteInstantSingleAction(Id.CastBonus)
        /// with CharacterActionParams built via reflection to set spell repertoire and effect.
        /// Only casts if Shillelagh is not already active on the character.
        /// </summary>
        public static void TryCastShillelaghInPostfix(GameLocationCharacter character)
        {
            try
            {
                if (!ModSettings.EnableSpellShillelagh) return;
                var hero = character?.RulesetCharacter as RulesetCharacterHero;
                if (hero?.SpellRepertoires == null) return;

                // Check if Shillelagh is already active
                if (hero.AllConditions != null)
                    foreach (var cond in hero.AllConditions)
                        if (cond?.ConditionDefinition?.Name != null &&
                            cond.ConditionDefinition.Name.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: Shillelagh already active, skip.");
                            return;
                        }

                foreach (var rep in hero.SpellRepertoires)
                {
                    if (rep == null) continue;
                    var spell = rep.KnownCantrips.Find(s => s != null &&
                        (s.Name.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         s.Name.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (spell == null) continue;

                    var impl = ServiceRepository.GetService<IRulesetImplementationService>();
                    if (impl == null) { ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: IRulesetImplementationService null!"); break; }

                    var effect = impl.InstantiateEffectSpell(hero, rep, spell, 0, false);
                    if (effect == null) { ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: Shillelagh effect is null!"); break; }

                    // Cast spell on RulesetCharacter level AND apply effect forms directly
                    hero.CastSpell(effect, true, false);
                    effect.ApplyEffectOnCharacter(hero, true, character.LocationPosition);
                    character.SpendActionType(ActionDefinitions.ActionType.Bonus);
                    ModEntry?.Logger.Log($"[SolastaAI] {character.Name}: Shillelagh cast and effect applied successfully!");
                    break;
                }
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] TryCastShillelaghInPostfix: {ex}"); }
        }

        /// <summary>
        /// Controls Ranged Fighter movement: spends Move action to prevent advancing,
        /// but preserves it when (a) melee-threatened (to retreat) or (b) on lower ground
        /// than enemies (to seek elevation via CasterCombatDecisions pathfinding).
        /// </summary>
        public static void HandleRangedFighterPositioning(GameLocationCharacter character)
        {
            try
            {
                if (character.ControllerId != PlayerControllerManager.DmControllerId) return;
                int minDist = GetMinDistanceToEnemy(character);

                if (minDist <= 2)
                {
                    ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Melee threat ({minDist} cells), move preserved for retreat.");
                    return;
                }

                // Allow movement if fighter is on lower ground than enemies → seek elevation
                if (IsOnLowerGroundThanEnemies(character))
                {
                    ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': On lower ground – move preserved to seek elevated position.");
                    return;
                }

                // Already at safe range and good elevation → spend move so AI doesn't advance
                character.SpendActionType(ActionDefinitions.ActionType.Move);
                ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Move spent (range {minDist} cells, good elevation).");
            }
            catch (Exception ex) { ModEntry?.Logger.Error($"[SolastaAI] HandleRangedFighterPositioning: {ex}"); }
        }

        /// <summary>
        /// Returns true if any living enemy is significantly higher (z > myZ + 1) than this character.
        /// Used to decide whether a ranged fighter should seek elevated ground.
        /// </summary>
        public static bool IsOnLowerGroundThanEnemies(GameLocationCharacter character)
        {
            try
            {
                var battleService = ServiceRepository.GetService<IGameLocationBattleService>();
                if (battleService?.IsBattleInProgress != true || battleService.Battle == null) return false;
                var enemies = (character.Side == RuleDefinitions.Side.Ally)
                    ? battleService.Battle.EnemyContenders
                    : battleService.Battle.PlayerContenders;
                if (enemies == null || enemies.Count == 0) return false;
                int myZ = character.LocationPosition.z;
                foreach (var enemy in enemies)
                {
                    if (enemy?.RulesetCharacter == null || enemy.RulesetCharacter.IsDeadOrDyingOrUnconsciousWithNoHealth) continue;
                    if (enemy.LocationPosition.z > myZ + 1) return true;
                }
                return false;
            }
            catch { return false; }
        }
    }

    // ===== HARMONY PATCHES =====

    /// <summary>
    /// PREFIX on StartBattleTurn: Sets CurrentTurnCharacterName so all patches know whose turn it is.
    /// Applies AI controller and runs class-specific tactics.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.StartBattleTurn))]
    public static class Patch_StartBattleTurn
    {
        public static bool Prefix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null) return true;
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return true;

                // Update context so spell/power patches know the active character's mode
                Main.CurrentTurnCharacterName = name;

                // Emergency low HP fallback
                if (Main.ModSettings.EnableEmergencyLowHpFallback && __instance.RulesetCharacter != null)
                {
                    int hp = __instance.RulesetCharacter.CurrentHitPoints;
                    int max = hp + __instance.RulesetCharacter.MissingHitPoints;
                    if (max > 0 && ((float)hp / max * 100f) < Main.ModSettings.EmergencyHpThresholdPercent)
                    {
                        Main.ModEntry?.Logger.Log($"[SolastaAI] Emergency Fallback: {name} HP critical.");
                        Main.CurrentTurnCharacterName = "";
                        Main.ApplyAIController(__instance, 0);
                        return true;
                    }
                }

                if (Main.CharacterAIChoices.TryGetValue(name, out int choice))
                {
                    Main.ApplyAIController(__instance, choice);
                    switch (choice)
                    {
                        case Main.MODE_DRUID_WILD:       Main.ExecuteDruidTactics(__instance); break;
                        case Main.MODE_DRUID_SHILLELAGH: Main.ExecuteShillelaghDruidTactics(__instance); break;
                        case Main.MODE_FIGHTER_MELEE:    Main.ExecuteFighterTactics(__instance, false); break;
                        case Main.MODE_FIGHTER_RANGED:   Main.ExecuteFighterTactics(__instance, true); break;
                        default:
                            if (choice > 0) Main.CheckAndAutoSwapWeapons(__instance, false);
                            break;
                    }
                }
            }
            catch (Exception ex) { Main.ModEntry?.Logger.Error($"[SolastaAI] Patch_StartBattleTurn: {ex}"); }
            return true;
        }
    }

    /// <summary>
    /// <summary>
    /// POSTFIX on StartBattleTurn: Spends the Move action for Ranged Fighters when enemies are
    /// not in immediate melee range. This prevents the AI from pathfinding towards enemies.
    /// The move action is preserved when an enemy is adjacent (≤2 cells) so the fighter can retreat.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.StartBattleTurn))]
    public static class Patch_StartBattleTurn_Post
    {
        public static void Postfix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null) return;
                string name = __instance.Name ?? "";
                if (!Main.CharacterAIChoices.TryGetValue(name, out int choice)) return;
                if (__instance.ControllerId != PlayerControllerManager.DmControllerId) return;

                switch (choice)
                {
                    case Main.MODE_DRUID_SHILLELAGH:
                        // Cast Shillelagh as a proper Bonus Action now that the turn is fully initialized.
                        Main.TryCastShillelaghInPostfix(__instance);
                        break;

                    case Main.MODE_FIGHTER_RANGED:
                        // Spend move action to prevent advancing, but preserve it when
                        // melee-threatened (retreat) or on lower ground (seek elevation).
                        Main.HandleRangedFighterPositioning(__instance);
                        break;
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[SolastaAI] Patch_StartBattleTurn_Post: {ex}");
            }
        }
    }


    /// <summary>
    /// POSTFIX on EndBattleTurn: Clears CurrentTurnCharacterName.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationCharacter), "EndBattleTurn")]
    public static class Patch_EndBattleTurn
    {
        public static void Postfix() { Main.CurrentTurnCharacterName = ""; }
    }

    /// <summary>
    /// Blocks disabled spells AND mode-inappropriate spells from AI evaluation (CanCastSpell).
    /// </summary>
    [HarmonyPatch(typeof(RulesetSpellRepertoire), nameof(RulesetSpellRepertoire.CanCastSpell), new Type[] { typeof(SpellDefinition), typeof(bool) })]
    public static class Patch_CanCastSpell
    {
        public static bool Prefix(SpellDefinition spellDefinition, ref bool __result)
        {
            try
            {
                if (spellDefinition != null && !Main.IsSpellEnabledForAI(spellDefinition.Name))
                {
                    __result = false;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    /// <summary>
    /// Blocks disabled spells AND mode-inappropriate spells from AI evaluation (IsSpellReady).
    /// </summary>
    [HarmonyPatch(typeof(RulesetSpellRepertoire), nameof(RulesetSpellRepertoire.IsSpellReady))]
    public static class Patch_IsSpellReady
    {
        public static bool Prefix(SpellDefinition spellDefinition, ref bool __result)
        {
            try
            {
                if (spellDefinition != null && !Main.IsSpellEnabledForAI(spellDefinition.Name))
                {
                    __result = false;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    /// <summary>
    /// Blocks disabled powers from AI evaluation (GetRemainingUsesOfPower).
    /// </summary>
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.GetRemainingUsesOfPower))]
    public static class Patch_GetRemainingUsesOfPower
    {
        public static bool Prefix(RulesetUsablePower usablePower, ref int __result)
        {
            try
            {
                if (usablePower?.PowerDefinition != null && !Main.IsPowerEnabledForAI(usablePower.PowerDefinition.Name))
                {
                    __result = 0;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    /// <summary>
    /// Last-resort block: prevents actual execution of disabled spells even if AI selected them.
    /// </summary>
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.CastSpell), new Type[] { typeof(RulesetEffectSpell), typeof(bool), typeof(bool) })]
    public static class Patch_CastSpell
    {
        public static bool Prefix(RulesetEffectSpell effectSpell)
        {
            try
            {
                if (effectSpell?.SpellDefinition != null)
                {
                    string name = effectSpell.SpellDefinition.Name;
                    // Allow explicit Shillelagh cast from our own Postfix script
                    if (name.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!Main.ModSettings.EnableSpellShillelagh) return false;
                        return true;
                    }

                    if (!Main.IsSpellEnabledForAI(name))
                    {
                        Main.ModEntry?.Logger.Log($"[SolastaAI] CastSpell blocked (disabled/mode): {name}");
                        return false;
                    }
                }
            }
            catch {}
            return true;
        }
    }

    /// <summary>
    /// Last-resort block: prevents actual execution of disabled powers.
    /// </summary>
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.UsePower))]
    public static class Patch_UsePower
    {
        public static bool Prefix(RulesetUsablePower usablePower)
        {
            try
            {
                if (usablePower?.PowerDefinition != null && !Main.IsPowerEnabledForAI(usablePower.PowerDefinition.Name))
                {
                    Main.ModEntry?.Logger.Log($"[SolastaAI] UsePower blocked (disabled): {usablePower.PowerDefinition.Name}");
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), "TriggerBattleEnd")]
    public static class Patch_TriggerBattleEnd
    {
        public static void Postfix()
        {
            try
            {
                Main.CurrentTurnCharacterName = "";
                Main.EnsureExplorationControl();
                Main.ModEntry?.Logger.Log("[SolastaAI] Combat ended. Party restored to Player Control.");
            }
            catch (Exception ex) { Main.ModEntry?.Logger.Error($"[SolastaAI] Patch_TriggerBattleEnd: {ex}"); }
        }
    }

    [HarmonyPatch(typeof(GameLocationCharacter), nameof(GameLocationCharacter.DamageSustained))]
    public static class Patch_DamageSustained
    {
        public static void Postfix(GameLocationCharacter __instance)
        {
            try
            {
                if (__instance == null || !Main.ModSettings.EnableEmergencyLowHpFallback) return;
                string name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return;
                if (!Main.CharacterAIChoices.TryGetValue(name, out int choice) || choice == 0) return;
                if (__instance.RulesetCharacter == null) return;
                int hp = __instance.RulesetCharacter.CurrentHitPoints;
                int max = hp + __instance.RulesetCharacter.MissingHitPoints;
                if (max > 0 && ((float)hp / max * 100f) < Main.ModSettings.EmergencyHpThresholdPercent)
                {
                    Main.ModEntry?.Logger.Log($"[SolastaAI] Emergency Fallback on damage: {name}");
                    Main.CharacterAIChoices[name] = 0;
                    Main.ApplyAIController(__instance, 0);
                    Main.SaveChoices();
                }
            }
            catch (Exception ex) { Main.ModEntry?.Logger.Error($"[SolastaAI] Patch_DamageSustained: {ex}"); }
        }
    }
}
