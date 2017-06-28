using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;
using KSP.UI.Screens;

/**
 * This mod is supposed to simmulate a very simple life-support system: Each kerbal needs one unit of life-support per day,
 * which can be produced by generators. The major feature here is that this is tracked for all vessels in the background.
 * To not overload the system with too many unnecessary updates we only track active vessels in near real time (one update per
 * scond via the timer-function) and for all other vessels we simply save their values like current production and last update
 * to calculate their new resources on demand. This is done when some event fires which changes the game-world (eg a vessel
 * is modified) or when the game is saved or loaded.
 **/
namespace CLLS
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class CLLS : UnityEngine.MonoBehaviour
    {
        static bool initialized = false;

        public static List<TrackedVessel> trackedVessels = null;

        public const String RESOURCE_LIFE_SUPPORT = "LifeSupport";
        public const double LIFE_SUPPORT_PER_KERBAL_PER_HOUR = 1d / 6d; // 1 per Kerbin-Day

        /**
         * KSP has some kind of secret backup-list in which they store which kerbal had which status and was sitting in which
         * seat of a craft that is currently not active (or I haven't found the right setting yet). When we kill a kerbal,
         * he will sometimes get resurected by KSP with the following message before saving the game:
         * 
         * Crewmember X Kerman found inside a part but status is set as missing. Vessel must have failed to save earlier. Restoring assigned status.
         * 
         * To circumvent this we keep our own list and re-kill any kerbal that comes back to live. Not pretty, but when you
         * actually quit the game after we've killed a kerbel and then start again from the last save, the kerbal will remain daed,
         * so we probably already do all we can do and KSP is just messing things up for us with some inaccesible function ...
         **/
        public static List<string> killList = null;

        /**
         * Whenever something happens which can change the state of the (game-) world (eg an event is triggered, a save is loaded),
         * we set this variable to signal to the timer-function to update all vessels during the next run. We do this in lieu of
         * calling the update function directly to avoid spamming updated (one event comes seldom alone) and because the time handles
         * the delayed call of the updates in case the game isn't properly initialized yet.
         **/
        public static bool forceGlobalUpdate = false;

        // Install handler for continous life support updates:
        public void Awake()
        {
            if (initialized) return;

            DontDestroyOnLoad(this);
            if (!IsInvoking("Timer")) InvokeRepeating("Timer", 1, 1); // once every second.
            if (trackedVessels == null) trackedVessels = new List<TrackedVessel>();
            if (killList == null) killList = new List<string>();

            // Whenever something relevant happens, we want to update our tracked vessels:
            GameEvents.onVesselCreate.Add(this.OnVesselUpdate);
            GameEvents.onNewVesselCreated.Add(this.OnVesselUpdate);
            GameEvents.onVesselChange.Add(this.OnVesselUpdate);
            GameEvents.onVesselCrewWasModified.Add(this.OnVesselUpdate);
            GameEvents.onVesselPartCountChanged.Add(this.OnVesselUpdate);
            GameEvents.onVesselWasModified.Add(this.OnVesselUpdate);
            GameEvents.onVesselDestroy.Add(this.OnVesselUpdate);
            GameEvents.onVesselRecovered.Add(this.OnVesselRecovered);
            GameEvents.onVesselTerminated.Add(this.OnVesselTerminated);

            initialized = true;
        }

        private void OnVesselTerminated(ProtoVessel data)
        {
            forceGlobalUpdate = true;
        }

        private void OnVesselRecovered(ProtoVessel data0, bool data1)
        {
            forceGlobalUpdate = true;
        }

        private void OnVesselUpdate(Vessel data)
        {
            forceGlobalUpdate = true;
        }

        public static int GetDayLength()
        {
            int dayLength = 24;
            if (GameSettings.KERBIN_TIME) dayLength = 6;
            return dayLength;
        }

        public static TrackedVessel GetTrackedVessel(Vessel vessel)
        {
            foreach (TrackedVessel trackedVessel in trackedVessels) if (trackedVessel.vessel == vessel) return trackedVessel;
            UpdateAllTrackedVessels();
            foreach (TrackedVessel trackedVessel in trackedVessels) if (trackedVessel.vessel == vessel) return trackedVessel;
            return null;
        }

        // Updates the list of tracked vessels as well as all the vessels on it (updates the resources in the vessels and
        // kills the crew, if appropriate):
        public static void UpdateAllTrackedVessels()
        {
            try
            {
                long time = DateTime.Now.Ticks;
                int trackingsAdded = 0;
                int trackingsRemoved = 0;

                // This should only happen when the game is still loading, at least there should be some asteroids:
                if (FlightGlobals.Vessels.Count == 0)
                {
                    trackedVessels.Clear();
                    return;
                }

                // Find new vessels, which are not yet tracked and track them:
                List<Guid> trackedIds = new List<Guid>();
                foreach (TrackedVessel trackedVessel in trackedVessels) trackedIds.Add(trackedVessel.vessel.id);
                List<Vessel> missingVessels = FlightGlobals.Vessels.FindAll(x => !trackedIds.Contains(x.id));
                foreach (Vessel vessel in missingVessels)
                {
                    if (vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.SpaceObject || vessel.vesselType == VesselType.Unknown) continue;
                    trackedVessels.Add(TrackedVessel.CreateFromVessel(vessel));
                    trackingsAdded++;
                }

                // Find vessels, which were removed:
                List<Guid> existingIds = new List<Guid>();
                foreach (Vessel vessel in FlightGlobals.Vessels) existingIds.Add(vessel.id);
                List<TrackedVessel> trackedVesselsToRemove = trackedVessels.FindAll(x => x?.vessel?.id == null || !existingIds.Contains(x.vessel.id));
                foreach (TrackedVessel trackedVessel in trackedVesselsToRemove)
                {
                    trackedVessels.Remove(trackedVessel);
                    trackingsRemoved++;
                }

                // Update all the tracked vessels:
                foreach (TrackedVessel trackedVessel in trackedVessels) trackedVessel.Update();
                time = (DateTime.Now.Ticks - time) / TimeSpan.TicksPerSecond;
                Debug.Log("[CLLS] tracked " + trackingsAdded.ToString() + " new, removed " + trackingsRemoved.ToString() + " old and updated " + trackedVessels.Count.ToString() + " vessels in " + time.ToString("0.000s"));
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] UpdateAllTrackedVessels(): " + e.ToString());
            }
        }

        // Called reguarly to check for vessels running out of life-support:
        public void Timer()
        {
            try
            {
                // Don't update in main menu:
                if (HighLogic.LoadedScene == GameScenes.MAINMENU || HighLogic.LoadedScene == GameScenes.CREDITS || HighLogic.LoadedScene == GameScenes.SETTINGS) return;

                // For reasons unknown this mod loads before all assets are properly initialized, so we have to wait a little bit:
                if (FlightGlobals.Vessels.Count == 0 || !ApplicationLauncher.Ready) return;

                // This should only happen when the game was just loaded or a vessel was modified externaly:
                if (forceGlobalUpdate || (FlightGlobals.Vessels.Count > 0 && trackedVessels.Count == 0))
                {
                    UpdateAllTrackedVessels();
                    forceGlobalUpdate = false;
                }

                // Update vessels whith either depleted life support or if they are active:
                foreach (TrackedVessel trackedVessel in trackedVessels)
                {
                    if (trackedVessel.vessel.isActiveVessel) trackedVessel.Update();
                    else if (trackedVessel.cachedCrewCount > 0 && trackedVessel.CalculateCurrentLifeSupportAmount() <= 0) trackedVessel.Update();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] Timer(): " + e.ToString());
            }
        }
    }

    // This class handels load- and save-operations.
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    class CLLSScenarioModule : ScenarioModule
    {
        public override void OnSave(ConfigNode node)
        {
            try
            {
                // Check for zombies:
                if (CLLS.killList.Count > 0)
                {
                    foreach (ProtoCrewMember kerbal in HighLogic.CurrentGame.CrewRoster.Kerbals(ProtoCrewMember.KerbalType.Crew))
                    {
                        if (CLLS.killList.Contains(kerbal.name) && kerbal.rosterStatus != ProtoCrewMember.RosterStatus.Dead)
                        {
                            Debug.Log("[CLLS] killing zombie " + kerbal.name);
                            kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                        }
                    }
                }

                // Update all vessels before their stats are made persistant. This way we don't have to store our
                // tracking-list with all the meta-information about the vessels:
                CLLS.UpdateAllTrackedVessels();

                // Save the kill-list so that a re-load of a save in the same session (or a switching of the game-scene,
                // which works by saving and then loading) causes dead kerbals to become alive again:
                foreach (string kerbalName in CLLS.killList)
                {
                    node.AddValue("kill_list", kerbalName);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] OnSave(): " + e.ToString());
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                // Retrieve our kill-list:
                if (CLLS.killList == null) CLLS.killList = new List<string>();
                CLLS.killList.Clear();
                foreach (string deadName in node.GetValues("kill_list"))
                {
                    CLLS.killList.Add(deadName);
                }

                // Update and rebuild all tracked vessels as soon as possible:
                if (CLLS.trackedVessels == null) CLLS.trackedVessels = new List<TrackedVessel>();
                CLLS.trackedVessels.Clear();
                CLLS.forceGlobalUpdate = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[CLLS] OnLoad(): " + e.ToString());
            }
        }
    }
}
