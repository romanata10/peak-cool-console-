using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace PeakCoolConsole
{
    [BepInPlugin("com.peakcoolconsole.helper", "Peak Cool Console Helper", "0.6.0")]
    [BepInDependency("com.keklick1337.peak.advancedconsole", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private bool visible;
        private bool savedCursorVisible;
        private CursorLockMode savedCursorLock;
        private bool legacyInputUnavailable;
        private bool paused;
        private float previousTimeScale = 1f;
        private bool fogModified;
        private bool previousFog;
        private float previousDensity;
        private float density = 0.03f;
        private Vector2 scroll;
        private Vector2 windowScroll;
        private Rect window = new Rect(30, 30, 720, 700);
        private string target = "";
        private string item = "";
        private string countText = "10";
        private int coconutsRemaining;
        private string coconutTarget;
        private float nextCoconut;
        private int coconutScene;
        private string status = "F1 opens AdvancedConsole; F8 opens this menu.";
        private bool fly;
        private float flySpeed = 10f;
        private float speed = 1f;
        private readonly Dictionary<Rigidbody, bool> savedGravity = new Dictionary<Rigidbody, bool>();
        private object speedMovement;
        private float oldSpeed;
        private Component flightCharacter;

        private static readonly string[] Commands = {
            "Players: InfoCommands.ListPlayers",
            "Items: SpawnCommands.ListItems",
            "Search items: SpawnCommands.SearchItems searchTerm",
            "Spawn item: SpawnCommands.SpawnItemByName itemNameOrID",
            "Coconut: FunCommands.DropCoconutOnPlayer playerName",
            "Banana slip: RPCCommands.TriggerBananaPeel playerName",
            "Sleep everyone: RPCCommands.PassOutAllPlayers",
            "Wake everyone: RPCCommands.UnPassOutAllPlayers",
            "Win: RPCCommands.ForceWin",
            "Go to peak: MapCommands.GoToPeak",
            "Everyone to beach (host): RPCCommands.SyncGoToBeach",
            "Speed player (host): ServerAdmin.SpeedPlayer playerName",
            "Slow player (host): ServerAdmin.SlowPlayer playerName",
            "Spawn bees near self: SpawnCommands.SpawnBeeSwarm",
            "Scoutmaster: ScoutMasterCommands.CallScoutmaster playerName",
            "Stop Scoutmaster: ScoutMasterCommands.StopScoutmaster",
            "Spawn Scoutmaster (host): ScoutMasterCommands.SpawnScoutmaster"
        };

        private void Awake()
        {
            Logger.LogInfo("Peak Cool Console Helper 0.6.0 loaded. Press F8 or F10 to open the menu.");
        }

        private void Update()
        {
            if (KeyDown("f8Key", KeyCode.F8) || KeyDown("f10Key", KeyCode.F10)) SetVisible(!visible);
            if (coconutsRemaining > 0 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != coconutScene)
            {
                coconutsRemaining = 0;
                status = "Coconut queue cancelled after scene change.";
            }
            if (coconutsRemaining > 0 && Time.realtimeSinceStartup >= nextCoconut)
            {
                if (!RunCommand("FunCommands", "DropCoconutOnPlayer", coconutTarget))
                    coconutsRemaining = 0;
                else
                {
                    coconutsRemaining--;
                    nextCoconut = Time.realtimeSinceStartup + 0.2f;
                    status = "Coconut calls remaining: " + coconutsRemaining + ". Delivery is not guaranteed.";
                }
            }
        }

        private void LateUpdate()
        {
            if (visible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            if (value)
            {
                savedCursorLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
            }
            else
            {
                Cursor.lockState = savedCursorLock;
                Cursor.visible = savedCursorVisible;
            }
            visible = value;
            Logger.LogInfo(value ? "Menu opened." : "Menu closed.");
        }

        // PEAK versions may use Unity's new input system instead of the legacy API.
        // Reflection keeps the plugin loadable even when that optional assembly is absent.
        private static object CurrentKeyboard()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.InputSystem");
            var type = assembly == null ? null : assembly.GetType("UnityEngine.InputSystem.Keyboard");
            var current = type == null ? null : type.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            return current == null ? null : current.GetValue(null, null);
        }

        private bool KeyState(string keyName, KeyCode fallback, bool pressedThisFrame)
        {
            try
            {
                var keyboard = CurrentKeyboard();
                if (keyboard != null)
                {
                    var key = ReadMember(keyboard, keyName);
                    var value = ReadMember(key, pressedThisFrame ? "wasPressedThisFrame" : "isPressed");
                    if (value is bool) return (bool)value;
                }
                if (legacyInputUnavailable) return false;
                return pressedThisFrame ? Input.GetKeyDown(fallback) : Input.GetKey(fallback);
            }
            catch (Exception ex)
            {
                if (!legacyInputUnavailable) Logger.LogWarning("Keyboard input unavailable: " + ex.Message);
                legacyInputUnavailable = true;
                return false;
            }
        }

        private bool KeyDown(string keyName, KeyCode fallback) { return KeyState(keyName, fallback, true); }
        private bool KeyHeld(string keyName, KeyCode fallback) { return KeyState(keyName, fallback, false); }

        private void FixedUpdate()
        {
            if (!fly) return;
            try
            {
                var character = LocalCharacter() as Component;
                if (character == null) { StopFlight(); status = "Flight stopped: local character unavailable."; return; }
                if (flightCharacter != null && flightCharacter != character) RestoreGravity();
                flightCharacter = character;
                var ragdoll = ReadMember(ReadMember(character, "refs"), "ragdoll");
                var parts = ReadMember(ragdoll, "partList") as IEnumerable;
                if (parts == null) { StopFlight(); status = "Flight unavailable: ragdoll not found."; return; }
                Vector3 direction = Vector3.zero;
                var camera = Camera.main;
                if (!visible && camera != null)
                {
                    if (KeyHeld("wKey", KeyCode.W)) direction += camera.transform.forward;
                    if (KeyHeld("sKey", KeyCode.S)) direction -= camera.transform.forward;
                    if (KeyHeld("dKey", KeyCode.D)) direction += camera.transform.right;
                    if (KeyHeld("aKey", KeyCode.A)) direction -= camera.transform.right;
                    if (KeyHeld("spaceKey", KeyCode.Space)) direction += Vector3.up;
                    if (KeyHeld("leftCtrlKey", KeyCode.LeftControl)) direction -= Vector3.up;
                }
                direction = Vector3.ClampMagnitude(direction, 1f) * flySpeed;
                int found = 0;
                foreach (var part in parts)
                {
                    var body = ReadMember(part, "Rig") as Rigidbody;
                    if (body == null) continue;
                    found++;
                    if (!savedGravity.ContainsKey(body)) savedGravity.Add(body, body.useGravity);
                    body.useGravity = false;
                    body.velocity = direction;
                }
                if (found == 0) { StopFlight(); status = "Flight unavailable: no rigidbodies found."; }
            }
            catch (Exception ex) { StopFlight(); status = "Flight error: " + ex.Message; Logger.LogWarning(ex); }
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(instance);
            var property = type.GetProperty(name, flags);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static object LocalCharacter()
        {
            var game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            var type = game == null ? null : game.GetType("Character");
            if (type == null) return null;
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField("localCharacter", flags);
            if (field != null) return field.GetValue(null);
            var property = type.GetProperty("localCharacter", flags);
            return property == null ? null : property.GetValue(null, null);
        }

        private void RestoreGravity()
        {
            foreach (var pair in savedGravity) if (pair.Key != null) pair.Key.useGravity = pair.Value;
            savedGravity.Clear();
            flightCharacter = null;
        }

        private void StopFlight() { fly = false; RestoreGravity(); }

        private void SetSpeed()
        {
            try
            {
                var movement = ReadMember(ReadMember(LocalCharacter(), "refs"), "movement");
                if (movement == null) { status = "Speed unavailable: no local movement component."; return; }
                var field = movement.GetType().GetField("movementModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null || field.FieldType != typeof(float)) { status = "Speed modifier not found."; return; }
                RestoreSpeed();
                oldSpeed = (float)field.GetValue(movement);
                speedMovement = movement;
                field.SetValue(movement, speed);
                status = "Local speed set; game effects may overwrite it.";
            }
            catch (Exception ex) { status = "Speed error: " + ex.Message; Logger.LogWarning(ex); }
        }

        private void RestoreSpeed()
        {
            if (speedMovement == null) return;
            try
            {
                var field = speedMovement.GetType().GetField("movementModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(speedMovement, oldSpeed);
            }
            catch (Exception ex) { Logger.LogWarning(ex); }
            speedMovement = null;
        }

        private void OnGUI()
        {
            if (visible)
            {
                window.width = Mathf.Min(720f, Screen.width);
                window.height = Mathf.Min(700f, Screen.height);
                window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width - window.width));
                window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, Screen.height - window.height));
                window = GUILayout.Window(908712, window, DrawWindow, "Peak Cool Console Helper | F8 / F10");
            }
        }

        private void DrawWindow(int id)
        {
            windowScroll = GUILayout.BeginScrollView(windowScroll);
            GUILayout.Label("LOCAL ONLY: pause and fog do not control other clients.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Resume local game" : "Pause local game")) TogglePause();
            if (GUILayout.Button("Restore local fog")) RestoreFog();
            GUILayout.EndHorizontal();
            GUILayout.Label("Local fog density: " + density.ToString("F3"));
            density = GUILayout.HorizontalSlider(density, 0.001f, 0.1f);
            if (GUILayout.Button("Apply local fog")) ApplyFog();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(fly ? "Stop flight" : "Start flight"))
            {
                if (fly) StopFlight(); else { fly = true; status = "Flight on: close menu, WASD + Space/Ctrl. Mind fall damage."; }
            }
            GUILayout.Label("Fly speed " + flySpeed.ToString("F0"));
            flySpeed = GUILayout.HorizontalSlider(flySpeed, 2f, 25f, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Local speed " + speed.ToString("F1") + "x");
            speed = GUILayout.HorizontalSlider(speed, 0.5f, 5f, GUILayout.Width(100));
            if (GUILayout.Button("Apply speed")) SetSpeed();
            if (GUILayout.Button("Restore speed")) { RestoreSpeed(); status = "Previous speed restored."; }
            GUILayout.EndHorizontal();
            GUILayout.Label("AdvancedConsole actions: enter the exact player name.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player", GUILayout.Width(60));
            target = GUILayout.TextField(target, 100);
            GUILayout.Label("Count 1-100", GUILayout.Width(90));
            countText = GUILayout.TextField(countText, 3, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Drop coconuts")) StartCoconuts();
            if (GUILayout.Button("Stop coconuts")) { coconutsRemaining = 0; status = "Coconut queue stopped."; }
            if (GUILayout.Button("Banana slip")) RunForTarget("RPCCommands", "TriggerBananaPeel");
            if (GUILayout.Button("Bees near self")) RunCommand("SpawnCommands", "SpawnBeeSwarm");
            if (GUILayout.Button("Tick")) RunForTarget("SpawnCommands", "TickPlayer");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Scoutmaster -> player")) RunForTarget("ScoutMasterCommands", "CallScoutmaster");
            if (GUILayout.Button("Stop Scoutmaster")) RunCommand("ScoutMasterCommands", "StopScoutmaster");
            if (GUILayout.Button("Spawn Scoutmaster (host)")) RunCommand("ScoutMasterCommands", "SpawnScoutmaster");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sleep all")) RunCommand("RPCCommands", "PassOutAllPlayers");
            if (GUILayout.Button("Wake all")) RunCommand("RPCCommands", "UnPassOutAllPlayers");
            if (GUILayout.Button("Go to peak")) RunCommand("MapCommands", "GoToPeak");
            if (GUILayout.Button("Win")) RunCommand("RPCCommands", "ForceWin");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All to beach (host)")) RunCommand("RPCCommands", "SyncGoToBeach");
            if (GUILayout.Button("Speed player (host)")) RunForTarget("ServerAdmin", "SpeedPlayer");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Teleport to player")) RunForTarget("Teleport", "To");
            if (GUILayout.Button("Bring player to me")) RunForTarget("Teleport", "PlayerToMe");
            if (GUILayout.Button("Bring all to me")) RunCommand("Teleport", "AllToMe");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Eruption on player")) RunForTarget("RPCCommands", "SpawnEruption");
            if (GUILayout.Button("Sleep player")) RunForTarget("RPCCommands", "PassOutPlayer");
            if (GUILayout.Button("Wake player")) RunForTarget("RPCCommands", "UnPassOutPlayer");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Item name or ID", GUILayout.Width(110));
            item = GUILayout.TextField(item, 100);
            if (GUILayout.Button("Spawn item"))
            {
                if (string.IsNullOrWhiteSpace(item)) status = "Enter an item name or ID.";
                else RunCommand("SpawnCommands", "SpawnItemByName", item.Trim());
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("List players (see F1)")) RunCommand("InfoCommands", "ListPlayers");
            if (GUILayout.Button("List items (see F1)")) RunCommand("SpawnCommands", "ListItems");
            if (GUILayout.Button("Random item")) RunCommand("SpawnCommands", "SpawnRandomItem");
            GUILayout.EndHorizontal();
            GUILayout.Label("Actions depend on AdvancedConsole, game version and network permissions.");
            GUILayout.Label("AdvancedConsole examples (copy, replace arguments, run in F1):");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(170));
            foreach (string entry in Commands)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Copy", GUILayout.Width(60)))
                {
                    int separator = entry.IndexOf(": ", StringComparison.Ordinal);
                    GUIUtility.systemCopyBuffer = entry.Substring(separator + 2);
                    status = "Copied. Replace arguments and paste into F1 console.";
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(status);
            if (GUILayout.Button("Close")) SetVisible(false);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, window.width, 20));
        }

        private void StartCoconuts()
        {
            int count;
            if (string.IsNullOrWhiteSpace(target)) { status = "Enter a player name."; return; }
            if (!int.TryParse(countText, out count) || count < 1 || count > 100)
            { status = "Count must be between 1 and 100."; return; }
            coconutTarget = target.Trim();
            coconutScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            coconutsRemaining = count;
            nextCoconut = Time.realtimeSinceStartup;
            status = "Coconut queue started; use Stop to cancel.";
        }

        private void RunForTarget(string type, string method)
        {
            if (string.IsNullOrWhiteSpace(target)) { status = "Enter a player name."; return; }
            RunCommand(type, method, target.Trim());
        }

        // Call only explicitly named AdvancedConsole methods. No compile-time dependency on
        // its implementation details; report missing or changed signatures rather than guessing.
        private bool RunCommand(string typeName, string methodName, params object[] args)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "com.keklick1337.peak.advancedconsole", StringComparison.OrdinalIgnoreCase));
                if (assembly == null) { status = "AdvancedConsole assembly not loaded."; return false; }
                var types = assembly.GetTypes().Where(t => t.Name == typeName).ToArray();
                if (types.Length != 1) { status = "Command class not found or ambiguous: " + typeName; return false; }
                var methods = types[0].GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == methodName && Compatible(m.GetParameters(), args)).ToArray();
                if (methods.Length != 1) { status = "Command signature unavailable or ambiguous: " + typeName + "." + methodName; return false; }
                var parameters = methods[0].GetParameters();
                var values = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i >= args.Length) values[i] = parameters[i].DefaultValue;
                    else if (parameters[i].ParameterType.FullName == "Photon.Realtime.Player" && args[i] is string)
                    {
                        values[i] = FindPhotonPlayer((string)args[i], parameters[i].ParameterType);
                        if (values[i] == null) return false;
                    }
                    else values[i] = args[i];
                }
                object result = methods[0].Invoke(null, values);
                if (result is bool && !(bool)result) { status = "Command reported failure: " + methodName; return false; }
                status = "Called " + typeName + "." + methodName + "; check game/console for result.";
                if (result is string && !string.IsNullOrEmpty((string)result)) status = (string)result;
                return true;
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                status = "Command failed: " + cause.Message;
                Logger.LogWarning(cause);
                return false;
            }
        }

        private static bool Compatible(ParameterInfo[] parameters, object[] args)
        {
            if (args.Length > parameters.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i >= args.Length) { if (!parameters[i].IsOptional) return false; }
                else if (args[i] == null || (!parameters[i].ParameterType.IsInstanceOfType(args[i]) &&
                    !(args[i] is string && parameters[i].ParameterType.FullName == "Photon.Realtime.Player"))) return false;
            }
            return true;
        }

        // AdvancedConsole 1.1.9 uses Photon Player arguments for several commands.
        // Do not guess on duplicate nicknames: targeting the wrong player is worse than failing.
        private object FindPhotonPlayer(string name, Type expectedType)
        {
            var photonAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                a.GetType("Photon.Pun.PhotonNetwork") != null);
            var network = photonAssembly == null ? null : photonAssembly.GetType("Photon.Pun.PhotonNetwork");
            var playersProperty = network == null ? null : network.GetProperty("PlayerList", BindingFlags.Public | BindingFlags.Static);
            var players = playersProperty == null ? null : playersProperty.GetValue(null, null) as IEnumerable;
            if (players == null) { status = "Photon player list unavailable. Join a lobby first."; return null; }
            object match = null;
            foreach (var player in players)
            {
                if (player == null || !expectedType.IsInstanceOfType(player)) continue;
                if (!string.Equals(ReadMember(player, "NickName") as string, name, StringComparison.Ordinal)) continue;
                if (match != null) { status = "Duplicate player name; cannot choose safely."; return null; }
                match = player;
            }
            if (match == null) status = "Player not found. Enter the exact lobby nickname.";
            return match;
        }

        private void TogglePause()
        {
            if (!paused)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                paused = true;
                status = "Local time scale paused; other clients are not paused.";
            }
            else RestorePause();
        }

        private void RestorePause()
        {
            if (!paused) return;
            Time.timeScale = previousTimeScale;
            paused = false;
            status = "Restored previous local time scale.";
        }

        private void ApplyFog()
        {
            if (!fogModified)
            {
                previousFog = RenderSettings.fog;
                previousDensity = RenderSettings.fogDensity;
                fogModified = true;
            }
            RenderSettings.fog = true;
            RenderSettings.fogDensity = density;
            status = "Local fog applied; appearance depends on the scene fog mode.";
        }

        private void RestoreFog()
        {
            if (!fogModified) return;
            RenderSettings.fog = previousFog;
            RenderSettings.fogDensity = previousDensity;
            fogModified = false;
            status = "Restored previous local fog.";
        }

        private void OnDestroy()
        {
            coconutsRemaining = 0;
            SetVisible(false);
            StopFlight();
            RestoreSpeed();
            RestorePause();
            RestoreFog();
        }
    }
}
