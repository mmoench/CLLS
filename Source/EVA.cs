using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

using System.Reflection;

namespace CLLS
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EVA : MonoBehaviour
    {
        public void Awake()
        {
            // Add event handlers for start and end of EVA:
            GameEvents.onCrewOnEva.Remove(OnCrewOnEva);
            GameEvents.onCrewOnEva.Add(OnCrewOnEva);
            GameEvents.onCrewBoardVessel.Remove(OnCrewBoardVessel);
            GameEvents.onCrewBoardVessel.Add(OnCrewBoardVessel);

            // Add life support ressources to EVA-parts (males and females have different "parts"):
            addLSResources("kerbalEVA");
            addLSResources("kerbalEVAfemale");
        }

        private void OnCrewBoardVessel(GameEvents.FromToAction<Part, Part> action)
        {
            try
            {
                // Add life support from kerbal to the ship which he/she boards:
                double lifeSupport = 0.0;
                foreach (PartResource resource in action.from.Resources)
                {
                    if (resource != null && resource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                    {
                        lifeSupport += resource.amount;
                    }
                }
                lifeSupport = action.from.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, lifeSupport);
                lifeSupport = action.to.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, -lifeSupport);
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] OnCrewBoardVessel(): " + e.ToString());
            }
        }

        private void OnCrewOnEva(GameEvents.FromToAction<Part, Part> action)
        {
            try
            {
                // Check how much life support the kerbal and the ship have:
                double lifeSupportShip = 0.0;
                double lifeSupportKerbal = 0.0;
                double lifeSupportKerbalMax = 0.0;
                foreach (PartResource resource in action.to.Resources)
                {
                    if (resource != null && resource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                    {
                        lifeSupportKerbal += resource.amount;
                        lifeSupportKerbalMax += resource.maxAmount;
                    }
                }
                foreach (PartResource resource in action.from.Resources)
                {
                    if (resource != null && resource.resourceName == CLLS.RESOURCE_LIFE_SUPPORT)
                    {
                        lifeSupportShip += resource.amount;
                    }
                }

                // Try to take the maximum amount of life support from the ship on the EVA:
                double lifeSupportRequested = lifeSupportKerbalMax - lifeSupportKerbal;
                double lifeSupportGiven = action.from.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, lifeSupportRequested);
                action.to.RequestResource(CLLS.RESOURCE_LIFE_SUPPORT, -lifeSupportGiven);
            }
            catch(Exception e)
            {
                Debug.LogError("[CLLS] OnCrewOnEva(): " + e.ToString());
            }
        }

        private void addLSResources(string partName)
        {
            try
            {
                // There seems to be a bug in the kerbalEVA-part (the kerbalEVAfemale is fine), which causes the part to
                // not have been properly initialized, resulting in an empty module list. If this happens we will try
                // to call the Awake-method manually to initialize the stock-modules:
                Part part = PartLoader.getPartInfoByName(partName).partPrefab;
                if (part.Modules.Count == 0)
                {
                    object[] paramList = new object[] { };
                    MethodInfo awakeMethod = typeof(Part).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (awakeMethod != null) awakeMethod.Invoke(part, paramList);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] addLSResources(" + partName + ") waking up part: " + e.ToString());
            }

            try
            {
                // Add CLLS-module:
                PartLoader.getPartInfoByName(partName).partPrefab.AddModule(typeof(CLLSProvider).Name);

                ConfigNode resourceNode = new ConfigNode();
                resourceNode.AddValue("name", "LifeSupport");
                resourceNode.AddValue("amount", 0); // Empty by default.
                resourceNode.AddValue("maxAmount", CLLS.LIFE_SUPPORT_PER_KERBAL_PER_HOUR * 6); // Enough for 1 Kerbin-day.
                PartLoader.getPartInfoByName(partName).partPrefab.AddResource(resourceNode);
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] addLSResources(" + partName + "): " + e.ToString());
            }
        }
    }
}
