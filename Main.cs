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
        public string FighterStyle = "Melee"; // "Melee" or "Ranged"
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
        public bool EnableSpellPoisonSting = true;
        public bool EnableSpellChillTouch = true;
        public bool EnableSpellResistElements = true;
        public bool EnableSpellAnimalFriendship = true;
        public bool EnableSpellCharmPerson = true;
        public bool EnableSpellCureWounds = true;
        public bool EnableSpellHealingWord = true;
        public bool EnableSpellDetectMagic = true;
        public bool EnableSpellDetectPoisonAndDisease = true;
        public bool EnableSpellEntangle = true;
        public bool EnableSpellFaerieFire = true;
        public bool EnableSpellFogCloud = true;
        public bool EnableSpellGoodberry = true;
        public bool EnableSpellJump = true;
        public bool EnableSpellLongstrider = true;
        public bool EnableSpellProtectionFromPoison = true;
        public bool EnableSpellBarkskin = true;
        public bool EnableSpellDarkvision = true;
        public bool EnableSpellFlameBlade = true;
        public bool EnableSpellFlamingSphere = true;
        public bool EnableSpellHeatMetal = true;
        public bool EnableSpellHoldPerson = true;
        public bool EnableSpellLesserRestoration = true;
        public bool EnableSpellMoonbeam = true;
        public bool EnableSpellSpikeGrowth = true;
        public bool EnableSpellPassWithoutTrace = true;
        public bool EnableSpellProtectionFromEnergy = true;
        public bool EnableSpellCallLightning = true;
        public bool EnableSpellDispelMagic = true;
        public bool EnableSpellSleetStorm = true;
        public bool EnableSpellWindWall = true;
        public bool EnableSpellDaylight = true;
        public bool EnableSpellCreateFoodAndWater = true;
        public bool EnableSpellRevivify = true;

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
        private static Dictionary<string, bool> CategoryFoldStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Mode constants
        public const int MODE_HUMAN         = 0;
        public const int MODE_MELEE         = 1;
        public const int MODE_RANGE_BACKUP  = 2;
        public const int MODE_CASTER        = 3;
        public const int MODE_CLERIC        = 4;
        public const int MODE_DRUID         = 5;
        public const int MODE_FIGHTER       = 6;
        public const int MODE_MAGE          = 7;
        public const int MODE_ROGUE         = 8;

        public static readonly string[] AIPackageNames = new string[]
        {
            "Human (Player)",
            "AI: Melee (Default)",
            "AI: Range (Default)",
            "AI: Caster (Default)",
            "AI: Cleric",
            "AI: Druid",
            "AI: Fighter",
            "AI: Mage",
            "AI: Rogue"
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

            // Check user-controlled spell toggles - disabled spells must ALWAYS be blocked for AI
            if (spellName.IndexOf("Shillelagh", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("Zauberstock", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellShillelagh;
            if (spellName.IndexOf("Guidance", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("GöttlicheFührung", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellGuidance;
            if (spellName.IndexOf("ProduceFlame", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellProduceFlame;
            if (spellName.IndexOf("ThornWhip", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellThornWhip;
            if (spellName.IndexOf("PoisonSpray", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("Giftsprühen", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellPoisonSpray;
            if (spellName.IndexOf("PoisonSting", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("GiftigerStachel", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellPoisonSting;
            if (spellName.IndexOf("ChillTouch", StringComparison.OrdinalIgnoreCase) >= 0 || spellName.IndexOf("KalteHand", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellChillTouch;
            if (spellName.IndexOf("ResistElements", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellResistElements;
            if (spellName.IndexOf("AnimalFriendship", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellAnimalFriendship;
            if (spellName.IndexOf("CharmPerson", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellCharmPerson;
            if (spellName.IndexOf("CureWounds", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellCureWounds;
            if (spellName.IndexOf("HealingWord", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellHealingWord;
            if (spellName.IndexOf("DetectMagic", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellDetectMagic;
            if (spellName.IndexOf("DetectPoisonAndDisease", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellDetectPoisonAndDisease;
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
            if (spellName.IndexOf("Darkvision", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellDarkvision;
            if (spellName.IndexOf("FlameBlade", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellFlameBlade;
            if (spellName.IndexOf("FlamingSphere", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellFlamingSphere;
            if (spellName.IndexOf("HeatMetal", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellHeatMetal;
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
            if (spellName.IndexOf("ProtectionFromEnergy", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellProtectionFromEnergy;
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
            if (spellName.IndexOf("CreateFoodAndWater", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellCreateFoodAndWater;
            if (spellName.IndexOf("Revivify", StringComparison.OrdinalIgnoreCase) >= 0)
                return ModSettings.EnableSpellRevivify;

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

                    if (currentChoice == MODE_DRUID)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>✨ Druid Spell & Ability Controls for {displayName}:</i>");

                        ModSettings.EnableDruidWildShape = GUILayout.Toggle(ModSettings.EnableDruidWildShape, " <b>Wild Shape / Tiergestalt</b>");

                        GUILayout.Space(3);

                        string dPrefix = $"{name}_druid_";

                        DrawCategoryHeader(dPrefix + "cantrips", "<b>🔮 Cantrips / Zaubertricks</b>", () =>
                        {
                            ModSettings.EnableSpellShillelagh = GUILayout.Toggle(ModSettings.EnableSpellShillelagh, "└─ <b>Shillelagh / Zauberstock</b>");
                            ModSettings.EnableSpellGuidance = GUILayout.Toggle(ModSettings.EnableSpellGuidance, "└─ <b>Guidance / Göttliche Führung</b>");
                            ModSettings.EnableSpellProduceFlame = GUILayout.Toggle(ModSettings.EnableSpellProduceFlame, "└─ <b>Produce Flame / Flamme erzeugen</b>");
                            ModSettings.EnableSpellThornWhip = GUILayout.Toggle(ModSettings.EnableSpellThornWhip, "└─ <b>Thorn Whip / Dornenpeitsche</b>");
                            ModSettings.EnableSpellPoisonSpray = GUILayout.Toggle(ModSettings.EnableSpellPoisonSpray, "└─ <b>Poison Spray / Giftsprühen</b>");
                            ModSettings.EnableSpellPoisonSting = GUILayout.Toggle(ModSettings.EnableSpellPoisonSting, "└─ <b>Poison Sting / Giftiger Stachel</b>");
                            ModSettings.EnableSpellChillTouch = GUILayout.Toggle(ModSettings.EnableSpellChillTouch, "└─ <b>Chill Touch / Kalte Hand</b>");
                            ModSettings.EnableSpellResistElements = GUILayout.Toggle(ModSettings.EnableSpellResistElements, "└─ <b>Resist Elements</b>");
                        });

                        DrawCategoryHeader(dPrefix + "healing", "<b>💚 Healing & Restoration</b>", () =>
                        {
                            ModSettings.EnableSpellCureWounds = GUILayout.Toggle(ModSettings.EnableSpellCureWounds, "└─ <b>Cure Wounds / Wunden heilen</b>");
                            ModSettings.EnableSpellHealingWord = GUILayout.Toggle(ModSettings.EnableSpellHealingWord, "└─ <b>Healing Word / Wort der Heilung</b>");
                            ModSettings.EnableSpellLesserRestoration = GUILayout.Toggle(ModSettings.EnableSpellLesserRestoration, "└─ <b>Lesser Restoration / Teilw. Genesung</b>");
                            ModSettings.EnableSpellGoodberry = GUILayout.Toggle(ModSettings.EnableSpellGoodberry, "└─ <b>Goodberry / Gute Beere</b>");
                            ModSettings.EnableSpellCreateFoodAndWater = GUILayout.Toggle(ModSettings.EnableSpellCreateFoodAndWater, "└─ <b>Create Food & Water / Nahrung erschaffen</b>");
                            ModSettings.EnableSpellRevivify = GUILayout.Toggle(ModSettings.EnableSpellRevivify, "└─ <b>Revivify / Wiederbeleben</b>");
                        });

                        DrawCategoryHeader(dPrefix + "protection", "<b>🛡️ Protection & Buffs</b>", () =>
                        {
                            ModSettings.EnableSpellProtectionFromPoison = GUILayout.Toggle(ModSettings.EnableSpellProtectionFromPoison, "└─ <b>Protection from Poison / Schutz vor Gift</b>");
                            ModSettings.EnableSpellProtectionFromEnergy = GUILayout.Toggle(ModSettings.EnableSpellProtectionFromEnergy, "└─ <b>Protection from Energy / Schutz vor Energie</b>");
                            ModSettings.EnableSpellBarkskin = GUILayout.Toggle(ModSettings.EnableSpellBarkskin, "└─ <b>Barkskin / Rindenhaut</b>");
                            ModSettings.EnableSpellDarkvision = GUILayout.Toggle(ModSettings.EnableSpellDarkvision, "└─ <b>Darkvision / Dunkelsicht</b>");
                            ModSettings.EnableSpellLongstrider = GUILayout.Toggle(ModSettings.EnableSpellLongstrider, "└─ <b>Longstrider / Langschritt</b>");
                            ModSettings.EnableSpellPassWithoutTrace = GUILayout.Toggle(ModSettings.EnableSpellPassWithoutTrace, "└─ <b>Pass Without Trace / Spurlos verbergen</b>");
                            ModSettings.EnableSpellJump = GUILayout.Toggle(ModSettings.EnableSpellJump, "└─ <b>Jump / Springen</b>");
                        });

                        DrawCategoryHeader(dPrefix + "attack", "<b>⚔️ Attack & Control Spells</b>", () =>
                        {
                            ModSettings.EnableSpellEntangle = GUILayout.Toggle(ModSettings.EnableSpellEntangle, "└─ <b>Entangle / Verstricken</b>");
                            ModSettings.EnableSpellFaerieFire = GUILayout.Toggle(ModSettings.EnableSpellFaerieFire, "└─ <b>Faerie Fire / Feenfeuer</b>");
                            ModSettings.EnableSpellFogCloud = GUILayout.Toggle(ModSettings.EnableSpellFogCloud, "└─ <b>Fog Cloud / Nebelwolke</b>");
                            ModSettings.EnableSpellAnimalFriendship = GUILayout.Toggle(ModSettings.EnableSpellAnimalFriendship, "└─ <b>Animal Friendship / Tierfreundschaft</b>");
                            ModSettings.EnableSpellCharmPerson = GUILayout.Toggle(ModSettings.EnableSpellCharmPerson, "└─ <b>Charm Person / Person bezaubern</b>");
                            ModSettings.EnableSpellDetectMagic = GUILayout.Toggle(ModSettings.EnableSpellDetectMagic, "└─ <b>Detect Magic / Magie entdecken</b>");
                            ModSettings.EnableSpellDetectPoisonAndDisease = GUILayout.Toggle(ModSettings.EnableSpellDetectPoisonAndDisease, "└─ <b>Detect Poison / Gift entdecken</b>");
                            ModSettings.EnableSpellFlameBlade = GUILayout.Toggle(ModSettings.EnableSpellFlameBlade, "└─ <b>Flame Blade / Flammenklinge</b>");
                            ModSettings.EnableSpellFlamingSphere = GUILayout.Toggle(ModSettings.EnableSpellFlamingSphere, "└─ <b>Flaming Sphere / Flammenkugel</b>");
                            ModSettings.EnableSpellHeatMetal = GUILayout.Toggle(ModSettings.EnableSpellHeatMetal, "└─ <b>Heat Metal / Metall erhitzen</b>");
                            ModSettings.EnableSpellHoldPerson = GUILayout.Toggle(ModSettings.EnableSpellHoldPerson, "└─ <b>Hold Person / Person festhalten</b>");
                            ModSettings.EnableSpellMoonbeam = GUILayout.Toggle(ModSettings.EnableSpellMoonbeam, "└─ <b>Moonbeam / Mondstrahl</b>");
                            ModSettings.EnableSpellSpikeGrowth = GUILayout.Toggle(ModSettings.EnableSpellSpikeGrowth, "└─ <b>Spike Growth / Dornenwuchs</b>");
                            ModSettings.EnableSpellCallLightning = GUILayout.Toggle(ModSettings.EnableSpellCallLightning, "└─ <b>Call Lightning / Blitzschlag rufen</b>");
                            ModSettings.EnableSpellDispelMagic = GUILayout.Toggle(ModSettings.EnableSpellDispelMagic, "└─ <b>Dispel Magic / Magie bannen</b>");
                            ModSettings.EnableSpellSleetStorm = GUILayout.Toggle(ModSettings.EnableSpellSleetStorm, "└─ <b>SleetStorm / Graupelschauer</b>");
                            ModSettings.EnableSpellWindWall = GUILayout.Toggle(ModSettings.EnableSpellWindWall, "└─ <b>Wind Wall / Windwand</b>");
                            ModSettings.EnableSpellDaylight = GUILayout.Toggle(ModSettings.EnableSpellDaylight, "└─ <b>Daylight / Tageslicht</b>");
                        });

                        GUILayout.Space(3);
                        ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(ModSettings.EnableAutoWeaponSwap, " <b>Auto-Weapon Swap</b>");
                        GUILayout.EndVertical();
                    }
                    else if (currentChoice == MODE_FIGHTER)
                    {
                        GUILayout.BeginVertical("box");
                        GUILayout.Label($"<i>✨ Fighter Combat Style & Skill Controls for {displayName}:</i>");

                        GUILayout.Space(3);
                        GUILayout.Label("  <b>⚔️ Combat Style / Kampfstil:</b>");
                        GUILayout.BeginHorizontal();
                        bool isMeleeStyle = ModSettings.FighterStyle != "Ranged";
                        if (GUILayout.Toggle(isMeleeStyle, "  <b>⚔️ Melee (Nahkampf)</b>", GUILayout.Width(150)))
                            ModSettings.FighterStyle = "Melee";
                        if (GUILayout.Toggle(!isMeleeStyle, "  <b>🏹 Ranged (Fernkampf)</b>", GUILayout.Width(150)))
                            ModSettings.FighterStyle = "Ranged";
                        GUILayout.EndHorizontal();

                        GUILayout.Space(3);

                        string fPrefix = $"{name}_fighter_";

                        DrawCategoryHeader(fPrefix + "defense", "<b>🛡️ Defense & Recovery</b>", () =>
                        {
                            ModSettings.EnableFighterSecondWind = GUILayout.Toggle(ModSettings.EnableFighterSecondWind, "└─ <b>Second Wind / Durchschnaufen</b>");
                            ModSettings.EnableFighterIndomitable = GUILayout.Toggle(ModSettings.EnableFighterIndomitable, "└─ <b>Indomitable / Unbeugsam</b>");
                        });

                        DrawCategoryHeader(fPrefix + "offensive", "<b>⚔️ Offensive Skills & Maneuvers</b>", () =>
                        {
                            ModSettings.EnableFighterActionSurge = GUILayout.Toggle(ModSettings.EnableFighterActionSurge, "└─ <b>Action Surge / Tatendrank</b>");
                            ModSettings.EnableFighterPushingAttack = GUILayout.Toggle(ModSettings.EnableFighterPushingAttack, "└─ <b>Pushing Attack / Stoßangriff</b>");
                            ModSettings.EnableFighterTripAttack = GUILayout.Toggle(ModSettings.EnableFighterTripAttack, "└─ <b>Trip Attack / Beinstellen</b>");
                            ModSettings.EnableFighterRiposte = GUILayout.Toggle(ModSettings.EnableFighterRiposte, "└─ <b>Riposte</b>");
                            ModSettings.EnableFighterPrecisionAttack = GUILayout.Toggle(ModSettings.EnableFighterPrecisionAttack, "└─ <b>Precision Attack / Präzisionsangriff</b>");
                        });

                        DrawCategoryHeader(fPrefix + "movement", "<b>🎯 Movement & Positioning</b>", () =>
                        {
                            if (ModSettings.FighterStyle == "Ranged")
                                ModSettings.EnableAvoidOpportunityAttacks = GUILayout.Toggle(ModSettings.EnableAvoidOpportunityAttacks, "└─ <b>Avoid Opportunity Attacks</b>");
                            ModSettings.EnableAutoWeaponSwap = GUILayout.Toggle(ModSettings.EnableAutoWeaponSwap, "└─ <b>Auto-Weapon Swap</b>");
                        });

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

        private static void DrawCategoryHeader(string key, string title, Action drawContent)
        {
            bool isExpanded = CategoryFoldStates.TryGetValue(key, out bool exp) && exp;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isExpanded ? " <b>[-]</b> " : " <b>[+]</b> ", GUILayout.Width(45)))
            {
                CategoryFoldStates[key] = !isExpanded;
            }
            GUILayout.Label(title, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            if (isExpanded)
            {
                GUILayout.BeginVertical("box");
                drawContent();
                GUILayout.EndVertical();
            }
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
                            case MODE_DRUID:          pkg = db.GetElement("DefaultSupportCasterWithBackupAttacksDecisions", true); break;
                            case MODE_FIGHTER:
                                pkg = (ModSettings.FighterStyle == "Ranged")
                                    ? db.GetElement("CasterCombatDecisions", true)
                                    : db.GetElement("DefaultMeleeWithBackupRangeDecisions", true);
                                break;
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

                if (ModSettings.EnableFighterSecondWind && IsPowerEnabledForAI("SecondWind"))
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

                if (ModSettings.EnableFighterActionSurge && IsPowerEnabledForAI("ActionSurge"))
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

                if (ModSettings.EnableDruidWildShape && IsPowerEnabledForAI("WildShape"))
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

                // Melee Fighter logic: check if any enemy can be reached within movement range (6 cells walk)
                int maxReachableDist = 6;

                // If nearest enemy is farther than 6 cells (out of 1-turn melee reach) and we are currently holding melee weapon:
                // Switch to ranged weapon so the fighter can shoot while advancing!
                if (minDist > maxReachableDist && !currentlyRanged)
                {
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (hero.IsWieldingRangedWeapon())
                    {
                        ModEntry?.Logger.Log($"[SolastaAI] Melee Fighter '{character.Name}': Enemy too far for melee ({minDist} cells > {maxReachableDist}), switched to ranged weapon set to attack from distance.");
                    }
                    else
                    {
                        inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                    }
                }
                else if (minDist <= maxReachableDist && currentlyRanged)
                {
                    // Enemy is within melee reach (<= 6 cells): switch back to melee weapon set
                    inventory.SwitchToWieldItemsOfConfiguration(otherConfig);
                    if (!hero.IsWieldingRangedWeapon())
                    {
                        ModEntry?.Logger.Log($"[SolastaAI] Melee Fighter '{character.Name}': Enemy reachable in melee ({minDist} cells <= {maxReachableDist}), switched to melee weapon set.");
                    }
                    else
                    {
                        inventory.SwitchToWieldItemsOfConfiguration(currentConfig);
                    }
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
        /// Controls Ranged Fighter movement:
        /// 1. If threatened in melee (≤2 cells): preserves move so fighter can retreat.
        /// 2. If out of weapon range (> attack range): preserves move so fighter can advance into range.
        /// 3. If higher ground is reachable: moves to highest cell.
        /// 4. If in range and on good ground: spends move action so fighter shoots without walking into melee.
        /// </summary>
        public static void HandleRangedFighterPositioning(GameLocationCharacter character)
        {
            try
            {
                if (character.ControllerId != PlayerControllerManager.DmControllerId) return;
                int minDist = GetMinDistanceToEnemy(character);

                // 1. Melee threat: preserve move so character can retreat
                if (minDist <= 2)
                {
                    ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Melee threat ({minDist} cells), move preserved for retreat.");
                    return;
                }

                // Get character's max ranged weapon range from active AttackModes (default 12 cells)
                int maxRangedRange = 12;
                var hero = character.RulesetCharacter as RulesetCharacterHero;
                if (hero?.AttackModes != null)
                {
                    foreach (var mode in hero.AttackModes)
                    {
                        if (mode != null && mode.Ranged && mode.MaxRange > 0)
                        {
                            maxRangedRange = Math.Max(maxRangedRange, mode.MaxRange);
                        }
                    }
                }

                // 2. Out of weapon range: preserve move so AI advances into shooting range
                if (minDist > maxRangedRange)
                {
                    ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Enemy out of range ({minDist} > {maxRangedRange} cells), move preserved to advance into range.");
                    return;
                }

                // 3. Search reachable destinations for elevated ground (z > current z)
                var pathfindingService = ServiceRepository.GetService<IGameLocationPathfindingService>();
                var positioningService = ServiceRepository.GetService<IGameLocationPositioningService>();
                var actionService = ServiceRepository.GetService<IGameLocationActionService>();

                if (pathfindingService != null && positioningService != null && actionService != null)
                {
                    var destinations = pathfindingService.ComputeValidDestinations(character, false, -1);
                    if (destinations != null && destinations.Count > 0)
                    {
                        int currentZ = character.LocationPosition.z;
                        int bestZ = currentZ;
                        TA.int3 bestPos = character.LocationPosition;

                        foreach (var pathStep in destinations)
                        {
                            var pos = pathStep.position;
                            if (pos.z > bestZ && positioningService.CanCharacterStayAtPosition(character, pos, true, true, true))
                            {
                                bestZ = pos.z;
                                bestPos = pos;
                            }
                        }

                        if (bestZ > currentZ)
                        {
                            ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Found elevated ground at {bestPos} (Z: {bestZ} vs current {currentZ}). Moving to high ground!");
                            actionService.MoveCharacter(character, bestPos, character.Orientation, 0f, ActionDefinitions.MoveStance.Walk, null, false, true, false);
                            return;
                        }
                    }
                }

                // 4. In range and no higher ground reachable → spend move so fighter shoots from current position without closing into melee
                character.SpendActionType(ActionDefinitions.ActionType.Move);
                ModEntry?.Logger.Log($"[SolastaAI] Ranged Fighter '{character.Name}': Move spent (in range {minDist}/{maxRangedRange} cells, no higher ground).");
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
                        case Main.MODE_DRUID:            Main.ExecuteDruidTactics(__instance); break;
                        case Main.MODE_FIGHTER:          Main.ExecuteFighterTactics(__instance, Main.ModSettings.FighterStyle == "Ranged"); break;
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

                if (choice == Main.MODE_FIGHTER && Main.ModSettings.FighterStyle == "Ranged")
                {
                    // Spend move action to prevent advancing, but preserve it when
                    // melee-threatened (retreat) or on lower ground (seek elevation).
                    Main.HandleRangedFighterPositioning(__instance);
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
    /// Blocks disabled spells & powers at the GameLocationActionManager level when an action is executed.
    /// </summary>
    [HarmonyPatch(typeof(GameLocationActionManager), nameof(GameLocationActionManager.ExecuteAction), new Type[] { typeof(CharacterActionParams), typeof(CharacterAction.ActionExecutedHandler), typeof(bool) })]
    public static class Patch_ExecuteAction
    {
        public static bool Prefix(CharacterActionParams actionParams)
        {
            try
            {
                if (actionParams == null || actionParams.ActingCharacter == null) return true;
                if (actionParams.ActingCharacter.ControllerId != PlayerControllerManager.DmControllerId) return true;

                // Check RulesetEffect (Spell or Power effect)
                if (actionParams.RulesetEffect != null)
                {
                    if (actionParams.RulesetEffect is RulesetEffectSpell spellEffect && spellEffect.SpellDefinition != null)
                    {
                        string sName = spellEffect.SpellDefinition.Name;
                        if (!Main.IsSpellEnabledForAI(sName))
                        {
                            Main.ModEntry?.Logger.Log($"[SolastaAI] ExecuteAction BLOCKED disabled spell effect: {sName} for {actionParams.ActingCharacter.Name}");
                            return false;
                        }
                    }
                    else if (actionParams.RulesetEffect is RulesetEffectPower powerEffect && powerEffect.PowerDefinition != null)
                    {
                        string pName = powerEffect.PowerDefinition.Name;
                        if (!Main.IsPowerEnabledForAI(pName))
                        {
                            Main.ModEntry?.Logger.Log($"[SolastaAI] ExecuteAction BLOCKED disabled power effect: {pName} for {actionParams.ActingCharacter.Name}");
                            return false;
                        }
                    }
                }

                // Check UsablePower directly
                if (actionParams.UsablePower?.PowerDefinition != null)
                {
                    string pName = actionParams.UsablePower.PowerDefinition.Name;
                    if (!Main.IsPowerEnabledForAI(pName))
                    {
                        Main.ModEntry?.Logger.Log($"[SolastaAI] ExecuteAction BLOCKED disabled power: {pName} for {actionParams.ActingCharacter.Name}");
                        return false;
                    }
                }

                // Check ActionDefinition parameters
                if (actionParams.ActionDefinition != null)
                {
                    if (actionParams.ActionDefinition.ActivatedPower != null)
                    {
                        string pName = actionParams.ActionDefinition.ActivatedPower.Name;
                        if (!Main.IsPowerEnabledForAI(pName))
                        {
                            Main.ModEntry?.Logger.Log($"[SolastaAI] ExecuteAction BLOCKED disabled activated power: {pName} for {actionParams.ActingCharacter.Name}");
                            return false;
                        }
                    }

                    // String parameters often carry spell or power names
                    if (!string.IsNullOrEmpty(actionParams.StringParameter))
                    {
                        if (!Main.IsSpellEnabledForAI(actionParams.StringParameter) || !Main.IsPowerEnabledForAI(actionParams.StringParameter))
                        {
                            Main.ModEntry?.Logger.Log($"[SolastaAI] ExecuteAction BLOCKED disabled stringParam: {actionParams.StringParameter} for {actionParams.ActingCharacter.Name}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex) { Main.ModEntry?.Logger.Error($"[SolastaAI] Patch_ExecuteAction: {ex}"); }
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
        public static bool Prefix(RulesetEffectSpell effectSpell, RulesetCharacter __instance)
        {
            try
            {
                if (effectSpell?.SpellDefinition != null)
                {
                    string name = effectSpell.SpellDefinition.Name;
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
        public static bool Prefix(RulesetUsablePower usablePower, RulesetCharacter __instance)
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
