using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

// Core game has both Comfort:Ignored and RoughLiving:Welcomed, and we need to do the inverse
// of both here (it is bad to both use bad furniture and not have furniture at all).
// The complication is that many actions have interactions spots where furniture can be placed,
// so we need to differentiate where its' necessary and where it's acceptable.
// Generally Wanted will only want furniture for beds, eating and working tables,
// Important will refuse not having furniture and Essential will refuse not having sufficient furniture.
namespace MorePrecepts
{

    public static class ComfortHelper
    {
        private static (float min,float ok,ThoughtDef thoughtDef, Precept precept) GetComfortInternal(Pawn pawn)
        {
            if(pawn.Ideo == null || pawn.needs == null || pawn.needs.mood == null || pawn.IsSlave)
                return (0,0,null,null);
            Precept precept;
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Wanted)) != null)
                // normal bedroll minimum, normal bed+extra is ok
                return (0.68f,0.85f,ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Wanted, precept);
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Important)) != null)
                // good bedroll minimum, good bed+extra is ok
                return (0.76f,0.95f,ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Important, precept);
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Essential)) != null)
                // excellent bedroll minimum, excellent bed+extra is ok
                return (0.84f,1.05f,ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Essential, precept);
            return (0,0,null,null);
        }

        public static (float min,float ok,ThoughtDef thoughtDef, Precept precept) GetComfort(Pawn pawn)
        {
            (float min, float ok, ThoughtDef thoughtDef, Precept precept) = GetComfortInternal(pawn);
            // Check also minimal expectations.
            if (thoughtDef != null && thoughtDef.minExpectationForNegativeThought != null && pawn.MapHeld != null
                && ExpectationsUtility.CurrentExpectationFor(pawn.MapHeld).order < thoughtDef.minExpectationForNegativeThought.order)
            {
                return (0,0,null,null);
            }
            return (min,ok,thoughtDef,precept);
        }

        public static void AddThoughtIfNeeded(Pawn pawn, Thing thing, float min, float ok, ThoughtDef thoughtDef, Precept precept)
        {
            float comfort = thing?.GetStatValue(StatDefOf.Comfort) ?? 0;
            if(comfort >= ok)
                return;
            Thought_Memory thought = ThoughtMaker.MakeThought(thoughtDef, precept);
            thought.SetForcedStage(comfort < min ? 1 : 0);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown))]
    public static class Toils_LayDown_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ApplyBedThoughts))]
        public static void ApplyBedThoughts(Pawn actor)
        {
            (float min, float ok, ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(actor);
            if(thoughtDef == null)
                return;
            ComfortHelper.AddThoughtIfNeeded(actor, actor.CurrentBed(), min, ok, thoughtDef, precept);
        }
    }

    [HarmonyPatch(typeof(Thing))]
    public static class Thing_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Ingested))]
        public static void Ingested(Thing __instance, Pawn ingester, float nutritionWanted)
        {
            (float min, float ok, ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(ingester);
            if(thoughtDef == null)
                return;
            Thing thing = __instance;
            // The comfort+ingest code is in Toils_Ingest, but Thing.Ingested is easier to patch.
            // chair
            if(thing.def.ingestible.chairSearchRadius > 0f)
                ComfortHelper.AddThoughtIfNeeded(ingester, ingester.Position.GetEdifice(ingester.Map), min, ok, thoughtDef, precept);
            // table
            if (ingester.needs.mood != null && thing.def.IsNutritionGivingIngestible && thing.def.ingestible.chairSearchRadius > 10f)
                if (ingester.GetPosture() == PawnPosture.Standing && !ingester.IsWildMan() && thing.def.ingestible.tableDesired)
                    ComfortHelper.AddThoughtIfNeeded(ingester, (ingester.Position + ingester.Rotation.FacingCell).GetEdifice(ingester.Map),
                        min, ok, thoughtDef, precept);
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed))]
    public static class CompAssignableToPawn_Bed_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(IdeoligionForbids))]
        public static bool IdeoligionForbids(ref bool __result, CompAssignableToPawn_Bed __instance, Pawn pawn)
        {
            (float min, float ok, ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            if(thoughtDef == null)
                return true;
            if(precept.def == PreceptDefOf.Comfort_Wanted)
                return true; // Wanted allows all.
            float comfort = __instance.parent.GetStatValue(StatDefOf.Comfort);
            // Important and Essential require at least minimal comfort.
            if(comfort >= min)
                return true;
            __result = true; // Forbid.
            return false;
        }
    }

    [HarmonyPatch(typeof(ReservationUtility))]
    public static class ReservationUtility_Patch
    {
        private static bool IsAcceptableSittableOrSpot(Pawn pawn, IntVec3 exactSittingPos)
        {
            (float min, float ok, ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            // Wanted don't refuse, Important refuse no furniture, Essential refuse furniture below minimal comfort.
            if(thoughtDef == null || precept.def == PreceptDefOf.Comfort_Wanted)
                return true;
            Building edifice = exactSittingPos.GetEdifice(pawn.Map);
            if(edifice == null)
                return false;
            if(precept.def == PreceptDefOf.Comfort_Essential)
                if(edifice.GetStatValue(StatDefOf.Comfort) < min)
                    return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanReserveSittableOrSpot))]
        public static bool CanReserveSittableOrSpot(ref bool __result, Pawn pawn, IntVec3 exactSittingPos, bool ignoreOtherReservations)
        {
            if(!IsAcceptableSittableOrSpot(pawn, exactSittingPos))
            {
                JobFailReason.Is("MorePrecepts.InsufficientComfortAtInteractionSpot".Translate());
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ReserveSittableOrSpot))]
        public static bool ReserveSittableOrSpot(ref bool __result, Pawn pawn, IntVec3 exactSittingPos, Job job, bool errorOnFailed)
        {
            if(!IsAcceptableSittableOrSpot(pawn, exactSittingPos))
            {
                JobFailReason.Is("MorePrecepts.InsufficientComfortAtInteractionSpot".Translate());
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Precept tooltip shows only some memory thought types, use situational, others depend on events.
    public class PreceptComp_MemoryThought : PreceptComp_SituationalThought
    {
    }
}
