using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CLLS
{
    [KSPAddon(KSPAddon.Startup.EveryScene, true)]
    public class Monitor : MonoBehaviour
    {
        private ApplicationLauncherButton button = null;
        private Rect windowPosition = new Rect(300, 60, 450, 400);
        private GUIStyle windowStyle = new GUIStyle(HighLogic.Skin.window) { fixedWidth=600f, fixedHeight=400f };
        private GUIStyle labelStyle = new GUIStyle(HighLogic.Skin.label);
        private GUIStyle buttonStyle = new GUIStyle(HighLogic.Skin.button);
        private GUIStyle scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
        private Vector2 scrollPos = Vector2.zero;
        private static Texture2D texture = null;

        void Awake()
        {
            if (texture == null)
            {
                texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                var textureFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Icon.png");
                texture.LoadImage(File.ReadAllBytes(textureFile));
            }

            // Add event-handlers to create and destroy our button:
            GameEvents.onGUIApplicationLauncherReady.Remove(ReadyEvent);
            GameEvents.onGUIApplicationLauncherReady.Add(ReadyEvent);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(DestroyEvent);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(DestroyEvent);
        }

        // Fires when a scene is ready so we can install our button.
        public void ReadyEvent()
        {
            if (ApplicationLauncher.Ready && button == null)
            {
                var visibleScense = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.MAPVIEW;
                button = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null, visibleScense, texture);
            }
        }

        // Fires when a scene is unloaded and we should destroy our button:
        public void DestroyEvent()
        {
            if (button == null) return;
            ApplicationLauncher.Instance.RemoveModApplication(button);
            RenderingManager.RemoveFromPostDrawQueue(144, Ondraw);
            button = null;
        }

        private void GuiOn()
        {
            // Draw window:
            RenderingManager.AddToPostDrawQueue(100, Ondraw);
        }

        private void GuiOff()
        {
            // Hide window:
            RenderingManager.RemoveFromPostDrawQueue(100, Ondraw);
        }

        private void Ondraw()
        {
            windowPosition = GUILayout.Window(100, windowPosition, OnWindow, "Closed Loop Life Support Status", windowStyle);
        }

        private void OnWindow(int windowId)
        {
            GenerateWindow();
        }

        private class LifeSupportDisplayStat
        {
            public double LastUpdate { get; set; }
            public double LastFeeding { get; set; }
            public double SupplyTime { get; set; }
            public string DisplayTitle { get; set; }
            public string UpdateLabel { get; set; }
            public string UpdateColor { get; set; }
        }

        private void GenerateWindow()
        {
            try
            {
                GUILayout.BeginVertical();
                scrollPos = GUILayout.BeginScrollView(scrollPos, scrollStyle, GUILayout.Width(580), GUILayout.Height(350));
                GUILayout.BeginVertical();
                double curTime = Planetarium.GetUniversalTime();

                foreach (TrackedVessel trackedVessel in TrackedVessel.GetTrackedVessels())
                {
                    double lifeSupportLeft = trackedVessel.GetLifeSupportCount();
                    double maxLifeSupport = trackedVessel.GetMaxLifeSupportCount();
                    int kerbalsOnVessel = trackedVessel.GetCrewCount();
                    double delta = trackedVessel.GetLifeSupportProductionPerDay() - trackedVessel.GetLifeSupportConsumptionPerDay();
                    delta = Math.Round(delta, 2);

                    string crew = "Crew: " + kerbalsOnVessel;
                    if (trackedVessel.Vessel.isEVA) crew = "EVA";

                    string situation = "";
                    switch (trackedVessel.Vessel.situation)
                    {
                        case Vessel.Situations.DOCKED:      situation = "docked near "; break;
                        case Vessel.Situations.ESCAPING:    situation = "escaping"; break;
                        case Vessel.Situations.FLYING:      situation = "flying at "; break;
                        case Vessel.Situations.LANDED:      situation = "landed on "; break;
                        case Vessel.Situations.ORBITING:    situation = "orbiting "; break;
                        case Vessel.Situations.PRELAUNCH:   situation = "pre-launch on "; break;
                        case Vessel.Situations.SPLASHED:    situation = "splashed on "; break;
                        case Vessel.Situations.SUB_ORBITAL: situation = "sub-orbital at "; break;
                    }
                    situation += trackedVessel.Vessel.mainBody.name;

                    string color = "FFFFFF"; // White
                    string timeLeft = "";
                    if (delta < 0)
                    {
                        double daysLeft = lifeSupportLeft / -delta;
                        if (daysLeft < 1) color = "FF5E5E"; // Red
                        else if (daysLeft < 3) color = "FFAE00"; // Orange
                        else color = "FFE100"; // Yellow

                        int secondsLeft = (int)Math.Round(daysLeft * (6 * 60 * 60));
                        timeLeft = String.Format("{0:D} days {1:D2}:{2:D2}:{3:D2}",
                            (secondsLeft / (60 * 60 * 6)),
                            (secondsLeft / (60 * 60)) % 6,
                            (secondsLeft / 60) % 60,
                            secondsLeft % 60
                        );
                    }
                    else
                    {
                        color = "6FFF00"; // Green
                    }

                    GUILayout.Label(String.Format("<color=#F9FF8A>{0}</color> <color=#FFFFFF>({1}): {2}</color>", trackedVessel.Vessel.vesselName, crew, situation), labelStyle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", labelStyle, GUILayout.Width(20));
                    string deltaString = String.Format("{0:F2}", (float)(delta));
                    if (delta > 0) deltaString = "+" + deltaString;
                    GUILayout.Label(String.Format("<color=#FFFFFF>Life Support: </color><color=#{0}>{1:F2}/{2:F2} (Δ {3}/day)</color>", color, (float)(lifeSupportLeft), (float)(maxLifeSupport), deltaString), labelStyle, GUILayout.Width(280));
                    GUILayout.Label(String.Format("<color=#{0}>{1}</color>", color, timeLeft), labelStyle, GUILayout.Width(150));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUI.DragWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] GenerateWindow(): " + e.ToString());
            }
        }
    }
}
