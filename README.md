# SolastaAI

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, Fighter & Druid class automation, emergency safety rules, automatic weapon swapping, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence & Update-Safety:**
   - Standalone mod with **zero dependencies** on third-party mods.
   - Operates directly via Solasta's native AI Decision Package engine (`TA.AI.DecisionPackageDefinition`).
2. **Structured Dropdown UI & Dynamic Mode Settings:**
   - Convenient **Dropdown Selector** per hero e.g. `[ AI: Druid (Shillelagh) ▼ ]`.
   - Displays **dynamic sub-settings** directly below each hero based on their chosen AI mode.
   - Archetypes available per hero:
     - `Human (Player Control)`
     - `AI: Melee (Default)`
     - `AI: Range (Backup Melee)`
     - `AI: Caster (Backup Attacks)`
     - `AI: Cleric Combat`
     - `AI: Druid (Wild Shape)` - Focused on support spellcasting and Tiergestalt (Wild Shape).
     - `AI: Druid (Shillelagh)` - Focused on melee combat with Shillelagh, self-buffing with Guidance, and optional ally healing.
     - `AI: Fighter (Melee)` - Focused on frontline melee aggression with automatic weapon swap.
     - `AI: Fighter (Ranged)` - Focused on ranged archery positioning and opportunity attack prevention.
     - `AI: Mage Combat`
     - `AI: Rogue Combat`
3. **Shillelagh Druid Melee Automation:**
   - **Shillelagh / Zauberstock:** Automatically casts Shillelagh on the primary melee weapon (no Wild Shape transformation!).
   - **Guidance / Göttliche Führung:** When out of melee reach ($> 2$ cells), casts Guidance on self while advancing towards target.
   - **Ranged Cantrips:** Uses ranged cantrips (Produce Flame, etc.) if Guidance is absent or out of melee reach.
   - **Toggleable Ally Healing:** Automatically heals wounded allies ($< 50\%$ HP) if healing toggle is enabled in settings.
4. **Fighter Class Automation:**
   - **Second Wind / Durchschnaufen:** Automatically triggered when HP drops below **60%**.
   - **Action Surge / Tatendrank:** Automatically activated during combat for extra actions.
5. **Toggleable Opportunity Attack Protection:**
   - When enabled, Ranged Fighters will automatically equip melee weapons to defeat adjacent enemies first, preventing opportunity attacks.
6. **Automatic Weapon Swapping:**
   - Automatically evaluates tactical grid distances during a character's turn.
7. **Emergency Low HP Protection:**
   - Automatically switches hero control back to the player if their hit points drop below a configurable threshold (5% - 50% Max HP, default: **30%**).
8. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the currently active turn character.
9. **Persistent Storage:**
   - Remembers choices automatically across map transitions, battle start/stop, and save game reloads via `SavedAIControllers.json`.

---

## 🛠️ Installation

1. Download the latest release of `SolastaAI`.
2. Extract the `SolastaAI` folder into your Solasta `Mods` directory:
   ```text
   <Solasta Installation Directory>/Mods/SolastaAI/
   ```
3. Launch Solasta. Unity Mod Manager will load `SolastaAI` automatically.

---

## 💻 Building from Source

To compile `SolastaAI.dll` on Linux or Windows:

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
