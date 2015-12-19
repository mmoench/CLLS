using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

namespace CLLS
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class CLLS : UnityEngine.MonoBehaviour
    {
        static bool initialized = false;
        double lastUpdate = 0;

        public const String RESOURCE_LIFE_SUPPORT = "LifeSupport";

        // Install handler for continous life support updates:
        public void Awake()
        {
            if (initialized) return;
            DontDestroyOnLoad(this);
            if (!IsInvoking("Update"))
            {
                InvokeRepeating("Update", 1, 1); // once every second.
            }
            initialized = true;
        }

        public void Update()
        {
            try
            {
                // Don't update in main menu:
                if (HighLogic.LoadedScene == GameScenes.MAINMENU) return;

                // Calculate how long it has been since the last update:
                if (lastUpdate <= 0) { lastUpdate = Planetarium.GetUniversalTime(); return; } // First start.
                double timeNow = Planetarium.GetUniversalTime();
                double timeElapsed = timeNow - lastUpdate;
                if (timeElapsed < 0) { lastUpdate = Planetarium.GetUniversalTime(); return; } // After reverting time.
                if (timeElapsed == 0) return; // Called twice maybe? Should not happen.
                if (timeElapsed < (TimeWarp.CurrentRate * 1.0)) return; // Only update once per real-time seconds, even during time-warp.
                lastUpdate = timeNow;

                // Update resources on all tracked vessels:
                foreach (TrackedVessel trackedVessel in TrackedVessel.GetTrackedVessels())
                {
                    double consumptionPerTick = trackedVessel.GetLifeSupportConsumptionPerDay() / (60 * 60 * 6);
                    double consumption = consumptionPerTick * timeElapsed;

                    double productionPerDay = trackedVessel.GetLifeSupportProductionPerDay();
                    double productionPerTick = productionPerDay / (60 * 60 * 6);
                    double production = productionPerTick * timeElapsed;

                    double delta = production - consumption;
                    if (Math.Abs(delta) < 1.0 / (60 * 60 * 6 * 10)) delta = 0;
                    if (delta != 0) trackedVessel.UpdateLifeSupport(delta);

                    // Disable or enable crew when the life support gets depleted or refreshed:
                    if (trackedVessel.GetLifeSupportCount() <= 0 && !trackedVessel.IsAtHome())
                    {
                        trackedVessel.UpdateCrew(false); // Disable / kill
                    }
                    else
                    {
                        trackedVessel.UpdateCrew(true); // Enable
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] Update(): " + e.ToString());
            }
        }

    }

}
