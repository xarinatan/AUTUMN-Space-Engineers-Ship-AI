using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripting
{
    class CodeEditorEmulator3
    {
        IMyGridTerminalSystem GridTerminalSystem = null;

        #region CodeEditor


        /*
         * ShipAI Interaction module, written by Anthropy March 2015. 
         * Requires ShipAI Core module to function. See Workshop page for more info.
         * 
         * The Interaction Module is a way for the AI to present interactive content.
         * This allows the user to make decisions, change configuration items such as power thresholds,
         * and read queued up messages in an inbox instead of relying on the status screen. 
         * 
        */

        void Main()
        {
            updateLCDPanels();
            updateCachedValues();



            debugOutput("Iteration done.");
        }

        string widescreenTemplate = @"                OPTION A                                                                          OPTION B







OPTION C                                                                                                                OPTION D";

        #region Variables
        //Cached variables. Talking with ai_storage is expensive, so keep everything in memory when possible.
        //All but the textpanel lists below are fetched from the ai_storage panel when updateCachedValues() is called.

        //Add extra customScreenX lists below here
        List<IMyTextPanel> customScreens1 = new List<IMyTextPanel>();

        List<IMyTextPanel> updatePanels = new List<IMyTextPanel>();
        List<IMyTextPanel> iconPanels = new List<IMyTextPanel>();
        List<IMyTextPanel> battlePanels = new List<IMyTextPanel>();
        IMyTextPanel debugPanel;
        IMyTextPanel storagePanel;

        Dictionary<string, int> missingBlocks = new Dictionary<string, int>();

        public double wattHourInBatteries;
        public double maxWattHourInBatteries ;

        public double solarpanelWatts ;
        public double solarpanelMaxWatts;
        public double reactorWatts ;
        public double reactorMaxWatts;
        public double batteryWatts ;
        public double batteryMaxWatts;
        public double batteryInputWatts ;
        public double batteryInputMaxWatts;

        public double maxPower ;   
        public double powerUsage ;
        
        public int reactorCount;
        public int solarpanelCount;
        public int batteryCount;
        public int refineryCount;
        public int assemblerCount;
        public int antennaCount;
        public int beaconCount;

        public int dischargingBatteries ;
        public int brokenBlockCount;
        public int offlineBlockCount;
        public int activeAssemblers ;
        public int activeRefineries;

        public bool tooManyBatteries ;
        public bool tooManyReactors ;
        public bool tooManySolarpanels ;
        public bool tooManyAntennas ;
        public bool tooManyBeacons ;

        public string shipName;

        public DateTime lastCoreUpdate = new DateTime();

        /// <summary>
        /// Returns the given string, with all {variable} names replaced with actual values.
        /// Yes, this is a template parser, 
        /// in an ingame script engine, in a game written in a 4th generation language. [Inception.wav]
        /// </summary>
        /// <param name="templateString"></param>
        /// <returns></returns>
        string formatScreenOutput(string templateString)
        {
            return templateString
                .Replace("{wattHourInBatteries}", getNormalizedPowerString(wattHourInBatteries, true))
                .Replace("{maxWattHourInBatteries}", getNormalizedPowerString(maxWattHourInBatteries, true))

                .Replace("{solarpanelWatts}", getNormalizedPowerString(solarpanelWatts, false))
                .Replace("{solarpanelMaxWatts}", getNormalizedPowerString(solarpanelMaxWatts, false))
                .Replace("{reactorWatts}", getNormalizedPowerString(reactorWatts, false))
                .Replace("{reactorMaxWatts}", getNormalizedPowerString(reactorMaxWatts, false))
                .Replace("{batteryWatts}", getNormalizedPowerString(batteryWatts, false))
                .Replace("{batteryMaxWatts}", getNormalizedPowerString(batteryMaxWatts, false))
                .Replace("{batteryInputWatts}", getNormalizedPowerString(batteryInputWatts, false))
                .Replace("{batteryInputMaxWatts}", getNormalizedPowerString(batteryInputMaxWatts, false))

                .Replace("{maxPower}", getNormalizedPowerString(maxPower, false))
                .Replace("{powerUsage}", getNormalizedPowerString(powerUsage, false))

                .Replace("{reactorCount}", reactorCount.ToString())
                .Replace("{solarpanelCount}", solarpanelCount.ToString())
                .Replace("{batteryCount}", batteryCount.ToString())
                .Replace("{refineryCount}", refineryCount.ToString())
                .Replace("{assemblerCount}", assemblerCount.ToString())
                .Replace("{antennaCount}", antennaCount.ToString())
                .Replace("{beaconCount}", beaconCount.ToString())


                .Replace("{dischargingBatteries}", dischargingBatteries.ToString())
                .Replace("{brokenBlockCount}", brokenBlockCount.ToString())
                .Replace("{offlineBlockCount}", offlineBlockCount.ToString())
                .Replace("{activeAssemblers}", activeAssemblers.ToString())
                .Replace("{activeRefineries}", activeRefineries.ToString())


                .Replace("{tooManyBatteries}", tooManyBatteries.ToString())
                .Replace("{tooManyReactors}", tooManyReactors.ToString())
                .Replace("{tooManySolarpanels}", tooManySolarpanels.ToString())
                .Replace("{tooManyAntennas}", tooManyAntennas.ToString())
                .Replace("{tooManyBeacons}", tooManyBeacons.ToString())

                .Replace("{shipName}", shipName)
                .Replace("{shortCommsStatus}", getVariable("shortCommsStatus"))

                //Below are calls to methods and properties, which means they don't exist in the variable list on the top of the script.
                .Replace("{alertState}", getVariable("alertState"))
                .Replace("{powerState}", getVariable("powerState"))
                .Replace("{antennaString}", getVariable("antennaString"))
                .Replace("{beaconString}", getVariable("beaconString"))

                .Replace("{damageReport}", getVariable("damageReport"))
                .Replace("{offlineReport}", getVariable("offlineReport"))
                .Replace("{missingReport}", getVariable("missingReport"))



                ;
        }

        #endregion

        #region masked variables

        public bool codeBlue
        {
            get { return getVariable("alertState").Equals("blue"); }
            set
            {
                if (value)
                {
                    storeVariable("alertState", "blue");
                }
                else
                {
                    storeVariable("alertState", "none");
                }
            }
        }
        public bool codeOrange
        {
            get { return getVariable("alertState").Equals("orange"); }
            set
            {
                if (value)
                {
                    storeVariable("alertState", "orange");
                }
                else
                {
                    storeVariable("alertState", "none");
                }
            }
        }
        public bool codeRed
        {
            //the easter egg here has been removed due to potential issues with the screen module template engine.
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

        #endregion

        #region internal functions

        //String formatting functions are listed below, you can use these if you extend your const string to a string method.
        //See the (to be created) pastebin wiki for more info.

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


        //Internal functions used for updating variables and what not. 
        //Don't call them unless you know what you're doing.

        void updateLCDPanels()
        {
            debugPanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("debugpanel_gpu");
            storagePanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("ai_storage");
            if (storagePanel == null)
            {
                debugOutput("ERROR: NO STORAGE FOUND. Throwing exception..");
                debugOutput("Please create an LCD panel named 'ai_storage'.");
                debugOutput("Storage is required for proper functioning of the AI.");
                debugOutput("Without storage the Core won't even run.\nDid you install the screen module before the AI Core?");
                throw new Exception("No storage found!\nNote that this module does not work without the AI Core!\nPlease install the AI Core for more info.");
            }
            storeVariable("Red one standing by.", "screensModuleStatus"); //Let the AI Core know we're ready to rock
            //Reset debugpanel
            if (debugPanel != null) { debugPanel.WritePublicText(""); }

            debugOutput("Updating LCD Panels..");
            List<IMyTerminalBlock> templist = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(templist);

            updatePanels.Clear();
            iconPanels.Clear();

            //Add your customScreens__ list here, or it will eventually overflow.
            customScreens1.Clear();
            
            for (int i = 0; i < templist.Count; i++)
            {
                    if (templist[i].DisplayNameText.EndsWith("_status"))
                    {
                        updatePanels.Add((IMyTextPanel)templist[i]);
                    }
                    else if (templist[i].DisplayNameText.EndsWith("_icons"))
                    {
                        iconPanels.Add((IMyTextPanel)templist[i]);
                    }
                    //Add extra customscreens here, or they won't be added to the list.
                    //Simply copypaste the else if(){} below and replace 1 by your custom name.
                    else if (templist[i].DisplayNameText.EndsWith("_customscreen1"))
                    {
                        customScreens1.Add((IMyTextPanel)templist[i]);
                    }
            }
        }

        /// <summary>
        /// Fetches last values from the ai_storage. 
        /// Expensive call, don't use more than necessary.
        /// Needed before any cached variables are initialized though,
        /// make sure to call BEFORE using any or you'll get NullReferenceExceptions.
        /// 
        /// </summary>
        void updateCachedValues()
        {

            debugOutput("Updating cached values from ai_storage..");
            if (getVariable("alertState") == null) //This variable should always be present.
            {
                debugOutput("Error: value retrieval test failed.\nThrowing helpful error message instead of nullreference garbage.");
                throw new Exception("Unable to load required values from ai_storage.\nIs the AI core running correctly?"); 
            }
            if (getVariable("wattHourInBatteries") == null) //This variable should always be present if the AI Core is saving Screens Module data correctly
            {
                debugOutput("Error: still waiting for Core to update ai_storage with useful values..");
                

                return;
            }
            //NOTE: Try{}catch(){} do not work! I left this here to remind me of the mistakes I made :'( 
            try
            {
                // The layout here should be exactly that of the Variables region, 
                // so you can confirm if there's anything missing in case of bugs.
                missingBlocks = deserializeDictionary(getVariable("missingBlocks"));

                double.TryParse((getVariable("wattHourInBatteries")), out wattHourInBatteries);
                maxWattHourInBatteries = double.Parse(getVariable("maxWattHourInBatteries"));

                solarpanelWatts = double.Parse(getVariable("solarpanelWatts"));
                solarpanelMaxWatts = double.Parse(getVariable("solarpanelMaxWatts"));
                reactorWatts = double.Parse(getVariable("reactorWatts"));
                reactorMaxWatts = double.Parse(getVariable("reactorMaxWatts"));
                batteryWatts = double.Parse(getVariable("batteryWatts"));
                batteryMaxWatts = double.Parse(getVariable("batteryMaxWatts"));
                batteryInputWatts = double.Parse(getVariable("batteryInputWatts"));
                batteryInputMaxWatts = double.Parse(getVariable("batteryInputMaxWatts"));

                maxPower = double.Parse(getVariable("maxPower"));
                powerUsage = double.Parse(getVariable("powerUsage"));

                reactorCount = int.Parse(getVariable("reactorCount"));
                solarpanelCount = int.Parse(getVariable("solarpanelCount"));
                batteryCount = int.Parse(getVariable("batteryCount"));
                refineryCount = int.Parse(getVariable("refineryCount"));
                assemblerCount = int.Parse(getVariable("assemblerCount"));
                antennaCount = int.Parse(getVariable("antennaCount"));
                beaconCount = int.Parse(getVariable("beaconCount"));

                dischargingBatteries = int.Parse(getVariable("dischargingBatteries"));
                brokenBlockCount = int.Parse(getVariable("brokenBlockCount"));
                offlineBlockCount = int.Parse(getVariable("offlineBlockCount"));
                activeAssemblers = int.Parse(getVariable("activeAssemblers"));
                activeRefineries = int.Parse(getVariable("activeRefineries"));


                tooManyBatteries = bool.Parse(getVariable("tooManyBatteries"));
                tooManyReactors = bool.Parse(getVariable("tooManyReactors"));
                tooManySolarpanels = bool.Parse(getVariable("tooManySolarpanels"));
                tooManyAntennas = bool.Parse(getVariable("tooManyAntennas"));
                tooManyBeacons = bool.Parse(getVariable("tooManyBeacons"));

                shipName = getVariable("shipName");
                //antennaString and beaconString are routed through a property due to newline conversion

                lastCoreUpdate = DateTime.Parse(getVariable("lastUpdate"));
                debugOutput("Laste core update: " + lastCoreUpdate.ToString());

                storeVariable("Red one standing by.", "screenModuleStatus"); //Let the AI Core know we're ready to rock
            }
            catch (Exception)
            {
                throw new Exception("Unable to load required values from ai_storage.\nIs the AI core running correctly?");
            }
            debugOutput("Done updating cached values.");
        }



        /// <summary>
        /// Prints a line to the debugpanel, if there is one (doesn't throw exceptions)
        /// </summary>
        /// <param name="output"></param>
        void debugOutput(string output)
        {
            if (debugPanel != null)
            {
                debugPanel.WritePublicText(output + "\n", true);
                if (debugPanel.GetPublicText().Length > 1000)
                {
                    debugPanel.WritePublicText("WARNING: Debug log truncated due to size!\n");
                }
            }
            
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

        //Stores a variable using a LCD panel named 'ai_storage'. 
        //Colons and newlines can now be used, as I'm substituting them internally.
        //Don't use [COLON] or [NEWLINE] though, they will be replaced by getVariable()
        void storeVariable(string variable, string name)
        {
            variable = variable.Replace(":", "[COLON]").Replace("\n", "[NEWLINE]");
            List<string> c = new List<string>();
            string b = storagePanel.GetPublicText();
            string oldvar;
            if (b.Contains(name + ":"))
            {
                oldvar = getVariable(name);
                b = b.Replace(name + ":" + oldvar, name + ":" + variable);
            }
            else
            {
                b += "\n" + name + ":" + variable;
            }

            storagePanel.WritePublicText(b);
            //This call is too often used to always log. Turn on if needed.
            //debugOutput(string.Format("storeVariable(): {0} '{1}'", name, variable));
        }

        //Retrieves a variables from the ai_storage LCD panel.
        string getVariable(string name)
        {
            List<string> b = new List<string>();
            b.AddRange(storagePanel.GetPublicText().Split('\n'));
            string toReturn = null;
            for (int i = 0; i < b.Count; i++)
            {
                if (b[i].StartsWith(name))
                {
                    toReturn = b[i].Split(':')[1].Replace("[NEWLINE]", "\n").Replace("[COLON]", ":");
                    break;
                }
            }
            if (toReturn == null)
            {
                debugOutput(string.Format("No variable '{0}' found.", name));
            }
            else
            {
                //This call is too heavy to always log on the Screen Module
               // debugOutput(string.Format("getVariable(): {0} '{1}'", name, toReturn));
            }

            return toReturn;
        }


        #endregion

     
        


        #endregion
    }
}