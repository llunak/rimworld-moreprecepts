using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MorePrecepts
{
    // Mostly copy&paste of EatAtCannibalPlatter
    public class JobGiver_EatAtFeast : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_Ritual lordJob_Ritual = pawn.GetLord().LordJob as LordJob_Ritual;
            if (!GatheringsUtility.TryFindRandomCellAroundTarget(pawn, lordJob_Ritual.selectedTarget.Thing, out var result) && !GatheringsUtility.TryFindRandomCellInGatheringArea(pawn, CellValid, out result))
                return null;
            Job job = JobMaker.MakeJob(JobDefOf.EatAtFeast, lordJob_Ritual.selectedTarget.Thing, result);
            job.doUntilGatheringEnded = true;
            if (lordJob_Ritual != null)
                job.expiryInterval = lordJob_Ritual.DurationTicks;
            else
                job.expiryInterval = 2000;
            return job;

            bool CellValid(IntVec3 c)
            {
                foreach (IntVec3 item in GenRadial.RadialCellsAround(c, 1f, useCenter: true))
                {
                    if (!pawn.CanReserveAndReach(item, PathEndMode.OnCell, pawn.NormalMaxDanger()))
                        return false;
                    }
                return true;
            }
        }
    }

    public class JobDriver_EatAtFeast : JobDriver
    {
        private const TargetIndex PlatterIndex = TargetIndex.A;
        private const TargetIndex CellIndex = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.ReserveSittableOrSpot(job.targetB.Cell, job, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (!ModLister.CheckIdeology("Feast eat job"))
                yield break;
            this.EndOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.Goto(TargetIndex.B, PathEndMode.OnCell);
            float totalBuildingNutrition = base.TargetA.Thing.def.CostList.Sum((ThingDefCountClass x) => x.thingDef.GetStatValueAbstract(StatDefOf.Nutrition) * (float)x.count);
            Toil eat = new Toil();
            eat.tickAction = delegate
            {
                pawn.rotationTracker.FaceCell(base.TargetA.Thing.OccupiedRect().ClosestCellTo(pawn.Position));
                pawn.GainComfortFromCellIfPossible();
                if (pawn.needs.food != null)
                    pawn.needs.food.CurLevel += totalBuildingNutrition / (float)pawn.GetLord().ownedPawns.Count / (float)eat.defaultDuration;
            };
            // This is a bit crude, but it should do.
            bool hasMeat = base.TargetA.Thing.def.defName == "LavishFeast" || base.TargetA.Thing.def.defName == "LavishFeast_Meat";
            bool hasNonMeat = base.TargetA.Thing.def.defName == "LavishFeast" || base.TargetA.Thing.def.defName == "LavishFeast_Veg";
            eat.AddFinishAction(delegate
            {
                if(hasMeat)
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(RimWorld.HistoryEventDefOf.AteMeat, pawn.Named(HistoryEventArgsNames.Doer)));
                if(hasNonMeat)
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(RimWorld.HistoryEventDefOf.AteNonMeat, pawn.Named(HistoryEventArgsNames.Doer)));
            });
            eat.WithEffect(EffecterDefOf.EatVegetarian, TargetIndex.A);
            eat.PlaySustainerOrSound(SoundDefOf.Meal_Eat);
            eat.handlingFacing = true;
            eat.defaultCompleteMode = ToilCompleteMode.Delay;
            eat.defaultDuration = (job.doUntilGatheringEnded ? job.expiryInterval : job.def.joyDuration);
            yield return eat;
        }
    }

    public class RitualOutcomeComp_LavishMealsUsed : RitualOutcomeComp_QualitySingleOffset
    {
        private static bool HasLavishMeals(TargetInfo target)
        {
            return target.Thing.def.defName.StartsWith("LavishFeast");
        }

        public override bool Applies(LordJob_Ritual ritual)
        {
            return HasLavishMeals(ritual.selectedTarget);
        }

        public override ExpectedOutcomeDesc GetExpectedOutcomeDesc(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            bool has = HasLavishMeals(ritualTarget);
            return new ExpectedOutcomeDesc
            {
                label = LabelForDesc.CapitalizeFirst(),
                effect = ExpectedOffsetDesc(positive: has, has ? qualityOffset : 0),
                present = has,
                quality = has ? qualityOffset : 0,
                positive = has
            };
        }
    }

}
