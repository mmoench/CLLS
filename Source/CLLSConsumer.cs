using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

namespace CLLS
{
    /**
     * This module is used on crewed parts to give them a display for the remaining life-support. This is for show
     * only, this mod would work without this module just as well.
     **/
    [KSPModule("CLLS Provider")]
    public class CLLSProvider : PartModule
    {
        [KSPField(guiActive = true, guiName = "Life Support", isPersistant = false)]
        public string lifeSupportStatus;

        public override string GetInfo()
        {
            return "Closed Loop Life Support Installed";
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            Vessel vessel = this.part.vessel;
            if (!vessel.loaded) return; // Shouldn't happen, but better safe than sorry
            TrackedVessel trackedVessel = CLLS.GetTrackedVessel(vessel);

            // Only calculate the remaining days of life support if there are kerbals on board and they use it:
            if (trackedVessel.cachedCrewCount <= 0 || trackedVessel.cachedLifeSupportDeltaPerHour > 0)
            {
                lifeSupportStatus = "On Standby";
            }
            else
            {
                double lifeSupport = trackedVessel.cachedLifeSupport;
                double consumptionPerHour = -trackedVessel.cachedLifeSupportDeltaPerHour;
                double displayRate;
                string unit;

                displayRate = (float) ((lifeSupport / consumptionPerHour)) / CLLS.GetDayLength(); // Show remaining days
                unit = " days ";

                // If there is only very little left, go to hours or even minutes:
                if (displayRate<2)
                {
                    displayRate *= 6;
                    unit = " hours ";
                }
                if (displayRate<1)
                {
                    displayRate *= 60;
                    unit = " min. ";
                }

                if (lifeSupport <= 0) lifeSupportStatus = "DEPLETED";
                else lifeSupportStatus = displayRate.ToString("0.00") + unit;
            }
        }
    }
}
