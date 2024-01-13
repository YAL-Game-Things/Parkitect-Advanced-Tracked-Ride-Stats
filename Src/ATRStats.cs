using System;
using System.Collections.Generic;
using System.Linq;
using Parkitect;
using UnityEngine;
using HarmonyLib;
using System.Windows.Markup;
using System.IO;
using MiniJSON;
using Mono.Security.Authenticode;

namespace AdvancedTrackedRideStats {
    public class ATRStats : AbstractMod, IModSettings
    {
        public const string VERSION_NUMBER = "1.0.1";
        public override string getIdentifier() => "cc.yal.AdvancedTrackedRideStats";
        public override string getName() => "Advanced Tracked Ride Stats";
        public override string getDescription() => @"Shows excitement contribution breakdown";

        public override string getVersionNumber() => VERSION_NUMBER;
        public override bool isMultiplayerModeCompatible() => true;
        public override bool isRequiredByAllPlayersInMultiplayerMode() => false;

        private string _modPath;
        public string _settingsFilePath;

        public static ATRStats Instance;
        private Harmony _harmony;
        private string editNearFactor = "";

        public override void onEnabled() {
            Instance = this;

            Debug.LogWarning("[ATRS] Loading!");

            _modPath = ModManager.Instance.getMod(this.getIdentifier()).path;
            _settingsFilePath = System.IO.Path.Combine(_modPath, "AdvancedTrackedRideStats.json");

            reloadSettingsFromFile();

            Debug.Log("[ATRS] Doing a Harmony patch!");
			_harmony = new Harmony(getIdentifier());
			_harmony.PatchAll();
			Debug.Log("[ATRS] Patched alright!");
		}

        public override void onDisabled() {
            _harmony?.UnpatchAll(getIdentifier());
			AnimationCurveCache.cache.Clear();
		}

        public void onDrawSettingsUI() {
			// GUI settings style
			GUIStyle guistyleTextLeft = new GUIStyle(GUI.skin.label);
			guistyleTextLeft.margin = new RectOffset(10, 10, 10, 0);
			guistyleTextLeft.alignment = TextAnchor.MiddleLeft;

			GUIStyle guistyleTextMiddle = new GUIStyle(GUI.skin.label);
			guistyleTextMiddle.margin = new RectOffset(0, 10, 10, 0);
			guistyleTextMiddle.alignment = TextAnchor.MiddleCenter;

			GUIStyle guistyleField = new GUIStyle(GUI.skin.textField);
			guistyleField.margin = new RectOffset(0, 10, 10, 0);
			guistyleField.alignment = TextAnchor.MiddleCenter;

			GUIStyle guistyleButton = new GUIStyle(GUI.skin.button);
			guistyleButton.margin = new RectOffset(0, 10, 10, 0);
			guistyleButton.alignment = TextAnchor.MiddleCenter;

			// GUI settings layout
			GUILayout.BeginVertical();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Version", guistyleTextLeft, GUILayout.Width(200));
			GUILayout.Label(VERSION_NUMBER, guistyleTextMiddle);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Near threshold", guistyleTextLeft, GUILayout.Width(200));
			editNearFactor = GUILayout.TextField(editNearFactor, 7, guistyleField);
			GUILayout.EndHorizontal();

			// Check the values when enter is pressed
			if (Event.current.isKey && Event.current.keyCode == KeyCode.Return) {
				// Try to convert the input text to a float
				if (float.TryParse(editNearFactor, out float result)) {
					ATRStatsConfig.Instance.nearFactor = Mathf.Clamp(result, 0, 1);
				}
				// Clear the focus from the TextField
				GUI.FocusControl(null);
			}

			GUILayout.EndVertical();
		}

        public void onSettingsOpened() {
            reloadSettingsFromFile();
        }

        public void onSettingsClosed() {
            saveSettingsToFile();
            GUI.FocusControl(null);
        }

        private void reloadSettingsFromFile() {
            if (File.Exists(_settingsFilePath)) {
                // Load existing settings from JSON file
                Debug.Log("[ATRS] Loading config!");
                string json = File.ReadAllText(_settingsFilePath);
				ATRStatsConfig.Instance = JsonUtility.FromJson<ATRStatsConfig>(json);
            } else {
                // Create new settings with default values
                Debug.Log("[ATRS] Creating a new config!");
                ATRStatsConfig.Instance = new ATRStatsConfig();
                saveSettingsToFile();
            }
            editNearFactor = ATRStatsConfig.Instance.nearFactor.ToString();
        }

        private void saveSettingsToFile()
        {
            Debug.Log("[ATRS] Saving config!");
            string json = JsonUtility.ToJson(ATRStatsConfig.Instance, true);
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}
