using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

namespace CLLS
{
    // Module for each crewable part to sumulate life support consumption.
    [KSPModule("CLLS Provider")]
    public class CLLSProvider : PartModule
    {
        [KSPField(guiActive = true, guiName = "Life Support Status", isPersistant = false)]
        public string lifeSupportStatus;

        [KSPField(guiActive = true, guiName = "Life Support", guiUnits = " days ", guiFormat = "F2", isPersistant = false)]
        public float displayRate;


        public override string GetInfo()
        {
            return "Closed Loop Life Support Installed";
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            Vessel vessel = this.part.vessel;
            int crewCount = TrackedVessel.GetCrewCount(vessel);
            double lifeSupportConsumption = TrackedVessel.GetLifeSupportConsumptionPerDay(vessel);

            // Only calculate the remaining days of life support if there are kerbals on board:
            if (crewCount <= 0 || lifeSupportConsumption <= 0)
            {
                this.Fields[1].guiActive = false;
                lifeSupportStatus = "On Standby";
            }
            else
            {
                this.Fields[1].guiActive = true;
                double lifeSupport = TrackedVessel.GetLifeSupportCount(vessel);
                displayRate = (float)(lifeSupport / lifeSupportConsumption);
                this.Fields[1].guiUnits = " days ";
                lifeSupportStatus = "Active";

                // If there is only very little left, go to hours or even minutes:
                if (displayRate<2)
                {
                    displayRate *= 6;
                    this.Fields[1].guiUnits = " hours ";
                }
                if (displayRate<1)
                {
                    displayRate *= 60;
                    this.Fields[1].guiUnits = " min. ";
                    lifeSupportStatus = "LOW";
                }

                if (lifeSupport <= 0) lifeSupportStatus = "DEPLETED";
            }
        }
    }
}
