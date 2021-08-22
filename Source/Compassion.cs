using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RimWorld;
using Verse;

// When a pawn dies in the open (not in a bed), we'll send events about it,
// together with thought stage computed from how long the pawn was incapacitated
// before dying (leaving the pawn to die longer has bigger mood debuff).
namespace MorePrecepts
{

    [HarmonyPatch(typeof(Pawn_HealthTracker))]
    public static class Pawn_HealthTracker_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MakeDowned))]
        public static void MakeDowned(Pawn_HealthTracker __instance)
        {
            FieldInfo fi = AccessTools.Field(typeof(Pawn_HealthTracker),"pawn");
            Pawn pawn = (Pawn)fi.GetValue(__instance);
            if(pawn.RaceProps.Humanlike && !pawn.Dead)
                PawnComp.SetLastDownedTickToNow(pawn);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MakeUndowned))]
        public static void MakeUndowned(Pawn_HealthTracker __instance)
        {
            FieldInfo fi = AccessTools.Field(typeof(Pawn_HealthTracker),"pawn");
            Pawn pawn = (Pawn)fi.GetValue(__instance);
            if(pawn.RaceProps.Humanlike && !pawn.Dead)
                PawnComp.ResetLastDownedTick(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn2_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Kill))]
        public static void Kill(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            Pawn pawn = __instance;
            if(!pawn.RaceProps.Humanlike)
                return;
            if(!pawn.Downed || PawnComp.GetLastDownedTick(pawn) < 0)
                return;
            if(pawn.CurrentBed() != null)
                return; // Is in bed => somebody's tried to treat him.
            // If there's any active threat on the map, ignore the death. This is
            // primarily to avoid debuffs for killing downed pawns during a fight
            // (e.g. grenades killing them).
            if(GenHostility.AnyHostileActiveThreatToPlayer_NewTemp(pawn.Map))
                return;
            // ExecutionThoughtStage appears to be meant only for executions, but it works generically,
            // so use it to set severity of the thought depending on how long the pawn has been left there.
            int hours = (Find.TickManager.TicksGame - PawnComp.GetLastDownedTick(pawn)) / GenDate.TicksPerHour;
            int stage = 0; // The least severe thought.
            if( hours > 2 )
                stage = 1;
            if( hours > 5 )
                stage = 2;
            if( hours > 10 )
                stage = 3; // The most severe thought.
            // All:
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Compassion_IncapacitatedPawnLeftToDie_All,
                pawn.Named(HistoryEventArgsNames.Victim), stage.Named(HistoryEventArgsNames.ExecutionThoughtStage)));
            // NonGuiltyEnemies (and everybody else who's not enemy):
            if(!pawn.guilt.IsGuilty || !pawn.HostileTo(Faction.OfPlayer))
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Compassion_IncapacitatedPawnLeftToDie_NonGuiltyEnemies,
                    pawn.Named(HistoryEventArgsNames.Victim), stage.Named(HistoryEventArgsNames.ExecutionThoughtStage)));
            }
            // NonHostile:
            if(!pawn.HostileTo(Faction.OfPlayer))
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Compassion_IncapacitatedPawnLeftToDie_NonHostile,
                    pawn.Named(HistoryEventArgsNames.Victim), stage.Named(HistoryEventArgsNames.ExecutionThoughtStage)));
            }
            // Allies:
            if(pawn.Faction != null && pawn.Faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Compassion_IncapacitatedPawnLeftToDie_Allies,
                    pawn.Named(HistoryEventArgsNames.Victim), stage.Named(HistoryEventArgsNames.ExecutionThoughtStage)));
            }
        }
    }

}
