# Keybinded Filter Mod
<img width="1411" height="652" alt="KeybindedFilter" src="https://github.com/user-attachments/assets/f2b207ce-4391-49cf-8fad-6430eff7de71" />

The Keybinded Filter Mod provides hotkey-driven control over Nuclear Option's targeting system, removing the need to repeatedly cycle through the filter list in the heat of combat.

# Features:
- Instant Context Switching: Bind specific keys to "Air", "Ground", or "All" to immediately instruct your targeting computer to filter for those exact threats.
- Custom Presets: Create up to 10 fully custom targeting profiles (e.g., "SEAD", "Anti-Ship", "Only MBTs") and assign them to dedicated hotkeys.

# How to create and use custom presets:
1. In the game, configure your MFD Targeting Filter screen exactly how you want it (e.g., turn off everything except Enemy, Ship, and Anti-Ship Missiles).
2. Open the F1 BepInEx Configuration Manager menu.
3. Under the "Keybinded Filter Mod" section, type a name for your preset in the "New Preset Name" box (e.g., "SEAD").
4. Click the "Save Current Filter as Preset" button.
5. In the same menu, look for "Preset 0 File" to "Preset 9 File". Click the dropdown next to the preset slot you want to assign, and select your newly saved "Anti-Ship" preset.
6. Open your game options, go to the Keybinds menu under "Debug" or the bottom-most category, and assign a hotkey to "FilterPreset0" (or whichever number you chose).
7. Press the hotkey in combat to instantly switch to that custom filter state!

# Tidbits:
This mod was coded with performance and client-side in mind. Unlike other mods that perform heavy hierarchy scans, this mod interacts directly with the active MFD UI buttons exactly as a player would, but instantly. Filters use matching based on predefined optimal JSON templates, ensuring that pressing a filter hotkey never causes framerate stutter or desync.
