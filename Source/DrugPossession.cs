using UnityEngine;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MorePrecepts
{
    public class Comp_DrugSelling : ThingComp
    {
        public override void PrePreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
        {
            base.PrePreTraded(action, playerNegotiator, trader);
            if(action == TradeAction.None)
                return;
            if(!parent.def.IsNonMedicalDrug)
                return;
            // Any drug.
            Find.HistoryEventsManager.RecordEvent(
                new HistoryEvent(HistoryEventDefOf.DrugPossession_TradedDrug,
                    playerNegotiator.Named(HistoryEventArgsNames.Doer)));
            // Non-alcoholic drug.
            if (!AlcoholHelper.IsAlcohol(parent.def))
                Find.HistoryEventsManager.RecordEvent(
                    new HistoryEvent(HistoryEventDefOf.DrugPossession_TradedNonAlcoholDrug,
                        playerNegotiator.Named(HistoryEventArgsNames.Doer)));
            // Hard drug.
            if(parent.def.ingestible.drugCategory == DrugCategory.Hard)
                Find.HistoryEventsManager.RecordEvent(
                    new HistoryEvent(HistoryEventDefOf.DrugPossession_TradedHardDrug,
                        playerNegotiator.Named(HistoryEventArgsNames.Doer)));
        }
    }

    public abstract class ThoughtWorker_Precept_DrugPossession_Base : ThoughtWorker_Precept
    {
        protected abstract bool IsRelevantDrug(Thing thing);

        private const float HoursUntilFull = 24f;

        public static readonly SimpleCurve MoodOffsetFromHoursSinceNoticedDrugsCurve = new SimpleCurve
        {
            new CurvePoint(0, 1f),
            new CurvePoint(HoursUntilFull, 10f)
        };

        private bool PawnHasDrugs(Pawn pawn)
        {
            foreach(Thing thing in pawn.inventory.innerContainer)
                if(IsRelevantDrug(thing))
                    return true;
            return false;
        }

        private bool CaravanHasDrugs(Pawn pawn)
        {
            if(!pawn.IsCaravanMember())
                return false;
            foreach(Pawn otherPawn in CaravanUtility.GetCaravan(pawn).PawnsListForReading)
                if(PawnHasDrugs(otherPawn))
                    return true;
            return false;
        }

        private bool PawnsHaveDrugs(Pawn pawn)
        {
            foreach(Pawn otherPawn in pawn.Map.mapPawns.FreeColonistsAndPrisoners)
                if(PawnHasDrugs(otherPawn))
                    return true;
            return false;
        }

        private bool HomeMapHasDrugs(Pawn pawn)
        {
            if(!pawn.Map.IsPlayerHome)
                return false;
            Thing thing = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Drug)
                .FirstOrDefault((Thing t) => IsRelevantDrug(t));
            return thing != null;
        }

        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (!ThoughtUtility.ThoughtNullified(pawn, def))
            {
                if(CaravanHasDrugs(pawn) || PawnsHaveDrugs(pawn) || HomeMapHasDrugs(pawn))
                {
                    PawnComp.SetNoticedDrugsTickIfNotSet(pawn, Find.TickManager.TicksGame);
                    return ThoughtState.ActiveAtStage(0);
                }
                PawnComp.SetNoticedDrugsTick(pawn, -1);
                return false;
            }
            return false;
        }

        public override float MoodMultiplier(Pawn pawn)
        {
            float x = (float)(Find.TickManager.TicksGame - PawnComp.GetNoticedDrugsTick(pawn)) / GenDate.TicksPerHour;
            return Mathf.RoundToInt(MoodOffsetFromHoursSinceNoticedDrugsCurve.Evaluate(x));
        }
    }

    public class ThoughtWorker_Precept_DrugPossession_Prohibited : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing) => thing.def.IsNonMedicalDrug;
    }

    public class ThoughtWorker_Precept_DrugPossession_Alcohol : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing)
            =>  !thing.def.IsNonMedicalDrug && !AlcoholHelper.IsAlcohol(thing.def);
    }

    public class ThoughtWorker_Precept_DrugPossession_Social : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing)
            => thing.def.IsNonMedicalDrug && thing.def.ingestible.drugCategory == DrugCategory.Hard;
    }

    public class ThoughtWorker_Precept_DrugPossession_HasDrugs : ThoughtWorker
    {
        private delegate bool IsRelevantDrug(Thing thing);

        private bool PawnHasDrugs(Pawn pawn, IsRelevantDrug isRelevantDrug)
        {
            foreach(Thing thing in pawn.inventory.innerContainer)
                if(isRelevantDrug(thing))
                    return true;
            return false;
        }

        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (!pawn.RaceProps.Humanlike || !other.RaceProps.Humanlike)
                return false;
            if(pawn.Ideo == null)
                return false;
            IsRelevantDrug isRelevantDrug = null;
            if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Prohibited))
                isRelevantDrug = delegate(Thing thing)
                {
                    return thing.def.IsNonMedicalDrug;
                };
            else if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Alcohol))
                isRelevantDrug = delegate(Thing thing)
                {
                    return !thing.def.IsNonMedicalDrug && !AlcoholHelper.IsAlcohol(thing.def);
                };
            else if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Social))
                isRelevantDrug = delegate(Thing thing)
                {
                    return thing.def.IsNonMedicalDrug && thing.def.ingestible.drugCategory == DrugCategory.Hard;
                };
            if( isRelevantDrug == null )
                return false;
            if (!RelationsUtility.PawnsKnowEachOther(pawn, other))
                return false;
            return PawnHasDrugs(other, isRelevantDrug);
        }
    }

}
