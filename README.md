# SolastaAI

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, Fighter & Druid class automation with individual spell & skill controls, emergency safety rules, automatic weapon swapping, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence & Update-Safety:**
   - Standalone mod with **zero dependencies** on third-party mods.
   - Operates directly via Solasta's native AI Decision Package engine (`TA.AI.DecisionPackageDefinition`).
2. **Structured Dropdown UI & Categorized Spell Controls:**
   - Convenient **Dropdown Selector** per hero e.g. `[ AI: Druid (Shillelagh) ▼ ]`.
   - **Categorized Individual Spell & Skill Controls:** Every single Druid spell and Fighter skill can be toggled on or off individually under clean thematic sub-categories!
   - Archetypes available per hero:
     - `Human (Player Control)`
     - `AI: Melee (Default)`
     - `AI: Range (Backup Melee)`
     - `AI: Caster (Backup Attacks)`
     - `AI: Cleric Combat`
     - `AI: Druid (Wild Shape)` - Support spellcasting and Tiergestalt (Wild Shape).
     - `AI: Druid (Shillelagh)` - Melee combat with Shillelagh, self-buffing with Guidance, cantrips, poison protection, and ally healing.
     - `AI: Fighter (Melee)` - Frontline melee aggression with automatic weapon swap.
     - `AI: Fighter (Ranged)` - Ranged archery positioning and opportunity attack prevention.
     - `AI: Mage Combat`
     - `AI: Rogue Combat`
3. **Categorized Spell & Skill Controls Per Mode:**
   - **Druid Modes:**
     - **💚 Healing & Restoration:** `Cure Wounds`, `Healing Word`, `Lesser Restoration`, `Goodberry`
     - **🛡️ Protection & Buffs:** `Shillelagh`, `Guidance`, `Protection from Poison`, `Barkskin`, `Longstrider`, `Pass Without Trace`
     - **⚔️ Attack & Crowd Control:** `Produce Flame`, `Thorn Whip`, `Poison Spray`, `Entangle`, `Faerie Fire`, `Flaming Sphere`, `Hold Person`, `Moonbeam`, `Spike Growth`, `Call Lightning`
   - **Fighter Modes:**
     - `[x] Use Second Wind / Durchschnaufen`
     - `[x] Use Action Surge / Tatendrank`
     - `[x] Avoid Opportunity Attacks` (For Ranged Fighters)
     - `[x] Auto-Weapon Swap`
4. **Automatic Weapon Swapping:**
   - Automatically evaluates tactical grid distances during a character's turn.
5. **Emergency Low HP Protection:**
   - Automatically switches hero control back to the player if their hit points drop below a configurable threshold (5% - 50% Max HP, default: **30%**).
6. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the currently active turn character.
7. **Persistent Storage:**
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
