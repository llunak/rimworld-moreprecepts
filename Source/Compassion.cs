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
    public static class CompassionHelper
    {
        public static void CheckPawnLeftToDie(Pawn pawn)
        {
            // ExecutionThoughtStage appears to be meant only for executions, but it works generically,
            // so use it to set severity of the thought depending on how long the pawn has been left there.
            int ticksUntilDeathWhenDowned = PawnComp.GetLastDownedTicksUntilDeath(pawn);
            if(ticksUntilDeathWhenDowned <= 0)
                return;
            int hours = ticksUntilDeathWhenDowned / GenDate.TicksPerHour;
            int stage = 0; // The least severe thought.
            if( hours > 2 )
                stage = 1;
            if( hours > 5 )
                stage = 2;
            if( hours > 10 )
                stage = 3; // The most severe thought.
            // If there's any active threat on the map, lower the death to the least severe stage. This is
            // primarily to reduce debuffs for killing downed pawns during a fight (e.g. grenades killing them).
            if(pawn.Map != null && GenHostility.AnyHostileActiveThreatToPlayer(pawn.Map))
                stage = 0;
            // All:
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Compassion_IncapacitatedPawnLeftToDie_All,
                pawn.Named(HistoryEventArgsNames.Victim), stage.Named(HistoryEventArgsNames.ExecutionThoughtStage)));
            // NonGuiltyEnemies (and everybody else who's not enemy):
            if(!(pawn.guilt != null && pawn.guilt.IsGuilty) || !pawn.HostileTo(Faction.OfPlayer))
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

        public static int TicksToDie(Pawn pawn)
        {
            int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            if(ticks <= 0 || ticks > GenDate.TicksPerDay * 10)
                ticks = -99999;
            return ticks;
        }
    }

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
                PawnComp.SetLastDownedTicksUntilDeath(pawn, CompassionHelper.TicksToDie(pawn));
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MakeUndowned))]
        public static void MakeUndowned(Pawn_HealthTracker __instance)
        {
            FieldInfo fi = AccessTools.Field(typeof(Pawn_HealthTracker),"pawn");
            Pawn pawn = (Pawn)fi.GetValue(__instance);
            if(pawn.RaceProps.Humanlike && !pawn.Dead)
                PawnComp.SetLastDownedTicksUntilDeath(pawn, -99999);
        }
    }

    [HarmonyPatch(typeof(TendUtility))]
    public static class TendUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DoTend))]
        public static void DoTend(Pawn doctor, Pawn patient, Medicine medicine)
        {
            // When a downed pawn is tended, reset the time it's been left to die.
            if(patient.Downed && patient.RaceProps.Humanlike && !patient.Dead)
                PawnComp.SetLastDownedTicksUntilDeath(patient, CompassionHelper.TicksToDie(patient));
        }
    }

    [HarmonyPatch(typeof(MapDeiniter))]
    public static class MapDeiniter_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PassPawnsToWorld))]
        public static void PassPawnsToWorld(Map map)
        {
            // Check any pawns left to die when leaving a map.
            foreach( Pawn pawn in map.mapPawns.AllPawns)
            {
                if(pawn.RaceProps.Humanlike && !pawn.Dead && pawn.Downed)
                {
                    if(CompassionHelper.TicksToDie(pawn) < 0)
                        continue; // Medically treated => ok.
                    CompassionHelper.CheckPawnLeftToDie(pawn);
                }
            }
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
            if(!pawn.Downed)
                return;
            if(pawn.InBed())
                return; // Is in bed => somebody's tried to treat him.
            CompassionHelper.CheckPawnLeftToDie(pawn);
        }
    }

}
