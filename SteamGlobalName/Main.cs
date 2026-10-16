using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SteamGlobalName
{
    [BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
    [BepInDependency("Nessie.ATLYSS.EasySettings", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> UseSteamName;

        const string LabelName = "_text_playerGlobalName";
        const string SelectCanvas = "_GameUI_MainMenu/_characterSelectMenu/Canvas_characterSelect";
        const string NicknameInput = "_dolly_characterManagement/_input_@nickname";

        readonly Dictionary<Player, string> originals = new Dictionary<Player, string>();
        readonly HashSet<ulong> requested = new HashSet<ulong>();

        float labelTimer, menuTimer;
        bool filledInMenu;

        void Awake()
        {
            Log = Logger;
            UseSteamName = Config.Bind("General", "Use Steam Name", true, "Set the global name from your Steam username. Off = use the local name.");

            if (Chainloader.PluginInfos.ContainsKey("Nessie.ATLYSS.EasySettings"))
                SettingsHook.Register(Config);

            Log.LogInfo($"{ModInfo.NAME} {ModInfo.VERSION} loaded");
        }

        void Update()
        {
            menuTimer += Time.deltaTime;
            if (menuTimer >= 0.25f)
            {
                menuTimer = 0f;
                CheckCharacterSelect();
            }

            labelTimer += Time.deltaTime;
            if (labelTimer >= 1f)
            {
                labelTimer = 0f;
                RefreshLabels();
            }
        }

        void CheckCharacterSelect()
        {
            // Find only returns active objects, so null means we're not in the menu
            var canvas = GameObject.Find(SelectCanvas);
            if (canvas == null)
            {
                filledInMenu = false;
                return;
            }
            if (filledInMenu || !UseSteamName.Value) return;

            var input = canvas.transform.Find(NicknameInput)?.GetComponent<InputField>();
            if (input == null)
            {
                Log.LogWarning("Nickname input not found in character select");
                filledInMenu = true;
                return;
            }

            string name = LocalSteamName();
            if (string.IsNullOrWhiteSpace(name)) return;

            input.text = name;
            filledInMenu = true;
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
            var label = FindChild(p.transform, LabelName);
            if (label == null) return;

            if (!UseSteamName.Value)
            {
                if (originals.TryGetValue(p, out var old))
                {
                    SetText(label.gameObject, old);
                    originals.Remove(p);
                }
                return;
            }

            if (!ulong.TryParse(p._steamID, out ulong id)) return;

            string name = p.isLocalPlayer ? LocalSteamName() : SteamNameFor(id);
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!originals.ContainsKey(p))
            {
                string current = GetText(label.gameObject);
                if (string.IsNullOrEmpty(current)) return;
                originals[p] = current;
            }

            // keep the @ if the game already puts one in this label
            string text = (originals[p].StartsWith("@") ? "@" : "") + name;
            if (GetText(label.gameObject) != text)
                SetText(label.gameObject, text);
        }

        static string LocalSteamName()
        {
            if (!Steamworks.SteamAPI.IsSteamRunning()) return null;
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

        // label is either TMP or UI.Text, just go through the text property
        static string GetText(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                var prop = c?.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.PropertyType == typeof(string)) return (string)prop.GetValue(c) ?? "";
            }
            return "";
        }

        static void SetText(GameObject go, string value)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                var prop = c?.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
                {
                    prop.SetValue(c, value);
                    return;
                }
            }
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }

    // separate class so EasySettings types only load when it's installed
    static class SettingsHook
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(ConfigFile config)
        {
            Nessie.ATLYSS.EasySettings.Settings.OnInitialized.AddListener(() =>
            {
                var tab = Nessie.ATLYSS.EasySettings.Settings.GetOrAddCustomTab(ModInfo.NAME);
                tab.AddHeader("Global Name");
                tab.AddToggle(Plugin.UseSteamName);
            });

            Nessie.ATLYSS.EasySettings.Settings.OnApplySettings.AddListener(() =>
            {
                config.Save();
                Object.FindObjectOfType<Plugin>()?.RefreshLabels();
            });
        }
    }
}