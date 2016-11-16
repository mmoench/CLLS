using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CLLS
{
    public class TrackedVessel
    {
        public Vessel vessel;           // The vessel which we are tracking
        public double lastUpdate;       // UTC when the vessel was last updated
        public int cachedCrewCount;
        public int cachedCrewCapacity;
        public double cachedLifeSupportDeltaPerHour;
        public double cachedLifeSupport;
        public double cachedMaxLifeSupport;

        public static TrackedVessel CreateFromVessel(Vessel vessel)
        {
            TrackedVessel trackedVessel = new TrackedVessel();
            trackedVessel.vessel = vessel;
            trackedVessel.UpdateCachedValues(); // Initialize the tracking
            return trackedVessel;
        }

        public double CalculateCurrentLifeSupportAmount()
        {
            if (cachedCrewCount == 0 && cachedLifeSupportDeltaPerHour == 0) return cachedLifeSupport;
            double timeDelta = Planetarium.GetUniversalTime() - lastUpdate;
            if (timeDelta <= 0) return cachedLifeSupport;
            double currentLifeSupport = cachedLifeSupport + (cachedLifeSupportDeltaPerHour / (60*60)) * timeDelta;
            if (currentLifeSupport < 0) currentLifeSupport = 0;
            else if (currentLifeSupport > cachedMaxLifeSupport) currentLifeSupport = cachedMaxLifeSupport;
            return currentLifeSupport;
        }

        // Checks if the given vessel is owned by the player:
        public bool IsUnowned()
        {
            if (vessel.loaded) return false;
            foreach (ProtoPartSnapshot protoPart in vessel.protoVessel.protoPartSnapshots)
            {
                if (protoPart.protoModuleCrew == null) continue;
                foreach (ProtoCrewMember crewMember in protoPart.protoModuleCrew)
                {
                    if (crewMember.type == ProtoCrewMember.KerbalType.Unowned) return true;
                }
            }
            return false;
        }

        // Adds or subtracts life-support from the vessel:
        private void RequestLifeSupport(double lifeSupportDelta)
        {
            // Depending on whether the vessel is active or not, we have to work with the object itself or the proto-snapshot:
            if (vessel.loaded)
            {
                // On active vessels we can simply request the total amount from the root-part:
                vessel.rootPart.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, -lifeSupportDelta);
            }
            else
            {
                // Update all parts individually:
                foreach (ProtoPartSnapshot protoPart in vessel.protoVessel.protoPartSnapshots)
                {
                    if (protoPart.resources == null) continue;
                    foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
                    {
                        if (protoResource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                        {
                            double storageCapacity = protoResource.maxAmount - protoResource.amount;
                            double partDelta = 0;
                            if (lifeSupportDelta < 0 && protoResource.amount > 0)
                            {
                                if (protoResource.amount + lifeSupportDelta < 0) partDelta = -protoResource.amount;
                                else partDelta = lifeSupportDelta;
                                lifeSupportDelta -= partDelta;
                            }
                            else if (lifeSupportDelta > 0 && storageCapacity > 0)
                            {
                                if (lifeSupportDelta > storageCapacity) partDelta = storageCapacity;
                                else partDelta = lifeSupportDelta;
                                lifeSupportDelta -= partDelta;
                            }

                            protoResource.amount += partDelta;
                            if (protoResource.amount < 0) protoResource.amount = 0;
                        }
                    }
                }
            }
            UpdateCachedValues();
        }

        public void Update()
        {
            try
            {
                if (vessel == null || vessel.name == null) return; // This should not happen, but better safe than sorry.
                double lifeSupportLeft = CalculateCurrentLifeSupportAmount();
                double lifeSupportDelta = lifeSupportLeft - cachedLifeSupport;

                // If the vessel is unowned, don't reduce the life-support, also add some if it was just created:
                if (!vessel.loaded && IsUnowned())
                {
                    if (lifeSupportLeft <= 0)
                    {
                        Debug.Log("[CLLS] setting life support to " + cachedMaxLifeSupport.ToString() + " for unowned vessel " + vessel.vesselName);
                        RequestLifeSupport(cachedMaxLifeSupport);
                    }
                }
                else
                {
                    RequestLifeSupport(lifeSupportDelta);
                    if (cachedLifeSupport <= 0 && cachedCrewCount > 0) KillCrew();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] TrackedVessel.Update(" + vessel.id + "): " + e.ToString());
            }
        }

        private void UpdateCachedValues()
        {
            try
            {
                cachedCrewCount = 0;
                cachedCrewCapacity = 0;
                cachedLifeSupport = 0;
                cachedMaxLifeSupport = 0;
                cachedLifeSupportDeltaPerHour = 0;
                lastUpdate = Planetarium.GetUniversalTime();

                if (vessel.loaded)
                {
                    foreach (Part part in vessel.parts)
                    {
                        cachedCrewCount += part.protoModuleCrew.Count;
                        cachedCrewCapacity += part.CrewCapacity;

                        foreach (PartResource resource in part.Resources)
                        {
                            if (resource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                            {
                                cachedLifeSupport += resource.amount;
                                cachedMaxLifeSupport += resource.maxAmount;
                            }
                        }

                        foreach (CLLSGenerator generator in part.Modules.GetModules<CLLSGenerator>())
                        {
                            if (generator.isRunning)
                            {
                                cachedLifeSupportDeltaPerHour += generator.currentProductionRatePerDay / 6; // This is stored in kerbin-days
                            }
                        }
                    }
                }
                else
                {
                    foreach (ProtoPartSnapshot protoPart in vessel.protoVessel.protoPartSnapshots)
                    {
                        cachedCrewCount += protoPart.protoModuleCrew.Count;
                        cachedCrewCapacity += protoPart.partPrefab.CrewCapacity;

                        foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
                        {
                            if (protoResource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                            {
                                cachedLifeSupport += protoResource.amount;
                                cachedMaxLifeSupport += protoResource.maxAmount;
                            }
                        }

                        foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
                        {
                            if (protoModule.moduleName == typeof(CLLSGenerator).Name)
                            {
                                
                                if (bool.Parse(protoModule.moduleValues.GetValue("isRunning")))
                                {
                                    cachedLifeSupportDeltaPerHour += float.Parse(protoModule.moduleValues.GetValue("currentProductionRatePerDay")) / 6; // This is stored in kerbin-days
                                }
                            }
                        }
                    }
                }
                if (cachedLifeSupport < (1.0 / (6*3600))) cachedLifeSupport = 0; // It can happen that there is a very small amount left in the tanks due to rounding errors

                cachedLifeSupportDeltaPerHour -= cachedCrewCount * CLLS.LIFE_SUPPORT_PER_KERBAL_PER_HOUR;
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] TrackedVessel.UpdateCachedValues(" + vessel.id + "): " + e.ToString());
            }
        }

        // Checks if the tracked vessel is landed on Kerbin.
        public bool IsAtHome()
        {
            return (vessel.Landed || vessel.Splashed) && vessel.mainBody == FlightGlobals.GetHomeBody();
        }

        // Kills all Kerbals on the vessel.
        public void KillCrew()
        {
            try
            {
                List<string> deadKerbalNames = new List<string>();
                if (vessel.loaded)
                {
                    if (vessel.isEVA)
                    {
                        deadKerbalNames.Add(vessel.GetVesselCrew()[0].name);
                        vessel.rootPart.explode();
                    }
                    else
                    {
                        foreach (Part part in vessel.parts)
                        {
                            foreach (ProtoCrewMember crewMember in part.protoModuleCrew)
                            {
                                crewMember.flightLog.AddEntry(FlightLog.EntryType.Die);
                                crewMember.ArchiveFlightLog();
                                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                                crewMember.Die();
                                deadKerbalNames.Add(crewMember.name);
                            }
                            part.protoModuleCrew.Clear();
                        }
                        vessel.MurderCrew(); // This does not seem to work, but lets be safe here
                    }
                }
                else
                {
                    // Find all crew members:
                    List<ProtoCrewMember> killList = new List<ProtoCrewMember>();
                    foreach (ProtoPartSnapshot protoPart in vessel.protoVessel.protoPartSnapshots)
                    {
                        if (protoPart.protoModuleCrew == null) continue;
                        foreach (ProtoCrewMember crewMember in protoPart.protoModuleCrew) killList.Add(crewMember);
                        protoPart.protoModuleCrew.Clear();
                        protoPart.protoCrewIndicesBackup.Clear();
                        protoPart.protoCrewNames.Clear();
                    }

                    // Kill all crew members:
                    foreach (ProtoCrewMember crewMember in killList)
                    {
                        crewMember.flightLog.AddEntry(FlightLog.EntryType.Die);
                        crewMember.ArchiveFlightLog();
                        crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                        if (vessel.isEVA)  FlightGlobals.Vessels.Remove(vessel);
                        deadKerbalNames.Add(crewMember.name);
                    }
                }

                if (deadKerbalNames.Count > 0)
                {
                    // Make sure KSP does not bring the dead back to life:
                    foreach (string kerbalName in deadKerbalNames)
                    {
                        if (!CLLS.killList.Contains(kerbalName)) CLLS.killList.Add(kerbalName);
                    }

                    // Log message about the now dead kerbals:
                    string message;
                    if (deadKerbalNames.Count > 1)
                    {
                        message = deadKerbalNames.Count.ToString() + " have died due to life support failure: " + String.Join(", ", deadKerbalNames.ToArray());
                    }
                    else
                    {
                        message = deadKerbalNames[0] + " has died due to life support failure!";
                    }
                    Debug.Log("[CLLS] " + message);
                    ScreenMessages.PostScreenMessage(message, 5f, ScreenMessageStyle.UPPER_CENTER);
                }

                // Update the tracked vessels during the next tick to maybe remove killed EVA-missions from the tracking-list:
                CLLS.forceGlobalUpdate = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] TrackedVessel.KillCrew(" + vessel.id + "): " + e.ToString());
            }
        }
    }
}
