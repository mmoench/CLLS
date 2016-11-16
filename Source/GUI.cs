using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using KSP.UI.Screens; // For "ApplicationLauncherButton"
using System.Collections.Generic;

namespace CLLS
{
    // Helper-Class to draw the window in the flight scene:
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GUIFlight : UnityEngine.MonoBehaviour
    {
        public void OnGUI()
        {
            if (GUI.showGui)
            {
                GUI.windowPosition = GUILayout.Window(GUI.WINDOW_ID, GUI.windowPosition, OnWindow, "", GUI.windowStyle);
            }
        }

        private void OnWindow(int windowId)
        {
            try
            {
                GUI.DrawWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KSTSGUIFlight.OnWindow(): " + e.ToString());
            }
        }
    }

    // Helper-Class to draw the window in the tracking-station scene:
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class GUITrackingStation : UnityEngine.MonoBehaviour
    {
        public void OnGUI()
        {
            if (GUI.showGui)
            {
                GUI.windowPosition = GUILayout.Window(GUI.WINDOW_ID, GUI.windowPosition, OnWindow, "", GUI.windowStyle);
            }
        }

        private void OnWindow(int windowId)
        {
            try
            {
                GUI.DrawWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KSTSGUITrackingStation.OnWindow(): " + e.ToString());
            }
        }
    }

    // Helper-Class to draw the window in the space-center scene:
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class GUISpaceCenter : UnityEngine.MonoBehaviour
    {
        public void OnGUI()
        {
            if (GUI.showGui)
            {
                GUI.windowPosition = GUILayout.Window(GUI.WINDOW_ID, GUI.windowPosition, OnWindow, "", GUI.windowStyle);
            }
        }

        private void OnWindow(int windowId)
        {
            try
            {
                GUI.DrawWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KSTSGUISpaceCenter.OnWindow(): " + e.ToString());
            }
        }
    }

    // Creates the button and contains the functionality to draw the GUI-window (we want to use the same window
    // for different scenes, which is why we have a few helper-classes above):
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class GUI : MonoBehaviour
    {
        private static ApplicationLauncherButton button = null;
        private static GUIStyle labelStyle = null;
        private static GUIStyle buttonStyle = null;
        private static GUIStyle scrollStyle = null;
        private static GUIStyle selectionGridStyle = null;
        private static Vector2 scrollPos = Vector2.zero;
        private static Texture2D icon = null;

        public static GUIStyle windowStyle = new GUIStyle(HighLogic.Skin.window) { fixedWidth = 450f, fixedHeight = 500f };
        public static Rect windowPosition = new Rect(300, 60, 450, 400);
        public static bool showGui = false;

        public const int WINDOW_ID = 0xF83AD; // Not sure if there isn't an easier way to create unique window-IDs than just picking one.

        void Awake()
        {
            if (icon == null)
            {
                icon = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                var textureFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Icon.png");
                icon.LoadImage(File.ReadAllBytes(textureFile));
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
                button = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null, visibleScense, icon);
            }

            // For reasons unknown the styles cannot be initialized in the constructor, only when the application is ready, probably because the
            // skin needs more time to load:
            if (ApplicationLauncher.Ready)
            {
                labelStyle = new GUIStyle("Label");
                buttonStyle = new GUIStyle("Button");
                scrollStyle = HighLogic.Skin.scrollView;
                selectionGridStyle = new GUIStyle(GUI.buttonStyle) { richText = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            }
        }

        // Fires when a scene is unloaded and we should destroy our button:
        public void DestroyEvent()
        {
            if (button == null) return;
            ApplicationLauncher.Instance.RemoveModApplication(button);
            button = null;
            showGui = false;
        }

        private void GuiOn()
        {
            showGui = true;
        }

        private void GuiOff()
        {
            showGui = false;
        }

        public static string FormatDuration(double duration)
        {
            int dayLength = CLLS.GetDayLength();
            double seconds = duration % 60;
            int minutes = ((int)(duration / 60)) % 60;
            int hours = ((int)(duration / 60 / 60)) % dayLength;
            int days = ((int)(duration / 60 / 60 / dayLength));
            return String.Format("{0:0} / {1:00}:{2:00}:{3:00.00}", days, hours, minutes, seconds);
        }

        // Returns a new color which is between startColor and endColor (RGB-coded) on a scale 0..1.
        public static int ScaleRGB(int startColor,int endColor, double scale)
        {
            int[] startParts = new int[3] { (startColor >> 16) & 0xFF, (startColor >> 8) & 0xFF, startColor & 0xFF };
            int[] endParts = new int[3] { (endColor >> 16) & 0xFF, (endColor >> 8) & 0xFF, endColor & 0xFF };
            int newColor = 0;
            for (int i = 0; i < 3; i++)
            {
                newColor <<= 8;
                int partSize = startParts[i] - endParts[i];
                int part = endParts[i] + (int)Math.Round(partSize * scale);
                if (part < 0) part = 0;
                else if (part > 0xFF) part = 0xFF;
                newColor += part;
            }
            return newColor;
        }

        public static void DrawWindow()
        {
            if (!showGui) return;
            try
            {
                int red = 0xD10D0D;
                int green = 0x00C000;
                int orange = 0xD79507;

                GUILayout.BeginVertical();

                // Title:
                GUILayout.BeginArea(new Rect(0, 3, windowStyle.fixedWidth, 20));
                GUILayout.Label("<size=14><b>Closed Loop Life Support</b></size>", new GUIStyle(GUI.labelStyle) { fixedWidth = windowStyle.fixedWidth, alignment = TextAnchor.MiddleCenter });
                GUILayout.EndArea();

                // Find all vessels which we want to display (we don't need debris, asterioids, etc):
                List<TrackedVessel> trackedVesselsToDisplay = new List<TrackedVessel>();
                foreach (TrackedVessel trackedVessel in CLLS.trackedVessels)
                {
                    if (trackedVessel.cachedCrewCapacity == 0 && trackedVessel.cachedLifeSupport == 0) continue;
                    if (trackedVessel.IsUnowned()) continue;
                    trackedVesselsToDisplay.Add(trackedVessel);
                }

                // Sort by remaining amount ascending but with empty vessels at the bottom:
                trackedVesselsToDisplay.Sort((x, y) =>
                    x.cachedCrewCount == 0 && y.cachedCrewCount != 0 ? 1 :
                    x.cachedCrewCount != 0 && y.cachedCrewCount == 0 ? -1 :
                    x.CalculateCurrentLifeSupportAmount().CompareTo(y.CalculateCurrentLifeSupportAmount())
                );

                // Content-Box:
                scrollPos = GUILayout.BeginScrollView(scrollPos, GUI.scrollStyle);
                if (trackedVesselsToDisplay.Count == 0)
                {
                    GUILayout.Label("<b>No active flights.</b>");
                }
                else
                {
                    double curTime = Planetarium.GetUniversalTime();

                    List<GUIContent> contents = new List<GUIContent>();
                    // MissionController.missions.Sort((x, y) => x.eta.CompareTo(y.eta)); // Sort list by ETA

                    foreach (TrackedVessel trackedVessel in trackedVesselsToDisplay)
                    {
                        string situation = "";
                        switch (trackedVessel.vessel.situation)
                        {
                            case Vessel.Situations.DOCKED: situation = "docked near "; break;
                            case Vessel.Situations.ESCAPING: situation = "escaping"; break;
                            case Vessel.Situations.FLYING: situation = "flying at "; break;
                            case Vessel.Situations.LANDED: situation = "landed on "; break;
                            case Vessel.Situations.ORBITING: situation = "orbiting "; break;
                            case Vessel.Situations.PRELAUNCH: situation = "pre-launch on "; break;
                            case Vessel.Situations.SPLASHED: situation = "splashed on "; break;
                            case Vessel.Situations.SUB_ORBITAL: situation = "sub-orbital at "; break;
                        }
                        situation += trackedVessel.vessel.mainBody.name;

                        string content = "<color=#FFFFFF><color=#F9FA86><b><size=14>" + trackedVessel.vessel.vesselName + "</size></b></color> (" + situation + ")\n";

                        content += "<b>Crew:</b> " + trackedVessel.cachedCrewCount.ToString() + " / " + trackedVessel.cachedCrewCapacity.ToString() + " ";

                        double lifeSupport = trackedVessel.CalculateCurrentLifeSupportAmount();
                        int color = trackedVessel.cachedLifeSupportDeltaPerHour < 0 ? red : green;
                        content += "<b>Life Support:</b> " + lifeSupport.ToString("#,##0.00") + " / " + trackedVessel.cachedMaxLifeSupport.ToString("#,##0.00") + " ";
                        if (trackedVessel.cachedLifeSupportDeltaPerHour != 0 || trackedVessel.cachedCrewCount > 0)
                        {
                            content += "<color=#" + color.ToString("X6") + ">(Δ " + (trackedVessel.cachedLifeSupportDeltaPerHour * CLLS.GetDayLength()).ToString("+0.00;-0.00") + "/day)</color>\n";
                        }

                        if (trackedVessel.cachedCrewCount > 0)
                        {
                            content += "<b>Remaining:</b> ";
                            if (trackedVessel.cachedLifeSupportDeltaPerHour >= 0)
                            {
                                content += "<color=#" + green.ToString("X6") + "><b>infinite</b></color>";
                            }
                            else if (trackedVessel.cachedLifeSupportDeltaPerHour < 0)
                            {
                                double timeRemaining = lifeSupport / -(trackedVessel.cachedLifeSupportDeltaPerHour / 3600d);
                                double boundsUpper = 3600 * 6 * 30; // 30 kerbin-days
                                double boundsLower = 3600 * 6; // 1 kerbin-day

                                if (timeRemaining > boundsUpper) color = green;
                                else if (timeRemaining > boundsLower) color = ScaleRGB(green, orange, (timeRemaining - boundsLower) / (boundsUpper - boundsLower));
                                else color = ScaleRGB(orange, red, timeRemaining / boundsLower);

                                content += "<color=#" + color.ToString("X6") + ">" + FormatDuration(timeRemaining) + "</color>";
                            }
                        }

                        content += "</color>";
                        contents.Add(new GUIContent(content));
                    }

                    GUILayout.SelectionGrid(-1, contents.ToArray(), 1, GUI.selectionGridStyle);
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                UnityEngine.GUI.DragWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] DrawWindow(): " + e.ToString());
            }
        }
    }
}
