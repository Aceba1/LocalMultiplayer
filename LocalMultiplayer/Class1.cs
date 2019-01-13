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
                    } catch { }
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
                            m_NudgeInput.SetValue(__instance, Overrides[name].GetAxis());
                        }
                    } catch { }
                }
            }
        }

        internal class OverrideController : MonoBehaviour
        {
            bool IsSettingKeybind = false;
            string SetTrigger = "";
            int ID = 5189012;
            bool Show = false;
            Rect rect = new Rect(0, 0, 800, 600);
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
            Vector2 scroll1, scroll2;
            string Selected = "";
            string[] Names, Triggers;
            int SelectionIndex, TriggerIndex;
            void GUIWindow(int ID)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create New Controller"))
                {
                    int NewID = 2;
                    while (Overrides.ContainsKey("Player Tech #" + NewID.ToString())) NewID++;
                    Overrides.Add("Player Tech #" + NewID.ToString(), new Overrider());
                    Names = null;
                }
                if (SelectionIndex != -1 && Overrides.Count != 0)
                {
                    if (GUILayout.Button("Duplicate Sel."))
                    {
                        int NewID = 2;
                        while (Overrides.ContainsKey(Selected + " " + NewID.ToString())) NewID++;
                        Overrides.Add(Selected + " " + NewID.ToString(), new Overrider(Overrides[Selected].Drive));
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
                    GUI.changed = false;
                    bool flag = Names == null;
                    if (flag) Names = Overrides.Keys.ToArray();
                    SelectionIndex = GUILayout.SelectionGrid(SelectionIndex, Names, Overrides.Count);
                    if ((flag || GUI.changed) && SelectionIndex != -1)
                    {
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
                        GUILayout.Label("Name of techs to override with this controller");
                        string oldsel = Selected;
                        Selected = GUILayout.TextField(Selected);
                        if (oldsel != Selected)
                        {
                            if (Overrides.ContainsKey(Selected))
                            {
                                Selected = oldsel;
                            }
                            else
                            {
                                var ovc = Overrides[oldsel];
                                Overrides.Remove(oldsel);
                                Overrides.Add(Selected, ovc);
                                Names = null;
                            }
                        }

                        if (IsSettingKeybind)
                        {
                            var e = Event.current;
                            if (e.isKey)
                            {
                                Overrides[Selected].Drive[SetTrigger] = e.keyCode;
                                IsSettingKeybind = false;
                            }
                        }
                        if (SetTrigger != "")
                        {
                            IsSettingKeybind = GUILayout.Button(IsSettingKeybind ? "Press a key for use" : Overrides[Selected].Drive[SetTrigger].ToString()) != IsSettingKeybind;
                        }
                        GUILayout.Label("Choose a function below to change the key bound to it");
                        scroll2 = GUILayout.BeginScrollView(scroll2);
                        if (Triggers == null) Triggers = Overrides[Selected].Drive.Keys.ToArray();
                        TriggerIndex = GUILayout.SelectionGrid(TriggerIndex, Triggers, 2);
                        if (GUI.changed && SelectionIndex != -1)
                        {
                            SetTrigger = Triggers[TriggerIndex];
                        }
                        GUILayout.EndScrollView();
                    }
                }
                GUI.DragWindow();
            }
        }

        public class Overrider
        {
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
            public Dictionary<string, KeyCode> Drive = new Dictionary<string, KeyCode>(){
                //{ "MoveX_MoveRight", KeyCode.None },
                //{ "MoveX_MoveLeft", KeyCode.None },
                //{ "MoveY_MoveUp", KeyCode.None },
                //{ "MoveY_MoveDown", KeyCode.None },
                { "MoveZ_MoveForward", KeyCode.None },
                { "MoveZ_MoveBackward", KeyCode.None },
                //{ "RotateX_PitchUp", KeyCode.None },
                //{ "RotateX_PitchDown", KeyCode.None },
                { "RotateY_YawLeft", KeyCode.None },
                { "RotateY_YawRight", KeyCode.None },
                //{ "RotateZ_RollRight", KeyCode.None },
                //{ "RotateZ_RollLeft", KeyCode.None },
                { "BoostPropellers", KeyCode.None },
                { "BoostJets", KeyCode.None },
                { "FireControl", KeyCode.None },
                { "BuildBeam", KeyCode.None },
                { "DetonateExplosiveBolt", KeyCode.None } };
            public Overrider()
            {

            }
            public Overrider(Dictionary<string,KeyCode> Copy)
            {
                foreach(var thing in Drive.Keys)
                {
                    if (Copy.ContainsKey(thing))
                    Drive[thing] = Copy[thing];
                }
            }

            public bool ReverseSteering = false;

            public bool ReadInput(string Key)
            {
                if (!Drive.ContainsKey(Key))
                    return false;
                return Input.GetKey(Drive[Key]);
            }
            public bool ReadInputDown(string Key)
            {
                if (!Drive.ContainsKey(Key))
                    return false;
                return Input.GetKeyDown(Drive[Key]);
            }

            //private Vector3 m_ThrottleLastInput;
            public void GetInput(TankControl This)
            {
                //Vector3 inputMovement = new Vector3(ReadDriveAxisPair("MoveX_MoveRight", "MoveX_MoveLeft"), ReadDriveAxisPair("MoveY_MoveUp", "MoveY_MoveDown"), ReadDriveAxisPair("MoveZ_MoveForward", "MoveZ_MoveBackward"));
                //Vector3 inputRotation = new Vector3(ReadDriveAxisPair("RotateX_PitchUp", "RotateX_PitchDown"), ReadDriveAxisPair("RotateY_YawLeft", "RotateY_YawRight"), ReadDriveAxisPair("RotateZ_RollRight", "RotateZ_RollLeft"));

                //ApplyThrottle(This, true, ref inputMovement.x, ref this.m_ThrottleValues.x, ref this.m_ThrottleLastInput.x, ref this.m_ThrottleTiming.x);
                //ApplyThrottle(This, true, ref inputMovement.y, ref this.m_ThrottleValues.y, ref this.m_ThrottleLastInput.y, ref this.m_ThrottleTiming.y);
                //ApplyThrottle(This, true, ref inputMovement.z, ref this.m_ThrottleValues.z, ref this.m_ThrottleLastInput.z, ref this.m_ThrottleTiming.z);
                //if (ReverseSteering && (inputMovement.z < -0.01f/* || this.m_ThrottleValues[2] < -0.01f*/))
                //{
                //    Vector3 forward = This.Tech.rootBlockTrans.forward;
                //    if (Vector3.Dot(This.Tech.rbody.velocity, forward) < 0f)
                //    {
                //        inputRotation.y *= -1f;
                //    }
                //}
                //this.m_ControlState.m_State.m_InputMovement = inputMovement;
                This.DriveControl = (ReadInput("MoveZ_MoveForward") ? 1 : 0) + (ReadInput("MoveZ_MoveBackward") ? -1 : 0);//inputMovement.z;
                //this.m_ControlState.m_State.m_InputRotation = inputRotation;
                This.TurnControl = (ReadInput("RotateY_YawLeft") ? 1 : 0) + (ReadInput("RotateY_YawRight") ? -1 : 0);//inputRotation.y;

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
            }

            internal Vector2 GetAxis()
            {
                return new Vector2((ReadInput("RotateY_YawLeft") ? 1 : 0) + (ReadInput("RotateY_YawRight") ? -1 : 0), (ReadInput("MoveZ_MoveForward") ? 1 : 0) + (ReadInput("MoveZ_MoveBackward") ? -1 : 0));
            }
        }
    }
}
