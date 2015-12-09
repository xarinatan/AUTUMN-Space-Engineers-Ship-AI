using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripting
{
    class CodeEditorEmulator
    {
        IMyGridTerminalSystem GridTerminalSystem = null;
        string Storage;

        //                               - - - Code start - - -                                 \\

        void Main(string argument)
        {
        }

        static class Functions
        {
            public static string getDamageReportString(bool listOfflineBlocks)
            {
                StringBuilder report = new StringBuilder();

                return report.ToString();
            }
            class DamageReport
            {
                public int brokenBlockCount, offlineBlockCount = 0;
                public List<IMyTerminalBlock> BrokenBlocks { get; set; }
            }
            public static DamageReport getDamageReport(bool listOfflineBlocks, IMyGridTerminalSystem GridTerminalSystem)
            {
                DamageReport dmgReportToReturn = new DamageReport();
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocks(blocks);
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (!blocks[i].IsFunctional)
                    {
                        
                        dmgReportToReturn.brokenBlockCount++;
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

                return dmgReportToReturn;
            }


        }
    }
}
