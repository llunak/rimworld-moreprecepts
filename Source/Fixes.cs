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

    [HarmonyPatch(typeof(PreceptComp_Thought))]
    public static class PreceptComp_Thought_Patch
    {
        private static int savedThoughtStage = -1;
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ParseDescription))]
        public static bool ParseDescription(string description, int thoughtStage)
        {
            savedThoughtStage = thoughtStage;
            return true;
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ParseDescription))]
        public static void ParseDescription(ref string __result, PreceptComp_Thought __instance, ref string description, int thoughtStage)
        {
            // The function doesn't show minExpectationForNegativeThought if the first thought stage
            // is positive (DrugUse:Essential, Alcohol:Essential, etc.), because if general
            // info is wanted (thoughtStage == -1), it checks only the first stage.
            ThoughtDef thought = __instance.thought;
            if(savedThoughtStage == -1 && thought.minExpectationForNegativeThought != null )
            {
                string textKey = " (" +  "MinExpectation".Translate() + ": ";
                if( !__result.Contains(textKey)) // Check just in case it's been fixed.
                {
                    for (int i = 0; i < thought.stages.Count; i++)
                    {
                        if (thought.stages[i].baseMoodEffect < 0)
                        {
                            string add = textKey + thought.minExpectationForNegativeThought.LabelCap + ")";
                            int pos = __result.LastIndexOf(": ");
                            if(pos >= 0)
                                __result = __result.Insert(pos, add);
                            else
                                __result += add;
                            return;
                        }
                    }
                }
            }
        }
    }

}
