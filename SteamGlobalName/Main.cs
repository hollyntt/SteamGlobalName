using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Nessie.ATLYSS.EasySettings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SteamGlobalName
{
    [BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
    [BepInDependency("Nessie.ATLYSS.EasySettings", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin? Instance;
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> UseSteamName;

        readonly Dictionary<Player, string> originals = new Dictionary<Player, string>();
        readonly HashSet<ulong> requested = new HashSet<ulong>();
        ConfigEntry<string>? savedOriginalNickname;
        ConfigEntry<bool>? hasSavedOriginalNickname;

        float labelTimer;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            UseSteamName = Config.Bind("General", "Use Steam Name", true, "Set the global name from your Steam username. Off = use the local name.");
            savedOriginalNickname = Config.Bind("Internal", "Original Nickname", "", "Nickname saved before applying the Steam name.");
            hasSavedOriginalNickname = Config.Bind("Internal", "Has Original Nickname", false, "Whether an original nickname is available to restore.");
            var harmony = new Harmony(ModInfo.GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            var patchedMethods = harmony.GetPatchedMethods()
                .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
                .ToArray();
            Log.LogInfo($"Installed Harmony patches: {string.Join(", ", patchedMethods)}");
            if (patchedMethods.Length == 0)
                Log.LogError("Harmony did not install any patches");

            Settings.OnInitialized.AddListener(AddSettings);
            Settings.OnApplySettings.AddListener(() =>
            {
                Log.LogInfo("Settings applied; refreshing labels and queuing nickname update");
                Config.Save();
                RefreshLabels();

                var characterSelect = Object.FindObjectOfType<CharacterSelectManager>();
                QueueApplySteamName(characterSelect);
            });

            Log.LogInfo($"{ModInfo.NAME} {ModInfo.VERSION} loaded");
        }

        void AddSettings()
        {
            SettingsTab tab = Settings.GetOrAddCustomTab(ModInfo.NAME);
            tab.AddHeader("Global Name");
            tab.AddToggle(UseSteamName);
        }

        void Update()
        {
            labelTimer += Time.deltaTime;
            if (labelTimer >= 1f)
            {
                labelTimer = 0f;
                RefreshLabels();
            }
        }

        internal void ApplySteamName(CharacterSelectManager characterSelect)
        {
            if (characterSelect == null)
            {
                Log.LogWarning("Cannot apply Steam name: CharacterSelectManager was not found");
                return;
            }

            var input = characterSelect._globalNickanmeInput;
            if (input == null)
            {
                Log.LogWarning("Character select nickname input is not available");
                return;
            }

            if (savedOriginalNickname == null || hasSavedOriginalNickname == null)
            {
                Log.LogError("Original nickname configuration is not initialized");
                return;
            }

            if (!UseSteamName.Value)
            {
                Log.LogInfo("Steam global nickname setting is disabled");
                if (hasSavedOriginalNickname.Value)
                {
                    string original = savedOriginalNickname.Value;
                    Log.LogInfo($"Restoring nickname to '{original}' (input currently '{input.text}')");
                    SetInputText(input, original, activate: false);
                    savedOriginalNickname.Value = "";
                    hasSavedOriginalNickname.Value = false;
                    Config.Save();
                }
                else
                {
                    Log.LogInfo("Steam name is disabled, but no original nickname is stored");
                }
                return;
            }

            string name = LocalSteamName();
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.LogWarning("Steam persona name is unavailable; cannot set the character select nickname");
                return;
            }

            if (!hasSavedOriginalNickname.Value && input.text != name)
            {
                savedOriginalNickname.Value = input.text;
                hasSavedOriginalNickname.Value = true;
                Config.Save();
                Log.LogInfo($"Saved original global nickname '{input.text}'");
            }
            else if (!hasSavedOriginalNickname.Value)
            {
                Log.LogInfo("Input already matches the Steam name; no original nickname was captured");
            }

            SetInputText(input, name, activate: true);
            Log.LogInfo($"Set character select global nickname to '{name}'");
        }

        internal void QueueApplySteamName(CharacterSelectManager? characterSelect)
        {
            StartCoroutine(ApplySteamNameAfterMenuUpdate(characterSelect));
        }

        IEnumerator ApplySteamNameAfterMenuUpdate(CharacterSelectManager? characterSelect)
        {
            yield return null;
            yield return new WaitForSeconds(0.25f);

            if (characterSelect == null)
            {
                characterSelect = Object.FindObjectOfType<CharacterSelectManager>();
                if (characterSelect == null)
                {
                    Log.LogWarning("CharacterSelectManager not found after waiting for the main menu to update");
                    yield break;
                }
            }

            ApplySteamName(characterSelect);
        }

        static void SetInputText(InputField input, string text, bool activate)
        {
            if (input.text == text) return;

            if (activate)
                input.ActivateInputField();

            input.text = text;
        }

        internal void RefreshLabels()
        {
            foreach (var p in FindObjectsOfType<Player>())
                if (p != null) UpdateLabel(p);

            foreach (var gone in originals.Keys.Where(k => k == null).ToList())
                originals.Remove(gone);
        }

        void UpdateLabel(Player p)
        {
            if (!UseSteamName.Value)
            {
                if (originals.TryGetValue(p, out var old))
                {
                    p._globalNickname = old;
                    originals.Remove(p);
                }
                return;
            }

            if (!ulong.TryParse(p._steamID, out ulong id)) return;

            string name = p.isLocalPlayer ? LocalSteamName() : SteamNameFor(id);
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!originals.ContainsKey(p))
            {
                string current = p._globalNickname;
                if (string.IsNullOrEmpty(current)) return;
                originals[p] = current;
            }

            if (p._globalNickname != name)
                p._globalNickname = name;
        }

        static string LocalSteamName()
        {
            if (!SteamManager.Initialized) return null;
            return Steamworks.SteamFriends.GetPersonaName();
        }

        string SteamNameFor(ulong id)
        {
            if (!Steamworks.SteamAPI.IsSteamRunning()) return null;

            var steamId = new Steamworks.CSteamID(id);
            string name = Steamworks.SteamFriends.GetFriendPersonaName(steamId);

            // people who aren't friends need to be requested first
            if (string.IsNullOrWhiteSpace(name) || name == "[unknown]")
            {
                if (requested.Add(id)) Steamworks.SteamFriends.RequestUserInformation(steamId, true);
                return null;
            }
            return name;
        }
    }

    [HarmonyPatch]
    public static class GameStatePatches
    {
        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Set_MenuCondition))]
        [HarmonyPostfix]
        public static void OnSetMenuCondition(MainMenuManager __instance)
        {
            if (__instance._mainMenuCondition != MainMenuCondition.CharacterSelect)
                return;

            Plugin.Log.LogInfo("Main menu entered character select");
            var characterSelect = __instance._characterSelectManager;
            if (Plugin.Instance == null)
            {
                Plugin.Log.LogWarning("Plugin.Instance is null; cannot apply Steam name");
                return;
            }

            if (characterSelect == null)
                Plugin.Log.LogWarning("MainMenuManager._characterSelectManager is null; will search after a delay");

            Plugin.Instance.QueueApplySteamName(characterSelect);
        }

        [HarmonyPatch(typeof(MainMenuManager), "Load_HostSettings")]
        [HarmonyPostfix]
        public static void OnLoadHostSettings(MainMenuManager __instance)
        {
            if (Plugin.Instance == null)
            {
                Plugin.Log.LogWarning("Plugin.Instance is null after host settings loaded");
                return;
            }

            var characterSelect = __instance._characterSelectManager;
            Plugin.Instance.QueueApplySteamName(characterSelect);
        }

        [HarmonyPatch(typeof(ProfileDataSender), "OnSend_CharacterFileData")]
        [HarmonyPostfix]
        public static void OnSendCharacterFileData()
        {
            var characterSelect = Object.FindObjectOfType<CharacterSelectManager>();
            if (Plugin.Instance == null)
            {
                Plugin.Log.LogWarning("Plugin.Instance is null after character file data was sent");
                return;
            }

            Plugin.Instance.QueueApplySteamName(characterSelect);
        }
    }

}