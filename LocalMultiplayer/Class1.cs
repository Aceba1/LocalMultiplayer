using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;
using System.Reflection;
using ModHelper.Config;

namespace LocalMultiplayer
{
    public class OverrideBase
    {
        internal static List<string> NewCamTargets = new List<string>();

        public static Dictionary<string, Overrider> Overrides;
        public static ModConfig modconfig;

        public static void Init()
        {
            var harmony = HarmonyInstance.Create("ttmm.localmp");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            modconfig = new ModConfig();
            modconfig.BindConfig<OverrideBase>(null, "Overrides", true);
            if (Overrides == null)
            {
                Overrides = new Dictionary<string, Overrider>();
            }
            foreach(var o in Overrides)
            {
                o.Value.Validate();
            }
            new GameObject("Override Controller").AddComponent<OverrideController>();
        }
        private static class Patches
        {
            [HarmonyPatch(typeof(ModuleTechController), "ExecuteControl")]
            private static class ControlOverride
            {
                private static bool Prefix(ModuleTechController __instance)
                {
                    try
                    {
                        var name = __instance.block.tank.name;
                        if (Overrides.ContainsKey(name))
                        {
                            Overrides[name].GetInput(__instance.block.tank.control);
                            return false;
                        }
                        return true;
                    }
                    catch { }
                    return true;
                }
            }

            static FieldInfo tank = typeof(TankBeam).GetField("tank", BindingFlags.NonPublic | BindingFlags.Instance), m_NudgeInput = typeof(TankBeam).GetField("m_NudgeInput", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPatch(typeof(TankBeam), "UpdateTankFloat")]
            private static class ControlOverrideBuildBeam
            {
                private static void Prefix(TankBeam __instance)
                {
                    try
                    {
                        string name = (tank.GetValue(__instance) as Tank).name;
                        if (Overrides.ContainsKey(name))
                        {
                            m_NudgeInput.SetValue(__instance, Overrides[name].BuildBeamAxis());
                        }
                    }
                    catch { }
                }
            }
        }

        internal class OverrideController : MonoBehaviour
        {
            bool IsSettingKeybind = false;
            string SetTrigger = "";
            int ID = 5189012;
            bool Show = false, ShowControllerSetup = false;
            Rect rect = new Rect(0, 0, 800, 600), rect2 = new Rect(0, 0, 800, 600);
            void Start()
            {
                modconfig.UpdateConfig += ConfigUpdate;
            }

            void ConfigUpdate()
            {
                IsSettingKeybind = false;
                Names = null;
                Selected = "";
                SelectionIndex = -1;
            }

            void OnGUI()
            {
                if (Show)
                {
                    rect = GUI.Window(ID, rect, GUIWindow, "Override Controllers");
                }
            }
            void Update()
            {
                if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.M))
                {
                    Show = !Show;
                    if (!Show)
                    {
                        modconfig.WriteConfigJsonFile();
                        IsSettingKeybind = false;
                    }
                }
            }
            Vector2 scroll1, scroll2, scroll3;
            string Selected = "";
            string[] Names;
            int SelectionIndex, TriggerIndex;
            void GUIWindow(int ID)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create New Controller"))
                {
                    int NewID = 2;
                    while (Overrides.ContainsKey("Player Tech " + NewID.ToString())) NewID++;
                    var o = new Overrider();
                    o.Validate();
                    Overrides.Add("Player Tech " + NewID.ToString(), o);

                    Names = null;
                }
                if (SelectionIndex != -1 && Overrides.Count != 0)
                {
                    if (GUILayout.Button("Duplicate Sel."))
                    {
                        int NewID = 2;
                        while (Overrides.ContainsKey(Selected + " " + NewID.ToString())) NewID++;
                        Overrides.Add(Selected + " " + NewID.ToString(), new Overrider(Overrides[Selected]));
                        Names = null;
                    }
                    if (GUILayout.Button("Delete Sel."))
                    {
                        Names = null;
                        Overrides.Remove(Selected);
                        Selected = "";
                        SelectionIndex = -1;
                        SetTrigger = "";
                    }
                }
                GUILayout.EndHorizontal();
                if (Overrides.Count != 0)
                {
                    scroll1 = GUILayout.BeginScrollView(scroll1, false, false);
                    if (Names == null) Names = Overrides.Keys.ToArray();
                    int nSI = GUILayout.SelectionGrid(SelectionIndex, Names, 5);
                    if (nSI != SelectionIndex && nSI != -1)
                    {
                        SelectionIndex = nSI;
                        try
                        {
                            Selected = Names[SelectionIndex];
                        }
                        catch
                        {
                            SelectionIndex = -1;
                        }
                        SetTrigger = "";
                    }
                    if (!Overrides.ContainsKey(Selected))
                    {
                        SelectionIndex = -1;
                    }
                    GUILayout.EndScrollView();
                    if (SelectionIndex != -1)
                    {
                        var Override = Overrides[Selected];
                        GUILayout.BeginHorizontal();
                        bool Joystick = Override.CurrentJoystick != -1;
                        bool SetOn = GUILayout.Toggle(Joystick, "");
                        Override.CurrentJoystick = SetOn ? (!Joystick ? 0 : Override.CurrentJoystick) : -1;
                        GUILayout.Label("Use Joystick : " + (Joystick ? "Joystick #" + (Override.CurrentJoystick + 1).ToString() : "Off"));
                        if (Override.CurrentJoystick != -1)
                        {
                            int val = Override.CurrentJoystick + 1;
                            Override.CurrentJoystick = Mathf.RoundToInt(GUILayout.HorizontalSlider(val, 1, 11)) - 1;
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        Override.ReverseSteering = GUILayout.Toggle(Override.ReverseSteering, "");
                        GUILayout.Label("Invert Reverse Steering");
                        bool camiswaiting = NewCamTargets.Contains(Selected);
                        if (GUILayout.Button(camiswaiting ? "Camera is waiting..." : "Toggle Camera"))
                        {
                            if (camiswaiting)
                            {
                                NewCamTargets.Remove(Selected);
                            }
                            else
                            {
                                NewCamTargets.Add(Selected);
                            }
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Label("Name of techs to override with this controller");
                        string oldsel = Selected;
                        GUI.SetNextControlName("TechName");
                        Selected = GUILayout.TextField(Selected);
                        if (oldsel != Selected)
                        {
                            if (Overrides.ContainsKey(Selected))
                            {
                                Selected = oldsel;
                            }
                            else
                            {
                                Overrides.Remove(oldsel);
                                Overrides.Add(Selected, Override);
                                for (int i = 0; i < Names.Length; i++)
                                {
                                    if (Names[i] == oldsel)
                                    {
                                        Names[i] = Selected;
                                        break;
                                    }
                                }
                                GUI.FocusControl("TechName");
                            }
                        }

                        if (IsSettingKeybind)
                        {
                            if (Override.CurrentJoystick != -1)
                            {
                                IsSettingKeybind = false;
                            }
                            else
                            {
                                var e = Event.current;
                                if (e.isKey)
                                {
                                    Override.Drive[SetTrigger] = e.keyCode;
                                    IsSettingKeybind = false;
                                }
                            }
                        }


                        if (Override.CurrentJoystick != -1)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.BeginVertical();
                                {
                                    GUILayout.Label($"Drive Turning : {(Override.JoystickAxisX == -1 ? "No Axis" : "Axis " + Override.JoystickAxisX)}");
                                    Override.JoystickAxisX = Mathf.RoundToInt(GUILayout.HorizontalSlider(Override.JoystickAxisX, -1, 19));
                                }
                                GUILayout.EndVertical();
                                GUILayout.BeginVertical();
                                {
                                    GUILayout.Label($"Drive Throttle : {(Override.JoystickAxisY == -1 ? "No Axis" : "Axis " + Override.JoystickAxisY)}");
                                    Override.JoystickAxisY = Mathf.RoundToInt(GUILayout.HorizontalSlider(Override.JoystickAxisY, -1, 19));
                                }
                                GUILayout.EndVertical();
                            }
                            GUILayout.EndHorizontal();
                            if (SetTrigger != "")
                            {
                                var key = Override.Drive[SetTrigger];
                                GUILayout.Label($"{SetTrigger} : Joystick button {(key < KeyCode.Joystick1Button0 ? (key == KeyCode.None?"None":"?") : ((int)key - 350).ToString())}");
                                var NewVal = (KeyCode)Mathf.RoundToInt(GUILayout.HorizontalSlider((int)key, 349, 369));
                                if (NewVal == KeyCode.JoystickButton19) NewVal = KeyCode.None;
                                Override.Drive[SetTrigger] = NewVal;
                            }
                        }
                        else
                        {
                            if (SetTrigger != "")
                            {
                                bool Clicked = GUILayout.Button(IsSettingKeybind ? "Press a key for use" : Override.Drive[SetTrigger].ToString());
                                if (Clicked && IsSettingKeybind) Override.Drive[SetTrigger] = KeyCode.None;
                                IsSettingKeybind = Clicked != IsSettingKeybind;
                            }
                        }
                        GUILayout.Label("Choose a function below to change the key bound to it");
                        scroll2 = GUILayout.BeginScrollView(scroll2);
                        GUI.changed = false;
                        TriggerIndex = GUILayout.SelectionGrid(TriggerIndex, Overrider.DriveReference, 2);
                        if (GUI.changed && SelectionIndex != -1)
                        {
                            SetTrigger = Overrider.DriveReference[TriggerIndex];
                            IsSettingKeybind = false;
                        }
                        GUILayout.EndScrollView();
                    }
                }
                GUI.DragWindow();
            }
        }

        public class Overrider
        {
            public class ControllerCam : MonoBehaviour
            {
                public static bool IsMoving = false;
                public bool isMoving = false;
                Tank BoundTank;
                string TankName;
                Camera Render;

                public static void Create(Tank tank)
                {
                    new GameObject("Cam").AddComponent<ControllerCam>().SetCamera(tank).parent = tank.transform;
                }
                Transform SetCamera(Tank tank)
                {
                    Render = gameObject.AddComponent<Camera>();
                    Start = new Vector2(0, 0);
                    Stretch = new Vector2(200, 150);
                    Render.rect = new Rect(Start.x / Screen.width, Start.y / Screen.height, Stretch.x / Screen.width, Stretch.y / Screen.height);
                    BoundTank = tank;
                    TankName = tank.name;
                    return transform;
                }

                Vector2 Start, Stretch;
                Vector2 OldValue;
                Vector2 StartMove;
                bool IsScaling;
                public float GetYAngle(Vector3 v1, Vector3 v2)
                {
                    return Mathf.Rad2Deg * Mathf.Atan2(v1.x - v2.x, v1.z - v2.z);
                }
                void OnGUI()
                {
                    if (BoundTank == null || Singleton.playerTank == null)
                        return;

                    var angle = Mathf.RoundToInt(GetYAngle(Vector3.zero, transform.InverseTransformPoint(Singleton.playerTank.rootBlockTrans.position)));

                    string anglechar = "↓";
                    if (angle >157 || angle < -157) anglechar = "↑";
                    else if (angle >  112)          anglechar = "↖";
                    else if (angle < -112)          anglechar = "↗";
                    else if (angle >   67)          anglechar = "←";
                    else if (angle <  -67)          anglechar = "→";
                    else if (angle >   22)          anglechar = "↙";
                    else if (angle <  -22)          anglechar = "↘";

                    GUI.Label(new Rect(Start.x, Screen.height - Start.y - Stretch.y, Stretch.x - 30, 30), TankName);
                    GUI.Box(new Rect(Start.x + Stretch.x - 30, Screen.height - Start.y - Stretch.y, 30, 30), anglechar);
                }

                void Update()
                {
                    if (BoundTank == null || BoundTank.name != TankName)
                    {
                        if (Overrides.ContainsKey(TankName))
                            {
                            NewCamTargets.Add(TankName);
                        }
                        if (isMoving)
                            IsMoving = false;
                        Destroy(gameObject);
                        return;
                    }
                    var euler = Quaternion.Euler(0, BoundTank.rootBlockTrans.rotation.eulerAngles.y, 0);
                    var offset = euler * new Vector3(0f, 2f, -4f);
                    offset = offset * BoundTank.blockBounds.extents.magnitude + offset;

                    transform.position = BoundTank.WorldCenterOfMass + offset;
                    transform.rotation = Quaternion.Euler(15, BoundTank.rootBlockTrans.rotation.eulerAngles.y, 0);

                    if (!IsMoving)
                    {
                        if (Input.GetMouseButtonDown(0))
                        {
                            Vector2 mouse = GUIUtility.ScreenToGUIPoint(new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                            if (new Rect(Start.x + Stretch.x - 30, Start.y+ Stretch.y - 30, 30, 30).Contains(mouse))
                            {
                                IsMoving = true; isMoving = true; IsScaling = true;
                                StartMove = mouse;
                                OldValue = Stretch;
                                //Start = new Vector2(Render.rect.x * Screen.width, Render.rect.y * Screen.height);
                            }
                            else if (new Rect(Start, Stretch).Contains(mouse))
                            {
                                IsMoving = true; isMoving = true; IsScaling = false;

                                Stretch = new Vector2(Render.rect.width * Screen.width, Render.rect.height * Screen.height);
                                StartMove = mouse;
                                OldValue = Start;
                            }
                        }
                    }
                    if (isMoving)
                    {
                        Vector2 mouse = GUIUtility.ScreenToGUIPoint(new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                        if (IsScaling)
                        {
                            Stretch = mouse - StartMove + OldValue;
                        }
                        else
                        {
                            Start = mouse - StartMove + OldValue;
                        }
                        Stretch.x = Mathf.Max(80, Stretch.x);
                        Stretch.y = Mathf.Max(60, Stretch.y);
                        Start.x = Mathf.Min(Screen.width - Stretch.x, Mathf.Max(0, Start.x));
                        Start.y = Mathf.Min(Screen.height - Stretch.y, Mathf.Max(0, Start.y));
                        Render.rect = new Rect(Start.x / Screen.width, Start.y / Screen.height, Stretch.x / Screen.width, Stretch.y / Screen.height);
                        if (!Input.GetMouseButton(0))
                        {
                            IsMoving = false;
                            isMoving = false;
                        }
                    }
                }
            }

            //static MethodInfo _ApplyThrottle;
            //public static void ApplyThrottle(TankControl This, bool enabled, ref float input, ref float throttleValue, ref float lastInput, ref float inputTiming)
            //{
            //    if (_ApplyThrottle == null)
            //    {
            //        var t = typeof(TankControl);
            //        _ApplyThrottle = t.GetMethod("ApplyThrottle", BindingFlags.Instance | BindingFlags.NonPublic);
            //    }
            //    _ApplyThrottle.Invoke(This, new object[] { enabled, input, throttleValue, lastInput, inputTiming });
            //}
            public Dictionary<string, KeyCode> Drive = new Dictionary<string, KeyCode>();

            public int JoystickAxisX = -1, JoystickAxisY = -1;

            public int CurrentJoystick = -1;

            public static string[] DriveReference = new string[]{
                // "MoveX_MoveRight", 
                // "MoveX_MoveLeft", 
                // "MoveY_MoveUp", 
                // "MoveY_MoveDown", 
                 "MoveZ_MoveForward",
                 "MoveZ_MoveBackward", 
                // "RotateX_PitchUp", 
                // "RotateX_PitchDown", 
                 "RotateY_YawLeft",
                 "RotateY_YawRight", 
                // "RotateZ_RollRight", 
                // "RotateZ_RollLeft",
                 "BoostPropellers",
                 "BoostJets",
                 "FireControl",
                 "BuildBeam",
                 "DetonateExplosiveBolt",
                 "Anchor"};
            public Overrider() { }
            public void Validate()
            {
                foreach(var Key in DriveReference)
                {
                    if (!Drive.ContainsKey(Key))
                    {
                        Drive.Add(Key, KeyCode.None);
                    }
                }
            }
            public Overrider(Overrider Copy)
            {
                foreach (var Key in DriveReference)
                {
                    if (Copy.Drive.ContainsKey(Key))
                        Drive.Add(Key, Copy.Drive[Key]);
                    else
                        Drive.Add(Key, KeyCode.None);
                }
            }

            public bool ReverseSteering = false;

            public static bool ReadInputJoystick(int Joystick, KeyCode Button)
            {
                if (Joystick == -1) return false;
                return Input.GetKey(Joystick * 20 + Button);
            }

            public bool ReadInput(string Key)
            {
                if (!Drive.ContainsKey(Key)) return false;
                return CurrentJoystick == -1 ? Input.GetKey(Drive[Key]) : ReadInputJoystick(CurrentJoystick, Drive[Key]);
            }
            List<string> HeldInputs = new List<string>();
            public bool ReadInputDown(string Key)
            {
                if (!Drive.ContainsKey(Key)) return false;
                bool Active = CurrentJoystick == -1 ? Input.GetKey(Drive[Key]) : ReadInputJoystick(CurrentJoystick, Drive[Key]);
                if (Active)
                {
                    if (HeldInputs.Contains(Key))
                    {
                        return false;
                    }
                    HeldInputs.Add(Key);
                    return true;
                }
                HeldInputs.Remove(Key);
                return false;
            }
            public bool ReadInputUp(string Key)
            {
                if (!Drive.ContainsKey(Key)) return false;
                bool Active = CurrentJoystick == -1 ? Input.GetKey(Drive[Key]) : ReadInputJoystick(CurrentJoystick, Drive[Key]);
                if (Active)
                {
                    if (!HeldInputs.Contains(Key))
                    {
                        HeldInputs.Add(Key);
                    }
                    return false;
                }
                return HeldInputs.Remove(Key);
            }
            public static float ReadAxisJoystick(int Joystick, int Axis)
            {
                if (Joystick == -1 || Axis == -1)
                    return 0f;
                var value = Input.GetAxisRaw($"Joy{Joystick + 1}Axis{Axis + 1}");
                return Mathf.Abs(value) > 0.1f ? value : 0f;
            }

            public float ReadAxis(string KeyPositive, string KeyNegative, int JoystickAxis = -1)
            {
                return Mathf.Clamp((ReadInput(KeyPositive) ? 1 : 0) + (ReadInput(KeyNegative) ? -1 : 0) - ReadAxisJoystick(CurrentJoystick, JoystickAxis),-1,1);
            }

            private int AnchorCache;
            //private Vector3 m_ThrottleLastInput;
            public void GetInput(TankControl This)
            {
                var Tank = This.GetComponentInParent<Tank>();
                if (NewCamTargets.Contains(Tank.name))
                {
                    NewCamTargets.Remove(Tank.name);
                    ControllerCam cam = Tank.gameObject.GetComponentInChildren<ControllerCam>();
                    if (cam == null)
                    {
                        ControllerCam.Create(Tank);
                    }
                    else
                    {
                        if (cam.isMoving)
                            ControllerCam.IsMoving = false;
                        GameObject.Destroy(cam.gameObject);
                    }
                }
                //Vector3 inputMovement = new Vector3(ReadDriveAxisPair("MoveX_MoveRight", "MoveX_MoveLeft"), ReadDriveAxisPair("MoveY_MoveUp", "MoveY_MoveDown"), ReadDriveAxisPair("MoveZ_MoveForward", "MoveZ_MoveBackward"));
                //Vector3 inputRotation = new Vector3(ReadDriveAxisPair("RotateX_PitchUp", "RotateX_PitchDown"), ReadDriveAxisPair("RotateY_YawLeft", "RotateY_YawRight"), ReadDriveAxisPair("RotateZ_RollRight", "RotateZ_RollLeft"));

                //ApplyThrottle(This, true, ref inputMovement.x, ref this.m_ThrottleValues.x, ref this.m_ThrottleLastInput.x, ref this.m_ThrottleTiming.x);
                //ApplyThrottle(This, true, ref inputMovement.y, ref this.m_ThrottleValues.y, ref this.m_ThrottleLastInput.y, ref this.m_ThrottleTiming.y);
                //ApplyThrottle(This, true, ref inputMovement.z, ref this.m_ThrottleValues.z, ref this.m_ThrottleLastInput.z, ref this.m_ThrottleTiming.z);
                var Drive = ReadAxis("MoveZ_MoveForward", "MoveZ_MoveBackward", JoystickAxisY);
                var Rotate = ReadAxis("RotateY_YawLeft", "RotateY_YawRight", JoystickAxisX);
                if (ReverseSteering && Drive < -0.01f)
                {
                    Vector3 forward = This.Tech.rootBlockTrans.forward;
                    if (Vector3.Dot(This.Tech.rbody.velocity, forward) < 0f)
                    {
                        Rotate = -Rotate;
                    }
                }
                //this.m_ControlState.m_State.m_InputMovement = inputMovement;
                This.DriveControl = Drive;//inputMovement.z;
                //this.m_ControlState.m_State.m_InputRotation = inputRotation;
                This.TurnControl = Rotate;//inputRotation.y;

                //this.m_ControlState.m_State.m_ThrottleValues = this.m_ThrottleValues;
                This.BoostControlProps = ReadInput("BoostPropellers");
                This.BoostControlJets = ReadInput("BoostJets");
                This.FireControl = ReadInput("FireControl");
                if (ReadInputDown("BuildBeam"))
                {
                    This.ToggleBeamActivated(true);
                }
                if (This.Tech.beam.IsActive)
                {

                }
                if (ReadInputDown("DetonateExplosiveBolt"))
                    This.DetonateExplosiveBolt();

                //ANCHOR
                {
                    bool flagAnchor = ReadInputDown("Anchor");
                    if (flagAnchor)
                    {
                        AnchorCache = Tank.IsAnchored ? 2 : 1;
                    }
                    if (flagAnchor || (ReadInput("Anchor") && AnchorCache == (Tank.IsAnchored ? 2 : 1)))
                    {
                        try
                        {
                            if (Tank.IsAnchored)
                            {
                                Tank.Anchors.UnanchorAll();
                                AnchorCache = ((AnchorCache == (Tank.IsAnchored ? 2 : 1)) ? 2 : 0);
                            }
                            else
                            {
                                Vector3 position = Tank.transform.position;
                                Quaternion rotation = Tank.transform.rotation;
                                Vector3 velocity = Tank.rbody.velocity;
                                Vector3 angularVelocity = Tank.rbody.angularVelocity;
                                Tank.Anchors.TryAnchorAll(false);
                                if (AnchorCache == (Tank.IsAnchored ? 2 : 1))
                                {
                                    Tank.transform.position = position;
                                    Tank.transform.rotation = rotation;
                                    Tank.rbody.velocity = velocity;
                                    Tank.rbody.angularVelocity = angularVelocity;
                                }
                                else
                                {
                                    AnchorCache = 0;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            internal Vector2 BuildBeamAxis()
            {
                return new Vector2(-ReadAxis("RotateY_YawLeft", "RotateY_YawRight", JoystickAxisX), ReadAxis("MoveZ_MoveForward", "MoveZ_MoveBackward", JoystickAxisY));
            }
        }
    }
}
//Whatever it takes