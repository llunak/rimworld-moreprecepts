using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MorePrecepts
{

    [HarmonyPatch(typeof(Thing))]
    public static class Thing2_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Ingested))]
        public static void Ingested(Thing __instance, Pawn ingester, float nutritionWanted)
        {
            LordJob_Ritual_Feast lordJob_Ritual = ingester.GetLord()?.LordJob as LordJob_Ritual_Feast;
            if (lordJob_Ritual != null)
                lordJob_Ritual.AddEatenMeal(__instance);
        }
    }

    // Extra worker and lordjob for keeping track of meals eaten during the feast.
    public class RitualBehaviorWorker_Feast : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Feast()
        {
        }

        public RitualBehaviorWorker_Feast(RitualBehaviorDef def)
            : base(def)
        {
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            return new LordJob_Ritual_Feast(target, ritual, obligation, def.stages, assignments, organizer);
        }
    }

    public class LordJob_Ritual_Feast : LordJob_Ritual
    {
        public int preferabilitySum = 0;
        public int mealsCount = 0;

        public LordJob_Ritual_Feast()
        {
        }

        public LordJob_Ritual_Feast(TargetInfo selectedTarget, Precept_Ritual ritual, RitualObligation obligation, List<RitualStage> allStages, RitualRoleAssignments assignments, Pawn organizer = null)
            : base(selectedTarget, ritual, obligation, allStages, assignments, organizer)
        {
        }

        public void AddEatenMeal(Thing thing)
        {
            preferabilitySum += (int)thing.def.ingestible.preferability; // 0-9
            ++mealsCount;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref preferabilitySum, "preferabilitySum", 0);
            Scribe_Values.Look(ref mealsCount, "mealsCount", 0);
        }
    }

    public class RitualOutcomeComp_FeastMeals : RitualOutcomeComp_Quality
    {
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            LordJob_Ritual_Feast lordJob = ritual as LordJob_Ritual_Feast;
            if(lordJob == null)
                return 0;
            int participantCount = 0;
            foreach (Pawn item in ritual.PawnsToCountTowardsPresence)
            {
                if (ritual.Ritual != null)
                {
                    RitualRole ritualRole = ritual.RoleFor(item, includeForced: true);
                    if (ritualRole != null && !ritualRole.countsAsParticipant)
                        continue;
                }
                ++participantCount;
            }
            if(lordJob.mealsCount < participantCount)
                return 0; // Not enough meals.
            return lordJob.preferabilitySum * 10 / lordJob.mealsCount; // map to average 0-90
        }

        public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
        {
            return new QualityFactor
            {
                label = label.CapitalizeFirst(),
                present = false,
                uncertainOutcome = true,
                qualityChange = ExpectedOffsetDesc(positive: true, 0),
                positive = true
            };
        }
    }

    // This is JobGiver_EatInGatheringArea, but eats more often.
    public class JobGiver_FeastInGatheringArea : JobGiver_EatInGatheringArea
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            PawnDuty duty = pawn.mindState.duty;
            if (duty == null)
            {
                return null;
            }
            if ((double)pawn.needs.food.CurLevelPercentage > 0.95)
            {
                return null;
            }
            IntVec3 cell = duty.focus.Cell;
            Thing thing = FindFood(pawn, cell);
            if (thing == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(RimWorld.JobDefOf.Ingest, thing);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, thing.def, thing.def.GetStatValueAbstract(StatDefOf.Nutrition));
            return job;
        }
    }

}
