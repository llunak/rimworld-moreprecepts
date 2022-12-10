using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using RimWorld;
using Verse;

namespace MorePrecepts
{

    [HarmonyPatch(typeof(JobDriver_Strip))]
    public static class JobDriver_Strip_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch("<MakeNewToils>b__2_2")] // Internal delegate function.
        public static IEnumerable<CodeInstruction> MakeNewToils_delegate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // .. pawn.records.Increment(RecordDefOf.BodiesStripped); ..
                // Append:
                // .. MakeNewToils_Hook(pawn, thing) ..
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:23:ldarg.0::
                // T:24:ldfld::Verse.Pawn pawn
                // T:25:ldfld::RimWorld.Pawn_RecordsTracker records
                // T:26:ldsfld::RimWorld.RecordDef BodiesStripped
                // T:27:callvirt::Void Increment(RimWorld.RecordDef)
                if(codes[i].opcode == OpCodes.Ldarg_0 && i+4 < codes.Count
                    && codes[i+1].opcode == OpCodes.Ldfld && codes[i+1].operand.ToString() == "Verse.Pawn pawn"
                    && codes[i+2].opcode == OpCodes.Ldfld && codes[i+2].operand.ToString() == "RimWorld.Pawn_RecordsTracker records"
                    && codes[i+3].opcode == OpCodes.Ldsfld && codes[i+3].operand.ToString() == "RimWorld.RecordDef BodiesStripped"
                    && codes[i+4].opcode == OpCodes.Callvirt && codes[i+4].operand.ToString() == "Void Increment(RimWorld.RecordDef)")
                {
                    codes.Insert(i+5, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i+6, codes[i+1].Clone());
                    codes.Insert(i+7, new CodeInstruction(OpCodes.Ldloc_0));
                    codes.Insert(i+8, new CodeInstruction(OpCodes.Call, typeof(JobDriver_Strip_Patch).GetMethod(nameof(MakeNewToils_Hook))));
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch JobDriver_Strip.MakeNewToils() delegate");
            return codes;
        }

        public static void MakeNewToils_Hook(Pawn pawn, Thing thing)
        {
            Pawn strippedPawn = thing as Pawn;
            if(strippedPawn == null)
                return;
            if(strippedPawn.RaceProps.Humanlike && !strippedPawn.Dead && strippedPawn.Downed
                && strippedPawn.Faction != pawn.Faction)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.TakingFromDowned_DownedStripped,
                    pawn.Named(HistoryEventArgsNames.Doer), strippedPawn.Named(HistoryEventArgsNames.Victim)));
            }
        }
    }
}
