using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Text;
using RimWorld;
using Verse;

// Things that really should be fixed in core RimWorld.
namespace MorePrecepts
{
    [HarmonyPatch(typeof(Precept))]
    public static class Precept_Patch
    {
        private static StringBuilder stringBuilder = new StringBuilder();
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GetTip))]
        public static void GetTip(ref string __result)
        {
            // https://ludeon.com/forums/index.php?topic=54992.msg490492
            // The function doesn't filter out duplicate mood buffs if caused by several thoughts.
            // This works only for consecutive lines, which is hopefully good enough.
            stringBuilder.Clear();
            string last = null;
            foreach( string line in __result.Split( new[] { Environment.NewLine }, StringSplitOptions.None ))
            {
                if( line != last )
                {
                    if( String.IsNullOrEmpty( line ))
                        stringBuilder.AppendLine();
                    else
                        stringBuilder.AppendInNewLine( line );
                }
                last = line;
            }
            __result = stringBuilder.ToString();
        }
    }

}
