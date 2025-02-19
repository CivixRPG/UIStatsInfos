using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine.AzureSky;
using Steamworks;
using System.Globalization;
using HMLLibrary;
using RaftModLoader;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.SceneManagement;
using System.Text;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace UIStatsInfos
{
    public class Main : Mod
    {
        Harmony harmony;
        public static List<Object> created = new List<Object>();
        public static CanvasHelper canvas => ComponentManager<CanvasHelper>.Value;
        public static Text textBase => canvas.dropText;
        public static JsonModInfo modInfo;
        public static Sprite compass;
        public static float playerHealthUpdateTime = 0;
        public static List<float> sharkTime = new List<float>();
        public const int ModUtils_Channel = 127;
        public const Messages MessageId = (Messages)5004;
        public const int healthCall = 0;
        public static Transform equipDisplay;
        public static bool updatePlayerHealth
        {
            get
            {
                playerHealthUpdateTime += Time.deltaTime;
                if (playerHealthUpdateTime > 1)
                {
                    playerHealthUpdateTime--;
                    return true;
                }
                return false;
            }
        }
        public static float aspectRatio
        {
            get
            {
                return (float)Screen.width / Screen.height;
            }
        }
        public static GameObject dmgDisplay
        {
            set
            {
                dmgObject = value;
                dmgOffset = dmgObject.GetComponent<RectTransform>();
                dmgStart = dmgOffset.anchoredPosition;
                dmgText = dmgObject.GetComponent<Text>();
            }
            get
            {
                return dmgObject;
            }
        }
        public static RectTransform dmgOffset;
        public static Vector2 dmgStart;
        public static Text dmgText;
        static GameObject dmgObject;
        public static float dmgTime = 0f;
        static List<int> storyNotes = new int[] { 901, 2, 17, 902, 43, 53, 60, 68 }.ToList();
        static List<string> objectiveNames = new string[] { "Construire un récepteur", "Tour radio", "Bateau de croisière", "Île Balboa", "Ville de caravanes", "Tangaroa", "Pointe Varuna", "Tempérance", "Utopia" }.ToList();
        static Dictionary<string, StatManager> boolSettings = new Dictionary<string, StatManager>
        {
            { "weather", new StatManager ("Weather is ", setWeatherVisible) },
            { "weathertimer", new StatManager ("Weather timer is ", setWeatherTimer) },
            { "sharktimer", new StatManager ("Shark respawn timer is ", setSharkTimer) },
            { "fullhealth", new StatManager ("Full health bars are ", setFullBars) },
            { "objective", new StatManager ("Story objective is ", setShowObjective) },
            { "uses", new StatManager ("Remaining item uses are ", setShowUses) },
            { "equipment", new StatManager ("Equipment is ", setHUDEquip) },
            { "weight", new StatManager ("Raft foundation count is ", setShowWeight) },
            { "speed", new StatManager ("Speed display is ", setShowSpeed) },
            { "itemids", new StatManager ("Item IDs are ", setShowIds) },
            { "raftcenter", new StatManager ("Raft's center is ", setShowCenter) },
            { "compass", new StatManager ("Compass is ", setShowCompass) },
            { "sharkattack", new StatManager ("Shark attack alert is ", setSharkAttack) },
            { "sharkcharge", new StatManager ("Shark charge alert is ", setSharkCharge) },
            { "sharksearch", new StatManager ("Shark search alert is ", setSharkSearch) },
            { "sharkalertfollow", new StatManager ("Shark alert is ", setSharkAlertFollow, "now locked on screen", "already locked on screen","no longer locked on screen", "already unlocked from the screen") }
        };
        static Clock clockCache;
        public static bool timeIsClock
        {
            get
            {
                int s = clockSetting;
                if (s == 3)
                {
                    if (clockCache)
                        return true;
                    if (ComponentManager<Raft>.Value)
                        return clockCache = ComponentManager<Raft>.Value.GetComponentInChildren<Clock>();
                }
                return s == 2;
            }
        }
        public static int clockSetting = 1;
        public static bool showFullBars = true;
        public static StatMode statSetting = StatMode.Decimal;
        public static bool showWeather = true;
        public static bool showWeatherTimer = false;
        public static bool showSharkRespawn = true;
        public static bool showHUDEquipment = true;
        public static bool showStoryObjective = true;
        public static bool showDurability = true;
        public static bool showFoundations = true;
        public static bool showCenter = false;
        public static Color32 center = new Color32(0,255,0,255);
        public static bool showCompass = true;
        public static bool showSpeed = true;
        public static SharkMode sharkMode = SharkMode.WhileAttacking | SharkMode.WhileCharging;
        public static bool sharkAlertScreen = false;
        public static Color32 attacking = new Color32(255, 0, 0, 255);
        public static Color32 charging = new Color32(255, 255, 0, 255);
        public static Color32 searching = new Color32(0, 255, 0, 255);
        public static float barDrawDistance = 10;
        public static float HUDPositionX = 50;
        public static float HUDPositionY = 50;
        public static bool showItemIds = false;

        public static HashSet<string> exclusions = new HashSet<string>();
        public void Start()
        {
            modInfo = modlistEntry.jsonmodinfo;
            try
            {
                compass = LoadImage("compass.png", true, FilterMode.Bilinear).ToSprite();
            }
            catch (Exception e)
            {
                ErrorLog(e);
            }
            (harmony = new Harmony("com.aidanamite.UIStatsInfos")).PatchAll();
            if (canvas)
                Patch_CanvasHelper.Start(canvas);
            Network_Player player = RAPI.GetLocalPlayer();
            if (player)
            {
                foreach (var slot in player.Inventory.equipSlots)
                    if (slot.HasValidItemInstance())
                        Patch_PlayerEquipment.Equip(player.PlayerEquipment, slot.itemInstance, slot);
                foreach (var entity in Resources.FindObjectsOfTypeAll<Network_Entity>())
                    if (entity.Network)
                        Patch_NetworkEntity.Start(entity);
            }
            foreach (var inv in Resources.FindObjectsOfTypeAll<Inventory>())
                if (Traverse.Create(inv).Field("canvas").GetValue<CanvasHelper>())
                    Patch_Inventory.Start(inv);
            foreach (var shark in Resources.FindObjectsOfTypeAll<AI_StateMachine_Shark>())
                if (shark.network)
                    Patch_SharkSpawn.Postfix(shark);
            Log("Mod has been loaded!");
        }

        public Texture2D LoadImage(string filename, bool removeMipMaps = false, FilterMode? mode = null, TextureWrapMode? wrap = null, bool leaveReadable = true)
        {
            var t = new Texture2D(0, 0);
            t.LoadImage(GetEmbeddedFileBytes(filename), !removeMipMaps && !leaveReadable);
            if (removeMipMaps)
            {
                var t2 = new Texture2D(t.width, t.height, t.format, false);
                t2.SetPixels(t.GetPixels(0));
                t2.Apply(false, !leaveReadable);
                Destroy(t);
                t = t2;
            }
            if (mode != null)
                t.filterMode = mode.Value;
            if (wrap != null)
                t.wrapMode = wrap.Value;
            created.Add(t);
            return t;
        }

        bool ModUtils_MessageRecieved(CSteamID steamID, NetworkChannel channel, Message message)
        {
            object[] values;
            if (message.Type == MessageId && (values = ModUtils_GetGenericMessageValues(message, false)) != null)
                switch (ModUtils_GetGenericMessageId(message))
                {
                    case healthCall:
                        Network_Player remotePlayer = ComponentManager<Raft_Network>.Value.GetPlayerFromID(steamID);
                        if (remotePlayer)
                            remotePlayer.Stats.stat_health.Value = (float)values[0];
                        return true;
                    default:
                        break;
                }
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object[] ModUtils_GetGenericMessageValues(Message message, bool logErrors) => null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ModUtils_GetGenericMessageId(Message message) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Message ModUtils_CreateGenericMessage(Messages type, int subType, params object[] values) => null;

        public void OnModUnload()
        {
            harmony?.UnpatchAll(harmony.Id);
            foreach (var o in created)
                if (o)
                    Destroy(o);
            WorldEvent_WorldUnloaded();
            Log("Mod has been unloaded!");
        }

        public static void ErrorLog(object message)
        {
            Debug.LogError("[" + modInfo.name + "]: " + message.ToString());
        }

        public static void ShowCrosshairText(string text)
        {
            canvas.displayTextManager.ShowText(text);
        }

        public static GameObject CreateText(Transform parent, float x, float y, string text_to_print, int font_size, Color text_color, float width, float height, Font font, string name = "Text")
        {
            GameObject UItextGO = new GameObject("Text");
            UItextGO.transform.SetParent(parent, false);
            RectTransform trans = UItextGO.GetOrAddComponent<RectTransform>();
            trans.offsetMin = new Vector2(x, y);
            trans.offsetMax = new Vector2(width + x, height + y);
            Text text = UItextGO.AddComponent<Text>();
            text.text = text_to_print;
            text.font = font;
            text.fontSize = font_size;
            text.color = text_color;
            text.name = name;
            Shadow shadow = UItextGO.AddComponent<Shadow>();
            shadow.effectColor = new Color();
            return UItextGO;
        }
        public static void AddTextShadow(GameObject textObject, Color shadowColor, Vector2 shadowOffset)
        {
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowOffset;
        }
        public static void CopyTextShadow(GameObject textObject, GameObject shadowSource)
        {
            Shadow sourcesShadow = shadowSource.GetComponent<Shadow>();
            if (sourcesShadow == null)
                sourcesShadow = shadowSource.GetComponentInChildren<Shadow>();
            AddTextShadow(textObject, sourcesShadow.effectColor, sourcesShadow.effectDistance);
        }
        public static GameObject CreateLine(Canvas canvas, float length, float width, float x, float y, Color lineColor, float angle)
        {
            GameObject lineContainer = new GameObject();
            lineContainer.transform.SetParent(canvas.transform, false);
            RectTransform trans = lineContainer.AddComponent<RectTransform>();
            trans.sizeDelta = canvas.pixelRect.size;
            trans.anchoredPosition = new Vector2(0, 0);
            UILine.SetCreationValues(new Vector2(x, y), length, width, angle);
            UILine lineObj = lineContainer.AddComponent<UILine>();
            lineObj.color = lineColor;
            return lineContainer;
        }

        [ConsoleCommand(name: "setClock", docs: "Syntax: 'setClock <none|bar|clock|auto>'  Changes the display type of the clock")]
        public static string MyCommand(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            args[0] = args[0].ToLower();
            int ind = new string[] { "none", "bar", "clock", "auto" }.ToList().IndexOf(args[0]);
            if (ind >= 0)
            {
                clockSetting = ind;
                ResaveValues();
                if (ind == 3)
                    return "Time display is now automatic";
                return "Time display is now " + (ind > 0 ? "a " : "") + ToTitle(args[0]);
            }
            return args[0] + " is not valid";
        }

        [ConsoleCommand(name: "setStatNumbers", docs: "Syntax: 'setStatNumbers <none|round|decimal>'  Changes the display type of the stat numbers")]
        public static string MyCommand10(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";

            args[0] = args[0].ToLower();
            int ind = new string[] { "none", "round", "decimal" }.ToList().IndexOf(args[0]);
            if (Enum.TryParse<StatMode>(args[0],true,out var v))
            {
                if (statSetting == v)
                    return "Setting is unchanged";
                statSetting = v;
                ResaveValues();
                if (ind == 0)
                    return "Stat numbers are no longer shown";
                return "Stat numbers are now set to " + (ind == 2 ? "un" : "") + "rounded";
            }
            return args[0] + " is not valid";
        }

        [ConsoleCommand(name: "showHealth", docs: "Syntax: 'showHealth <animal>'  Enables floating health bars for the specified mob")]
        public static string MyCommand2(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            args[0] = args[0].ToLower();
            if (removeExclusion(args[0]))
                return ToTitle(args[0]) + " are now showing health bars";
            return ToTitle(args[0]) + " are already showing health bars";
        }

        [ConsoleCommand(name: "hideHealth", docs: "Syntax: 'hideHealth <animal>'  Disables floating health bars for the specified mob")]
        public static string MyCommand3(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            args[0] = args[0].ToLower();
            if (addExclusion(args[0]))
                return ToTitle(args[0]) + " are no longer showing health bars";
            return ToTitle(args[0]) + " already don't show health bars";
        }

        [ConsoleCommand(name: "show", docs: "Syntax: 'show <GUI Aspect>'  Enables a part of the Statistic Mod GUI elements")]
        public static string MyCommand4(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            args[0] = args[0].ToLower();
            if (!boolSettings.ContainsKey(args[0]))
                return args[0] + " is not a valid GUI aspect name";
            return boolSettings[args[0]].Set(true);
        }

        [ConsoleCommand(name: "hide", docs: "Syntax: 'hide <GUI Aspect>'  Disables a part of the Statistic Mod GUI elements")]
        public static string MyCommand5(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            args[0] = args[0].ToLower();
            if (!boolSettings.ContainsKey(args[0]))
                return args[0] + " is not a valid GUI aspect name";
            return boolSettings[args[0]].Set(false);
        }

        [ConsoleCommand(name: "guiAspects", docs: "Lists the names of the Statistic Mod GUI elements that can be toggled with 'show' and 'hide'")]
        public static string MyCommand6(string[] args)
        {
            string str = "";
            foreach (string name in boolSettings.Keys)
                if (str == "")
                    str = name;
                else
                    str += "\n" + name;
            return str;
        }

        [ConsoleCommand(name: "healthDrawDistance", docs: "Syntax: 'healthDrawDistance <distance>'  Changes how far away health bars can be seen from")]
        public static string MyCommand7(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            try
            {
                barDrawDistance = float.Parse(args[0]);
                ResaveValues();
                return "Can now see health bars from " + args[0];
            }
            catch
            {
                return "Cannot parse " + args[0] + " as a number";
            }
        }

        [ConsoleCommand(name: "setHUDPosition", docs: "Syntax: 'setHUDPosition <\u200bx> <\u200by>'  Changes the location, of the extra HUD information, on the screen")]
        public static string MyCommand8(string[] args)
        {
            if (args.Length < 2)
                return "Not enough arguments";
            if (args.Length > 2)
                return "Too many arguments";
            try
            {
                float tmp = float.Parse(args[1]);
                HUDPositionX = float.Parse(args[0]);
                HUDPositionY = tmp;
                ResaveValues();
                return "HUD has been moved";
            }
            catch
            {
                return "Failed to parse either " + args[0] + " or " + args[1] + " as a number";
            }
        }

        [ConsoleCommand(name: "setHUDToMouse", docs: "Sets the location, of the extra HUD information, on the screen to the location of the mouse")]
        public static string MyCommand9(string[] args)
        {
            HUDPositionX = Input.mousePosition.x / aspectRatio;
            HUDPositionY = canvas.GetComponent<RectTransform>().sizeDelta.y - Input.mousePosition.y;
            ResaveValues();
            return "HUD has been moved";
        }

        [ConsoleCommand(name: "sharkAttackColor", docs: "Syntax: 'sharkAttackColor <0-1530>'  Changes the color of the shark attacking alert")]
        public static string MyCommand11(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            try
            {
                attacking = GetColor(int.Parse(args[0]));
                ResaveValues();
                return $"Attacking color set to <color=#{attacking.GetHex()}>ALERTE</color>";
            }
            catch
            {
                return "Cannot parse " + args[0] + " as a number";
            }
        }

        [ConsoleCommand(name: "sharkChargeColor", docs: "Syntax: 'sharkAttackColor <0-1530>'  Changes the color of the shark charging alert")]
        public static string MyCommand12(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            try
            {
                charging = GetColor(int.Parse(args[0]));
                ResaveValues();
                return $"Charging color set to <color=#{charging.GetHex()}>ALERTE</color>";
            }
            catch
            {
                return "Cannot parse " + args[0] + " as a number";
            }
        }

        [ConsoleCommand(name: "sharkSearchColor", docs: "Syntax: 'sharkAttackColor <0-1530>'  Changes the color of the shark searching alert")]
        public static string MyCommand13(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            try
            {
                searching = GetColor(int.Parse(args[0]));
                ResaveValues();
                return $"Searching color set to <color=#{searching.GetHex()}>ALERTE</color>";
            }
            catch
            {
                return "Cannot parse " + args[0] + " as a number";
            }
        }

        [ConsoleCommand(name: "raftCenterColor", docs: "Syntax: 'raftCenterColor <0-1530>'  Changes the color of the raft center indicator")]
        public static string MyCommand14(string[] args)
        {
            if (args.Length < 1)
                return "Not enough arguments";
            if (args.Length > 1)
                return "Too many arguments";
            try
            {
                center = GetColor(int.Parse(args[0]));
                ResaveValues();
                return $"Searching color set to <color=#{center.GetHex()}>ALERTE</color>";
            }
            catch
            {
                return "Cannot parse " + args[0] + " as a number";
            }
        }

        static string ToTitle(string str)
        {
            if (str.Length == 0)
                return "";
            if (str.Length == 1)
                return str.ToUpper();
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        public static void SetDamage(float amount)
        {
            dmgText.text = (-amount).ToString();
            dmgText.color = amount < 0 ? Color.green : Color.red;
            dmgTime = 0;
            dmgObject.SetActiveSafe(true);
        }

        public static bool hasExclusion(string name) => exclusions.Contains(name);

        static bool addExclusion(string name)
        {
            if (exclusions.Add(name))
            {
                ExtraSettingsAPI_SetDataValue("exclusions", name, "");
                return true;
            }
            return false;
        }

        static bool removeExclusion(string name)
        {
            if (exclusions.Remove(name))
            {
                ResaveValues();
                return true;
            }
            return false;
        }

        static bool setFullBars(bool visible)
        {
            if (showFullBars == visible)
                return false;
            showFullBars = visible;
            ResaveValues();
            return true;
        }

        static bool setWeatherVisible(bool visible)
        {
            if (showWeather == visible)
                return false;
            showWeather = visible;
            ResaveValues();
            return true;
        }

        static bool setWeatherTimer(bool visible)
        {
            if (showWeatherTimer == visible)
                return false;
            showWeatherTimer = visible;
            ResaveValues();
            return true;
        }

        static bool setSharkTimer(bool visible)
        {
            if (showSharkRespawn == visible)
                return false;
            showSharkRespawn = visible;
            ResaveValues();
            return true;
        }

        static bool setShowObjective(bool visible)
        {
            if (showStoryObjective == visible)
                return false;
            showStoryObjective = visible;
            ResaveValues();
            return true;
        }

        static bool setShowUses(bool visible)
        {
            if (showDurability == visible)
                return false;
            showDurability = visible;
            ResaveValues();
            return true;
        }

        static bool setHUDEquip(bool visible)
        {
            if (showHUDEquipment == visible)
                return false;
            showHUDEquipment = visible;
            ResaveValues();
            return true;
        }

        static bool setShowWeight(bool visible)
        {
            if (showFoundations == visible)
                return false;
            showFoundations = visible;
            ResaveValues();
            return true;
        }

        static bool setShowSpeed(bool visible)
        {
            if (showSpeed == visible)
                return false;
            showSpeed = visible;
            ResaveValues();
            return true;
        }

        static bool setShowIds(bool visible)
        {
            if (showItemIds == visible)
                return false;
            showItemIds = visible;
            ResaveValues();
            return true;
        }

        static bool setShowCenter(bool visible)
        {
            if (showCenter == visible)
                return false;
            showCenter = visible;
            ResaveValues();
            return true;
        }

        static bool setShowCompass(bool visible)
        {
            if (showCompass == visible)
                return false;
            showCompass = visible;
            ResaveValues();
            return true;
        }

        static bool setSharkAttack(bool visible)
        {
            if (sharkMode.HasFlag(SharkMode.WhileAttacking) == visible)
                return false;
            sharkMode ^= SharkMode.WhileAttacking;
            ResaveValues();
            return true;
        }

        static bool setSharkCharge(bool visible)
        {
            if (sharkMode.HasFlag(SharkMode.WhileCharging) == visible)
                return false;
            sharkMode ^= SharkMode.WhileCharging;
            ResaveValues();
            return true;
        }

        static bool setSharkSearch(bool visible)
        {
            if (sharkMode.HasFlag(SharkMode.WhileSearching) == visible)
                return false;
            sharkMode ^= SharkMode.WhileSearching;
            ResaveValues();
            return true;
        }

        static bool setSharkAlertFollow(bool visible)
        {
            if (sharkAlertScreen == visible)
                return false;
            sharkAlertScreen = visible;
            ResaveValues();
            return true;
        }

        public static string getNextStoryLocation()
        {
            if (RAPI.GetLocalPlayer() == null)
                return "Player not found";
            int ind = -1;
            NoteBookUI book = RAPI.GetLocalPlayer().NoteBookUI;
            foreach (NoteBookNote note in book.GetAllNotes())
            {
                if (note.isUnlocked)
                    ind = Math.Max(storyNotes.IndexOf(note.noteIndex), ind);
            }
            return objectiveNames[ind + 1];
        }

        public static void ResaveValues()
        {
            ExtraSettingsAPI_SetComboboxSelectedIndex("Mode horloge", clockSetting);
            ExtraSettingsAPI_SetComboboxSelectedIndex("Barre de statistiques", (int)statSetting);
            ExtraSettingsAPI_SetCheckboxState("Affichage météo", showWeather);
            ExtraSettingsAPI_SetCheckboxState("Durée de météo", showWeatherTimer);
            ExtraSettingsAPI_SetCheckboxState("Délai de réapparition des requins", showSharkRespawn);
            ExtraSettingsAPI_SetCheckboxState("Affichage de la barre de vie", showFullBars);
            ExtraSettingsAPI_SetCheckboxState("Histoire", showStoryObjective);
            ExtraSettingsAPI_SetCheckboxState("Durabilité de l'objet", showDurability);
            ExtraSettingsAPI_SetCheckboxState("Affichage de l'équipement", showHUDEquipment);
            ExtraSettingsAPI_SetCheckboxState("Poids du radeau", showFoundations);
            ExtraSettingsAPI_SetCheckboxState("Boussole", showCompass);
            ExtraSettingsAPI_SetCheckboxState("Affichage de la vitesse", showSpeed);
            ExtraSettingsAPI_SetCheckboxState("ID d'objet", showItemIds);
            ExtraSettingsAPI_SetCheckboxState("Position du radeau", showCenter);
            ExtraSettingsAPI_SetSliderValue("Couleur de l'icône du radeau", GetHue(center));
            ExtraSettingsAPI_SetSliderValue("Distance de la barre de vie", barDrawDistance);
            ExtraSettingsAPI_SetSliderValue("Position HUD (X)", HUDPositionX / 1200);
            ExtraSettingsAPI_SetSliderValue("Position HUD (Y)", HUDPositionY / 1200);
            ExtraSettingsAPI_SetCheckboxState("While Attacking Raft", sharkMode.HasFlag(SharkMode.WhileAttacking));
            ExtraSettingsAPI_SetCheckboxState("While Charging Raft", sharkMode.HasFlag(SharkMode.WhileCharging));
            ExtraSettingsAPI_SetCheckboxState("While Searching", sharkMode.HasFlag(SharkMode.WhileSearching));
            ExtraSettingsAPI_SetSliderValue("Attacking Color", GetHue(attacking));
            ExtraSettingsAPI_SetSliderValue("Charging Color", GetHue(charging));
            ExtraSettingsAPI_SetSliderValue("Searching Color", GetHue(searching));
            ExtraSettingsAPI_SetCheckboxState("Alert Never Out of View", sharkAlertScreen);
            ExtraSettingsAPI_SetDataValues("exclusions", exclusions.ToDictionary(x => ""));
        }

        public void ExtraSettingsAPI_Load() => ExtraSettingsAPI_SettingsClose();

        public void ExtraSettingsAPI_SettingsClose()
        {
            clockSetting = ExtraSettingsAPI_GetComboboxSelectedIndex("Mode horloge");
            statSetting = (StatMode)ExtraSettingsAPI_GetComboboxSelectedIndex("Barre de statistiques");
            showWeather = ExtraSettingsAPI_GetCheckboxState("Affichage météo");
            showWeatherTimer = ExtraSettingsAPI_GetCheckboxState("Durée de météo");
            showSharkRespawn = ExtraSettingsAPI_GetCheckboxState("Délai de réapparition des requins");
            showFullBars = ExtraSettingsAPI_GetCheckboxState("Affichage de la barre de vie");
            showStoryObjective = ExtraSettingsAPI_GetCheckboxState("Histoire");
            showDurability = ExtraSettingsAPI_GetCheckboxState("Durabilité de l'objet");
            showHUDEquipment = ExtraSettingsAPI_GetCheckboxState("Affichage de l'équipement");
            showFoundations = ExtraSettingsAPI_GetCheckboxState("Poids du radeau");
            showSpeed = ExtraSettingsAPI_GetCheckboxState("Affichage de la vitesse");
            showItemIds = ExtraSettingsAPI_GetCheckboxState("ID d'objet");
            showCenter = ExtraSettingsAPI_GetCheckboxState("Position du radeau");
            center = GetColor(Mathf.RoundToInt(ExtraSettingsAPI_GetSliderValue("Couleur de l'icône du radeau")));
            showCompass = ExtraSettingsAPI_GetCheckboxState("Boussole");
            barDrawDistance = ExtraSettingsAPI_GetSliderValue("Distance de la barre de vie");
            HUDPositionX = 500 * ExtraSettingsAPI_GetSliderRealValue("Position HUD (X)");
            HUDPositionY = 10000 * ExtraSettingsAPI_GetSliderRealValue("Position HUD (Y)");
            sharkMode = SharkMode.None;
            if (ExtraSettingsAPI_GetCheckboxState("While Attacking Raft"))
                sharkMode |= SharkMode.WhileAttacking;
            if (ExtraSettingsAPI_GetCheckboxState("While Charging Raft"))
                sharkMode |= SharkMode.WhileCharging;
            if (ExtraSettingsAPI_GetCheckboxState("While Searching"))
                sharkMode |= SharkMode.WhileSearching;
            attacking = GetColor(Mathf.RoundToInt(ExtraSettingsAPI_GetSliderValue("Attacking Color")));
            charging = GetColor(Mathf.RoundToInt(ExtraSettingsAPI_GetSliderValue("Charging Color")));
            searching = GetColor(Mathf.RoundToInt(ExtraSettingsAPI_GetSliderValue("Searching Color")));
            sharkAlertScreen = ExtraSettingsAPI_GetCheckboxState("Alert Never Out of View");
            exclusions = (ExtraSettingsAPI_GetDataNames("exclusions") ?? new string[0]).ToHashSet();
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string ExtraSettingsAPI_GetComboboxSelectedItem(string SettingName) => "";
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float ExtraSettingsAPI_GetSliderRealValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetComboboxSelectedIndex(string SettingName, int value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetComboboxSelectedItem(string SettingName, string value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetCheckboxState(string SettingName, bool value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetSliderValue(string SettingName, float value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string[] ExtraSettingsAPI_GetDataNames(string settingName) => new string[0];
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetDataValue(string settingName, string subname, string value) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetDataValues(string settingName, Dictionary<string, string> values) { }

        static Color32 GetColor(int hue)
        {
            if (hue < 0)
                return Color.black;
            if (hue <= 255)
                return new Color32(255, (byte)hue, 0, 0);
            if (hue <= 510)
                return new Color32((byte)(510 - hue), 255, 0, 0);
            if (hue <= 765)
                return new Color32(0, 255, (byte)(hue - 510), 0);
            if (hue <= 1020)
                return new Color32(0, (byte)(1020 - hue), 255, 0);
            if (hue <= 1275)
                return new Color32((byte)(hue - 1020), 0, 255, 0);
            if (hue <= 1530)
                return new Color32(255, 0, (byte)(1530 - hue), 0);
            return Color.black;
        }
        static int GetHue(Color32 color)
        {
            if (color.r > 0)
            {
                if (color.g > 0)
                {
                    if (color.g < color.r)
                        return color.g;
                    return 510 - color.r;
                }
                if (color.b < color.r)
                    return 1020 + color.b;
                return 1530 - color.r;
            }
            if (color.b < color.g)
                return 510 + color.b;
            return 1020 - color.g;
        }

        public string ExtraSettingsAPI_HandleSliderText(string name, float value) => $"<color=#{GetColor(Mathf.RoundToInt(value)).GetHex()}>{(name.StartsWith("Raft") ? "◈" : "ALERTE")}</color>";
    }

    public enum StatMode
    {
        None,
        Round,
        Decimal
    }

    [Flags]
    public enum SharkMode
    {
        None,
        WhileAttacking = 1,
        WhileCharging = 2,
        WhileSearching = 4
    }

    public static class ExtentionMethods
    {
        public static Text GetText(this Transform canvas, string name)
        {
            foreach (Text gO in canvas.GetComponentsInChildren<Text>())
            {
                if (gO.name == name)
                    return gO;
            }
            return null;
        }

        public static string GetFoodValues(this Item_Base item) => (item.settings_consumeable.HungerYield != 0 ? "\nFood: " + item.settings_consumeable.HungerYield.ToString() : "")
                    + (item.settings_consumeable.BonusHungerYield != 0 ? "\nFood Bonus: " + item.settings_consumeable.BonusHungerYield.ToString() : "")
                    + (item.settings_consumeable.ThirstYield != 0 ? "\nWater: " + item.settings_consumeable.ThirstYield.ToString() : "")
                    + (item.settings_consumeable.BonusThirstYield != 0 ? "\nWater Bonus: " + item.settings_consumeable.BonusThirstYield.ToString() : "");


        public static void AppendTimeString(this StringBuilder builder, float TimeOfDay, bool addZero = true)
        {
            int hour = (int)TimeOfDay;
            int minute = (int)((TimeOfDay - hour) * 60);
            if (addZero && hour < 10)
                builder.Append('0');
            builder.Append(hour);
            builder.Append(':');
            if (addZero && minute < 10)
                builder.Append('0');
            builder.Append(minute);
        }

        public static string GetWeatherString(this UniqueWeatherType type)
        {
            if (type == UniqueWeatherType.Default)
            {
                return "Normal";
            }
            else
                return type.ToString();
        }

        public static Sprite ToSprite(this Texture2D texture, Rect? rect = null, Vector2? pivot = null)
        {
            var s = Sprite.Create(texture, rect ?? new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f));
            Main.created.Add(s);
            return s;
        }

        public static T GetOrAddComponent<T>(this GameObject g) where T : Component => g.GetComponent<T>() ?? g.AddComponent<T>();
        public static T GetOrAddComponent<T>(this Component g) where T : Component => g.gameObject.GetOrAddComponent<T>();

        public static string GetHex(this Color32 color) => BitConverter.ToString(new[] { color.r }) + BitConverter.ToString(new[] { color.g }) + BitConverter.ToString(new[] { color.b });
        public static Color Tweak(this Color color, float? r = null, float? g = null, float? b = null, float? a = null)
        {
            if (r != null)
                color.r = r.Value;
            if (g != null)
                color.g = g.Value;
            if (b != null)
                color.b = b.Value;
            if (a != null)
                color.a = a.Value;
            return color;
        }
        public static Color32 Tweak(this Color32 color, byte? r = null, byte? g = null, byte? b = null, byte? a = null)
        {
            if (r != null)
                color.r = r.Value;
            if (g != null)
                color.g = g.Value;
            if (b != null)
                color.b = b.Value;
            if (a != null)
                color.a = a.Value;
            return color;
        }

        public static bool IsSearching(this AI_StateMachine_Shark shark) => shark.SearchBlockInterval - Traverse.Create(shark).Field("searchBlockProgress").GetValue<float>() <= 10;

        public static bool Contains(this Enum e, Enum flag)
        {
            if (flag.Equals(Enum.ToObject(flag.GetType(), 0)))
                return false;
            var a = e as IConvertible;
            var b = flag as IConvertible;
            try
            {
                var c = b.ToInt64(CultureInfo.InvariantCulture);
                return (a.ToInt64(CultureInfo.InvariantCulture) & c) == c;
            } catch { }
            try
            {
                var c = b.ToUInt64(CultureInfo.InvariantCulture);
                return (a.ToUInt64(CultureInfo.InvariantCulture) & c) == c;
            } catch { }
            return false;
        }
    }

    [HarmonyPatch(typeof(Battery), "OnIsRayed")]
    public class Patch_BatteryRayed
    {
        public static Battery batteryInstance;
        static void Prefix(Battery __instance)
        {
            batteryInstance = __instance;
        }
        static void Postfix()
        {
            batteryInstance = null;
        }
    }

    [HarmonyPatch(typeof(DisplayTextManager), "ShowText", new Type[] { typeof(string), typeof(KeyCode), typeof(int), typeof(int), typeof(bool) })]
    public class Patch_BatteryDisplayText
    {
        static void Prefix(ref string text)
        {
            if (Patch_BatteryRayed.batteryInstance != null)
                text += "\n" + Mathf.CeilToInt(Patch_BatteryRayed.batteryInstance.NormalizedBatteryLeft * 100) + "%";
        }
    }

    [HarmonyPatch(typeof(Pickup), "Update")]
    public class Patch_LookingUpdate
    {
        static bool shown = false;
        static void Postfix(Pickup __instance)
        {
            if (Helper.HitAtCursor(out var raycastHit, Player.UseDistance, LayerMasks.MASK_Item | LayerMasks.MASK_RaycastInteractable))
            {
                Plant crop = raycastHit.transform.GetComponentInParent<Plant>();
                if (crop)
                {
                    showPlantProgress(crop);
                    return;
                }
            }
            if (shown)
            {
                Main.canvas.displayTextManager.HideDisplayTexts();
                shown = false;
            }
        }

        static void showPlantProgress(Plant target)
        {
            if (target.FullyGrown())
            {
                if (target.GetComponent<HarvestableTree>())
                {
                    var left = Mathf.FloorToInt(target.pickupComponent.yieldHandler.Yield.Count / target.pickupComponent.yieldHandler.yieldAsset.yieldAssets.Count * 100);
                    if (left < 100)
                    {
                        shown = true;
                        Main.ShowCrosshairText(left + "%");
                    }
                }
                return;
            }
            var growTime = Mathf.FloorToInt(target.GetGrowTimer() / Traverse.Create(target).Field("growTimeSec").GetValue<float>() * 100);
            if (growTime < 100)
            {
                shown = true;
                Main.ShowCrosshairText(growTime + "%");
            }
        }
    }

    [HarmonyPatch(typeof(CanvasHelper), "Start")]
    static class Patch_CanvasHelper
    {
        static bool playerFound = false;
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start(CanvasHelper __instance)
        {
            HUDText.Create(__instance.transform);
            Main.dmgDisplay = Main.CreateText(__instance.transform, 130, 0, "0", Main.textBase.fontSize, Color.red, 200, Main.textBase.fontSize * 1.5f, Main.textBase.font, "aDmgDisplay");
            Main.created.Add(Main.dmgDisplay);
            Main.CopyTextShadow(Main.dmgDisplay, Main.textBase.gameObject);
            Main.dmgDisplay.transform.SetAsFirstSibling();
            Main.dmgDisplay.SetActiveSafe(false);
            var player = RAPI.GetLocalPlayer();
            if (playerFound = player)
            {
                StatDisplay.Create(__instance.healthSlider.transform, Main.textBase.color, player.Stats.stat_health, player.Stats.stat_BonusHealth);
                StatDisplay.Create(__instance.hungerSlider.transform, new Color(0, 0.5f, 0), player.Stats.stat_hunger);
                StatDisplay.Create(__instance.thirstSlider.transform, new Color(0.2f, 0.5f, 0.9f), player.Stats.stat_thirst);
            }
            Main.sharkTime = new List<float>();
            CenterPointer.Create();
            (Main.equipDisplay = new GameObject("HUDEquipmentParent",typeof(RectTransform)).transform).SetParent(__instance.transform);
            Main.created.Add(Main.equipDisplay.gameObject);
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void Update(CanvasHelper __instance)
        {
            float timePassed = Time.deltaTime;
            Network_Player player = RAPI.GetLocalPlayer();
            if (!playerFound && (playerFound = player))
            {
                StatDisplay.Create(__instance.healthSlider.transform, Main.textBase.color, player.Stats.stat_health, player.Stats.stat_BonusHealth);
                StatDisplay.Create(__instance.hungerSlider.transform, new Color(0, 0.5f, 0), player.Stats.stat_hunger);
                StatDisplay.Create(__instance.thirstSlider.transform, new Color(0.2f, 0.5f, 0.9f), player.Stats.stat_thirst);
            }
            try
            {
                Main.dmgTime += timePassed;
                if (Main.dmgDisplay.activeSelf)
                {
                    Main.dmgOffset.anchoredPosition = Main.dmgStart + new Vector2(Main.dmgTime, 0) * 100;
                    Main.dmgText.color = new Color(Main.dmgText.color.r, Main.dmgText.color.g, Main.dmgText.color.b, 1 - Main.dmgTime);
                    if (Main.dmgText.color.a <= 0)
                        Main.dmgDisplay.SetActiveSafe(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("HUD Damage Indicator Update Failed: " + e);
            }
            try
            {
                if (player != null)
                {
                    if (Main.updatePlayerHealth)
                    {
                        var msg = Main.ModUtils_CreateGenericMessage(Main.MessageId, Main.healthCall, player.Stats.stat_health.Value + player.Stats.stat_BonusHealth.Value);
                        if (msg == null)
                            Debug.LogWarning($"[{Main.modInfo.name}]: Could not send health data to connected clients, you're probably missing the ModUtils mod");
                        else
                            foreach (KeyValuePair<CSteamID, Network_Player> pair in player.Network.remoteUsers.ToArray())
                                if (!pair.Value.IsLocalPlayer)
                                    RAPI.SendNetworkMessage(msg, Main.ModUtils_Channel);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Stat Network Update Failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerEquipment))]
    static class Patch_PlayerEquipment
    {
        [HarmonyPatch("Equip")]
        [HarmonyPostfix]
        public static void Equip(PlayerEquipment __instance, ItemInstance item, Slot_Equip slot)
        {
            Network_Player player = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
            if (item.BaseItemMaxUses > 1)
                HUDEquipmentDisplay.Create(Main.equipDisplay, slot, player.Inventory.slotPrefab, new Vector2(10, 200), player.Inventory.gridLayoutGroup.cellSize);

        }
    }

    [HarmonyPatch(typeof(Network_Entity))]
    public class Patch_NetworkEntity
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start(Network_Entity __instance)
        {
            if (Traverse.Create(__instance as PlayerStats).Field("playerNetwork").GetValue<Network_Player>()?.IsLocalPlayer ?? false)
                return;
            HealthBar.Create(__instance);
        }

        [HarmonyPatch("Damage")]
        [HarmonyPostfix]
        static void Damage(Network_Entity __instance, float damage, Vector3 hitPoint)
        {
            if (RAPI.GetLocalPlayer().gameObject == __instance.gameObject)
            {
                Main.SetDamage(damage);
                return;
            }
            DamageIndicator.Create(__instance.transform, damage, hitPoint);
        }
    }

    [HarmonyPatch(typeof(ItemInstance_Inventory))]
    static class Patch_ItemInstanceInventory
    {
        [HarmonyPatch("Description", MethodType.Getter)]
        [HarmonyPostfix]
        static void Description(ItemInstance_Inventory __instance, ref string __result)
        {
            List<Item_Base> items = ItemManager.GetAllItems();
            foreach (Item_Base item in items)
                if (item.settings_Inventory == __instance)
                {
                    MeleeWeapon weapon = null;
                    foreach (ItemConnection connect in RAPI.GetLocalPlayer().PlayerItemManager.useItemController.allConnections)
                        if (connect.inventoryItem == item)
                        {
                            weapon = connect.obj.GetComponent<MeleeWeapon>();
                            break;
                        }
                    string str = "";
                    if (item.settings_consumeable != null)
                        str += item.GetFoodValues();
                    if ((item.settings_buildable?.HasBuildablePrefabs ?? false) && item.settings_buildable.GetBlockPrefab(0)?.GetComponent<CookingTable_Recipe_UI>())
                        str += item.settings_buildable.GetBlockPrefab(0).GetComponent<CookingTable_Recipe_UI>().Recipe.Result.GetFoodValues();
                    if (weapon != null)
                        str += weapon != null ? "\nDamage: " + Traverse.Create(weapon).Field("damage").GetValue<int>().ToString() : "";
                    if (str.Length != 0)
                        __result += "\n" + str;
                    break;
                }
        }

        [HarmonyPatch("DisplayName", MethodType.Getter)]
        [HarmonyPostfix]
        static void DisplayName(ItemInstance_Inventory __instance, ref string __result)
        {
            if (!Main.showItemIds)
                return;
            List<Item_Base> items = ItemManager.GetAllItems();
            foreach (Item_Base item in items)
                if (item.settings_Inventory == __instance)
                {
                    __result += " (" + item.UniqueName + "#" + item.UniqueIndex + ")";
                    break;
                }
        }
    }

    [HarmonyPatch(typeof(AI_StateMachine_Shark),"Awake")]
    static class Patch_SharkSpawn
    {
        public static void Postfix(AI_StateMachine_Shark __instance)
        {
            SharkIndicator.Create(__instance);
        }
    }

    [HarmonyPatch(typeof(Network_Host_Entities), "CreateShark")]
    static class Patch_SharkRespawnStart
    {
        static void Prefix(float timeDelay)
        {
            Main.sharkTime.Add(timeDelay);
        }
    }

    [HarmonyPatch(typeof(Inventory))]
    static class Patch_Inventory
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start(Inventory __instance)
        {
            DuraText newObj = __instance.gameObject.AddComponent<DuraText>();
            Main.created.Add(newObj);
            newObj.container = Main.CreateText(__instance.transform, 0, 0, "hi", Main.textBase.fontSize, Main.textBase.color, 400, 400, Main.textBase.font, "duraText");
            Main.created.Add(newObj.container);
            newObj.textComponent = newObj.container.GetComponent<Text>();
            newObj.textComponent.rectTransform.anchorMin = Vector2.one * 0.5f;
            newObj.textComponent.rectTransform.anchorMax = Vector2.one * 0.5f;
            newObj.textComponent.alignment = TextAnchor.LowerLeft;
            Main.CopyTextShadow(newObj.container, Main.textBase.gameObject);
            newObj.container.SetActive(false);
        }

        [HarmonyPatch("HoverEnter")]
        [HarmonyPostfix]
        static void HoverEnter(Inventory __instance, Slot slot)
        {
            if (!slot.IsEmpty && slot.itemInstance.BaseItemMaxUses > 1)
            {
                DuraText obj = __instance.GetComponent<DuraText>();
                if (obj)
                {
                    obj.container.SetActive(true);
                    obj.textComponent.text = slot.itemInstance.Uses.ToString() + "/" + slot.itemInstance.BaseItemMaxUses.ToString();
                }
            }
        }

        [HarmonyPatch("HoverExit")]
        [HarmonyPostfix]
        static void HoverExit(Inventory __instance)
        {
            DuraText obj = __instance.GetComponent<DuraText>();
            if (obj && Main.showDurability)
            {
                obj.container.SetActive(false);
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void Update(Inventory __instance)
        {
            DuraText obj = __instance.GetComponent<DuraText>();
            if (obj)
            {
                obj.textComponent.rectTransform.sizeDelta = Vector2.zero;
                obj.textComponent.transform.position = Input.mousePosition;
                obj.textComponent.rectTransform.offsetMax = (obj.textComponent.rectTransform.offsetMin += new Vector2(10,10)) + new Vector2(500, 500);
            }
        }
    }


    public class UILine : Image
    {
        Vector2 _p;
        Vector2 _s;
        float _a;
        Memory<float, float> _angle = new Memory<float, float>(x => x);
        Memory<Vector2, Vector2> _size = new Memory<Vector2, Vector2>(x => x);
        Memory<Vector2, Vector2> _position = new Memory<Vector2, Vector2>(x => x);
        bool _dirty = false;
        static Vector2 _cP;
        static Vector2 _cS;
        static float _cA;
        public Vector2 start
        {
            get => _p;
            set
            {
                _p = value;
                _dirty = true;
            }
        }
        public float length
        {
            get => _s.x;
            set
            {
                _s.x = value;
                _dirty = true;
            }
        }
        public float angle
        {
            get => _a;
            set
            {
                _a = value;
                _dirty = true;
            }
        }
        public float width
        {
            get => _s.y;
            set
            {
                _s.y = value;
                _dirty = true;
            }
        }
        UILine()
        {
            _p = _cP;
            _s = _cS;
            _a = _cA;
        }
        protected override void Start()
        {
            base.Start();
            UpdatePosition( true);
        }
        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                UpdatePosition();
            }
        }
        void UpdatePosition(bool force = false)
        {
            if (_angle.GetValue(_a,out var a) || force)
                rectTransform.localRotation = Quaternion.Euler(0, 0, a);
            var f = _size.GetValue(_s, out var s);
            if (f || force)
                rectTransform.pivot = new Vector2(s.y / 2f / s.x, 0.5f);
            if (_position.GetValue(_p, out var p) || f || force)
            {
                rectTransform.offsetMin = new Vector2(p.x - s.y / 2, p.y - s.y / 2);
                rectTransform.offsetMax = new Vector2(p.x - s.y / 2 + s.x, p.y + s.y / 2);
            }
        }
        public static void SetCreationValues(Vector2 start, float lineLength, float lineWidth, float lineAngle)
        {
            _cP = start;
            _cS = new Vector2(lineLength, lineWidth);
            _cA = lineAngle;
        }
    }

    public class HealthBar : MonoBehaviour
    {
        Canvas canvas;
        UILine green;
        Text numDisplay;
        float barlength;
        Network_Entity parent;
        Memory<Network_Entity, (float value, float max)> health = new Memory<Network_Entity, (float, float)>(x => (x.stat_health.Value,x.stat_health.Max));
        Memory<HealthBar, float, bool> active = new Memory<HealthBar, float, bool>((x, y) => y < Main.barDrawDistance * Main.barDrawDistance && !Main.hasExclusion(CleanName(x.parent.name).ToLower()) && (Main.showFullBars || x.parent.stat_health.Value < x.parent.stat_health.Max));


        public static HealthBar Create(Network_Entity creature)
        {
            creating = true;
            var o = new GameObject("Healthbar").AddComponent<HealthBar>();
            creating = false;
            Main.created.Add(o.gameObject);
            o.parent = creature;
            o.transform.SetParent(creature.transform, false);
            o.transform.localPosition = new Vector3(0, 2, 0);
            var rect = o.gameObject.GetOrAddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1, 1f);
            o.canvas = new GameObject("canvas").AddComponent<Canvas>();
            o.canvas.transform.SetParent(o.transform, false);
            o.canvas.gameObject.layer = 21;
            o.canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            var trans = o.canvas.GetOrAddComponent<RectTransform>();
            trans.sizeDelta = new Vector2(1, 1f);
            o.barlength = 300;
            var lineWidth = 50f;
            var textWidth = 500f;
            var borderSize = 10f;
            Main.CreateLine(o.canvas, o.barlength + borderSize * 2, lineWidth + borderSize * 2, lineWidth / 2 - o.barlength / 2, 0, Color.grey, 0).GetComponent<UILine>();
            Main.CreateLine(o.canvas, o.barlength, lineWidth, lineWidth / 2 - o.barlength / 2, 0, new Color(0.8f, 0, 0), 0).GetComponent<UILine>();
            o.green = Main.CreateLine(o.canvas, o.barlength / 2, lineWidth, lineWidth / 2 - o.barlength / 2, 0, Color.green, 0).GetComponent<UILine>();
            o.numDisplay = Main.CreateText(o.canvas.transform, -o.barlength / 2 - textWidth - borderSize * 2, -lineWidth, "??? / ???", (int)lineWidth, Main.textBase.color, textWidth, lineWidth * 2, Main.textBase.font).GetComponent<Text>();
            o.numDisplay.alignment = TextAnchor.MiddleRight;
            Main.CopyTextShadow(o.numDisplay.gameObject, Main.textBase.gameObject);
            var shad = o.canvas.gameObject.AddComponent<Shadow>();
            shad.effectColor = Color.black;
            shad.effectDistance = Vector2.zero;
            shad.useGraphicAlpha = false;
            o.canvas.gameObject.SetActive(false);
            return o;
        }

        static bool creating = false;
        void Awake()
        {
            if (!creating)
                Destroy();
        }

        void Update()
        {
            var dirVec = Helper.MainCamera.transform.position - canvas.transform.position;
            if (active.GetValue(this, dirVec.sqrMagnitude, out var a))
                canvas.gameObject.SetActive(a);
            if (a)
            {
                transform.rotation = Quaternion.LookRotation(-dirVec);
                if (health.GetValue(parent, out var h))
                {
                    green.length = barlength * h.value / h.max;
                    numDisplay.text = ((int)h.value).ToString() + " / " + ((int)h.max).ToString();
                }
            }
        }

        static Dictionary<string, string> cleanCache = new Dictionary<string, string>();
        public static string CleanName(string entityName)
        {
            if (cleanCache.TryGetValue(entityName, out var res))
                return res;
            res = entityName;
            if (res.EndsWith("(Clone)"))
                res = res.Substring(0, res.Length - 7);
            if (res.StartsWith("AI_"))
                res = res.Remove(0, 3);
            return cleanCache[entityName] = res;
        }

        public void Destroy() => Destroy(gameObject);
    }

    public class StatDisplay : MonoBehaviour
    {
        Stat bonus;
        Stat stat;
        Text text;
        Memory<bool> active = new Memory<bool>(() => Main.statSetting != StatMode.None);
        Memory<StatDisplay, (float current, float max, float bonus, bool rounded)> value = new Memory<StatDisplay, (float, float, float, bool)>(x => (x.stat.Value, x.stat.Max, x.bonus ? x.bonus.Value : 0, Main.statSetting == StatMode.Round));

        public static StatDisplay Create(Transform parent, Color color, Stat_Bonus stat) => Create(parent, color, stat.Normal, stat.Bonus);
        public static StatDisplay Create(Transform parent, Color color, Stat stat, Stat bonus = null)
        {
            var o = new GameObject("StatDisplay").AddComponent<StatDisplay>();
            Main.created.Add(o.gameObject);
            o.stat = stat;
            o.bonus = bonus;
            o.transform.SetParent(parent, false);
            var r = o.GetOrAddComponent<RectTransform>();
            r.anchorMin = Vector3.zero;
            r.anchorMax = Vector3.one;
            r.offsetMin = Vector3.zero;
            r.offsetMax = Vector3.zero;
            var s = 2;
            o.text = Main.CreateText(o.transform, 0, 0, "??/??", Main.textBase.fontSize * s, color, 0, 0, Main.textBase.font, "Text").GetComponent<Text>();
            o.text.transform.localScale = Vector3.one / s;
            o.text.rectTransform.anchorMin = Vector2.one * (-0.5f * s + 0.5f);
            o.text.rectTransform.anchorMax = Vector2.one * (0.5f * s + 0.5f);
            o.text.rectTransform.offsetMin = new Vector2(70, 0);
            Main.AddTextShadow(o.text.gameObject, new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, color.a), new Vector2(2 * s, -2 * s));
            o.active.GetValue(out var a);
            o.text.gameObject.SetActive(a);
            return o;
        }
        void Update()
        {
            if (active.GetValue(out var a))
                text.gameObject.SetActive(a);
            if (a && value.GetValue(this, out var v))
                text.text = StatValue(v.current) + "/" + StatValue(v.max) + (v.bonus > 0 ? " + " + StatValue(v.bonus) : "");
        }

        static string StatValue(float value) => Math.Round(value, Main.statSetting == StatMode.Round ? 0 : 1).ToString();
    }

    public class HUDText : MonoBehaviour
    {
        public static HUDText Create(Transform parent)
        {
            var o = new GameObject("HUD").AddComponent<HUDText>();
            Main.created.Add(o.gameObject);
            o.transform.SetParent(parent, false);
            o.transform.SetAsFirstSibling();
            var r = o.GetOrAddComponent<RectTransform>();
            r.offsetMax = Vector2.zero;
            r.offsetMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.anchorMin = Vector2.zero;
            o.text = Main.CreateText(o.transform, -500 * Main.aspectRatio, 350, "\nIf you can see this something has gone wrong", Main.textBase.fontSize, Main.textBase.color, 400, 300, Main.textBase.font, "HUD Text").GetComponent<Text>();
            Main.CopyTextShadow(o.text.gameObject, Main.textBase.gameObject);
            o.text.rectTransform.offsetMax = Vector2.zero;
            o.text.rectTransform.offsetMin = Vector2.zero;
            o.text.rectTransform.anchorMax = new Vector2(0, 1);
            o.text.rectTransform.anchorMin = new Vector2(0, 1);
            try { TimeBar.Create(o.text.gameObject, Main.textBase.font.lineHeight, 50, 25); } catch (Exception e) { Debug.LogError(e); }
            try { Compass.Create(o.text.gameObject, 80, 400, 100); } catch (Exception e) { Debug.LogError(e); }
            o.time.GetValue(out _);
            return o;
        }
        int steps;
        Memory<float> time = new Memory<float>(() => Time.time);
        Memory<Vector3?> position = new Memory<Vector3?>(() => RAPI.GetLocalPlayer()?.transform.position);
        float speed;
        Text text;
        Memory<(float x, float y, float r)> offset = new Memory<(float, float, float)>(() => (Main.HUDPositionX, Main.HUDPositionY, Main.aspectRatio));
        Memory<HUDText, (int, UniqueWeatherType?, float?, float?, int?, int, string)> changes = new Memory<HUDText, (int, UniqueWeatherType?, float?, float?, int?, int, string)>(x => 
        (
            Main.timeIsClock ? Main.clockSetting : -Main.clockSetting,
            Main.showWeather ? ComponentManager<WeatherManager>.Value.GetCurrentWeather().so_weather.uniqueWeatherType : default(UniqueWeatherType?),
            Main.showWeatherTimer ? ComponentManager<WeatherManager>.Value.WeatherTimer : default(float?),
            Main.showSpeed ? x.speed : default(float?),
            Main.showFoundations ? ComponentManager<RaftBounds>.Value?.FoundationCount : default(int?),
            Main.showSharkRespawn ? Main.sharkTime.Count : 0,
            Main.showStoryObjective ? Main.getNextStoryLocation() : null
        ));
        void Update()
        {
            if (Time.deltaTime != 0)
            {
                steps++;
                if (steps >= 20)
                {
                    position.GetValue(out var p, out var c);
                    if (p != null && c != null)
                    {
                        time.GetValue(out var start, out var end);
                        speed = (p.Value - c.Value).magnitude / (end - start);
                        steps = 0;
                    }
                }
                for (var i = Main.sharkTime.Count - 1; i >= 0; i--)
                    if ((Main.sharkTime[i] -= Time.deltaTime) <= 0)
                        Main.sharkTime.RemoveAt(i);
            }
            WeatherManager wm = ComponentManager<WeatherManager>.Value;
            AzureSkyController sc = ComponentManager<AzureSkyController>.Value;
            if (wm && sc && wm.GetCurrentWeather())
            {
                if (offset.GetValue(out var off))
                {
                    text.rectTransform.offsetMin = new Vector2(off.x * off.r, -off.y - 300);
                    text.rectTransform.offsetMax = new Vector2(off.x * off.r + 400, -off.y);
                }
                if (changes.GetValue(this, out _))
                {
                    var builder = new StringBuilder();
                    var line = false;
                    void LineStart(int n = 1)
                    {
                        if (line)
                            for (int i = 0; i < n; i++)
                                builder.Append('\n');
                        else
                            line = true;
                    }
                    if (Main.showStoryObjective)
                    {
                        LineStart();
                        builder.Append("Histoire : ");
                        builder.Append(Main.getNextStoryLocation());
                    }
                    if (Main.clockSetting != 0)
                    {
                        LineStart();
                        builder.Append("Heure : ");
                        if (Main.timeIsClock)
                            builder.AppendTimeString(sc.timeOfDay.hour);
                    }
                    if (Main.showWeather)
                    {
                        LineStart();
                        builder.Append("Météo : ");
                        builder.Append(wm.GetCurrentWeather().so_weather.uniqueWeatherType.GetWeatherString());
                    }
                    if (Main.showWeatherTimer)
                    {
                        LineStart();
                        builder.Append("Durée météo : ");
                        builder.AppendTimeString(wm.WeatherTimer / 60, false);
                    }
                    if (Main.showSpeed)
                    {
                        LineStart();
                        builder.Append("Vitesse actuelle : ");
                        builder.Append(speed.ToString("N1"));
                        builder.Append(" m/s");
                    }
                    if (Main.showFoundations)
                    {
                        LineStart();
                        builder.Append("Fondations : ");
                        builder.Append(ComponentManager<RaftBounds>.Value?.FoundationCount);
                    }
                    if (Main.showFoundations)
                    {
                        LineStart();
                        builder.Append("Moteur requis : ");
                        var n = Math.Ceiling(RaftWeightManager.FoundationWeight / 100f);
                        builder.Append(n);
                    }
                    if (Main.sharkTime.Count > 0 && Main.showSharkRespawn)
                    {
                        LineStart();
                        builder.Append("Le requin réapparaît dans : [");
                        var flag = false;
                        foreach (var t in Main.sharkTime)
                        {
                            if (flag)
                                builder.Append(", ");
                            else
                                flag = true;
                            builder.AppendTimeString(t / 60, false);
                        }
                        builder.Append(']');
                    }
                    text.text = builder.ToString();
                }
            }
        }

        static string StatValue(float value) => Math.Round(value, Main.statSetting == StatMode.Round ? 0 : 1).ToString();
    }

    public class TimeBar : MonoBehaviour
    {
        Canvas canvas;
        UILine colored;
        UILine fading;
        float barlength;
        static Color night = new Color(0, 0, 0.4f);
        static Color day = new Color(0.3f, 0.6f, 1);
        static float sunrise = 4;
        static float sunset = 20;
        Memory<float?> time = new Memory<float?>(() => !Main.timeIsClock && Main.clockSetting != 0 ? ComponentManager<AzureSkyController>.Value.timeOfDay.hour : default(float?));
        Memory<float, bool?> isDay = new Memory<float, bool?>(x => x >= sunrise && x < sunset);

        public static TimeBar Create(GameObject parent, float size, float X, float Y)
        {
            var o = new GameObject("TimeBar",typeof(TimeBar)).GetComponent<TimeBar>();
            Main.created.Add(o.gameObject);
            o.transform.SetParent(parent.transform, false);
            var oR = o.GetOrAddComponent<RectTransform>();
            oR.offsetMax = Vector2.zero;
            oR.offsetMin = Vector2.zero;
            oR.anchorMin = Vector2.zero;
            oR.anchorMax = Vector2.one;
            var c = new GameObject("canvas").GetOrAddComponent<RectTransform>();
            c.SetParent(o.transform, false);
            c.offsetMax = Vector2.zero;
            c.offsetMin = Vector2.zero;
            c.anchorMin = Vector2.zero;
            c.anchorMax = Vector2.one;
            o.canvas = c.gameObject.AddComponent<Canvas>();
            o.barlength = size * 4;
            var lineWidth = size;
            var border = 3f;
            var barX = lineWidth + 12 + border + X;
            var barY = lineWidth - 42 + border - Y;
            var anc = new Vector2(0, 1);
            var back = Main.CreateLine(o.canvas, o.barlength + border * 2, lineWidth + border * 2, barX, barY, Color.grey, 0).GetComponent<UILine>();
            back.rectTransform.anchorMin = anc;
            back.rectTransform.anchorMax = anc;
            o.colored = Main.CreateLine(o.canvas, o.barlength, lineWidth, barX, barY, Color.black, 0).GetComponent<UILine>();
            o.colored.rectTransform.anchorMin = anc;
            o.colored.rectTransform.anchorMax = anc;
            o.fading = Main.CreateLine(o.canvas, o.barlength / 2, lineWidth, barX, barY, Color.yellow, 0).GetComponent<UILine>();
            o.fading.rectTransform.anchorMin = anc;
            o.fading.rectTransform.anchorMax = anc;
            o.SetActive(false);
            return o;
        }

        public void Update()
        {
            if (time.GetValue(out var p, out var v))
            {
                if ((v == null) != (p == null))
                    SetActive(v != null);
                if (v != null)
                {
                    if (isDay.GetValue(v.Value, out var IsDay))
                    {
                        colored.color = IsDay.Value ? day : night;
                        fading.color = IsDay.Value ? night : day;
                    }
                    if (IsDay.Value)
                        fading.length = barlength * (v.Value - sunrise) / (sunset - sunrise);
                    else
                        fading.length = barlength * (v.Value < sunset ? v.Value - sunset + 24 : v.Value - sunset) / (24 - sunset + sunrise);
                }
            }
        }

        public void SetActive(bool active) => canvas.gameObject.SetActive(active);
    }

    public class Compass : MonoBehaviour
    {
        GameObject pointer;
        Memory<float?> angle = new Memory<float?>(() => Main.showCompass ? RAPI.GetLocalPlayer()?.transform.rotation.eulerAngles.y : default(float?));
        public static Compass Create(GameObject parent, int size, float X, float Y)
        {
            var obj = new GameObject("Compass").AddComponent<Compass>();
            Main.created.Add(obj.gameObject);
            obj.transform.SetParent(parent.transform, false);
            var r2 = obj.GetOrAddComponent<RectTransform>();
            r2.offsetMax = Vector2.zero;
            r2.offsetMin = Vector2.zero;
            r2.anchorMin = Vector2.zero;
            r2.anchorMax = Vector2.one;
            obj.pointer = new GameObject("Image");
            obj.pointer.transform.SetParent(obj.transform);
            var r = obj.pointer.GetOrAddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.offsetMin = new Vector2(X, -Y);
            r.offsetMax = new Vector2(X + size, -Y + size);
            var i = obj.pointer.AddComponent<Image>();
            i.sprite = Main.compass;
            obj.SetActive(false);
            return obj;
        }

        public void Update()
        {
            if (angle.GetValue(out var p, out var v))
            {
                if ((v == null) != (p == null))
                    SetActive(v != null);
                if (v != null)
                    pointer.transform.rotation = Quaternion.Euler(0, 0, v.Value);
            }
        }

        public void SetActive(bool active) => pointer.SetActive(active);
    }

    public class DamageIndicator : MonoBehaviour
    {
        Vector3 startPos;
        Vector3 angle;
        float timePassed;
        Text numDisplay;
        public static DamageIndicator Create(Transform init, float amount, Vector3 hitPos)
        {
            amount = -amount;
            var obj = new GameObject();
            Main.created.Add(obj);
            obj.transform.SetParent(init, false);
            obj.transform.parent = null;
            var ind = obj.AddComponent<DamageIndicator>();
            ind.startPos = obj.transform.position;
            ind.startPos.y += 1;
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1, 1f);
            float scaler = 100;
            var canvas = obj.AddComponent<Canvas>();
            obj.layer = 21;
            canvas.transform.SetParent(obj.transform, false);
            canvas.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f) / scaler;
            var trans = canvas.GetOrAddComponent<RectTransform>();
            trans.sizeDelta = new Vector2(1, 1f);
            var text = Main.CreateText(obj.transform, 0, 0, DamageString(amount), (int)(2 * scaler), amount >= 0 ? Color.green : Color.red, 5 * scaler, 5 * scaler, Main.canvas.dropText.font);
            Main.CopyTextShadow(text, Main.canvas.dropText.gameObject);
            ind.numDisplay = text.GetComponent<Text>();
            var a = new System.Random().NextDouble() * 2 * Math.PI;
            ind.angle = new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a));
            return ind;
        }
        void Update()
        {
            if (Time.deltaTime > 0)
            {
                timePassed += Time.deltaTime * 1.5f;
                var newPos = startPos + angle * timePassed;
                newPos.y += (float)(Math.Pow(1, 2) - Math.Pow(timePassed - 1, 2)) / 2;
                transform.position = newPos;
                var dirVec = Helper.MainCamera.transform.position - transform.position;
                var a = Mathf.Acos(dirVec.z / dirVec.XZOnly().magnitude) / (float)Math.PI * 180f + 180;
                transform.rotation = Quaternion.Euler(0, dirVec.x < 0 ? -a : a, 0);
                numDisplay.color = new Color(numDisplay.color.r, numDisplay.color.g, numDisplay.color.b, 1 - timePassed / 3);
                if (numDisplay.color.a <= 0)
                    Destroy();
            }
        }
        static string DamageString(float amount) => (amount > 0 ? "+" : "") + ((int)amount).ToString();
        public void Destroy()
        {
            Destroy(gameObject);
        }
    }

    public class SharkIndicator : MonoBehaviour
    {
        Memory<SharkIndicator, bool> active = new Memory<SharkIndicator, bool>(x => Main.sharkMode != SharkMode.None && x.shark.biteRaftState && Main.sharkMode.Contains(x.currentMode) && CanvasHelper.ActiveMenu != MenuType.Inventory);
        Memory<SharkIndicator, bool> activeSecond = new Memory<SharkIndicator, bool>(x => Main.sharkAlertScreen || x.text.transform.position.z >= 0);
        Memory<SharkIndicator, Vector3> position = new Memory<SharkIndicator, Vector3>(x =>
        {
            var result = Helper.MainCamera.WorldToScreenPoint((Raft_Network.IsHost && x.shark.biteRaftState && x.shark.biteRaftState.IsActive() && x.shark.biteRaftState.currentTargetBlock ? (Component)x.shark.biteRaftState.currentTargetBlock : x.shark).transform.position);
            if (Main.sharkAlertScreen)
            {
                var border = new Vector2(x.text.preferredWidth + Screen.height * 0.2f, x.text.preferredHeight + Screen.height * 0.2f) / 2;
                if (result.z >= 0 && result.x > border.x && result.y > border.y && result.x <= Screen.width - border.x && result.y <= Screen.height - border.y)
                    return result;
                var halfScreen = new Vector2(Screen.width, Screen.height) / 2;
                var size = halfScreen - border;
                if (result.z < 0)
                    result = -result;
                var r = Mathf.Max(Mathf.Abs(result.x - halfScreen.x) / size.x, Mathf.Abs(result.y - halfScreen.y) / size.y);
                return new Vector3((result.x - halfScreen.x) / r + halfScreen.x, (result.y - halfScreen.y) / r + halfScreen.y, 1);
            }
            return result;
        });
        Memory<SharkIndicator, Vector2> size = new Memory<SharkIndicator, Vector2>(x => new Vector2(x.text.preferredWidth, x.text.preferredHeight));
        Memory<SharkIndicator, Color> color = new Memory<SharkIndicator, Color>(x =>
        {
            var a = (float)Math.Sin(x.timePassed * 5) / 4 + 0.75f;
            switch (x.currentMode)
            {
                case SharkMode.WhileAttacking:
                    return ((Color)Main.attacking).Tweak(a: a);
                case SharkMode.WhileCharging:
                    return ((Color)Main.charging).Tweak(a: a);
                case SharkMode.WhileSearching:
                    return ((Color)Main.searching).Tweak(a: a);
                default:
                    return Color.black;
            }
        });
        SharkMode currentMode => Raft_Network.IsHost
            ? shark.IsSearching() ? SharkMode.WhileSearching : shark.biteRaftState.currentTargetBlock ? shark.biteRaftState.isBitingRaft ? SharkMode.WhileAttacking : SharkMode.WhileCharging : SharkMode.None
            : shark.IsSearching() ? SharkMode.WhileSearching : shark.currentState == shark.biteRaftState ? shark.animController.GetBool("BitingRaft") ? SharkMode.WhileAttacking : SharkMode.WhileCharging : SharkMode.None;
        float timePassed;
        Text text;
        AI_StateMachine_Shark shark;
        public static SharkIndicator Create(AI_StateMachine_Shark shark)
        {
            var rect = Main.canvas.GetComponent<RectTransform>();
            var o = new GameObject("SharkIndicator").AddComponent<SharkIndicator>();
            Main.created.Add(o.gameObject);
            o.transform.SetParent(Main.canvas.transform, false);
            o.transform.SetAsFirstSibling();
            o.timePassed = 0;
            o.shark = shark;
            o.text = Main.CreateText(o.transform, rect.sizeDelta.x * rect.localScale.x * 0.45f, rect.sizeDelta.y * rect.localScale.y * 0.45f, "alerte", Main.canvas.dropText.fontSize * 2, Color.red, rect.sizeDelta.x * rect.localScale.x * 0.06f, Main.canvas.dropText.fontSize * 3, Main.canvas.dropText.font).GetComponent<Text>();
            Main.CopyTextShadow(o.text.gameObject, Main.canvas.dropText.gameObject);
            o.text.rectTransform.sizeDelta = new Vector2(o.text.preferredWidth, o.text.preferredHeight);
            o.SetActive(false);
            return o;
        }
        public void SetActive(bool visible) => text.gameObject.SetActive(visible);
        void Update()
        {
            if (!shark)
            {
                Destroy();
                return;
            }
            var flag = false;
            if (active.GetValue(this, out var v))
            {
                flag = true;
                if (!v)
                    SetActive(v);
            }
            if (v)
            {
                if (position.GetValue(this, out var c))
                    text.transform.position = c;
                if (activeSecond.GetValue(this,out var n) || flag)
                    SetActive(n);
                if (size.GetValue(this, out var s))
                    text.rectTransform.sizeDelta = s;
                if (Time.deltaTime > 0)
                {
                    timePassed += Time.deltaTime;
                    if (color.GetValue(this, out var nc))
                        text.color = nc;
                }
            }
        }
        public void Destroy() => Object.Destroy(gameObject);
    }

    public class CenterPointer : MonoBehaviour
    {
        float timePassed;
        Text text;
        Memory<CenterPointer, bool> active = new Memory<CenterPointer, bool>(x => Main.showCenter && CanvasHelper.ActiveMenu != MenuType.Inventory && x.text.transform.position.z > 0);
        Memory<Vector3?> position = new Memory<Vector3?>(() => Helper.MainCamera && ComponentManager<Raft>.Value ? Helper.MainCamera.WorldToScreenPoint(ComponentManager<Raft>.Value.transform.position) : default(Vector3?));
        Memory<CenterPointer, Vector2> size = new Memory<CenterPointer, Vector2>(x => new Vector2(x.text.preferredWidth, x.text.preferredHeight));
        Memory<CenterPointer, Color> color = new Memory<CenterPointer, Color>(x => ((Color)Main.center).Tweak(a: (float)Math.Sin(x.timePassed * 5) / 4 + 0.75f));
        public static CenterPointer Create()
        {
            var o = new GameObject("CenterPointer").AddComponent<CenterPointer>();
            Main.created.Add(o.gameObject);
            o.transform.SetParent(Main.canvas.transform, false);
            o.transform.SetAsFirstSibling();
            o.timePassed = 0;
            o.text = Main.CreateText(o.transform, 0, 0, "◈", Main.canvas.dropText.fontSize * 2, Color.green, Main.canvas.dropText.fontSize * 3, Main.canvas.dropText.fontSize * 3, Main.canvas.dropText.font, "CenterText").GetComponent<Text>();
            Main.CopyTextShadow(o.text.gameObject, Main.canvas.dropText.gameObject);
            o.text.rectTransform.sizeDelta = new Vector2(o.text.preferredWidth, o.text.preferredHeight);
            o.SetActive(false);
            return o;

        }
        public void SetActive(bool visible) => text.gameObject.SetActive(visible);
        void Update()
        {
            if (position.GetValue(out var c) && c != null)
                text.transform.position = c.Value;
            if (active.GetValue(this, out var v))
                SetActive(v);
            if (v)
            {
                if (size.GetValue(this,out var s))
                    text.rectTransform.sizeDelta = s;
                if (Time.deltaTime > 0)
                {
                    timePassed += Time.deltaTime;
                    if (color.GetValue(this,out var cl))
                        text.color = cl;
                }
            }
        }
        public void Destroy() => Destroy(gameObject);
    }

    public class DuraText : MonoBehaviour
    {
        public GameObject container;
        public Text textComponent;
    }

    public class HUDEquipmentDisplay : MonoBehaviour
    {
        public Slot displaySlot;
        public Slot parentSlot;
        public RectTransform rectTransform;
        Vector2 basePosition;
        Vector2 offset;
        Memory<HUDEquipmentDisplay, Vector2> index = new Memory<HUDEquipmentDisplay, Vector2>(x =>
        {
            var screenSize = Main.canvas.GetComponent<RectTransform>().sizeDelta;
            var index = x.transform.GetSiblingIndex();
            return new Vector2((x.basePosition.x + x.offset.x * index) * Main.aspectRatio - screenSize.x / 2, x.basePosition.y + x.offset.y * index - screenSize.y / 2 + x.rectTransform.sizeDelta.y);
        });
        Memory<bool> active = new Memory<bool>(() => CanvasHelper.ActiveMenu == MenuType.None && Main.showHUDEquipment);
        public static HUDEquipmentDisplay Create(Transform parent, Slot parentSlot, GameObject prefab, Vector2 basePosition, Vector2 displaySize)
        {
            var top = new GameObject().transform;
            Main.created.Add(top.gameObject);
            top.SetParent(parent, false);
            var obj = Instantiate(prefab, top);
            var dis = top.gameObject.AddComponent<HUDEquipmentDisplay>();
            dis.parentSlot = parentSlot;
            dis.displaySlot = obj.GetComponent<Slot>();
            dis.rectTransform = obj.GetComponent<RectTransform>();
            obj.transform.localScale = prefab.transform.localScale;
            dis.rectTransform.localPosition = new Vector2(0, 0);
            dis.rectTransform.sizeDelta = displaySize;
            dis.offset = new Vector2(0, dis.rectTransform.sizeDelta.y * 1.2f);
            dis.basePosition = basePosition;
            dis.active.GetValue(out var a);
            dis.rectTransform.gameObject.SetActive(a);
            return dis;
        }

        public void Update()
        {
            if (active.GetValue(out var a))
                rectTransform.gameObject.SetActive(a);
            if (!a)
                return;
            if (parentSlot.HasValidItemInstance() && parentSlot.itemInstance.BaseItemMaxUses > 1)
            {
                if (!Equals(displaySlot.itemInstance, parentSlot.itemInstance))
                    displaySlot.SetItem(parentSlot.itemInstance?.Clone());
                if (index.GetValue(this, out var v))
                    rectTransform.localPosition = v;
            }
            else
                Destroy();
        }

        static bool Equals(ItemInstance a, ItemInstance b) => a != null && b != null ? a.UniqueIndex == b.UniqueIndex && a.Uses == b.Uses && a.Amount == b.Amount && a.exclusiveString == b.exclusiveString : (a == b);

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }

    class StatManager
    {
        string messagePrefix;
        Func<bool, bool> modify;
        string nT;
        string aT;
        string nF;
        string aF;
        public StatManager(string prefix, Func<bool, bool> editor, string nowTrue = "now visible", string alreadyTrue = "already visible", string nowFalse = "now hidden", string alreadyFalse = "already hidden")
        {
            messagePrefix = prefix;
            modify = editor;
            nT = nowTrue;
            aT = alreadyTrue;
            nF = nowFalse;
            aF = alreadyFalse;
        }
        public string Set(bool value)
        {
            return messagePrefix + (value ? (modify(true) ? nT : aT) : (modify(false) ? nF : aF));
        }
    }

    class Memory<R>
    {
        Func<R> getter;
        R last;
        public R Last => last;
        public Memory(Func<R> Getter) => getter = Getter;
        public static implicit operator Memory<R>(Func<R> Getter) => new Memory<R>(Getter);
        public bool GetValue(out R current) => GetValue(out _, out current);
        public bool GetValue(out R prev, out R current)
        {
            current = getter == null ? default : getter();
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }

    class Memory<A, R>
    {
        Func<A, R> getter;
        R last;
        public R Last => last;
        public Memory(Func<A, R> Getter) => getter = Getter;
        public static implicit operator Memory<A, R>(Func<A, R> Getter) => new Memory<A, R>(Getter);
        public bool GetValue(A a, out R current) => GetValue(a, out _, out current);
        public bool GetValue(A a, out R prev, out R current)
        {
            current = getter == null ? default : getter(a);
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }

    class Memory<A, B, R>
    {
        Func<A, B, R> getter;
        R last;
        public R Last => last;
        public Memory(Func<A, B, R> Getter) => getter = Getter;
        public static implicit operator Memory<A, B, R>(Func<A, B, R> Getter) => new Memory<A, B, R>(Getter);
        public bool GetValue(A a, B b, out R current) => GetValue(a, b, out _, out current);
        public bool GetValue(A a, B b, out R prev, out R current)
        {
            current = getter == null ? default : getter(a, b);
            prev = last;
            last = current;
            return !(current?.Equals(prev) ?? prev?.Equals(current) ?? true);
        }
    }
}