# SolastaAI

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, Fighter class automation, emergency safety rules, automatic weapon swapping, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence & Update-Safety:**
   - Standalone mod with **zero dependencies** on third-party mods.
   - Operates directly via Solasta's native AI Decision Package engine (`TA.AI.DecisionPackageDefinition`).
2. **Structured & Modern Unity Mod Manager UI:**
   - Clearly categorized into 3 sections:
     - 🛡️ **Global Safety & Hotkey Settings** (Emergency Low HP Fallback, Combat Hotkey `'N'`, Guest AI).
     - ⚔️ **Class Tactics & Combat Intelligence** (Fighter Skill Automation, Opportunity Attack Protection, Auto-Weapon Swap).
     - 👥 **Party Character AI Archetype Selection** (Per-hero AI assignment grid).
3. **Toggleable Opportunity Attack Protection:**
   - Option: `Enable Opportunity Attack Protection for Ranged Fighters`.
   - When enabled, Ranged Fighters will automatically equip melee weapons to defeat adjacent enemies first, preventing them from provoking opportunity attacks.
4. **Fighter Class Tactics & Skill Automation:**
   - **Second Wind / Durchschnaufen:** Automatically triggered when a Fighter's HP drops below **60%** to heal.
   - **Action Surge / Tatendrank:** Automatically activated during combat to grant extra actions and attacks.
5. **Automatic Weapon Swapping:**
   - Automatically evaluates tactical grid distances during a character's turn.
   - If no enemy is reachable in melee range ($\le 2$ cells), the hero automatically switches to their secondary ranged weapon set (bow/crossbow) to attack from afar.
6. **Emergency Low HP Protection:**
   - Automatically switches hero control back to the player if their hit points drop below a configurable threshold (5% - 50% Max HP, default: **30%**), preventing accidental AI wipes.
7. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the currently active turn character.
8. **Persistent Storage:**
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
