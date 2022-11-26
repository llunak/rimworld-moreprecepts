using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Verse.AI;
using System;

// Core game has both Comfort:Ignored and RoughLiving:Welcomed, and we need to do the inverse
// of both here (it is bad to both use bad furniture and not have furniture at all).
// The complication is that many actions have interactions spots where furniture can be placed,
// so we need to differentiate where its' necessary and where it's acceptable.
// Generally Wanted will only want furniture for beds, eating and working tables,
// Important will refuse not having furniture and Essential will refuse not having sufficient furniture.
// Eating without furniture is not blocked as an exception (caravans would be difficult, and it's generally
// life-threatening).
namespace MorePrecepts
{

    public static class ComfortHelper
    {
        private static (float bedMin,float bedOk,float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
            ThoughtDef thoughtDef, Precept precept) GetComfortInternal(Pawn pawn)
        {
            if(pawn.Ideo == null || pawn.needs == null || pawn.needs.mood == null || pawn.IsSlave)
                return (0,0,0,0,QualityCategory.Awful,QualityCategory.Awful,null,null);
            Precept precept;
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Wanted)) != null)
                // normal bedroll, normal bed+extra, normal stool(awful chair), normal chair, poor/normal table
                return (0.68f,0.85f,0.5f,0.7f,QualityCategory.Poor,QualityCategory.Normal,
                    ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Wanted, precept);
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Important)) != null)
                // good bedroll, good bed+extra, good stool(poor chair), good chair, normal/good table
                return (0.76f,0.95f,0.56f,0.78f,QualityCategory.Normal,QualityCategory.Good,
                    ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Important, precept);
            if((precept = pawn.Ideo.GetPrecept(PreceptDefOf.Comfort_Essential)) != null)
                // excellent bedroll, excellent bed+extra, normal chair (masterwork stool,poor armchair), excellent chair, good/excellent table
                return (0.84f,1.05f,0.7f,0.87f,QualityCategory.Good,QualityCategory.Excellent,
                    ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Essential, precept);
            return (0,0,0,0,QualityCategory.Awful,QualityCategory.Awful,null,null);
        }

        public static (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
            ThoughtDef thoughtDef, Precept precept) GetComfort(Pawn pawn)
        {
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = GetComfortInternal(pawn);
            // Check also minimal expectations.
            if (thoughtDef != null && thoughtDef.minExpectationForNegativeThought != null && pawn.MapHeld != null
                && ExpectationsUtility.CurrentExpectationFor(pawn.MapHeld).order < thoughtDef.minExpectationForNegativeThought.order)
            {
                return (0,0,0,0,QualityCategory.Awful,QualityCategory.Awful,null,null);
            }
            return (bedMin, bedOk, chairMin, chairOk, tableMin, tableOk, thoughtDef, precept);
        }

        public static int GetThoughtLevel(Thing thing, float min, float ok)
        {
            float comfort = thing?.GetStatValue(StatDefOf.Comfort) ?? 0;
            if(comfort >= ok)
                return -1;
            return comfort == 0 ? 2 : comfort < min ? 1 : 0;
        }

        public static int GetThoughtLevel(Thing thing, QualityCategory min, QualityCategory ok)
        {
            QualityCategory quality;
            if(thing == null)
                return 0;
            else if(thing.TryGetQuality(out quality))
            {
                if(quality >= ok)
                    return -1;
                return quality < min ? 1 : 0;
            }
            else
                return -1; // no quality setting, always consider acceptable
        }

        public static void AddThoughtIfNeeded(Pawn pawn, int level, ThoughtDef thoughtDef, Precept precept)
        {
            if(level < 0)
                return;
            Thought_Memory thought = ThoughtMaker.MakeThought(thoughtDef, precept);
            thought.SetForcedStage(level);
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
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(actor);
            if(thoughtDef == null)
                return;
            ComfortHelper.AddThoughtIfNeeded(actor, ComfortHelper.GetThoughtLevel(actor.CurrentBed(), bedMin, bedOk),
                thoughtDef, precept);
        }
    }

    [HarmonyPatch(typeof(Thing))]
    public static class Thing_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Ingested))]
        public static void Ingested(Thing __instance, Pawn ingester, float nutritionWanted)
        {
            Thing thing = __instance;
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(ingester);
            if(thoughtDef == null)
                return;
            // The comfort+ingest code is in Toils_Ingest, but Thing.Ingested is easier to patch.
            Thing chair = null;
            if(thing.def.ingestible.chairSearchRadius > 0f)
                chair = ingester.Map != null ? ingester.Position.GetEdifice(ingester.Map) : null;
            Thing table = null;
            if (ingester.needs.mood != null && thing.def.IsNutritionGivingIngestible && thing.def.ingestible.chairSearchRadius > 10f)
                if (ingester.GetPosture() == PawnPosture.Standing && !ingester.IsWildMan() && thing.def.ingestible.tableDesired)
                    table = ingester.Map != null ? (ingester.Position + ingester.Rotation.FacingCell).GetEdifice(ingester.Map) : null;
            int chairLevel = ComfortHelper.GetThoughtLevel(chair, chairMin, chairOk);
            int tableLevel = ComfortHelper.GetThoughtLevel(table, tableMin, tableOk);
            ComfortHelper.AddThoughtIfNeeded(ingester, Math.Max(chairLevel, tableLevel), thoughtDef, precept);
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed))]
    public static class CompAssignableToPawn_Bed_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(IdeoligionForbids))]
        public static bool IdeoligionForbids(ref bool __result, CompAssignableToPawn_Bed __instance, Pawn pawn)
        {
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            if(thoughtDef == null)
                return true;
            if(precept.def == PreceptDefOf.Comfort_Wanted)
                return true; // Wanted allows all.
            float comfort = __instance.parent.GetStatValue(StatDefOf.Comfort);
            // Important and Essential require at least minimal comfort.
            if(comfort >= bedMin)
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
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            // Wanted don't refuse, Important refuse no furniture, Essential refuse furniture below minimal comfort.
            if(thoughtDef == null || precept.def == PreceptDefOf.Comfort_Wanted)
                return true;
            Building edifice = exactSittingPos.GetEdifice(pawn.Map);
            if(edifice == null)
                return false;
            if(precept.def == PreceptDefOf.Comfort_Essential)
                if(edifice.GetStatValue(StatDefOf.Comfort) < chairMin)
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

    [HarmonyPatch(typeof(JobGiver_GetRest))]
    public static class JobGiver_GetRest_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TryGiveJob))]
        public static void TryGiveJob(ref Job __result, Pawn pawn)
        {
            // Essential pawns refuse to sleep on the ground (until they faint).
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            if(thoughtDef == null || precept.def != PreceptDefOf.Comfort_Essential)
                return;
            Job job = __result;
            if(job.def == RimWorld.JobDefOf.LayDown && !job.targetA.HasThing)
                __result = null;
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest))]
    public static class Toils_Ingest_Patch
    {
        // Again, the transpiller is set up manually because it's an internal delegate function.
        public static IEnumerable<CodeInstruction> CarryIngestibleToChewSpot_delegate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int foundCount = 0;
            CodeInstruction actorLoad = null;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // .. thing2.def.ingestible.chairSearchRadius ..
                // Replace it with:
                // .. CarryIngestibleToChewSpot_Hook(thing2.def.ingestible.chairSearchRadius, actor) ..
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:44:ldloc.0::
                // T:45:ldfld::Verse.Pawn actor
                // T:52:ldloc.3::
                // T:53:ldfld::Verse.ThingDef def
                // T:54:ldfld::RimWorld.IngestibleProperties ingestible
                // T:55:ldfld::System.Single chairSearchRadius
                if(actorLoad == null && codes[i].opcode == OpCodes.Ldloc_0
                    && i+1 < codes.Count && codes[i+1].opcode == OpCodes.Ldfld && codes[i+1].operand.ToString() == "Verse.Pawn actor")
                {
                    actorLoad = codes[i+1].Clone();
                }
                if(actorLoad != null && codes[i].opcode == OpCodes.Ldfld
                    && codes[i].operand.ToString() == "System.Single chairSearchRadius")
                {
                    codes.Insert(i+1, new CodeInstruction(OpCodes.Ldloc_0));
                    codes.Insert(i+2, actorLoad.Clone());
                    codes.Insert(i+3, new CodeInstruction(OpCodes.Call, typeof(Toils_Ingest_Patch).GetMethod(nameof(CarryIngestibleToChewSpot_Hook))));
                    ++foundCount;
                }
            }
            if(foundCount != 3)
                Log.Error("MorePrecepts: Failed to patch Toils_Ingest.FinalizeIngest() delegate");
            return codes;
        }

        // Multiply chair search radius, i.e. comfortable pawns try harder when searching for a place to sit.
        public static float CarryIngestibleToChewSpot_Hook(float chairSearchRadius, Pawn pawn)
        {
            if(chairSearchRadius == 0)
                return chairSearchRadius;
            (float bedMin, float bedOk, float chairMin, float chairOk, QualityCategory tableMin, QualityCategory tableOk,
                ThoughtDef thoughtDef, Precept precept) = ComfortHelper.GetComfort(pawn);
            if(thoughtDef == null)
                return chairSearchRadius;
            float factor = 1;
            if( precept.def == PreceptDefOf.Comfort_Wanted)
                factor = 2;
            if( precept.def == PreceptDefOf.Comfort_Important)
                factor = 3;
            if( precept.def == PreceptDefOf.Comfort_Essential)
                factor = 5;
            return chairSearchRadius * factor;
        }
    }

    // Precept tooltip shows only some memory thought types, use situational, others depend on events.
    public class PreceptComp_MemoryThought : PreceptComp_SituationalThought
    {
    }
}
