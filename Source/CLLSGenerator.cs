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
        // The following fields are just for show:
        [KSPField]
        public string producerName = "Life Support Generator";

        [KSPField(guiActive = true, guiName = "State", isPersistant = false)]
        public string displayStatus;

        [KSPField(guiActive = true, guiName = "Life Support", guiUnits = "/day", guiFormat = "F2", isPersistant = false)]
        public float currentProductionRatePerDayGui = 0;

        [KSPField(guiActive = true, guiName = "Electricity", guiUnits = "/sec", guiFormat = "F2", isPersistant = false)]
        public float currentElectricityRatePerSecGui = 0;

        [KSPField(guiActive = true, guiName = "Production Efficiency", guiUnits = "", guiFormat = "P", isPersistant = false)]
        public float efficiencyGui = 0;

        // This slider can be used to set the production rate:
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Production Rate", guiFormat = "F1", guiUnits = "%", isPersistant = true)]
        [UI_FloatRange(maxValue = 100, minValue = 0, scene = UI_Scene.All, stepIncrement = 0.1f)]
        public float productionRate = 100;

        // These fields are used to cache the current settings and are used in the background-processing:
        [KSPField(isPersistant = true)]
        public double currentProductionRatePerDay = 0; // The rate is set by the player via the slider above.

        [KSPField(isPersistant = true)]
        public double currentElectricityRatePerSec = 0;

        [KSPField(isPersistant = true)]
        public double efficiency = 0; // The efficiency is calculated at runtime when the vessel produced less electricity than is consumed.

        [KSPField(isPersistant = true)]
        public bool isRunning = false;

        // These fields are set externaly by the part's config:
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
            UpdateGUI();
        }

        [KSPEvent(active = false, guiActive = true, guiActiveEditor = true, guiName = "Stop Generator")]
        public void ShutDown()
        {
            isRunning = false;
            UpdateGUI();
        }

        [KSPAction("Toggle Generator")]
        public void Toggle(KSPActionParam param)
        {
            isRunning = !isRunning;
            UpdateGUI();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            if (state != StartState.Editor)
            {
                UpdateGUI();
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
            currentProductionRatePerDayGui = (float)(currentProductionRatePerDay * (6f / CLLS.GetDayLength())); // Maybe convert this to 24 hour days
            currentElectricityRatePerSecGui = (float)currentElectricityRatePerSec;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            UpdateGUI();
        }

        // Is called on each physics-tick while the vessel is active.
        public override void OnFixedUpdate()
        {
            try
            {
                if (!isRunning)
                {
                    currentProductionRatePerDay = 0;
                    currentElectricityRatePerSec = 0;
                    efficiency = 0;
                }
                else
                {
                    // Comsume electricity; we can't do this in our background-process because all other
                    // electricity producing parts are only running while the vessel is loaded:
                    double rate = (productionRate / 100d);
                    currentElectricityRatePerSec = electricityConsumptionPerSecond * rate;

                    if (currentElectricityRatePerSec <= 0)
                    {
                        currentElectricityRatePerSec = 0;
                        currentProductionRatePerDay = 0;
                        efficiency = 1;
                    }
                    else
                    {

                        double electricityRequested = TimeWarp.fixedDeltaTime * currentElectricityRatePerSec;
                        double electricityReceived = this.part.RequestResource("ElectricCharge", electricityRequested); // Scale the amount with while in time-warp.

                        // We will produce less life support if we don't receive the full amount of electricity:
                        efficiency = electricityReceived / electricityRequested;
                        currentProductionRatePerDay = lifeSupportGeneratedPerDay * rate * efficiency;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] CLLSGenerator.OnFixedUpdate(): " + e.ToString());
            }
        }
    }
}
