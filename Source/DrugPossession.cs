using UnityEngine;
using System.Linq;
using RimWorld;
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

        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (!ThoughtUtility.ThoughtNullified(pawn, def))
            {
                Thing thing = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Drug)
                    .FirstOrDefault((Thing t) => IsRelevantDrug(t));
                if(thing == null)
                {
                    PawnComp.SetNoticedDrugsTick(pawn, -1);
                    return false;
                }
                PawnComp.SetNoticedDrugsTickIfNotSet(pawn, Find.TickManager.TicksGame);
                return ThoughtState.ActiveAtStage(0);
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

}
