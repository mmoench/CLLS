using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

namespace CLLS
{
    // Module for parts which produce life support.
    [KSPModule("CLLS Generator")]
    public class CLLSGenerator : PartModule
    {
        [KSPField]
        public string producerName = "Life Support Generator";

        [KSPField(guiActive = true, guiName = "State", isPersistant = false)]
        public string displayStatus;

        [KSPField(guiActive = true, guiName = "Production Rate", guiUnits = "/day", guiFormat = "F2", isPersistant = false)]
        public float currentProductionRatePerDayGui = 0;

        [KSPField(guiActive = true, guiName = "Production Efficiency", guiUnits = "", guiFormat = "P", isPersistant = false)]
        public float efficiencyGui = 0;

        [KSPField(isPersistant = true)]
        public double currentProductionRatePerDay = 0;

        [KSPField(isPersistant = true)]
        public double efficiency = 0;

        [KSPField(isPersistant = true)]
        public bool isRunning = false;

        [KSPField]
        public string startStopAnimation = string.Empty;

        [KSPField]
        public double lifeSupportGeneratedPerDay = 0.0;

        [KSPField]
        public double electricityConsumptionPerSecond = 0.0;


        public override string GetInfo()
        {
            return String.Format(
                "<color=#99FF00>Outputs:</color>\n"+
                "- Life Support: {0:F2}/day\n\n"+
                "<color=#FFA500>Requires:</color>\n"+
                "- ElectricCharge: {1:F2}/sec.",
                lifeSupportGeneratedPerDay,
                electricityConsumptionPerSecond
            );
        }

        [KSPEvent(active = false, guiActive = true, guiActiveEditor = true, guiName = "Start Generator")]
        public void StartUp()
        {
            isRunning = true;
            RunAnimation(false);
            UpdateGUI();
        }

        [KSPEvent(active = false, guiActive = true, guiActiveEditor = true, guiName = "Stop Generator")]
        public void ShutDown()
        {
            isRunning = false;
            RunAnimation(true);
            UpdateGUI();
        }

        [KSPAction("Toggle Generator")]
        public void Toggle(KSPActionParam param)
        {
            isRunning = !isRunning;
            RunAnimation(!isRunning);
            UpdateGUI();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            if (state != StartState.Editor)
            {
                UpdateGUI();
                if (isRunning) RunAnimation(false, 10000); // Quick-run start-animation on load if the part is already running.
                this.part.force_activate(); // Activate the part so it will run OnFixedUpdate for each physics-tick.
            }
        }

        protected void UpdateGUI()
        {
            if (isRunning)
            {
                Events["StartUp"].active = false;
                Events["ShutDown"].active = true;
                displayStatus = "Running";
            }
            else
            {
                Events["StartUp"].active = true;
                Events["ShutDown"].active = false;
                displayStatus = "Stopped";
            }

            // KSP-Gui elements only work with float, but we are using double, so we use extra varaiables for dispalying these values:
            efficiencyGui = (float)efficiency;
            currentProductionRatePerDayGui = (float)currentProductionRatePerDay;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            UpdateGUI();
        }

        public void RunAnimation(bool reverse=false, float speed=1)
        {
            if (string.IsNullOrEmpty(startStopAnimation)) return;

            // Set up the animation:
            Animation[] animators = part.FindModelAnimators(startStopAnimation);
            Animation animation;
            if (animators.Length > 0) animation = animators[0];
            else return;
            animation[startStopAnimation].wrapMode = WrapMode.Once;

            if (reverse)
            {
                if (animation[startStopAnimation].time == 0) animation[startStopAnimation].time = animation[startStopAnimation].length;
                animation[startStopAnimation].speed = -speed;
            }
            else
            {
                animation[startStopAnimation].speed = speed;
            }
            animation.Play(startStopAnimation);
        }

        // Is called on each physics-tick while the vessel is active.
        public override void OnFixedUpdate()
        {
            try
            {
                if (!isRunning)
                {
                    currentProductionRatePerDay = 0;
                    efficiency = 0;
                }
                else
                {
                    // Comsume electricity; we can't do this in our background-process because all other
                    // electricity producing parts are only running while the vessel is loaded:
                    if (electricityConsumptionPerSecond <= 0) return;
                    double electricityReceived = this.part.RequestResource("ElectricCharge", TimeWarp.fixedDeltaTime * electricityConsumptionPerSecond); // Scale the amount with while in time-warp.

                    // We will produce less life support if we don't receive the full amount of electricity:
                    efficiency = electricityReceived / (electricityConsumptionPerSecond * TimeWarp.fixedDeltaTime);
                    currentProductionRatePerDay = lifeSupportGeneratedPerDay * efficiency;

                    // Create heat:
//                    this.part.temperature += 1; // TODO: NO! Do this differently
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] OnFixedUpdate(): " + e.ToString());
            }
        }
    }
}
