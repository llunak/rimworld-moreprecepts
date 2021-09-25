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
    
    // remove requirement of Ideo if the pawn is prisoner or downed to prevent comfort precept issue
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.IsValidBedFor))]
	internal static class RimWorld__RestUtility__IsValidBedFor {

		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var info1 = AccessTools.DeclaredMethod(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.IdeoligionForbids));
			var info2 = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Downed));
			foreach(var code in instructions) {
				yield return code;
				if(code.Calls(info1)) {
					yield return new CodeInstruction(OpCodes.Ldloc_1);
					yield return new CodeInstruction(OpCodes.Ldc_I4_0);
					yield return new CodeInstruction(OpCodes.Ceq);
					yield return new CodeInstruction(OpCodes.Ldarg_1);
					yield return new CodeInstruction(OpCodes.Call, info2);
					yield return new CodeInstruction(OpCodes.Ldc_I4_0);
					yield return new CodeInstruction(OpCodes.Ceq);
					yield return new CodeInstruction(OpCodes.And);
					yield return new CodeInstruction(OpCodes.And);
				}
			}
		}


	}

    // remove the annoying warning caused by assigning bed with ideo restriction
	[HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimBedIfNonMedical))]
	internal static class RimWorld__Pawn_Ownership__ClaimBedIfNonMedical {

		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var info1 = AccessTools.PropertyGetter(typeof(Building_Bed), nameof(Building_Bed.CompAssignableToPawn));
			var info2 = AccessTools.DeclaredField(typeof(Pawn_Ownership), "pawn");
			var info3 = AccessTools.DeclaredMethod(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.IdeoligionForbids));
			var info4 = AccessTools.DeclaredMethod(typeof(Log), nameof(Log.Error), new Type[] { typeof(string) });
			var list = instructions.ToList();
			int i = 0;
			while(i < list.Count) {
				if(CodeSection(list, i,
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Callvirt, info1),
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Ldfld, info2),
					new CodeInstruction(OpCodes.Callvirt, info3)
				)) {
					while(!list[i++].Calls(info4)) ;
				}
				yield return list[i];
				//Log.Message(list[i].ToString());
				i++;
			}
		}

		internal static bool CodeSection(List<CodeInstruction> list, int index, params CodeInstruction[] instruction) {
			for(int i = 0; i < instruction.Length; i++) {
				var a = list[i + index];
				var b = instruction[i];
				if(!(i + index < list.Count && a.opcode == b.opcode && ((a.operand == null && b.operand == null) || (a.operand != null && b.operand != null && a.OperandIs(b.operand))))) return false;
			}
			return true;
		}

	}

}
