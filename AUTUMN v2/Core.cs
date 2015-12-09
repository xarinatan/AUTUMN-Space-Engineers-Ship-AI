using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripting
{
    class CodeEditorEmulator1
    {
        public IMyGridTerminalSystem GridTerminalSystem;
        string Storage;

        public void Echo(string text) { }
        public IMyProgrammableBlock Me;
        public TimeSpan ElapsedTime;
        //END OF TEMPLATE


       // public delegate void myEchodel(string text);
       // public static myEchodel myEcho;


                StatusReporter sReporter;

                void Main(string argument)
                {
                    //myEcho = Echo;  
                    if (sReporter == null) { sReporter = new StatusReporter(GridTerminalSystem); }
                    List<IMyTextPanel> textpanels = new List<IMyTextPanel>();
                    List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tempterminalblocklist);
                    textpanels.AddRange(tempterminalblocklist.ConvertAll<IMyTextPanel>(new Converter<IMyTerminalBlock, IMyTextPanel>(x => (IMyTextPanel)x)));
                    if (textpanels == null) { Echo("No Textpanel found"); return; }
                    int screensUsed = 0;
                    string report = sReporter.buildStatusString();
                    foreach (IMyTextPanel textpanel in textpanels)
                    {
                        if (textpanel.DisplayNameText.EndsWith("_status"))
                        {
                            textpanel.WritePublicText(report);
                            screensUsed++;
                        }
                    }
                    Echo("Screens used: " + screensUsed);
                    Echo(report);
                }

                //public List<T> getBlocksFromGTS<T>()
                //{
                //    List<T> toReturn = new List<T>();
                //    List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                //    GridTerminalSystem.GetBlocksOfType<T>(tempterminalblocklist);
                //    toReturn.AddRange(tempterminalblocklist.ConvertAll<T>(new Converter<IMyTerminalBlock, T>(x => (T)x)));
                //    return toReturn;
                //} 

                public class StatusReporter
                {
                    double lastBatPercentage;
                    float lastOxyPressLevel, lastOxyTankLevel, lastSolWattage;
                    IMyGridTerminalSystem GridTerminalSystem;
                        

                    public StatusReporter(IMyGridTerminalSystem _gts)
                    {
                        GridTerminalSystem = _gts;
                        Reports = new List<StatusReportItem>();
                
                    }
                    public List<StatusReportItem> Reports;

                    public string buildStatusString()
                    {
                        StringBuilder sbuilder = new StringBuilder();
                        sbuilder.AppendLine("A.U.T.U.M.N v2.0 ALPHA - " + DateTime.Now);

                        if (Reports.Count > 4000) { Reports.RemoveRange(3500, 500); }
                

                        float batpercentage = getPercentageFromArrayCurMax(getBatteryWattHours());
                        double[] batPowerOutput = getBatteryWattOutput();
                        sbuilder.Append(string.Format("BAT: {0}% {1}/{2}", new object[] { batpercentage, getNormalizedPowerString(batPowerOutput[0]), getNormalizedPowerString(batPowerOutput[1]) }));
                        if (lastBatPercentage < batpercentage) { sbuilder.Append(" [^]"); }
                        if (lastBatPercentage > batpercentage) { sbuilder.Append(" [v]"); }
                        if (batpercentage < 10.0f) { Reports.Add(new StatusReportItem() { Text = "Batteries Low", Details = "Batteries are at " + batpercentage.ToString() + "%", Severity = StatusReportItem.SeverityLevel.Warning, TimeStamp = DateTime.Now }); }
                        lastBatPercentage = batpercentage;
                        sbuilder.AppendLine();

                        float[] oxiPressureFactor = getOxygenPressureFactor();
                        sbuilder.Append(string.Format("O² PRSS: {0}/{1} vents", oxiPressureFactor[0], oxiPressureFactor[1]));
                        if (lastOxyPressLevel < oxiPressureFactor[0]) { sbuilder.Append(" [^]"); }
                        if (lastOxyPressLevel > oxiPressureFactor[0]) { sbuilder.Append(" [v]"); }
                        if (oxiPressureFactor[0] < oxiPressureFactor[1]/5) { Reports.Add(new StatusReportItem() { Text = "Oxygen Pressure Low", Details = "Oxygen pressure is at " + oxiPressureFactor[0].ToString() + "%", Severity = StatusReportItem.SeverityLevel.Warning, TimeStamp = DateTime.Now }); }
                        lastOxyPressLevel = oxiPressureFactor[0];
                        sbuilder.AppendLine();

                        float oxiTankPressurePercent = getPercentageFromArrayCurMax(getOxygenTankPressureFactor());
                        sbuilder.Append(string.Format("O² STOR: {0}%", oxiTankPressurePercent));
                        if (lastOxyTankLevel < oxiTankPressurePercent) { sbuilder.Append(" [^]"); }
                        if (lastOxyTankLevel > oxiTankPressurePercent) { sbuilder.Append(" [v]"); }
                        if (oxiTankPressurePercent < 10.0f) { Reports.Add(new StatusReportItem() { Text = "Oxygen Storage Low", Details = "Oxygen storage is at " + oxiPressureFactor[0].ToString() + "%", Severity = StatusReportItem.SeverityLevel.Warning, TimeStamp = DateTime.Now }); }
                        lastOxyTankLevel = oxiTankPressurePercent;
                        sbuilder.AppendLine();

                        double[] solarPanelWattage = getSolarPanelWattage();
                        sbuilder.Append(string.Format("SOL PWR: {0}/{1}", getNormalizedPowerString(solarPanelWattage[0]), getNormalizedPowerString(solarPanelWattage[1])));
                        if (lastSolWattage < solarPanelWattage[0]) { sbuilder.Append(" [^]"); }
                        if (lastSolWattage > solarPanelWattage[0]) { sbuilder.Append(" [v]"); }
                        lastSolWattage = (float)solarPanelWattage[0];
                        sbuilder.AppendLine();

                        int lastMessagesShownCount = 4;
                        if (Reports.Count > lastMessagesShownCount)
                        {
                            sbuilder.AppendFormat("Last {1} of {0} Messages:\n", Reports.Count, lastMessagesShownCount);
                            for (int i = Reports.Count - lastMessagesShownCount; i < Reports.Count; i++)
                            {
                                sbuilder.AppendLine(Reports[i].Severity.ToString() + " : " + Reports[i].Text);
                            }
                        }
                        else
                        {
                            sbuilder.Append("Messages:\n");
                            foreach (var item in Reports)
                            {
                                sbuilder.AppendLine(item.Severity.ToString() + " : " + item.Text);
                            }
                        }

                        return sbuilder.ToString();
                    }

                    public float getPercentageFromArrayCurMax(double[] input)
                    {
                        return ((float)input[0] / (float)input[1]) * 100;
                    }
                    public float getPercentageFromArrayCurMax(float[] input)
                    {
                        return (input[0] / input[1]) * 100;
                    }

                    /// <summary>
                    /// Get normalized power string, in watts, kilowatts, megawatts, or gigawatts.
                    /// </summary>
                    /// <param name="watts">Raw value in watts</param>
                    /// <param name="hour">Whether or not to add an h to the end of the string</param>
                    /// <returns></returns>
                    public string getNormalizedPowerString(double watts, bool hour = false)
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



                    /// <summary>
                    /// Gets the current and max oxygen pressure across the ship/station. Every vent is +1.0 maxpressure, where 1.0 is the 100% pressurized and 0.0 is 0% pressurized. 
                    /// </summary>
                    /// <returns>Array of floats, first is the current pressure, second is the max pressure</returns>
                    public float[] getOxygenPressureFactor()
                    {
                        List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                        List<IMyAirVent> airvents = new List<IMyAirVent>();
                        GridTerminalSystem.GetBlocksOfType<IMyAirVent>(tempterminalblocklist);
                        airvents.AddRange(tempterminalblocklist.ConvertAll<IMyAirVent>(new Converter<IMyTerminalBlock, IMyAirVent>(x => (IMyAirVent)x)));

                        //if (airvents.Count < 1) { myEcho("WARN: No Air Vents found."); return null; }


                        float maxPressure = 0, airPressure = 0;
                        foreach (var vent in airvents)
                        {
                            if (vent.DisplayNameText.ToLower().Contains("emergency") || !isHealthy(vent)) { continue; }
                            airPressure += vent.GetOxygenLevel();
                            maxPressure += 1.0f;
                        }

                        return new float[] { airPressure, maxPressure };
                    }

                    public double[] getSolarPanelWattage()
                    {
                        List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                        List<IMySolarPanel> solarpanels = new List<IMySolarPanel>();
                        GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(tempterminalblocklist);
                        solarpanels.AddRange(tempterminalblocklist.ConvertAll<IMySolarPanel>(new Converter<IMyTerminalBlock, IMySolarPanel>(x => (IMySolarPanel)x)));

                       // if (solarpanels.Count < 1) { myEcho("WARN: No Solar Panels found."); return null; }
                
                        return new double[] { getSolarPower(false, solarpanels), getSolarPower(true, solarpanels) };

                    }

                    public double[] getBatteryWattHours()
                    {
                        List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();

                        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
                        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(tempterminalblocklist);
                        batteries.AddRange(tempterminalblocklist.ConvertAll<IMyBatteryBlock>(new Converter<IMyTerminalBlock, IMyBatteryBlock>(x => (IMyBatteryBlock)x)));

                        //if (batteries.Count < 1) { myEcho("WARN: No Batteries found."); return null; }

                        double maxBatCapacity = 0, BatCapacity = 0;
                        foreach (var bat in batteries)
                        {
                            if (bat.DisplayNameText.ToLower().Contains("emergency") || !isHealthy(bat)) { continue; }
                            BatCapacity += bat.CurrentStoredPower;
                            maxBatCapacity += bat.MaxStoredPower;
                        }
                        return new double[] { BatCapacity, maxBatCapacity };
                    }

                    public double[] getBatteryWattOutput()
                    {
                        List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(tempterminalblocklist);

                        //if (tempterminalblocklist.Count < 1) { myEcho("WARN: No Batteries found."); return null; }
                        double[] batteryPower = { 0, 0 };
                        foreach (var item in tempterminalblocklist)
                        {
                            batteryPower[0] += getPower(item, false);
                            batteryPower[1] += getPower(item, true);
                        }
                        return batteryPower;

                    }

                    /// <summary>
                    /// Gets the max pressure and current pressure in all WORKING oxygen tanks (not average). (Also excludes emergency tanks)
                    /// </summary>
                    /// <returns>Array of floats, first is current pressure, second is max pressure.</returns>
                    public float[] getOxygenTankPressureFactor()
                    {
                        List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                        List<IMyOxygenTank> oxygentanks = new List<IMyOxygenTank>();
                        GridTerminalSystem.GetBlocksOfType<IMyOxygenTank>(tempterminalblocklist);
                        oxygentanks.AddRange(tempterminalblocklist.ConvertAll<IMyOxygenTank>(new Converter<IMyTerminalBlock, IMyOxygenTank>(x => (IMyOxygenTank)x)));

                        //if (oxygentanks.Count < 1) { myEcho("WARN: No oxygen tank found."); return null; }


                        float maxPressure = 0, airPressure = 0;
                        foreach (var tank in oxygentanks)
                        {
                            if (tank.DisplayNameText.ToLower().Contains("emergency") || !isHealthy(tank)) { continue; }
                            airPressure += tank.GetOxygenLevel();
                            maxPressure += 1.0f;
                        }

                        return new float[] { airPressure, maxPressure };
                    }

                    ///// <summary>
                    ///// Gets the max pressure and current pressure in all WORKING oxygen tanks (not average). (Also excludes emergency tanks)
                    ///// </summary>
                    ///// <returns>Array of floats, first is current pressure, second is max pressure.</returns>
                    //public float[] getHydrogenTankPressureFactor()
                    //{
                    //    List<IMyTerminalBlock> tempterminalblocklist = new List<IMyTerminalBlock>();
                    //    List<IMy> oxygentanks = new List<IMyOxygenTank>();
                    //    GridTerminalSystem.GetBlocksOfType<IMyOxygenTank>(tempterminalblocklist);
                    //    oxygentanks.AddRange(tempterminalblocklist.ConvertAll<IMyOxygenTank>(new Converter<IMyTerminalBlock, IMyOxygenTank>(x => (IMyOxygenTank)x)));

                    //    if (oxygentanks.Count < 1) { throw new Exception("ERROR: No oxygen tank found. Please fix issue and recompile."); }


                    //    float maxPressure = 0, airPressure = 0;
                    //    foreach (var tank in oxygentanks)
                    //    {
                    //        if (tank.DisplayNameText.ToLower().Contains("emergency") || !isHealthy(tank)) { continue; }
                    //        airPressure += tank.GetOxygenLevel();
                    //        maxPressure += 1.0f;
                    //    }

                    //    return new float[] { airPressure, maxPressure };
                    //}



                    /// <summary>
                    /// Original function written by Stiggan/Malekeh, took from the Battery management script.
                    /// Splits down the DetailedInfo field of a block and retrieves the value corresponding to the given name of a field.
                    /// E.g 
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
                                value = line[1].Substring(1);
                                break;
                            }
                        }
                        return value;
                    }

                    public Dictionary<string, string> getAllDetailedInfoValues(IMyTerminalBlock block)
                    {
                        Dictionary<string, string> values = new Dictionary<string, string>();
                        string[] lines = block.DetailedInfo.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string[] line = lines[i].Split(':');
                            values.Add(line[0], line[1]);
                        }
                        return values;
                    }

                    public bool detailExist(IMyTerminalBlock block, string name)
                    {
                        return !String.IsNullOrEmpty(getDetailedInfoValue(block, name));
                    }

                    public bool isHealthy(IMyTerminalBlock block)
                    {
                        return (block.IsFunctional && block.IsWorking);
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


                    public double getSolarPower(bool max, List<IMySolarPanel> solarpanels)
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

                        public class StatusReportItem
                        {
                    
                            public string Text { get; set; }
                            public string Details { get; set; }
                            public DateTime TimeStamp { get; set; }
                            public SeverityLevel Severity { get; set; }



                            public abstract class Stringy<T, K> where T : new()
                            {
                                public abstract bool Equals(T other);

                                public static bool operator ==(Stringy<T, K> a, Stringy<T, K> b)
                                {
                                    return a.Equals(b);
                                }

                                public static bool operator !=(Stringy<T, K> a, Stringy<T, K> b)
                                {
                                    return !a.Equals(b);
                                }

                                public static bool operator ==(Stringy<T, K> a, K b)
                                {
                                    return a.ToString() == b.ToString();
                                }

                                public static bool operator !=(Stringy<T, K> a, K b)
                                {
                                    return a.ToString() != b.ToString();
                                }

                                public static bool operator ==(Stringy<T, K> a, T b)
                                {
                                    return a.ToString() == b.ToString();
                                }

                                public static bool operator !=(Stringy<T, K> a, T b)
                                {
                                    return a.ToString() != b.ToString();
                                }
                            }

                            public class SeverityLevel : Stringy<SeverityLevel, string>
                            {
                                private string PublicName;

                                public static readonly SeverityLevel ERROR = new SeverityLevel("ERROR");
                                public static readonly SeverityLevel Information = new SeverityLevel("Information");
                                public static readonly SeverityLevel Warning = new SeverityLevel("Warning");
                                public static readonly SeverityLevel CRITICAL = new SeverityLevel("CRITICAL");

                                private static List<SeverityLevel> IntToSeverity = new List<SeverityLevel> { Information, Warning, ERROR, CRITICAL };

                                private static int IndexSeverity(SeverityLevel x)
                                {
                                    for (int i = 0; i < IntToSeverity.Count; ++i)
                                    {
                                        if (IntToSeverity[i].Equals(x))
                                        {
                                            return i;
                                        }
                                    }
                                    
                                    throw new Exception("WELP SEEMS WE FUCKED UP NOW.");
                                }

                                public SeverityLevel Worse()
                                {
                                    int MyIndex = IndexSeverity(this);
                                    MyIndex = Math.Min(MyIndex + 1, IntToSeverity.Count - 1);
                                    return IntToSeverity[MyIndex];
                                }

                                public SeverityLevel Better()
                                {
                                    int MyIndex = IndexSeverity(this);
                                    MyIndex = Math.Max(MyIndex - 1, 0);
                                    return IntToSeverity[MyIndex];
                                }

                                public SeverityLevel(string Name)
                                {
                                    this.PublicName = Name;
                                }

                                public SeverityLevel()
                                {
                                    this.PublicName = null;
                                }

                                public override string ToString()
                                {
                                    return this.PublicName;
                                }

                                public override bool Equals(SeverityLevel other)
                                {
                                    return other.ToString() == this.PublicName; // We don't actually care for real identity.
                                }
                            }

                            ///// <summary>
                            ///// This exists because Keen still hasn't fixed Enums. :( 
                            ///// </summary>
                            //public class SeverityLevel
                            //{
                            //    private string PublicName;
                            //    public static  SeverityLevel ERROR = new SeverityLevel("ERROR");
                            //    public static  SeverityLevel Info = new SeverityLevel("Info");
                            //    public static  SeverityLevel Warning = new SeverityLevel("Warning");
                            //    public static  SeverityLevel CRITICAL = new SeverityLevel("CRITICAL");

                            //    SeverityLevel(string Name)
                            //    {
                            //        this.PublicName = Name;
                            //    }

                            //    public override string ToString()
                            //    {
                            //        return this.PublicName;
                            //    }

                            //    public override bool Equals(object obj)
                            //    {
                            //        return this._Equals(new SeverityLevel(obj.ToString()));
                            //    }

                            //    public bool _Equals(SeverityLevel other)
                            //    {
                            //        return other.ToString() == this.PublicName; // We don't actually care for real identity.
                            //    }

                            //    public static bool operator ==(SeverityLevel a, SeverityLevel b)
                            //    {
                            //        return a.Equals(b);
                            //    }
                            //    public static bool operator !=(SeverityLevel a, SeverityLevel b)
                            //    {
                            //        return !a.Equals(b);
                            //    }
                            //}
                        }


                }

                /// <summary>
                /// Manages power generation, consumption, and allows for schedules to be setup(?)
                /// </summary>
                public class PowerManagement
                {


                }


                /// <summary>
                /// Routes alerts and sets off the appropriate groups and sequences and speakers
                /// </summary>
                public class AlertManagement
                {

                }

                /// <summary>
                /// Everything inventory related, from sorting to compacting to routing stuff about as specified.
                /// Also makes sure there's enough of a specified item, and tells the StatusReporter when shit's up.
                /// </summary>
                public class InventoryManagement
                {

                }



    }
}
