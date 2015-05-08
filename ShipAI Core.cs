using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripting
{
    class CodeEditorEmulator
    {
        IMyGridTerminalSystem GridTerminalSystem = null;
        public string Storage; //Only used when storagePanel isn't present.

        #region CodeEditor


        /*
     * Full ship/station AI system by Anthropy in February 2015
     * Parts of battery management code originally written by Stiggan and Malakeh in January 2015.
     * There are lots of comments throughout the code to help you along. 
     * 100K is a lot of bytes so I'm not afraid to run out of them any time soon.
     * Note however that it's still in heavy development and may radically change with updates.
     */
        //You can change some basic behavior here with these variables. 
        //If you want to toggle modules, see the Main() method at the bottom of the file.

        //Due to execution limits, we can not enumerate more than about 120 devices per category, or the script will simply be halted.
        const int executionCap = 120;
        const int debugMaxCharacters = 5000;
        const float minimumGravity = 0.1f;
        const string shipName = "AUTUMN AI Core v1.3-WIP";
        const bool ignoreDevicesThatAreTooMuch = true; //Enforces execution cap
        const bool maintenanceMode = false; //Set this to disable alarms on broken blocks
        const string healingProjectorName = "Projector_healing";
        const bool compactInventories = true; //This will be part of the configuration module later on.

        VariableUpdateManagement VUM;

        void debugOutput(string text) { VUM.debugOutput(text); }
        public string getNormalizedPowerString(double watts, bool hour) { return VUM.getNormalizedPowerString(watts, hour); }

        /// <summary>
        /// This is the main flow of the script/AI. You can enable/disable subroutines by adding/removing them here.
        /// </summary>
        void Main()
        {
            if (VUM == null)
            {
                VUM = new VariableUpdateManagement(GridTerminalSystem, Storage);
            }
            VUM.currentInternalIteration++; //Don't remove this
            

            debugOutput("Running External Modules");
            if (!VUM.isScreenModulePresent)
            {
                debugOutput("No Screen module found, printing to status panels from core..");
                VUM.updateStatusPanels(); //Turn this off if you have the AI Screen Module.
            }


            //Management modules are below here. Things like power management, turret management, etc.
            //You can find all modules at the bottom of the file in the 'AI_Modules' region.
            debugOutput("Running Ship Management Subroutines");
            //Alert system subroutines
            soundAlerts();
            manageTurrets(); //Toggles turrets; Red means on, no alerts means off.

            if (compactInventories && VUM.currentInternalIteration == 2) { doCompactInventories(); }


            //Power management subroutines
            //manageBatteries(); //Power Generation Management is broken and to be replaced in v1.3
            manageAnntenas(500, 35000); //First number is minimum range, and second is maximum range, in meters.
            manageBeacons(500, 35000);
            manageGravityGenerators(minimumGravity); //minimumGravity is a constant right now, can be found at the top of the script.
            manageAssemblers(); //Toggles off assemblers if below specified (default 1/4th) kwh is left in batteries.
            manageRefineries(); //Same as above



            if (VUM.currentInternalIteration > 9) { VUM.currentInternalIteration = 0; }
            debugOutput("AI Iteration " + VUM.currentInternalIteration.ToString() + " finished.");

            // Without this, changes to variables won't persist.
            debugOutput("Dumping variable cache to persistent storage");
            VUM.flushVariables();
        }

        public static bool isHealthy(IMyTerminalBlock block)
        {
            return (block.IsFunctional && block.IsWorking);
        }


        #region AI_Subroutines
        //Below here are the subroutines of the script you can enable in the Main() void.

        void soundAlerts()
        {
            if (VUM.codeBlue)
            {
                for (int i = 0; i < VUM.speakersBlue.Count; i++)
                {
                    debugOutput("Playing sound on " + VUM.speakersBlue[i].DefinitionDisplayNameText);
                    VUM.speakersBlue[i].ApplyAction("PlaySound");
                    if (!VUM.speakersBlue[i].IsSoundSelected)
                    {
                        debugOutput("No sound selected on " + VUM.speakersBlue[i].DefinitionDisplayNameText);
                    }
                }
            }
            else if (VUM.codeOrange)
            {
                for (int i = 0; i < VUM.speakersOrange.Count; i++)
                {
                    debugOutput("Playing sound on " + VUM.speakersOrange[i].DefinitionDisplayNameText);
                    VUM.speakersOrange[i].ApplyAction("PlaySound");
                    if (!VUM.speakersOrange[i].IsSoundSelected)
                    {
                        debugOutput("No sound selected on " + VUM.speakersOrange[i].DefinitionDisplayNameText);
                    }
                }
            }
            else if (VUM.codeRed)
            {
                for (int i = 0; i < VUM.speakersRed.Count; i++)
                {
                    debugOutput("Playing sound on " + VUM.speakersRed[i].DefinitionDisplayNameText);
                    VUM.speakersRed[i].ApplyAction("PlaySound");
                    if (!VUM.speakersRed[i].IsSoundSelected)
                    {
                        debugOutput("No sound selected on " + VUM.speakersRed[i].DefinitionDisplayNameText);
                    }
                }
            }
        }

        

        ///// <summary>
        ///// Uses a set of 4 lights to toggle on and off configuration values displayed on the config screen.
        ///// </summary>
        //void runConfigurationModule()
        //{
        //    if (configLights.Count < 1 || configPanels.Count < 1) { return; }
        //    StringBuilder output = new StringBuilder();

        //    output.AppendFormat("Current screen: {0}", currentConfigScreen);
        //    if (currentConfigScreen == ConfigScreens.Main)
        //    {
        //        output.AppendLine("Hello! Welcome to the Configuration and Interation module.");

        //    }

        //    updateConfigScreen(output.ToString());
        //}
        ////Stuff for Config module
        //enum ConfigScreens
        //{
        //    Main
        //}
        //ConfigScreens currentConfigScreen;
        //void updateConfigScreen(string screenContent)
        //{
        //    if (configPanels.Count < 1) { return; }
        //    string configPanelContents;

        //}
        ////End stuff for config module


        /// <summary>
        /// Automatically increases/decreases antenna range to available power
        /// Attempting to not drain any battery
        /// </summary>
        /// <param name="minimumRangeMeters">Minimum amount of meters Antenna must be visible</param>
        void manageAnntenas(long minimumRangeMeters, long maximumRangeMeters)
        {
            var antennas = VUM.antennas;
            var maxPower = VUM.maxPower;
            var powerUsage = VUM.powerUsage;
            
            if (antennas.Count < 1) { return; }
            if (maxPower < powerUsage) //If there's power being drained from the batteries
            {
                for (int i = 0; i < antennas.Count; i++)
                {
                    var ant = (IMyRadioAntenna)antennas[i];
                    if (ant.Radius > minimumRangeMeters && isHealthy(ant))
                    {
                        ant.ApplyAction("DecreaseRadius");
                    }
                }
            }
            else if (maxPower - 100 > powerUsage) //Only up the range if there's enough power overhead
            {
                for (int i = 0; i < antennas.Count; i++)
                {
                    var ant = (IMyRadioAntenna)antennas[i];
                    if (ant.Radius < maximumRangeMeters && isHealthy(ant))
                    {
                        ant.ApplyAction("IncreaseRadius");
                    }
                }
            }
        }

        /// <summary>
        /// Automatically increases/decreases antenna range to available power
        /// Attempting to not drain any battery
        /// </summary>
        /// <param name="minimumRangeMeters">Minimum amount of meters Antenna must be visible</param>
        void manageBeacons(long minimumRangeMeters, long maximumRangeMeters)
        {
            var beacons = VUM.beacons;
            var maxPower = VUM.maxPower;
            var powerUsage = VUM.powerUsage;

            if (beacons.Count < 1) { return; }
            if (maxPower < powerUsage) //If there's power being drained from the batteries
            {
                for (int i = 0; i < beacons.Count; i++)
                {
                    var beacon = (IMyBeacon)beacons[i];
                    if (beacon.Radius > minimumRangeMeters && isHealthy(beacon))
                    {
                        beacon.ApplyAction("DecreaseRadius");
                    }
                }
            }
            else if (maxPower - 100 > powerUsage) //Only up the range if there's enough power overhead
            {
                for (int i = 0; i < beacons.Count; i++)
                {
                    var beacon = (IMyBeacon)beacons[i];
                    if (beacon.Radius < maximumRangeMeters && isHealthy(beacon))
                    {
                        beacon.ApplyAction("IncreaseRadius");
                    }
                }
            }
        }

        /// <summary>
        /// Automatically increases and decreases artificial gravity strength based on available power.
        /// 
        /// </summary>
        /// <param name="minimumGravity"></param>
        /// <param name="maximumGravity"></param>
        void manageGravityGenerators(float minimumGravity, float maximumGravity = 1)
        {
            var gravityGenerators = VUM.gravityGenerators;
            var powerUsage = VUM.powerUsage;
            var maxPower = VUM.maxPower;

            if (gravityGenerators.Count < 1) { debugOutput("No gravity generators found to manage"); return; }
            for (int i = 0; i < gravityGenerators.Count; i++)
            {
                var curGen = ((IMyGravityGenerator)gravityGenerators[i]);
                var curGenGravity = Math.Abs(((IMyGravityGenerator)gravityGenerators[i]).Gravity);
                bool isUpsideDown = curGen.Gravity.ToString().StartsWith("-");
                if (powerUsage > maxPower) //FIXME once new powermanagement arrives
                {
                    if (curGenGravity > minimumGravity)
                    {
                        debugOutput(string.Format("Turning down {0} due to power conditions", curGen.DisplayNameText));

                        if (!isUpsideDown)
                        {
                            curGen.ApplyAction("DecreaseGravity");
                        }
                        else
                        {
                            curGen.ApplyAction("IncreaseGravity");
                        }
                    }
                }
                else
                {
                    debugOutput(string.Format("Turning up {0} because of available power", curGen.DisplayNameText));
                    if (isUpsideDown)
                    {
                        curGen.ApplyAction("DecreaseGravity");
                    }
                    else
                    {
                        curGen.ApplyAction("IncreaseGravity");
                    }
                }
            }
        }


        /// <summary>
        /// Toggles refineries to make sure at least the given amount of energy in KWh is left in the batteries (default is 25%)
        /// Note: any value for mininmumKWh must be an ABSOLUTE value, e.g 500KWh, not in percentages. If you want percentages, edit the method.
        /// </summary>
        /// <param name="toggleOffset">Offset before refineries are turned on again to prevent togglestorm</param>
        /// <param name="minimumKWh">Least amount of KWh (e.g 500KWh) that must be left in batttery</param>
        void manageRefineries(int toggleOffset = 350, int minimumKWh = 0)
        {
            var power = VUM.wattHourInBatteries;
            var maxPowerInBatteries = VUM.maxWattHourInBatteries; 
            var refineries = VUM.refineries;

            bool toggleRefineries = true;
            for (int i = 0; i < refineries.Count; i++)
            {
                if (minimumKWh <= 0)
                {
                    if (power <= (maxPowerInBatteries / 4)) { debugOutput("Turning off refinery " + i + " to preserve power."); refineries[i].ApplyAction("OnOff_Off"); }
                    else if (power >= ((maxPowerInBatteries / 4) + toggleOffset)) { debugOutput("Turning on refinery " + i + " as there's enough power."); refineries[i].ApplyAction("OnOff_On"); }
                }
                else
                {
                    if (power <= minimumKWh) { debugOutput("Turning off refinery " + i + " to preserve power."); refineries[i].ApplyAction("OnOff_Off"); }
                    else if (power >= (minimumKWh + toggleOffset)) { debugOutput("Turning on refinery " + i + " as there's enough power."); refineries[i].ApplyAction("OnOff_On"); }
                }
            }

            if (toggleRefineries)
            { refineries.ForEach(s => s.RequestEnable(true)); }
            else { refineries.ForEach(s => s.RequestEnable(false)); }
        }

        /// <summary>
        /// Works just like manageRefineries, except with Assemblers. Literally I used the same code with a quick case-sensitive replace.
        /// </summary>
        /// <param name="toggleOffset"></param>
        /// <param name="minimumKWh"></param>
        void manageAssemblers(int toggleOffset = 350, int minimumKWh = 0)
        {
            var power = VUM.wattHourInBatteries;
            var maxPowerInBatteries = VUM.maxWattHourInBatteries;
            var assemblers = VUM.assemblers;


            bool toggleAssemblers = true;
            for (int i = 0; i < assemblers.Count; i++)
            {
                if (minimumKWh <= 0)
                {
                    if (power <= (maxPowerInBatteries / 4)) { debugOutput("Turning off assembler " + i + " to preserve power."); assemblers[i].ApplyAction("OnOff_Off"); }
                    else if (power >= ((maxPowerInBatteries / 4) + toggleOffset)) { debugOutput("Turning on assembler " + i + "as there's enough power."); assemblers[i].ApplyAction("OnOff_On"); }
                }
                else
                {
                    if (power <= minimumKWh) { debugOutput("Turning off assembler " + i + " to preserve power."); assemblers[i].ApplyAction("OnOff_Off"); }
                    else if (power >= (minimumKWh + toggleOffset)) { debugOutput("Turning on assembler " + i + " as there's power."); assemblers[i].ApplyAction("OnOff_On"); }
                }
            }

            if (toggleAssemblers)
            { assemblers.ForEach(s => s.RequestEnable(true)); }
            else { assemblers.ForEach(s => s.RequestEnable(false)); }
        }

        /// <summary>
        /// This function automatically toggles turrets when the alertState goes to Red.
        /// </summary>
        void manageTurrets()
        {
            bool codeRed = VUM.codeRed;
            var turrets = VUM.turrets;

            if (codeRed)
            {
                debugOutput("Turning on all turrets for Code Red");
                for (int i = 0; i < turrets.Count; i++)
                {
                    turrets[i].ApplyAction("OnOff_On");
                }
            }
            else
            {
                debugOutput("Turning off all turrets due to no alert being ongoing.");
                for (int i = 0; i < turrets.Count; i++)
                {
                    turrets[i].ApplyAction("OnOff_Off");
                }
            }
        }

        void doCompactInventories()
        {
            debugOutput("Compacting inventories..");
            var blocks = new List<IMyTerminalBlock>(); //FIXME: doesn't exclude _unmanaged
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(blocks); //FIXME: This only targets Cargo containers. 
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = (IMyCargoContainer)blocks[blockIndex];
                for (int i = 0; i < block.GetInventoryCount(); i++)
                {
                    var inventory = block.GetInventory(i);
                    var itemsCollection = inventory.GetItems();
                    for (int x = itemsCollection.Count - 1; x >= 0; x--)
                    {
                        inventory.TransferItemTo(inventory, x, stackIfPossible: true);
                    }
                }
            }
        }

        //Below are internally used functions

        /// <summary>
        /// Turns a device on or off, but only if it's not already in the requested state.
        /// </summary>
        /// <param name="device">The device to toggle</param>
        /// <param name="state">Desired state (i.e on or off)</param>
        void toggleDeviceIfNeeded(IMyFunctionalBlock device, bool state)
        {
            if (state)
            {
                if (!device.Enabled)
                {
                    device.RequestEnable(state);
                }
            }
            else
            {
                if (device.Enabled)
                {
                    device.RequestEnable(state);
                }
            }

        }

        #endregion

        #region AI_Subsystems
        class VariableUpdateManagement
        {
            IMyGridTerminalSystem GridTerminalSystem;
            string Storage;
            public VariableUpdateManagement(IMyGridTerminalSystem _sys, string _stor)
            {
                GridTerminalSystem = _sys;
                updateLCDPanels(); //Must be first to execute, otherwise ai_storage and debugpanel are unavailable.

                // Without this, persisted variables won't be usable.
                debugOutput("Loading variables into internal cache");
                loadVariables();

                debugOutput("Running internal Update Modules");
                updateModules(); //Checks if there's a screen module present or other modules in the future.
                updateMiscWithBlacklist(); //Updates things like assemblers, refineries, lights, speakers, etc, and excludes anything that ends in _unmanaged
                updateBrokenBlocks(); //Checks for broken or missing blocks

                updateAssemblers();
                updateRefineries();
                updateSpeakers();
                updateStatusString();
                updateScreenModuleValues(); //This is optional if you don't have the Screen module.
                //updateIconPanels(); //A fix is underway: http://forums.keenswh.com/post/01071008-lcd-panel-image-api-unsuable-in-scripts-7318058?pid=1286386079 
            }






            // A dictionary of variables to be persisted, effectively serving as a cache.
            Dictionary<string, string> Variables = new Dictionary<string, string>();

            //Cached variables, attempting to only fetch everything a single time throughout program life.
            public List<IMyAssembler> assemblers = new List<IMyAssembler>();
            public List<IMyRefinery> refineries = new List<IMyRefinery>();

            public List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> solarpanels = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> antennas = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> beacons = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> welders = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> configLights = new List<IMyTerminalBlock>();
            public List<IMyTerminalBlock> gravityGenerators = new List<IMyTerminalBlock>();

            public List<IMyTextPanel> statusPanels = new List<IMyTextPanel>();
            public List<IMyTextPanel> iconPanels = new List<IMyTextPanel>();
            public List<IMyTextPanel> configPanels = new List<IMyTextPanel>();
            public IMyTextPanel debugPanel;
            public IMyTextPanel storagePanel; //Can't use Storage string if there's modules present!

            public IMyProjector healingProjector;
            public Dictionary<string, int> missingBlocks = new Dictionary<string, int>();


            public List<IMySoundBlock> speakersBlue = new List<IMySoundBlock>();
            public List<IMySoundBlock> speakersOrange = new List<IMySoundBlock>();
            public List<IMySoundBlock> speakersRed = new List<IMySoundBlock>();
            public List<IMySoundBlock> speakersPowerlow = new List<IMySoundBlock>();



            public double wattHourInBatteries = 0;
            public double maxWattHourInBatteries = 0;

            public double solarpanelWatts = 0;
            public double reactorWatts = 0;
            public double batteryWatts = 0;
            public double batteryInputWatts = 0;

            public double maxPower = 0;
            public double powerUsage = 0;

            public int dischargingBatteries = 0;
            public int brokenBlockCount = 0;
            public int offlineBlockCount = 0;
            public int missingBlockCount = 0;
            public int activeAssemblers = 0;
            public int activeRefineries = 0;
            public int currentInternalIteration; //This is used to trigger things like purging old modules and such, and distribute load across iterations. Should never go higher than 10.

            public bool tooManyBatteries = false;
            public bool tooManyReactors = false;
            public bool tooManySolarpanels = false;
            public bool tooManyAntennas = false;
            public bool tooManyBeacons = false;
            public bool isScreenModulePresent = false;
            public bool isProjectorPresent = false;

            public string alertState = "";
            public string powerState = ""; //This is NOT the message, this is the internal powerstate of the AI!
            public string antennaString = "";
            public string beaconString = "";
            public string StatusString = "";
            public string shortStatus = "";
            public string damageReport = "";
            public string offlineReport = "";
            //public string missingReport; //I implemented everything, only to realize I don't even have the data :v..
            public string powerReport = ""; //This is the message, NOT the internal powerstate!
            public string shortCommsStatus = "";

            //Below here are technically variables, but they mask a var from ai_storage. don't use them more than needed.
            public bool codeBlue
            {
                get { return alertState.Equals("blue"); }
                set
                {
                    if (value)
                    {
                        alertState = "blue";
                    }
                    else
                    {
                        alertState = "none";
                    }
                }
            }
            public bool codeOrange
            {
                get { return alertState.Equals("orange"); }
                set
                {
                    if (value)
                    {
                        alertState = "orange";
                    }
                    else
                    {
                        alertState = "none";
                    }
                }
            }
            public bool codeRed
            {
                //the easteregg here has been removed due to potential issues with the screen module template engine.
                get { return getVariable("alertState").Equals("red"); }
                set
                {
                    if (value)
                    {
                        storeVariable("alertState", "red");
                    }
                    else
                    {
                        storeVariable("alertState", "none");
                    }
                }
            }





            void updateSpeakers()
            {
                debugOutput("Updating speakers..");
                List<IMyTerminalBlock> templist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(templist);
                speakersRed.Clear();
                speakersOrange.Clear();
                speakersBlue.Clear();
                for (int i = 0; i < templist.Count; i++)
                {
                    if (!templist[i].DisplayNameText.EndsWith("_unmanaged"))
                    {
                        if (templist[i].DisplayNameText.EndsWith("_blue"))
                        {
                            speakersBlue.Add((IMySoundBlock)templist[i]);
                        }
                        else if (templist[i].DisplayNameText.EndsWith("_orange"))
                        {
                            speakersOrange.Add((IMySoundBlock)templist[i]);
                        }
                        else if (templist[i].DisplayNameText.EndsWith("_red"))
                        {
                            speakersRed.Add((IMySoundBlock)templist[i]);
                        }
                    }
                }


                debugOutput(string.Format("Found {0} blue, {1} orange, {2} red speakers.", speakersBlue.Count, speakersOrange.Count, speakersRed.Count));
            }

            /// <summary>
            /// All external modules must check in every 10 iterations to make sure they're still OK
            /// </summary>
            void updateModules()
            {

                var screenModuleMessage = getVariable("screenModuleStatus");
                if (screenModuleMessage == "Red one standing by.")
                {
                    isScreenModulePresent = screenModuleMessage == "Red one standing by.";
                }
                else
                {
                    debugOutput("No screen module found");
                }
                //storeVariable("", "screenModuleStatus"); //Reset the var so the Screen module has to check in again.
            }

            void updateLCDPanels()
            {
                debugPanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("debugpanel");
                storagePanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("ai_storage");
                if (storagePanel == null)
                {
                    debugOutput("WARNING: NO EXTERNAL STORAGE FOUND.");
                    debugOutput("If you want to connect modules, external storage is required.");
                    debugOutput("You can do this by creating an LCD panel named 'ai_storage'.");
                    //throw new Exception("No storage found!\nPlease create an LCD panel named 'ai_storage', edit the script and Remember and Exit again.\nCreate a panel named 'debugpanel' for more info.");
                }
                if (debugPanel != null) { debugPanel.WritePublicText(""); }

                debugOutput("Updating LCD Panels..");
                List<IMyTerminalBlock> templist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(templist);

                statusPanels.Clear();
                iconPanels.Clear();


                for (int i = 0; i < templist.Count; i++)
                {
                    if (!templist[i].DisplayNameText.EndsWith("_unmanaged"))
                    {
                        if (templist[i].DisplayNameText.EndsWith("_status"))
                        {
                            statusPanels.Add((IMyTextPanel)templist[i]);
                        }
                        else if (templist[i].DisplayNameText.EndsWith("_icons") || templist[i].DisplayNameText.EndsWith("_icon"))
                        {
                            iconPanels.Add((IMyTextPanel)templist[i]);
                        }
                        else if (templist[i].DisplayNameText.EndsWith("_configmod"))
                        {
                            configPanels.Add((IMyTextPanel)templist[i]);
                        }
                    }
                }
            }

            void updateRefineries()
            {

                activeRefineries = 0;
                if (debugPanel != null)
                {
                    debugPanel.WritePublicText("Updating refineries..", true);
                }
                refineries = getRefineries();
                for (int i = 0; i < refineries.Count; i++)
                {
                    if (!refineries[i].DisplayNameText.EndsWith("_unmanaged"))
                    {
                        if (refineries[i].IsProducing) { activeRefineries++; }
                    }
                }
                if (debugPanel != null)
                {
                    debugPanel.WritePublicText(refineries.Count + " found.\n", true);
                }
            }

            void updateAssemblers()
            {
                activeAssemblers = 0;
                if (debugPanel != null)
                {
                    debugPanel.WritePublicText("Updating assemblers..", true);
                }

                assemblers = getAssemblers();
                for (int i = 0; i < assemblers.Count; i++)
                {
                    if (!assemblers[i].DisplayNameText.EndsWith("_unmanaged"))
                    {
                        if (assemblers[i].IsProducing) { activeAssemblers++; }
                    }
                }
                if (debugPanel != null)
                {
                    debugPanel.WritePublicText(assemblers.Count + " found.\n", true);
                }
            }

            void updateBrokenBlocks()
            {
                if (healingProjector == null)
                {
                    debugOutput("No projector found, cannot update broken blocks!");
                    return;
                }
                missingBlocks = new Dictionary<string, int>();
                missingBlockCount = 0;

                string[] tempLines = ((IMyTerminalBlock)healingProjector).DetailedInfo.Split('\n');
                bool a = false;
                for (int i = 0; i < tempLines.Length; i++)
                {
                    if (tempLines[i] == "Blocks remaining:") { a = true; continue; }

                    if (a)
                    {
                        string[] tempParts = tempLines[i].Split(':');
                        int parsedCount = 0;
                        if (tempParts[1].Length > 0)
                        {
                            int.TryParse(tempParts[1].Trim(), out parsedCount);
                        }
                        missingBlocks.Add(tempParts[0], parsedCount);
                        missingBlockCount += parsedCount;
                    }
                }
            }

            void updateMiscWithBlacklist()
            {
                debugOutput("Updating Misc items..");
                if (alertState == null)
                {
                    storeVariable("none", "alertState");
                    debugOutput("alertstate persistent variable created");
                }
                if (powerState == null)
                {
                    storeVariable("none", "powerState");
                    debugOutput("powerstate persistent variable created");
                }

                healingProjector = (IMyProjector)GridTerminalSystem.GetBlockWithName(healingProjectorName);
                if (healingProjector == null)
                {
                    debugOutput("No projector found! " + healingProjectorName);
                }
                else
                {
                    debugOutput("Found projector: " + ((IMyTerminalBlock)healingProjector).DisplayNameText);
                    isProjectorPresent = true;
                }

                batteries = new List<IMyTerminalBlock>();
                solarpanels = new List<IMyTerminalBlock>();
                reactors = new List<IMyTerminalBlock>();
                antennas = new List<IMyTerminalBlock>();
                beacons = new List<IMyTerminalBlock>();
                turrets = new List<IMyTerminalBlock>();
                welders = new List<IMyTerminalBlock>();
                configLights = new List<IMyTerminalBlock>();

                List<IMyTerminalBlock> newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
                for (int i = 0; i < batteries.Count; i++)
                {
                    if (!batteries[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(batteries[i]); } if (i > executionCap) { tooManyBatteries = true; if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring batteries out of processing range."); break; } }
                } debugOutput(newlist.Count + " batteries found.");
                batteries = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(solarpanels);
                for (int i = 0; i < solarpanels.Count; i++)
                {
                    if (!solarpanels[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(solarpanels[i]); if (i > executionCap) { tooManySolarpanels = true; if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring solarpanels out of processing range."); break; } } }
                }
                solarpanels = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors);
                for (int i = 0; i < reactors.Count; i++)
                {
                    if (!reactors[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(reactors[i]); if (i > executionCap) { tooManyReactors = true; if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring reactors out of processing range."); break; } } }
                }
                reactors = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyBeacon>(beacons);
                for (int i = 0; i < beacons.Count; i++)
                {
                    if (!beacons[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(beacons[i]); if (i > executionCap) { tooManyBeacons = true; if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring beacons out of processing range."); break; } } }
                }
                beacons = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennas);
                for (int i = 0; i < antennas.Count; i++)
                {
                    if (!antennas[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(antennas[i]); if (i > executionCap) { tooManyAntennas = true; if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring antennas out of processing range."); break; } } }
                }
                antennas = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets);
                for (int i = 0; i < turrets.Count; i++)
                {
                    if (!turrets[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(turrets[i]); if (i > executionCap) { if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring turrets out of processing range."); break; } } }
                }
                turrets = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(welders);
                for (int i = 0; i < welders.Count; i++)
                {
                    if (!welders[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(welders[i]); if (i > executionCap) { if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring welders out of processing range."); break; } } }
                }
                welders = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyInteriorLight>(configLights);
                for (int i = 0; i < configLights.Count; i++)
                {
                    if (!configLights[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(configLights[i]); }
                }
                configLights = newlist; newlist = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(gravityGenerators);
                for (int i = 0; i < gravityGenerators.Count; i++)
                {
                    if (!gravityGenerators[i].DisplayNameText.EndsWith("_unmanaged"))
                    { newlist.Add(gravityGenerators[i]); if (i > executionCap) { if (ignoreDevicesThatAreTooMuch) { debugOutput("Ignoring gravity generators out of processing range."); break; } } }
                }
                gravityGenerators = newlist; newlist = new List<IMyTerminalBlock>();




                wattHourInBatteries = getWattHourInBatteries();
                maxWattHourInBatteries = getMaxWattHourInBatteries();
                solarpanelWatts = solarPower(true);
                reactorWatts = reactorPower(true);
                batteryWatts = batteryPower();
                batteryInputWatts = batteryPowerInput();
                maxPower = reactorWatts + solarpanelWatts;
                powerUsage = reactorPower(false) + solarPower(false) + batteryWatts;
                dischargingBatteries = getDischargingBatteryCount();
                debugOutput("Done.");
            }

            void updateScreenModuleValues()
            {
                //Layout below should be EXACTLY that of the above Variables region! 
                //This is to make sure discrependancies are easily spotted.
                debugOutput("Writing missing blockcounts");
                storeVariable("", "missingBlocks"); //Bugfix
                storeVariable(serializeDictionary(missingBlocks), "missingBlocks");

                debugOutput("Writing power values");
                storeVariable(wattHourInBatteries.ToString(), "wattHourInBatteries");
                storeVariable(maxWattHourInBatteries.ToString(), "maxWattHourInBatteries");

                storeVariable(solarpanelWatts.ToString(), "solarpanelWatts");
                storeVariable(solarPower(true).ToString(), "solarpanelMaxWatts");
                storeVariable(reactorWatts.ToString(), "reactorWatts");
                storeVariable(reactorPower(true).ToString(), "reactorMaxWatts");
                storeVariable(batteryWatts.ToString(), "batteryWatts");
                storeVariable(batteryPower(true).ToString(), "batteryMaxWatts");
                storeVariable(batteryInputWatts.ToString(), "batteryInputWatts");
                storeVariable(batteryPower(true).ToString(), "batteryInputMaxWatts");

                storeVariable(maxPower.ToString(), "maxPower");
                storeVariable(powerUsage.ToString(), "powerUsage");

                debugOutput("Writing block counts");
                storeVariable(reactors.Count.ToString(), "reactorCount");
                storeVariable(solarpanels.Count.ToString(), "solarpanelCount");
                storeVariable(batteries.Count.ToString(), "batteryCount");
                storeVariable(refineries.Count.ToString(), "refineryCount");
                storeVariable(assemblers.Count.ToString(), "assemblerCount");
                storeVariable(antennas.Count.ToString(), "antennaCount");
                storeVariable(beacons.Count.ToString(), "beaconCount");


                debugOutput("Writing meta blockcounts");
                storeVariable(dischargingBatteries.ToString(), "dischargingBatteries");
                storeVariable(wattHourInBatteries.ToString(), "brokenBlockCount");
                storeVariable(offlineBlockCount.ToString(), "offlineBlockCount");
                storeVariable(missingBlockCount.ToString(), "missingBlockCount");
                storeVariable(activeAssemblers.ToString(), "activeAssemblers");
                storeVariable(activeRefineries.ToString(), "activeRefineries");
                storeVariable(currentInternalIteration.ToString(), "currentInternalIteration");

                debugOutput("Writing booleans");
                storeVariable(tooManyBatteries.ToString(), "tooManyBatteries");
                storeVariable(tooManyReactors.ToString(), "tooManyReactors");
                storeVariable(tooManySolarpanels.ToString(), "tooManySolarpanels");
                storeVariable(tooManyAntennas.ToString(), "tooManyAntennas");
                storeVariable(tooManyBeacons.ToString(), "tooManyBeacons");

                debugOutput("Writing strings");
                storeVariable(shipName, "shipName");

                storeVariable(alertState, "alertState");
                storeVariable(powerState, "powerState");
                storeVariable(antennaString, "antennaString");
                storeVariable(beaconString, "beaconString");
                storeVariable(StatusString, "StatusString");
                storeVariable(shortStatus, "shortStatus");
                storeVariable(damageReport, "damageReport");
                storeVariable(offlineReport, "offlineReport");
                //storeVariable(missingReport, "missingReport");
                storeVariable(powerReport, "powerReport");
                storeVariable(shortCommsStatus, "shortCommsStatus");

                //This is the ONLY variable not present in the above variables region.
                storeVariable(DateTime.Now.ToString(), "lastUpdate");


                debugOutput("Done writing values to disk");
            }


            /// <summary>
            /// Updates all LCD panels with _Status suffix with useful information and messages.
            /// This is by far the heaviest function, but turning it off will also cripple the AI's functionality heavily.
            /// </summary>
            public void updateStatusPanels()
            {
                //Fetch and prepare variables for use

                //Format information for displaying
                StringBuilder updateScreenBuilder = new StringBuilder();

                updateScreenBuilder.AppendFormat( //Status screen parts
                    "{0} - {1}\nStatus: {2}{3}{4}",
                    shipName,
                    DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString(),
                        shortStatus,
                        damageReport,
                        StatusString
                    );
                //Show Assembler information if there are assemblers
                if (assemblers.Count > 0)
                {
                    updateScreenBuilder.AppendFormat(
                        "\nAssembly: {0}",
                        string.Format(
                        "{0} assemblers, {1} active.",
                        new string[] {
                            assemblers.Count.ToString(), 
                            activeAssemblers.ToString() 
                            })
                        );
                }

                //Show Ore processing information if there are refineries
                if (refineries.Count > 0)
                {
                    updateScreenBuilder.AppendFormat(
                        "\nOre processing: {0}",
                        string.Format(
                        "{0} refineries, {1} active.",
                        new string[] {
                            refineries.Count.ToString(), 
                            activeRefineries.ToString() 
                            })
                        );
                }

                //Show power information 
                updateScreenBuilder.AppendFormat("\nPower: {0}", powerReport);
                //Show Comms information
                updateScreenBuilder.AppendFormat(
                    "\nComms: {0}{1}{2}",
                    shortCommsStatus,
                    antennaString,
                    beaconString
                    );




                //Apply text to all screens with _status suffix
                for (int i = 0; i < statusPanels.Count; i++)
                {
                    if (statusPanels[i].DisplayNameText.EndsWith("_status"))
                    {
                        IMyTextPanel screen = (IMyTextPanel)statusPanels[i];
                        screen.WritePublicText(updateScreenBuilder.ToString());
                    }
                }


            }

            public void updateIconPanels()
            {
                if (codeBlue)
                {
                    for (int i = 0; i < iconPanels.Count; i++)
                    {
                        debugOutput("Setting texture on " + iconPanels[i].DisplayNameText);
                        iconPanels[i].ShowPublicTextOnScreen();
                        iconPanels[i].ShowTextureOnScreen();
                        iconPanels[i].AddImageToSelection("Construction");

                        //THIS IS BROKEN UNTIL WE CAN REMOVE IMAGES. For fuck sake Keen Dx
                    }
                }
            }




            // Stores a variable in the Variables dictionary.
            // Use flushVariables (preferrably at the end of execution) to actually save.
            // ** Colons and newlines can now be used, as I'm substituting them internally.
            public void storeVariable(string variable, string name)
            {
                if (Variables.ContainsKey(name))
                {
                    Variables[name] = variable;
                }
                else
                {
                    Variables.Add(name, variable);
                }
            }

            // Reads a variable from the Variables dictionary, which will persist using
            // a screen as storage.
            public string getVariable(string name)
            {
                if (Variables.ContainsKey(name))
                {
                    return Variables[name];
                }
                else
                {
                    return null;
                }
            }

            // Write the contents of Variables to a screen for use as storage.
            // If this isn't called, then any changes to variables will not be persistent.
            public void flushVariables(bool append = false)
            {
                StringBuilder result = new StringBuilder();
                if (append)
                {
                    // This may result in duplicates if you aren't careful.
                    if (storagePanel == null)
                    {
                        result.Append(Storage);
                    }
                    else
                    {
                        result.Append(storagePanel.GetPublicText());
                    }
                }
                string[] keys = new string[Variables.Count];
                Variables.Keys.CopyTo(keys, 0);
                string[] values = new string[Variables.Count];
                Variables.Values.CopyTo(values, 0);
                for (int i = 0; i < Variables.Count; i++)
                {
                    KeyValuePair<string, string> entry = new KeyValuePair<string, string>(keys[i], values[i]);
                    // Not sure if first value has no \n, but oh well.
                    string value = entry.Value.Replace(":", "[COLON]").Replace("\n", "[NEWLINE]");
                    result.Append("\n" + entry.Key + ":" + value);
                }

                if (storagePanel == null)
                {
                    Storage = result.ToString();
                }
                else
                {
                    storagePanel.WritePublicText(result.ToString());
                    Storage = result.ToString(); //Keep them in sync in just in case.
                }
            }

            // Load variables from a screen used as storage.
            // If this isn't called, then previously persisted variables will not be visible.
            public void loadVariables()
            {
                string[] source = null;
                if (storagePanel == null)
                {
                    if (Storage == null) { Storage = ""; }
                    if (Storage == null) { throw new Exception("Failed to initialize Storage."); }
                    source = Storage.Split('\n');
                }
                else
                {
                    source = storagePanel.GetPublicText().Split('\n');
                }
                foreach (string pair in source)
                {
                    if (pair == "")
                    {
                        // This is usually true of the first entry.
                        continue;
                    }
                    string[] truePair = pair.Split(':');
                    string name = truePair[0];
                    string value = "";
                    if (truePair.Length > 1)
                    {
                        value = truePair[1].Replace("[NEWLINE]", "\n").Replace("[COLON]", ":");
                    }
                    storeVariable(value, name);
                }
            }


            //This is needed both for the core as well as the screen module.
            void updateStatusString(bool doDamageReport = true)
            {
                debugOutput("Updating information messages..");
                StatusString = ""; //used by status panels
                shortStatus = "All systems functional."; //Override if necessary 
                damageReport = "";
                shortCommsStatus = " OFFLINE"; //Overridden if there's antennas found.

                if (doDamageReport)
                {
                    damageReport = getDamageReport(false);
                }

                if (offlineBlockCount > 0)
                {
                    shortStatus = string.Format("All systems functional. {0} systems offline.", offlineBlockCount);
                }

                if (brokenBlockCount > 0)
                {
                    shortStatus = string.Format("Warning: {0} systems unavailable.", brokenBlockCount);
                }

                if (isProjectorPresent)
                {
                    if (missingBlockCount > 0)
                    {
                        shortStatus = string.Format("Warning: {0} missing blocks detected.", missingBlockCount);
                        StatusString += generateReportFromDictionary(missingBlocks);
                    }
                    if (missingBlockCount > 0 && brokenBlockCount > 0)
                    {
                        shortStatus = "Warning: Ship has sustained damage to various systems.";
                    }
                    if (missingBlockCount > 10 && brokenBlockCount > 1)
                    {
                        shortStatus = "Error: Ship has sustained extensive damage to various systems. Exit strategy advised.";
                    }

                    debugOutput("Adding missing block report");

                }
                else
                {
                    StatusString += "\n  -No projector detected. Missing block detection offline.";
                }



                //Update stuff for the Screens module
                if (isScreenModulePresent)
                {
                    offlineReport = getDamageReport(true);
                }



                // Generate Status messages 
                if (refineries.Count > 0)
                {
                    for (int i = 0; i < refineries.Count; i++)
                    {
                        if (refineries[i].IsProducing)
                        { StatusString += string.Format("\n  -{0} is processing ore.", refineries[i].DisplayNameText); }
                    }
                }
                if (assemblers.Count > 0)
                {
                    for (int i = 0; i < assemblers.Count; i++)
                    {
                        if (assemblers[i].IsProducing)
                        { StatusString += string.Format("\n  -{0} is assembling parts.", assemblers[i].DisplayNameText); }
                    }
                }
                if (maxPower < powerUsage) { StatusString += "\n  -Warning: Draining batteries."; }
                if ((maxPower > powerUsage) && (wattHourInBatteries < maxWattHourInBatteries)) { StatusString += "\n  -Batteries charging."; }
                if (wattHourInBatteries == maxWattHourInBatteries) { StatusString += "\n  -Batteries fully charged."; }
                else if (wattHourInBatteries > (maxWattHourInBatteries) / 4) { StatusString += "\n  -Batteries OK."; }
                else if (wattHourInBatteries < (maxWattHourInBatteries / 4)) { StatusString += "\n  -Batteries low."; shortStatus = "Low power."; }
                bool tooManyMessage = false;
                if (tooManyAntennas) { StatusString += "\n >Too many antennas to handle."; tooManyMessage = true; }
                if (tooManyBatteries) { StatusString += "\n >Too many batteries to handle."; tooManyMessage = true; }
                if (tooManyBeacons) { StatusString += "\n >Too many beacons to handle."; tooManyMessage = true; }
                if (tooManyReactors) { StatusString += "\n >Too many reactors to handle."; tooManyMessage = true; }
                if (tooManySolarpanels) { StatusString += "\n >Too many solar panels to handle."; tooManyMessage = true; }
                if (tooManyMessage) { StatusString += "\n >Remove devices or alter execution cap at own risk. (see top of script)"; }
                if (!ignoreDevicesThatAreTooMuch) { StatusString += "\n >Execution cap disabled."; }

                if (batteryInputWatts > powerUsage)
                {
                    StatusString += "\n >Unknown power source detected."; //What the hell are we charging the batteries with?
                }

                if (gravityGenerators.Count > 0)
                {
                    StatusString += "\nCurrent gravity: " + Math.Abs(((IMyGravityGenerator)gravityGenerators[0]).Gravity).ToString() + "G";
                }

                // Generate Power Overview
                powerReport = string.Format(
                    " {0}/{1} currently used.",
                    new string[] { 
                        getNormalizedPowerString(powerUsage, false) ,
                        getNormalizedPowerString(maxPower, false)
                    }); ;
                if (batteries.Count > 0)
                {
                    powerReport += string.Format(
                        "\n *Batteries: {0}/{1} in {2} batteries ({3} in use)",
                        new string[] {
                            getNormalizedPowerString(wattHourInBatteries,true),
                            getNormalizedPowerString(maxWattHourInBatteries,true),
                            batteries.Count.ToString(),
                            dischargingBatteries.ToString()
                        });
                }
                if (solarpanels.Count > 0)
                {
                    powerReport += string.Format(
                       "\n *Solar: {2} panels | {0}/{1}",
                       new string[] {
                            getNormalizedPowerString(solarPower(false),false),
                            getNormalizedPowerString(solarpanelWatts,false), 
                            solarpanels.Count.ToString()
                        });
                }
                if (reactors.Count > 0)
                {
                    powerReport += string.Format(
                        "\n *Reactors: {2} reactors | {0}/{1}",
                        new string[] {
                            getNormalizedPowerString(reactorPower(false),false),
                            getNormalizedPowerString(reactorWatts,false),
                            reactors.Count.ToString()
                        });
                }
                debugOutput("status: " + StatusString);

                //Check comms status    
                antennaString = "\n *No antennas found.";
                beaconString = "\n *No beacons found.";
                string tempshortCommsStatus = " OFFLINE.";
                float tempmaxAntennaRange = 0;
                if (antennas.Count > 0)
                {
                    string antString = "";
                    int workingAnts = 0;
                    for (int i = 0; i < antennas.Count; i++)
                    {
                        var ant = (IMyRadioAntenna)antennas[i];
                        antString += "\n *" + ant.DisplayNameText + " (" + ant.Radius + "m)";
                        if (!ant.IsFunctional) { antString += " - Damaged"; }
                        if (!ant.IsWorking) { antString += " - Offline"; }
                        if (isHealthy(ant))
                        {
                            workingAnts++;
                            if (ant.Radius > tempmaxAntennaRange) { tempmaxAntennaRange = ant.Radius; }
                            tempshortCommsStatus = string.Format(" Comms Online ({0}m range, {1}/{2} antennas in use)", tempmaxAntennaRange, workingAnts, antennas.Count);
                            debugOutput("Healthy antenna found. " + ant.DisplayNameText);
                        }
                    }
                    antennaString = string.Format(
                        "{1}",
                        new string[] {
                            antennas.Count.ToString(),
                            antString
                        });
                    shortCommsStatus = tempshortCommsStatus;
                }

                if (shortCommsStatus.Contains("OFFLINE"))
                {
                    StatusString += "\n >Communications offline.";
                    shortStatus = "Core systems functional. Comms offline.";
                }


                if (beacons.Count > 0)
                {
                    string beacString = "";
                    for (int i = 0; i < beacons.Count; i++)
                    {
                        var beac = (IMyBeacon)beacons[i];
                        beacString += "\n *" + beac.DisplayNameText + " (Beacon, " + beac.Radius + "m range)";
                        if (!beac.IsFunctional) { beacString += " - Damaged"; }
                        if (!beac.IsWorking) { beacString += " - Offline"; }
                    }
                    beaconString = string.Format(
                        "{1}",
                        new string[] {
                            beacons.Count.ToString(),
                            beacString
                        });
                }
                debugOutput("Done.");
            }


            /// <summary>
            /// Original function written by Stiggan/Malekeh, took from the Battery management script.
            /// Splits down the DetailedInfo field of a block and retrieves the value corresponding to the given name of a field.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="name"></param>
            /// <returns></returns>
            public string getDetailedInfoValue(IMyTerminalBlock block, string name)
            {
                string value = "";
                string[] lines = block.DetailedInfo.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] line = lines[i].Split(':');
                    if (line[0].Equals(name))
                    {
                        value = line[1].Trim();
                        break;
                    }
                }
                return value;
            }

            public string generateReportFromDictionary(Dictionary<string, int> dict)
            {
                string toReturn = "";
                string[] keys = new string[dict.Keys.Count];
                int[] values = new int[dict.Values.Count];
                dict.Keys.CopyTo(keys, 0);
                dict.Values.CopyTo(values, 0);
                for (int i = 0; i < dict.Count; i++)
                {
                    toReturn += string.Format("\n  >Missing {0}: {1}", keys[i], values[i]);
                }
                return toReturn;
            }

            public bool detailExist(IMyTerminalBlock block, string name)
            {
                return !String.IsNullOrEmpty(getDetailedInfoValue(block, name));
            }

            /// <summary>
            /// Internal function to convert a string like "2.00 MWh" or "536 W" to a normalized double value in watts.
            /// </summary>
            /// <param name="text"></param>
            /// <param name="hour">Whether or not you're retrieving a battery value, e.g 203.12 KWh. </param>
            /// <returns>A normalized double value in watt(hour)s</returns>
            public double getPowerAsDouble(string text, bool hour = false)
            {
                string h = "";
                if (hour) { h = "h"; }
                double totalPowerWatts = 0;
                if (String.IsNullOrWhiteSpace(text))
                {
                    return 0;
                }
                var splittedStoredPower = text.ToLower().Split(' ');
                if (splittedStoredPower[1] == "mw" + h)
                {
                    totalPowerWatts += double.Parse(splittedStoredPower[0]) * 1000000;
                }
                else if (splittedStoredPower[1] == "gw" + h)
                {
                    totalPowerWatts += double.Parse(splittedStoredPower[0]) * 1000000000;
                }
                else if (splittedStoredPower[1] == "kw" + h)
                {
                    totalPowerWatts += double.Parse(splittedStoredPower[0]) * 1000;

                }
                else if (splittedStoredPower[1] == "w" + h)
                {
                    totalPowerWatts += double.Parse(splittedStoredPower[0]);

                }
                return totalPowerWatts;
            }


            /// <summary>
            /// Gets the current power drawn from the batteries.
            /// </summary>
            /// <returns></returns>
            public double batteryPower(bool max = false)
            {
                double output = 0;
                for (int i = 0; i < batteries.Count; i++)
                {
                    if (isHealthy(batteries[i]) && ((IMyFunctionalBlock)batteries[i]).Enabled && !max)
                    {
                        output += getPowerAsDouble(getDetailedInfoValue(batteries[i], "Current Output"));
                        output -= getPowerAsDouble(getDetailedInfoValue(batteries[i], "Current Input"));
                    }
                    else if (max)
                    {
                        output += getPowerAsDouble(getDetailedInfoValue(batteries[i], "Max Output"));
                    }
                }
                return output;
            }

            public double batteryPowerInput(bool max = false)
            {
                double output = 0;
                if (!max)
                {
                    for (int i = 0; i < batteries.Count; i++)
                    {
                        output += getPowerAsDouble(getDetailedInfoValue(batteries[i], "Current Input"));
                    }
                }
                else
                {
                    for (int i = 0; i < batteries.Count; i++)
                    {
                        output += getPowerAsDouble(getDetailedInfoValue(batteries[i], "Max Required Input"));
                    }
                }
                return output;
            }

            public bool healthyReactor(IMyTerminalBlock b)
            {
                // Abusing unfinished reactors not displaying Current Output field.
                return isHealthy(b) && detailExist(b, "Current Output");
            }

            /// <summary>
            /// Prints a line to the debugpanel, if there is one (doesn't throw exceptions)
            /// </summary>
            /// <param name="output"></param>
            public void debugOutput(string output)
            {
                if (debugPanel != null)
                {
                    debugPanel.WritePublicText(output + "\n", true);
                    if (debugPanel.GetPublicText().Length > debugMaxCharacters)
                    {
                        debugPanel.WritePublicText("WARNING: Debug log truncated due to size!\nYou can change the max size in the constants.");
                    }
                }

            }

            public double getPower(IMyTerminalBlock block, bool max)
            {
                if (max)
                {
                    if (!block.IsBeingHacked)
                    {
                        return getPowerAsDouble(getDetailedInfoValue(block, "Max Output"));
                    }
                }
                else
                {
                    return getPowerAsDouble(getDetailedInfoValue(block, "Current Output"));
                }
                return 0;
            }

            public int getDischargingBatteryCount()
            {
                int dischargingBats = 0;
                for (int i = 0; i < batteries.Count; i++)
                {
                    var bat = (IMyBatteryBlock)batteries[i];
                    if (!isRecharging(bat))
                    {
                        dischargingBats++;
                    }
                }
                return dischargingBats;
            }

            public double reactorPower(bool max)
            {
                double power = 0;
                for (int i = 0; i < reactors.Count; i++)
                {
                    if (healthyReactor(reactors[i]) && ((IMyFunctionalBlock)reactors[i]).Enabled)
                    {
                        power += getPower(reactors[i], max);
                    }
                }
                return power;
            }

            public double solarPower(bool max)
            {
                double power = 0;
                for (int i = 0; i < solarpanels.Count; i++)
                {
                    if (isHealthy(solarpanels[i]))
                    {
                        power += getPower(solarpanels[i], max);
                    }
                }
                return power;
            }

            public bool isRecharging(IMyTerminalBlock block)
            {
                return detailExist(block, "Fully recharged in");
            }

            public void chargeAllBatteries()
            {
                debugOutput("Setting all but one battery to charge.");
                dischargingBatteries = 0;
                for (int i = 0; i < batteries.Count; i++)
                {
                    if (dischargingBatteries > 0) //Make sure there's at least one battery always on to prevent blackouts.
                    {
                        batteries[i].GetActionWithName("Recharge").Apply(batteries[i]);
                    }
                    else { dischargingBatteries++; }
                }
            }

            public void toggleRandomBattery()
            {
                Random rnd = new Random();
                int i = rnd.Next(0, batteries.Count);
                if (i < batteries.Count)
                {
                    if (!isRecharging(batteries[i]) && dischargingBatteries < 2) //If we're requested to toggle the only currently online battery
                    {
                        if (batteries.Count < 2) //And there's no other batteries available
                        {
                            debugOutput("There are no secondary batteries available. This means there's no protection against blackouts. Toggling anyway.");
                            batteries[i].GetActionWithName("Recharge").Apply(batteries[i]);
                        }
                        else
                        {
                            //First enable a different battery to make sure there are no blackouts.
                            int tryingBat = 0;
                            bool success = false;
                            int retries = 0;
                            while (!success)
                            {
                                tryingBat = rnd.Next(0, batteries.Count);
                                if (isRecharging(batteries[tryingBat]) && tryingBat != i)
                                {
                                    debugOutput("Turning on secondary battery to prevent blackouts");
                                    batteries[tryingBat].GetActionWithName("Recharge").Apply(batteries[tryingBat]);
                                    success = true;
                                    //We can't check if this has worked within the same cycle, the status updates too slow.
                                    //So we can't do anything else here.
                                }
                                retries++;
                                if (retries > 10) { debugOutput("FIXME: Loop detected"); break; } //Against infinity and beyond..
                            }
                        }
                    }
                    else
                    {
                        debugOutput(string.Format("Toggling random {0}", batteries[i].DisplayNameText));
                        batteries[i].GetActionWithName("Recharge").Apply(batteries[i]);

                    }
                }
            }

            /// <summary>
            /// Get normalized power string, in watts, kilowatts, megawatts, or gigawatts.
            /// </summary>
            /// <param name="watts">Raw value in watts</param>
            /// <param name="hour">Whether or not to add an h to the end of the string</param>
            /// <returns></returns>
            public string getNormalizedPowerString(double watts, bool hour)
            {
                string h = "";
                if (hour) { h = "h"; }
                if (watts > 1000000000)
                {
                    return string.Format("{0}Gw{1}", watts / 1000000000, h);
                }
                else if (watts > 1000000)
                {
                    return string.Format("{0}Mw{1}", watts / 1000000, h);
                }
                else if (watts > 1000)
                {
                    return string.Format("{0}Kw{1}", watts / 1000, h);
                }
                else
                {
                    return string.Format("{0}w{1}", watts, h);
                }
            }

            public string getDamageReport(bool listOfflineBlocks)
            {
                StringBuilder report = new StringBuilder();
                brokenBlockCount = 0;
                offlineBlockCount = 0;
                for (int i = 0; i < GridTerminalSystem.Blocks.Count; i++)
                {
                    if (!GridTerminalSystem.Blocks[i].IsFunctional)
                    {
                        report.AppendFormat("\n  >{0} is not functional.", GridTerminalSystem.Blocks[i].DisplayNameText);
                        brokenBlockCount++;
                        codeBlue = true; //Alert for damaged blocks
                        debugOutput("set codeOrange to " + codeOrange.ToString());
                    }
                    if (!GridTerminalSystem.Blocks[i].IsWorking && GridTerminalSystem.Blocks[i].IsFunctional)
                    {
                        if (listOfflineBlocks)
                        {
                            report.AppendFormat("\n  -{0} is offline.", GridTerminalSystem.Blocks[i].DisplayNameText);
                        }
                        offlineBlockCount++;
                    }
                }
                if (brokenBlockCount < 1 && codeBlue)
                {
                    codeBlue = false; //Turn off alarm again
                }

                return report.ToString();
            }



            /// <summary>
            /// As proud as I am of this function I really hope it becomes legacy fast, KeenSWH. :L
            /// </summary>
            /// <returns>Amount of KWh in all the batteries</returns>
            public double getWattHourInBatteries()
            {
                var screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("debugpanel");

                if (batteries.Count < 1) { return 0; }
                //Note: Function does not support ranges in terawatts. Add support if necessary. 
                double totalPowerWatts = 0;
                for (int y = 0; y < batteries.Count; y++)
                {
                    var storedPower = getDetailedInfoValue(batteries[y], "Stored power");
                    //screen.WritePublicText(storedPower.Replace("kWh", "").Trim(), false); //Debugging line. 
                    totalPowerWatts += getPowerAsDouble(storedPower, true);
                }


                return totalPowerWatts;
            }

            /// <summary>
            /// Gets the maximum amount of energy stored in all batteries (i.e full capacity)
            /// </summary>
            /// <returns></returns>
            public double getMaxWattHourInBatteries()
            {
                var screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("debugpanel");

                if (batteries.Count < 1) { return 0; }
                double totalPowerWatts = 0;
                for (int y = 0; y < batteries.Count; y++)
                {
                    var storedPower = getDetailedInfoValue(batteries[y], "Max Stored Power");
                    //screen.WritePublicText(storedPower.Replace("kWh", "").Trim(), false); //Debugging line. 
                    totalPowerWatts += getPowerAsDouble(storedPower, true);
                }


                return totalPowerWatts;
            }


            public List<IMyRefinery> getRefineries()
            {
                List<IMyTerminalBlock> a = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRefinery>(a);
                List<IMyRefinery> refineries = new List<IMyRefinery>();
                for (int i = 0; i < a.Count; i++)
                {
                    refineries.Add((IMyRefinery)a[i]);
                }
                return refineries;
            }

            public List<IMyAssembler> getAssemblers()
            {
                List<IMyTerminalBlock> a = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyAssembler>(a);
                List<IMyAssembler> assemblers = new List<IMyAssembler>();
                for (int i = 0; i < a.Count; i++)
                {
                    assemblers.Add((IMyAssembler)a[i]);
                }
                return assemblers;
            }

            public string serializeDictionary(Dictionary<string, int> dict)
            {
                string output = "";
                string[] keys = new string[dict.Keys.Count];
                int[] values = new int[dict.Values.Count];
                dict.Keys.CopyTo(keys, 0);
                dict.Values.CopyTo(values, 0);
                for (int i = 0; i < dict.Count; i++)
                {
                    output += "\n" + keys[i] + ":" + values[i].ToString();
                }
                return output;
            }

            public Dictionary<string, int> deserializeDictionary(string input)
            {
                Dictionary<string, int> returndict = new Dictionary<string, int>();
                string[] inputLines = input.Split('\n');
                for (int i = 0; i < inputLines.Length - 1; i++)
                {
                    if (inputLines[i].Length < 1) { continue; }
                    string[] inputLineParts = inputLines[i].Split(':');
                    if (inputLineParts.Length > 0)
                    {
                        returndict.Add(inputLineParts[0], int.Parse(inputLineParts[1]));
                    }
                }

                return returndict;
            }





        }

        #endregion

        #endregion

    }

}