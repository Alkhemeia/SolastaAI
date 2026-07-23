# SolastaAI

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, Fighter & Druid class automation with individual spell & skill controls, collapsible UI categories, emergency safety rules, automatic weapon swapping, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence & Update-Safety:**
   - Standalone mod with **zero dependencies** on third-party mods.
   - Operates directly via Solasta's native AI Decision Package engine (`TA.AI.DecisionPackageDefinition`).
2. **Structured Dropdown UI & Collapsible Categories:**
   - Convenient **Dropdown Selector** per hero e.g. `[ AI: Fighter ▼ ]`.
   - **Collapsible `[+]` / `[-]` Category Headers:** Expand or collapse sub-categories to keep the UI clean and readable.
   - Archetypes available per hero:
     - `Human (Player)`
     - `AI: Melee (Default)`
     - `AI: Range (Default)`
     - `AI: Caster (Default)`
     - `AI: Cleric`
     - `AI: Druid` - Full spellcasting support and Tiergestalt (Wild Shape).
     - `AI: Fighter` - Melee & Ranged fighter combat with sub-style selector.
     - `AI: Mage`
     - `AI: Rogue`
3. **Granular Categorized Spell & Skill Toggles:**
   - Every single spell and ability for Druids and Fighters can be toggled on or off individually under collapsible sub-categories:
   - **Fighter Options (`AI: Fighter`):**
     - **Combat Style Selector:** Toggle directly between `⚔️ Melee (Nahkampf)` and `🏹 Ranged (Fernkampf)`.
     - **`[+]` 🛡️ Defense & Recovery:** `Second Wind / Durchschnaufen`, `Indomitable / Unbeugsam`
     - **`[+]` ⚔️ Offensive Skills & Maneuvers:** `Action Surge / Tatendrank`, `Pushing Attack / Stoßangriff`, `Trip Attack / Beinstellen`, `Riposte`, `Precision Attack / Präzisionsangriff`
     - **`[+]` 🎯 Movement & Positioning:** `Avoid Opportunity Attacks` (For Ranged Fighter), `Auto-Weapon Swap`
   - **Druid Options (`AI: Druid`):**
     - **`[+]` 🔮 Cantrips / Zaubertricks:** `Shillelagh / Zauberstock`, `Guidance / Göttliche Führung`, `Produce Flame`, `Thorn Whip`, `Poison Spray / Giftiger Stachel`, `Chill Touch / Kalte Hand`, `Resist Elements`
     - **`[+]` 💚 Healing & Restoration:** `Cure Wounds`, `Healing Word`, `Lesser Restoration`, `Goodberry`, `Create Food & Water`, `Revivify`
     - **`[+]` 🛡️ Protection & Buffs:** `Protection from Poison`, `Protection from Energy`, `Barkskin`, `Darkvision`, `Longstrider`, `Pass Without Trace`, `Jump`
     - **`[+]` ⚔️ Attack & Control Spells:** `Entangle`, `Faerie Fire`, `Fog Cloud`, `Animal Friendship`, `Charm Person`, `Detect Magic`, `Detect Poison`, `Flame Blade`, `Flaming Sphere`, `Heat Metal`, `Hold Person`, `Moonbeam`, `Spike Growth`, `Call Lightning`, `Dispel Magic`, `Sleet Storm`, `Wind Wall`, `Daylight`
     - **Wild Shape / Tiergestalt:** Toggle shape-shifting behavior independently.
4. **Strict Multi-Channel Spell & Power Blocking:**
   - Disabled spells and powers are intercepted across all engine channels (`RulesetSpellRepertoire`, `RulesetUsablePower`, `GameLocationActionManager.ExecuteAction`, and `RulesetCharacter.CastSpell/UsePower`).
   - Blocked actions are completely prevented from execution, forcing the AI to evaluate and perform alternative valid actions.
5. **Fighter Melee Out-of-Reach & Ranged Positioning:**
   - Melee Fighters automatically swap to ranged weapons when enemies are beyond 1-turn movement reach (> 6 cells), closing distance while firing.
   - Ranged Fighters seek high ground elevation ($Z$-coordinate) and preserve movement to enter shooting range when enemies are out of range.
6. **Emergency Low HP Protection:**
   - Automatically switches hero control back to the player if hit points drop below a configurable threshold (5% - 50% Max HP, default: **30%**).
7. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the active character.
8. **Automated Release & Version Alignment:**
   - Integrated Git `pre-commit` hook automatically aligns `Info.json` version and ZIP package name using commit count (`1.2.<CommitCount>`).

---

## 🛠️ Installation

1. Download the latest release `SolastaAI-v1.2.<version>.zip`.
2. Install via Unity Mod Manager or extract the `SolastaAI` folder into your Solasta `Mods` directory:
   ```text
   <Solasta Installation Directory>/Mods/SolastaAI/
   ```
3. Launch Solasta. Unity Mod Manager will load `SolastaAI` automatically.

---

## 💻 Building from Source

To compile `SolastaAI.dll`:

```bash
mcs -target:library \
  -r:"<Solasta_Data>/Managed/Assembly-CSharp.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.CoreModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.IMGUIModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.InputLegacyModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityModManager/UnityModManager.dll" \
  -r:"<Solasta_Data>/Managed/UnityModManager/0Harmony.dll" \
  Main.cs -out:SolastaAI.dll
```

---

## 📜 Repository & License
- Repository: [https://github.com/Alkhemeia/SolastaAI](https://github.com/Alkhemeia/SolastaAI)
- License: [MIT License](LICENSE)

