using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace KeybindedFilterMod
{
    [BepInPlugin("com.KeybindedFilterMod", "Keybinded Filter Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static BepInEx.Logging.ManualLogSource Log;

        // Preset configs
        public static ConfigEntry<string>[] PresetConfigs = new ConfigEntry<string>[10];
        
        // Save preset UI
        public static ConfigEntry<string> NewPresetName;
        public static ConfigEntry<bool> SavePresetTrigger;

        public static string PresetDirectory => Path.Combine(Paths.PluginPath, "KeybindedFilterMod", "filter-preset");

        private void Awake()
        {
            Log = Logger;

            if (!Directory.Exists(PresetDirectory))
            {
                Directory.CreateDirectory(PresetDirectory);
            }

            // Register default filters
            ExtraInputManager.LoadPendingActions();
            ExtraInputManager.RegisterAction("FilterAir", Rewired.InputActionType.Button);
            ExtraInputManager.RegisterAction("FilterGround", Rewired.InputActionType.Button);
            ExtraInputManager.RegisterAction("FilterAll", Rewired.InputActionType.Button);

            // Register Presets 0-9
            for (int i = 0; i < 10; i++)
            {
                ExtraInputManager.RegisterAction($"FilterPreset{i}", Rewired.InputActionType.Button);
                
                PresetConfigs[i] = Config.Bind(
                    "Presets", 
                    $"Preset {i} File", 
                    "", 
                    new ConfigDescription($"The filter preset file to load when pressing Preset {i} hotkey.", 
                    null, 
                    new ConfigurationManagerAttributes { CustomDrawer = PresetDropdownDrawer })
                );
            }

            // Save feature
            NewPresetName = Config.Bind("Save Preset", "New Preset Name", "MyPreset", "Type the name for the new preset here.");
            SavePresetTrigger = Config.Bind("Save Preset", "Save Current Filter", false, 
                new ConfigDescription("Click to save current filter state to the name above.", 
                null, 
                new ConfigurationManagerAttributes { CustomDrawer = SavePresetDrawer }));

            try
            {
                Harmony harmony = new Harmony("com.KeybindedFilterMod");
                harmony.PatchAll();
                Log.LogInfo("Keybinded Filter Mod loaded and keybinds registered!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch KeybindedFilterMod: {ex}");
            }
        }

        private static bool showDropdown = false;
        private static string activeDropdownKey = "";
        private static string[] cachedFiles = new string[0];

        private static void PresetDropdownDrawer(ConfigEntryBase entry)
        {
            string currentValue = (string)entry.BoxedValue;
            
            GUILayout.BeginVertical();
            if (GUILayout.Button(string.IsNullOrEmpty(currentValue) ? "Select Preset..." : currentValue, GUILayout.ExpandWidth(true)))
            {
                if (activeDropdownKey == entry.Definition.Key)
                {
                    showDropdown = !showDropdown;
                }
                else
                {
                    activeDropdownKey = entry.Definition.Key;
                    showDropdown = true;
                    if (Directory.Exists(PresetDirectory))
                    {
                        cachedFiles = Directory.GetFiles(PresetDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).ToArray();
                    }
                }
            }

            if (showDropdown && activeDropdownKey == entry.Definition.Key)
            {
                if (cachedFiles.Length == 0)
                {
                    GUILayout.Label("No presets found in filter-preset folder.");
                }
                else
                {
                    foreach (var f in cachedFiles)
                    {
                        if (GUILayout.Button(f))
                        {
                            entry.BoxedValue = f;
                            showDropdown = false;
                        }
                    }
                }
                if (GUILayout.Button("Clear / None"))
                {
                    entry.BoxedValue = "";
                    showDropdown = false;
                }
            }
            GUILayout.EndVertical();
        }

        private static void SavePresetDrawer(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Save Current Filter as Preset", GUILayout.ExpandWidth(true)))
            {
                SaveCurrentFilterState(NewPresetName.Value);
            }
        }

        public static void SaveCurrentFilterState(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;
            string path = Path.Combine(PresetDirectory, presetName + ".json");
            
            var selector = FindObjectOfType<TargetListSelector>();
            if (selector != null)
            {
                // We could serialize the exact list of toggles that are active
                List<string> activeToggles = new List<string>();
                
                var allToggles = new List<TargetListSelector_ToggleButton>();
                if (selector.toggleFactionItems != null) allToggles.AddRange(selector.toggleFactionItems);
                if (selector.toggleUnitTypesItems != null) allToggles.AddRange(selector.toggleUnitTypesItems);
                if (selector.toggleVehicleTypesItems != null) allToggles.AddRange(selector.toggleVehicleTypesItems);

                foreach (var toggle in allToggles)
                {
                    if (toggle.status) // status is the boolean field we dumped
                    {
                        activeToggles.Add(toggle.gameObject.name);
                    }
                }
                
                string json = "{\"ActiveToggles\": [\"" + string.Join("\", \"", activeToggles) + "\"]}";
                File.WriteAllText(path, json);
                
                Log.LogInfo($"Saved current filter state to {path}");
            }
            else
            {
                Log.LogWarning("No TargetListSelector found to save state.");
            }
        }

        public static void LoadFilterPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;
            string path = Path.Combine(PresetDirectory, presetName + ".json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                Log.LogInfo($"Loading preset {presetName}: \n{json}");
                // We apply the specific filter strings based on the presets
                var selector = FindObjectOfType<TargetListSelector>();
                if (selector != null)
                {
                    var allToggles = new List<TargetListSelector_ToggleButton>();
                    if (selector.toggleFactionItems != null) allToggles.AddRange(selector.toggleFactionItems);
                    if (selector.toggleUnitTypesItems != null) allToggles.AddRange(selector.toggleUnitTypesItems);
                    if (selector.toggleVehicleTypesItems != null) allToggles.AddRange(selector.toggleVehicleTypesItems);

                    // Unset all filters first
                    foreach (var toggle in allToggles)
                    {
                        toggle.Set(false);
                    }

                    // Try to parse the ActiveToggles list
                    List<string> activeToggles = new List<string>();
                    try 
                    {
                        // Minimal parsing to avoid full JSON library dependency
                        string[] parts = json.Split('"');
                        for (int i = 0; i < parts.Length; i++) {
                            if (parts[i] == "ActiveToggles") {
                                // Extract the array values
                                for (int j = i + 1; j < parts.Length; j++) {
                                    if (parts[j] == "]") break;
                                    if (parts[j] != " " && parts[j] != ":" && parts[j] != "[" && parts[j] != ", " && !parts[j].Contains("}")) {
                                        if (parts[j].Trim().Length > 0 && parts[j] != ",")
                                        {
                                            activeToggles.Add(parts[j]);
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    } catch {}

                    foreach (var toggle in allToggles)
                    {
                        if (activeToggles.Contains(toggle.gameObject.name))
                        {
                            toggle.Set(true);
                        }
                    }
                    
                    Log.LogInfo($"Applied filter preset: {presetName}");
                }
                else
                {
                    Log.LogWarning($"Could not find TargetListSelector to apply filter: {presetName}");
                }
            }
            else
            {
                Log.LogWarning($"Preset file not found: {path}");
            }
        }

        private static readonly string[] DefaultAir = new string[] { "EnemyToggle", "MissileToggle", "AircraftToggle" };
        private static readonly string[] DefaultGround = new string[] { "EnemyToggle", "VehicleToggle", "BuildingToggle", "ShipToggle", "Item_TRUCK", "Item_UGV", "Item_LCV", "Item_AFV", "Item_MBT", "Item_ART", "Item_AAA", "Item_IR_SAM", "Item_R_SAM", "Item_RDR" };

        private void SetFilterRaw(string filterType)
        {
            var selector = FindObjectOfType<TargetListSelector>();
            if (selector == null) return;
            
            var allToggles = new List<TargetListSelector_ToggleButton>();
            if (selector.toggleFactionItems != null) allToggles.AddRange(selector.toggleFactionItems);
            if (selector.toggleUnitTypesItems != null) allToggles.AddRange(selector.toggleUnitTypesItems);
            if (selector.toggleVehicleTypesItems != null) allToggles.AddRange(selector.toggleVehicleTypesItems);

            string[] targetToggles = null;
            if (filterType == "Air") targetToggles = DefaultAir;
            else if (filterType == "Ground") targetToggles = DefaultGround;

            for (int i = 0; i < allToggles.Count; i++)
            {
                var toggle = allToggles[i];
                if (filterType == "All") 
                {
                    toggle.Set(true);
                }
                else 
                {
                    // Cache the name string to avoid multiple native C++ allocations (Lesson 3)
                    string name = toggle.gameObject.name;
                    bool isTarget = false;
                    
                    if (targetToggles != null)
                    {
                        // Use exact ordinal matching implicitly via Array.IndexOf/Contains
                        for (int j = 0; j < targetToggles.Length; j++)
                        {
                            if (string.Equals(name, targetToggles[j], StringComparison.Ordinal))
                            {
                                isTarget = true;
                                break;
                            }
                        }
                    }
                    toggle.Set(isTarget);
                }
            }
        }

        private void Update()
        {
            if (!ExtraInputManager.RewiredInitialized) return;

            bool inChat = false;
            try { inChat = CursorManager.GetFlag(CursorFlags.Chat); } catch {}
            if (inChat) return;

            Rewired.Player localPlayer = ReInput.players.GetPlayer(0);
            if (localPlayer == null) return;

            // Base Filters
            if (localPlayer.GetButtonDown("FilterAir")) { Log.LogInfo("Air Filter"); SetFilterRaw("Air"); }
            if (localPlayer.GetButtonDown("FilterGround")) { Log.LogInfo("Ground Filter"); SetFilterRaw("Ground"); }
            if (localPlayer.GetButtonDown("FilterAll")) { Log.LogInfo("All Filter"); SetFilterRaw("All"); }

            // Preset Filters 0-9
            for (int i = 0; i < 10; i++)
            {
                if (localPlayer.GetButtonDown($"FilterPreset{i}"))
                {
                    Log.LogInfo($"Filter Preset {i} Hotkey Pressed");
                    LoadFilterPreset(PresetConfigs[i].Value);
                }
            }
        }
    }
}
