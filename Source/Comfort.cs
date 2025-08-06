using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
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
    public class ComfortInfo
    {
        public float bedMin;
        public float bedOk;
        public float chairMin;
        public float chairOk;
        public QualityCategory tableMin;
        public QualityCategory tableOk;
        public ThoughtDef thoughtDef;
        public PreceptDef preceptDef;
    };

    public static class ComfortHelper
    {
        private static readonly ComfortInfo infoNone = new ComfortInfo{ bedMin = 0, bedOk = 0, chairMin = 0, chairOk = 0,
            tableMin = QualityCategory.Awful, tableOk = QualityCategory.Awful, thoughtDef = null, preceptDef = null };
        // normal bedroll, normal bed+extra, normal stool(awful chair), normal chair, poor/normal table
        private static readonly ComfortInfo infoWanted = new ComfortInfo{ bedMin = 0.68f, bedOk = 0.85f, chairMin = 0.5f, chairOk = 0.7f,
            tableMin = QualityCategory.Poor, tableOk = QualityCategory.Normal,
            thoughtDef =  ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Wanted, preceptDef = PreceptDefOf.Comfort_Wanted };
        // good bedroll, good bed+extra, good stool(poor chair), good chair, normal/good table
        private static readonly ComfortInfo infoImportant = new ComfortInfo{ bedMin = 0.76f, bedOk = 0.95f, chairMin = 0.56f, chairOk = 0.78f,
            tableMin = QualityCategory.Normal, tableOk = QualityCategory.Good,
            thoughtDef = ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Important, preceptDef = PreceptDefOf.Comfort_Important };
        // excellent bedroll, excellent bed+extra, normal chair (masterwork stool,poor armchair), excellent chair, good/excellent table
        private static readonly ComfortInfo infoEssential = new ComfortInfo{ bedMin = 0.84f, bedOk = 1.05f, chairMin = 0.7f, chairOk = 0.87f,
            tableMin = QualityCategory.Good, tableOk = QualityCategory.Excellent,
            thoughtDef = ThoughtDefOf.Comfort_UsedUncomfortableFurniture_Essential, preceptDef = PreceptDefOf.Comfort_Essential };

        private static ComfortInfo GetComfortInternal(Pawn pawn)
        {
            if(pawn.Ideo == null || pawn.needs == null || pawn.needs.mood == null || pawn.IsSlave)
                return infoNone;
            if(pawn.Ideo.HasPrecept(PreceptDefOf.Comfort_Wanted))
                return infoWanted;
            if(pawn.Ideo.HasPrecept(PreceptDefOf.Comfort_Important))
                return infoImportant;
            if(pawn.Ideo.HasPrecept(PreceptDefOf.Comfort_Essential))
                return infoEssential;
            return infoNone;
        }

        public static ComfortInfo GetComfort(Pawn pawn)
        {
            ComfortInfo info = GetComfortInternal(pawn);
            // Check also minimal expectations.
            if (info.thoughtDef != null && info.thoughtDef.minExpectationForNegativeThought != null && pawn.MapHeld != null
                && ExpectationsUtility.CurrentExpectationFor(pawn.MapHeld).order < info.thoughtDef.minExpectationForNegativeThought.order)
            {
                return infoNone;
            }
            return info;
        }

        public const int ThoughtLevelNoFurniture = 2;
        public const int ThoughtLevelOk = -1;

        public static int GetThoughtLevel(Thing thing, float min, float ok)
        {
            if( thing == null ) // min == 0 means no requirements
                return min == 0 ? ThoughtLevelOk : ThoughtLevelNoFurniture;
            float comfort = thing.GetStatValue(StatDefOf.Comfort, cacheStaleAfterTicks : GenDate.TicksPerHour);
            if(comfort >= ok)
                return ThoughtLevelOk;
            return comfort < min ? 1 : 0;
        }

        public static int GetThoughtLevel(Thing thing, QualityCategory min, QualityCategory ok)
        {
            if( thing == null ) // min == QualityCategory.Awful means no requirement
                return min == QualityCategory.Awful ? ThoughtLevelOk : ThoughtLevelNoFurniture;
            QualityCategory quality;
            if(thing.TryGetQuality(out quality))
            {
                if(quality >= ok)
                    return ThoughtLevelOk;
                return quality < min ? 1 : 0;
            }
            else
                return ThoughtLevelOk; // no quality setting, always consider acceptable
        }

        public static void AddThoughtIfNeeded(Pawn pawn, int level, ThoughtDef thoughtDef, PreceptDef preceptDef, Thing thing)
        {
            if(level < 0)
                return;
            Thought_Memory thought = ThoughtMaker.MakeThought(thoughtDef, pawn.Ideo.GetPrecept(preceptDef));
            if( thing != null && thought is Thought_Comfort thoughtComfort )
                thoughtComfort.SetThing(thing);
            thought.SetForcedStage(level);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }

        public static float AdjustChairSearchRadius(float chairSearchRadius, Pawn pawn)
        {
            if(chairSearchRadius == 0)
                return chairSearchRadius;
            ComfortInfo info = ComfortHelper.GetComfort(pawn);
            if(info.thoughtDef == null)
                return chairSearchRadius;
            float factor = 1;
            if( info.preceptDef == PreceptDefOf.Comfort_Wanted)
                factor = 2;
            if( info.preceptDef == PreceptDefOf.Comfort_Important)
                factor = 3;
            if( info.preceptDef == PreceptDefOf.Comfort_Essential)
                factor = 5;
            return chairSearchRadius * factor;
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown))]
    public static class Toils_LayDown1_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ApplyBedThoughts))]
        public static void ApplyBedThoughts(Pawn actor, Building_Bed bed)
        {
            ComfortInfo info = ComfortHelper.GetComfort(actor);
            if(info.thoughtDef == null)
                return;
            ComfortHelper.AddThoughtIfNeeded(actor, ComfortHelper.GetThoughtLevel(bed, info.bedMin, info.bedOk),
                info.thoughtDef, info.preceptDef, bed);
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
            // Some ingestibles like psychite tea or Dubs Bad Hygiene water do not search very far
            // for a place to sit, so ignore. This condition is the same like ate-without-table uses.
            if(!( thing.def.IsNutritionGivingIngestible && thing.def.ingestible.chairSearchRadius > 10f ))
                return;
            ComfortInfo info = ComfortHelper.GetComfort(ingester);
            if(info.thoughtDef == null)
                return;
            if(ingester.InBed()) // Getting fed while in a (medical) bed?
            {
                ComfortHelper.AddThoughtIfNeeded(ingester, ComfortHelper.GetThoughtLevel(ingester.CurrentBed(), info.bedMin, info.bedOk),
                    info.thoughtDef, info.preceptDef, ingester.CurrentBed());
                return;
            }
            // The comfort+ingest code is in Toils_Ingest, but Thing.Ingested is easier to patch.
            Thing chair = null;
            if(thing.def.ingestible.chairSearchRadius > 0f)
                chair = ingester.Map != null ? ingester.Position.GetEdifice(ingester.Map) : null;
            Thing table = null;
            if (ingester.needs.mood != null && thing.def.IsNutritionGivingIngestible && thing.def.ingestible.chairSearchRadius > 10f)
                if (ingester.GetPosture() == PawnPosture.Standing && !ingester.IsWildMan() && thing.def.ingestible.tableDesired)
                    table = ingester.Map != null ? (ingester.Position + ingester.Rotation.FacingCell).GetEdifice(ingester.Map) : null;
            int chairLevel = ComfortHelper.GetThoughtLevel(chair, info.chairMin, info.chairOk);
            int tableLevel = ComfortHelper.GetThoughtLevel(table, info.tableMin, info.tableOk);
            if( chair != null && chairLevel >= tableLevel )
                ComfortHelper.AddThoughtIfNeeded(ingester, chairLevel, info.thoughtDef, info.preceptDef, chair);
            else if( table != null )
                ComfortHelper.AddThoughtIfNeeded(ingester, tableLevel, info.thoughtDef, info.preceptDef, table);
            else // record the ingested thing as the thought source
                ComfortHelper.AddThoughtIfNeeded(ingester, ComfortHelper.ThoughtLevelNoFurniture, info.thoughtDef, info.preceptDef, __instance);
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed))]
    public static class CompAssignableToPawn_Bed_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(IdeoligionForbids))]
        public static bool IdeoligionForbids(ref bool __result, CompAssignableToPawn_Bed __instance, Pawn pawn)
        {
            ComfortInfo info = ComfortHelper.GetComfort(pawn);
            if(info.thoughtDef == null)
                return true;
            if(info.preceptDef == PreceptDefOf.Comfort_Wanted)
                return true; // Wanted allows all.
            float comfort = __instance.parent.GetStatValue(StatDefOf.Comfort, cacheStaleAfterTicks : GenDate.TicksPerHour);
            // Important and Essential require at least minimal comfort.
            if(comfort >= info.bedMin)
                return true;
            if(pawn.Downed)
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
            ComfortInfo info = ComfortHelper.GetComfort(pawn);
            // Wanted don't refuse, Important refuse no furniture, Essential refuse furniture below minimal comfort.
            if(info.thoughtDef == null || info.preceptDef == PreceptDefOf.Comfort_Wanted)
                return true;
            Building edifice = exactSittingPos.GetEdifice(pawn.Map);
            if(edifice == null)
                return false;
            if(info.preceptDef == PreceptDefOf.Comfort_Essential)
                if(edifice.GetStatValue(StatDefOf.Comfort, cacheStaleAfterTicks : GenDate.TicksPerHour) < info.chairMin)
                    return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanReserveSittableOrSpot))]
        [HarmonyPatch(new Type[] { typeof( Pawn ), typeof( IntVec3 ), typeof( Thing ), typeof( bool ) } )]
        public static bool CanReserveSittableOrSpot(ref bool __result, Pawn pawn, IntVec3 exactSittingPos,
            Thing ignoreThing, bool ignoreOtherReservations)
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
            ComfortInfo info = ComfortHelper.GetComfort(pawn);
            if(info.thoughtDef == null || info.preceptDef != PreceptDefOf.Comfort_Essential)
                return;
            Job job = __result;
            if(job.def == RimWorld.JobDefOf.LayDown && !job.targetA.HasThing)
                __result = null;
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest))]
    public static class Toils_Ingest_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(TryFindChairOrSpot))]
        public static IEnumerable<CodeInstruction> TryFindChairOrSpot(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int foundCount = 0;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function refers several times to:
                // .. ingestibleProperties.chairSearchRadius ..
                // Replace it with:
                // .. TryFindChairOrSpot_Hook(ingestibleProperties.chairSearchRadius, pawn) ..
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if(codes[i].opcode == OpCodes.Ldfld && codes[i].operand.ToString() == "System.Single chairSearchRadius")
                {
                    codes.Insert(i+1, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i+2, new CodeInstruction(OpCodes.Call, typeof(Toils_Ingest_Patch).GetMethod(nameof(TryFindChairOrSpot_Hook))));
                    ++foundCount;
                }
            }
            if(foundCount != 2)
                Log.Error("MorePrecepts: Failed to patch Toils_Ingest.TryFindChairOrSpot()");
            return codes;
        }

        // Multiply chair search radius, i.e. comfortable pawns try harder when searching for a place to sit.
        public static float TryFindChairOrSpot_Hook(float chairSearchRadius, Pawn pawn)
        {
            return ComfortHelper.AdjustChairSearchRadius( chairSearchRadius, pawn );
        }
    }

    // JobDriver_FeedBaby has 32 hardcoded.
    [HarmonyPatch]
    public static class JobDriver_FeedBaby_Patch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            Type nestedClass = typeof(JobDriver_FeedBaby).GetNestedType("<>c__DisplayClass14_0", BindingFlags.NonPublic);
            return AccessTools.Method(nestedClass, "<GoToChair>b__0");
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiller(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            int pawnLoad = -1;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // An additional problem here is that 'this' is the internal class, so it is necessary to get the Pawn.
                // The variable is loaded several times in the function, so just find one case.
                if(pawnLoad == -1 && codes[ i ].opcode == OpCodes.Ldarg_0
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld && codes[ i + 1 ].operand.ToString().EndsWith( "__this" )
                    && codes[ i + 2 ].opcode == OpCodes.Ldfld && codes[ i + 2 ].operand.ToString() == "Verse.Pawn pawn" )
                {
                    pawnLoad = i;
                }

                // The function has code:
                // GenClosest.ClosestThingReachable(..., 32f, ...)
                // Replace it with:
                // GenClosest.ClosestThingReachable(..., Transpiller_Hook(32f, pawn), ...)
                if(pawnLoad != -1 && codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand.ToString() == "32"
                    && i + 11 < codes.Count
                    && codes[ i + 11 ].opcode == OpCodes.Call
                    && codes[ i + 11 ].operand.ToString().StartsWith( "Verse.Thing ClosestThingReachable(" ))
                {
                    // 32f is already on the stack, load 'pawn'.
                    codes.Insert( i + 1, codes[ pawnLoad ].Clone());
                    codes.Insert( i + 2, codes[ pawnLoad + 1 ].Clone());
                    codes.Insert( i + 3, codes[ pawnLoad + 2 ].Clone());
                    codes.Insert( i + 4, new CodeInstruction(OpCodes.Call, typeof(JobDriver_FeedBaby_Patch).GetMethod(nameof(Transpiller_Hook))));
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch JobDriver_FeedBaby.GoToChair() delegate");
            return codes;
        }

        // Multiply chair search radius, i.e. comfortable pawns try harder when searching for a place to sit.
        public static float Transpiller_Hook(float chairSearchRadius, Pawn pawn)
        {
            return ComfortHelper.AdjustChairSearchRadius( chairSearchRadius, pawn );
        }
    }

    // JobDriver_Reading has 32 hardcoded.
    [HarmonyPatch(typeof(JobDriver_Reading))]
    public static class JobDriver_Reading_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(JobDriver_Reading.TryGetClosestChairFreeSittingSpot))]
        public static IEnumerable<CodeInstruction> Transpiller(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // GenClosest.ClosestThingReachable(..., 32f, ...)
                // Replace it with:
                // GenClosest.ClosestThingReachable(..., Transpiller_Hook(32f, this), ...)
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if(codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand.ToString() == "32"
                    && i + 11 < codes.Count
                    && codes[ i + 11 ].opcode == OpCodes.Call
                    && codes[ i + 11 ].operand.ToString().StartsWith( "Verse.Thing ClosestThingReachable(" ))
                {
                    codes.Insert(i+1, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i+2, new CodeInstruction(OpCodes.Call, typeof(JobDriver_Reading_Patch).GetMethod(nameof(Transpiller_Hook))));
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch JobDriver_Reading.TryGetClosestChairFreeSittingSpot()");
            return codes;
        }

        // Multiply chair search radius, i.e. comfortable pawns try harder when searching for a place to sit.
        public static float Transpiller_Hook(float chairSearchRadius, JobDriver_Reading jobDriver)
        {
            return ComfortHelper.AdjustChairSearchRadius( chairSearchRadius, jobDriver.pawn );
        }
    }

    // Precept tooltip shows only some memory thought types, use situational, others depend on events.
    public class PreceptComp_MemoryThought : PreceptComp_SituationalThought
    {
    }

    // Say also what caused the thought in the tooltip (based on Thought_FoodEaten).
    public class Thought_Comfort : Thought_Memory
    {
        private string foodThoughtDescription;

        public override string Description => base.Description + foodThoughtDescription;

        public void SetThing(Thing thing)
        {
            foodThoughtDescription = "\n\n" + "MorePrecepts.ComfortProblemSource".Translate() + ": " + thing.LabelCap;
        }
    }

}
