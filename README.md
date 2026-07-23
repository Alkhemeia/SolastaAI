# SolastaAI - Standalone Better AI & Persistence Mod

A standalone Unity Mod Manager (UMM) mod for **Solasta: Crown of the Magister** that provides full AI control management, tactical behavior selection, emergency safety rules, and persistent character AI settings.

## 🌟 Key Features

1. **Complete Independence:** 
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
3. **Emergency HP Protection (Low HP Fallback):**
   - Automatically switches hero control back to the player if their hit points drop below **30%**, preventing accidental AI wipes.
4. **In-Combat Quick Hotkey (`N`):**
   - Press **`N`** during combat to instantly toggle AI / Manual control for the currently active turn character.
5. **Persistent Storage:**
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

## 🇩🇪 Deutsche Beschreibung

### SolastaAI - Eigenständige Erweiterte KI & Persistenz Mod

Eine eigenständige Mod für **Solasta: Crown of the Magister**, die Helden-KI-Steuerungen, taktische Verhaltensmuster, Notfall-Schutzregeln und dauerhafte Speicherung bietet.

* **Vollkommen Unabhängig:** Benötigt keine anderen Mods und bleibt nach Updates voll funktionsfähig.
* **Eigenes UMM-Menü:** Wähle im Mod Manager für jeden Charakter aus 7 Spezial-KI-Paketen (z. B. *Cleric Combat*, *Mage Combat*, *Rogue Combat*).
* **Notfall-Schutz:** Sinkt die Gesundheit eines Helden unter **30 % TP**, schaltet die Mod automatisch auf manuelle Steuerung zurück.
* **Hotkey `N` im Kampf:** Drücke `N`, um den aktiven Helden im Kampf sofort zwischen KI und manueller Steuerung umzuschalten.

---

## 📜 Repository & License
- Repository: [https://github.com/Alkhemeia/SolastaAI](https://github.com/Alkhemeia/SolastaAI)
- License: [MIT License](LICENSE)
