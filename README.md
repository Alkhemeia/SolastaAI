# SolastaAI - Standalone Better AI & Persistence Mod

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, emergency safety rules, automatic weapon swapping, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence & Update-Safety:**
   - Standalone mod with **zero dependencies** on Unfinished Business or third-party mods.
   - Operates directly via Solasta's native AI Decision Package engine (`TA.AI.DecisionPackageDefinition`).
2. **Dedicated Unity Mod Manager UI:**
   - Configure AI settings for each party member individually right inside the UMM options panel.
   - Choose tactical archetypes per hero:
     - `Human (Player Control)`
     - `AI: Melee (Default)`
     - `AI: Range (Backup Melee)`
     - `AI: Caster (Backup Attacks)`
     - `AI: Cleric Combat`
     - `AI: Fighter Combat`
     - `AI: Mage Combat`
     - `AI: Rogue Combat`
3. **Automatic Weapon Swapping:**
   - Automatically evaluates tactical grid distances during a character's turn.
   - If no enemy is reachable in melee range ($\le 2$ cells), the hero automatically switches to their secondary ranged weapon set (bow/crossbow) to attack from afar.
   - Automatically switches back to the melee weapon set when an enemy moves into melee reach.
4. **Emergency Low HP Protection:**
   - Automatically switches hero control back to the player if their hit points drop below a configurable threshold (5% - 50% Max HP, default: **30%**), preventing accidental AI wipes.
5. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the currently active turn character.
6. **Persistent Storage:**
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

To compile `SolastaAIPersistence.dll` on Linux or Windows:

```bash
mcs -target:library \
  -r:"<Solasta_Data>/Managed/Assembly-CSharp.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.CoreModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.IMGUIModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityEngine.InputLegacyModule.dll" \
  -r:"<Solasta_Data>/Managed/UnityModManager/UnityModManager.dll" \
  -r:"<Solasta_Data>/Managed/UnityModManager/0Harmony.dll" \
  Main.cs -out:SolastaAIPersistence.dll
```

---

## 📜 Repository & License
- Repository: [https://github.com/Alkhemeia/SolastaAI](https://github.com/Alkhemeia/SolastaAI)
- License: [MIT License](LICENSE)
