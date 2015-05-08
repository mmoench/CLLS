using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CLLS
{
    public class TrackedVessel
    {
        public const String RESOURCE_LIFE_SUPPORT = "LifeSupport";

        public Vessel Vessel { get; set; }

        // Returns a list of all vessels for which we should make life support calculations.
        public static List<TrackedVessel> GetTrackedVessels()
        {
            List<TrackedVessel> vessels = new List<TrackedVessel>();

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                // We don't need to keep track of unowned or unkerbaled vessels:
                if (vessel == null) continue;
                if (vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.SpaceObject || vessel.vesselType == VesselType.Unknown) continue;
                if (GetCrewCount(vessel) <= 0) continue;

                TrackedVessel tracking = new TrackedVessel();
                tracking.Vessel = vessel;
                vessels.Add(tracking);
            }

            return vessels;
        }

        public int GetCrewCount()
        {
            return GetCrewCount(this.Vessel);
        }

        public double GetLifeSupportCount()
        {
            return GetLifeSupportCount(this.Vessel);
        }

        public double GetMaxLifeSupportCount()
        {
            return GetMaxLifeSupportCount(this.Vessel);
        }

        // Returns the number of kerbals onboard the given vessel:
        public static int GetCrewCount(Vessel vessel){
            if (!vessel.loaded)
            {
                return vessel.protoVessel.GetVesselCrew().Count;
            }
            else
            {
                return vessel.GetCrewCount();
            }
        }

        public double GetLifeSupportConsumptionPerDay()
        {
            return GetLifeSupportConsumptionPerDay(Vessel);
        }

        public static double GetLifeSupportConsumptionPerDay(Vessel vessel)
        {
            if (TrackedVessel.IsAtHome(vessel)) return 0.0; // No consumption when landed on kerbin.
            return TrackedVessel.GetCrewCount(vessel); // 1 unit per kerbal per day.
        }

        // Returns how many resources of the given type the vessel has left.
        public static double GetLifeSupportCount(Vessel vessel)
        {
            try
            {
                string resource = RESOURCE_LIFE_SUPPORT;
                double resourceSum = 0.0;
                if (vessel.loaded)
                {
                    // The vessel is loaded, itrate through the objects:
                    foreach (Part part in vessel.parts)
                    {
                        foreach (PartResource partResource in part.Resources)
                        {
                            if (partResource.resourceName.Equals(resource))
                            {
                                if (partResource.flowState)
                                {
                                    resourceSum += partResource.amount;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // The vessel is not loaded, parse the stored values:
                    foreach (ProtoPartSnapshot proto in vessel.protoVessel.protoPartSnapshots)
                    {
                        foreach (ProtoPartResourceSnapshot protoResource in proto.resources)
                        {
                            if (protoResource.resourceName.Equals(resource))
                            {
                                ConfigNode configNode = protoResource.resourceValues;
                                double amount = 0;
                                System.Double.TryParse(configNode.GetValue("amount"), out amount);
                                resourceSum += amount;
                            }
                        }
                    }
                }
                return resourceSum;
            }
            catch(Exception e)
            {
                Debug.LogError("[CLLS] GetLifeSupportCount(" + vessel.GetName() + "): " + e.ToString());
                return 0.0;
            }
        }

        // Returns how many resources of the given type the vessel can hold.
        public static double GetMaxLifeSupportCount(Vessel vessel)
        {
            try
            {
                string resource = RESOURCE_LIFE_SUPPORT;
                double resourceSum = 0.0;
                if (vessel.loaded)
                {
                    // The vessel is loaded, itrate through the objects:
                    foreach (Part part in vessel.parts)
                    {
                        foreach (PartResource partResource in part.Resources)
                        {
                            if (partResource.resourceName.Equals(resource))
                            {
                                if (partResource.flowState)
                                {
                                    resourceSum += partResource.maxAmount;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // The vessel is not loaded, parse the stored values:
                    foreach (ProtoPartSnapshot proto in vessel.protoVessel.protoPartSnapshots)
                    {
                        foreach (ProtoPartResourceSnapshot protoResource in proto.resources)
                        {
                            if (protoResource.resourceName.Equals(resource))
                            {
                                ConfigNode configNode = protoResource.resourceValues;
                                double amount = 0;
                                System.Double.TryParse(configNode.GetValue("maxAmount"), out amount);
                                resourceSum += amount;
                            }
                        }
                    }
                }
                return resourceSum;
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] GetMaxLifeSupportCount(" + vessel.GetName() + "): " + e.ToString());
                return 0.0;
            }
        }

        public double GetLifeSupportProductionPerDay()
        {
            return GetLifeSupportProductionPerDay(this.Vessel);
        }

        // Returns how much life support the given vessel will produce per day.
        public static double GetLifeSupportProductionPerDay(Vessel vessel)
        {
            double productionSum = 0.0;
            try
            {
                if (vessel.loaded)
                {
                    // The vessel is loaded, itrate through the objects:
                    foreach (Part part in vessel.parts)
                    {
                        foreach (PartModule module in part.Modules)
                        {
                            if (module.ClassName == typeof(CLLSGenerator).Name)
                            {
                                productionSum += (double)(module.Fields["currentProductionRatePerDay"].GetValue(module.Fields["currentProductionRatePerDay"].host));
                            }
                        }
                    }
                }
                else
                {
                    // The vessel is not loaded, parse the stored values:
                    foreach (ProtoPartSnapshot proto in vessel.protoVessel.protoPartSnapshots)
                    {
                        foreach (ProtoPartModuleSnapshot protoModule in proto.modules)
                        {
                            if (protoModule.moduleName == typeof(CLLSGenerator).Name)
                            {
                                double amount = 0;
                                System.Double.TryParse(protoModule.moduleValues.GetValue("currentProductionRatePerDay"), out amount);
                                productionSum += amount;
                            }
                        }
                    }
                }
                return productionSum;
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] GetLifeSupportProductionPerDay(" + vessel.GetName() + ")" + e.ToString());
                return 0.0;
            }
        }

        // Adds of removes life support from the tracked vessel.
        public void UpdateLifeSupport(double delta)
        {
            if (this.Vessel.loaded)
            {
                // We can use the vessel-object to request the desired amount of resources:
                this.Vessel.rootPart.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, -delta); // If we want to add -1, we have to request 1
            }
            else
            {
                // The vessel is currently not loaded into the scene, we have to modify the unloaded configuration, wich
                // consists of raw string values like in the savegame:
                foreach (ProtoPartSnapshot proto in this.Vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartResourceSnapshot protoResource in proto.resources)
                    {
                        if (protoResource.resourceName != CLLS.RESOURCE_LIFE_SUPPORT) continue;
                        ConfigNode configNode = protoResource.resourceValues;
                        double amount = 0, maxAmount = 0;
                        System.Double.TryParse(configNode.GetValue("amount"),out amount);
                        System.Double.TryParse(configNode.GetValue("maxAmount"),out maxAmount);

                        if (delta > 0)
                        {
                            // We want to add some life support:
                            if (amount + delta > maxAmount)
                            {
                                delta -= maxAmount - amount;
                                amount = maxAmount;
                            }
                            else
                            {
                                amount += delta;
                                delta = 0;
                            }
                        }
                        else
                        {
                            // We want to reduce the life support:
                            if (amount + delta < 0)
                            {
                                delta += amount;
                                amount = 0;
                            }
                            else
                            {
                                amount += delta;
                                delta = 0;
                            }
                        }

                        // Update the part with the new amount:
                        configNode.SetValue("amount", amount.ToString());
                    }
                }
            }
        }

        // Checks if the tracked vessel is landed on Kerbin.
        public bool IsAtHome()
        {
            return IsAtHome(this.Vessel);
        }

        // Checks if the given vessel is landed on Kerbin.
        public static bool IsAtHome(Vessel vessel)
        {
            if (vessel.loaded)
            {
                return (vessel.Landed || vessel.Splashed) && vessel.mainBody.name.Equals("Kerbin");
            }
            else
            {
                return (vessel.protoVessel.landed || vessel.protoVessel.splashed) && vessel.mainBody.name.Equals("Kerbin");
            }
        }

        // Enables or disables the crew when of the given vessel (sets their type to tourist, which is
        // basically just an passenger). Kerbals on EVA or with 0xp will be killed when disabled. We do
        // this because we can't otherwise differenciate disabled kerbals from real tourists and the
        // tourist-type has no effect on EVA.
        public static void UpdateCrew(Vessel vessel, bool enableCrew)
        {
            try
            {
                List<ProtoCrewMember> crewList = vessel.GetVesselCrew();
                foreach (ProtoCrewMember crewMember in crewList)
                {
                    if (enableCrew)
                    {
                        if (crewMember.type == ProtoCrewMember.KerbalType.Tourist && (crewMember.experience > 0 || crewMember.experienceLevel > 0))
                        {
                            string msg = string.Format("{0} has woken up from hibernation", crewMember.name);
                            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                            crewMember.type = ProtoCrewMember.KerbalType.Crew;
                        }
                    }
                    else
                    {
                        if (crewMember.type != ProtoCrewMember.KerbalType.Tourist)
                        {
                            if ((crewMember.experience > 0 || crewMember.experienceLevel > 0) && !vessel.isEVA)
                            {
                                string msg = string.Format("{0} has gone into hibernation", crewMember.name);
                                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                                crewMember.type = ProtoCrewMember.KerbalType.Tourist;
                            }
                            else
                            {
                                string msg = string.Format("{0} has died due to life support failure", crewMember.name);
                                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;

                                // Kerbal has to be removed from the vessel before he can die:
                                if (vessel.loaded)
                                {
                                    vessel.rootPart.RemoveCrewmember(crewMember);
                                    if (vessel.isEVA) vessel.rootPart.explode(); // Remove from game
                                }
                                else
                                {
                                    foreach (ProtoPartSnapshot proto in vessel.protoVessel.protoPartSnapshots)
                                    {
                                        if (proto.protoModuleCrew.Contains(crewMember)) proto.protoModuleCrew.Remove(crewMember);
                                    }
                                    if (vessel.isEVA) FlightGlobals.Vessels.Remove(vessel); // Remove from game
                                }
                                crewMember.Die();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] UpdateCrew(" + vessel.GetName() + "): " + e.ToString());
            }
        }

        public void UpdateCrew(bool enable)
        {
            UpdateCrew(this.Vessel, enable);
        }
    }
}
