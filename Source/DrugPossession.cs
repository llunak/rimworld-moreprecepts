using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MorePrecepts
{
    public static class DrugPossessionHelper
    {
        public delegate bool IsRelevantDrug(Thing thing);

        public static bool PawnHasDrugs(Pawn pawn, IsRelevantDrug isRelevantDrug, ref Thing drug)
        {
            foreach(Thing thing in pawn.inventory.innerContainer)
            {
                if(isRelevantDrug(thing))
                {
                    drug = thing;
                    return true;
                }
            }
            return false;
        }

        public static bool CaravanHasDrugs(Pawn pawn, IsRelevantDrug isRelevantDrug, ref Thing drug)
        {
            if(!pawn.IsCaravanMember())
                return false;
            foreach(Pawn otherPawn in CaravanUtility.GetCaravan(pawn).PawnsListForReading)
                if(PawnHasDrugs(otherPawn, isRelevantDrug, ref drug))
                    return true;
            return false;
        }

        public static bool MapPawnsHaveDrugs(Pawn pawn, IsRelevantDrug isRelevantDrug, ref Thing drug)
        {
            if(pawn.Map == null)
                return false;
            foreach(Pawn otherPawn in pawn.Map.mapPawns.FreeColonistsAndPrisoners)
                if(PawnHasDrugs(otherPawn, isRelevantDrug, ref drug))
                    return true;
            return false;
        }

        public static bool HomeMapHasDrugs(Pawn pawn, IsRelevantDrug isRelevantDrug, ref Thing drug)
        {
            if(pawn.Map == null || !pawn.Map.IsPlayerHome)
                return false;
            Thing thing = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Drug)
                .FirstOrDefault((Thing t) => isRelevantDrug(t));
            drug = thing;
            return thing != null;
        }

        public static bool IsDrug(Thing thing)
        {
            return thing.def.IsNonMedicalDrug;
        }

        public static bool IsNonAlcohol(Thing thing)
        {
            return thing.def.IsNonMedicalDrug && !AlcoholHelper.IsAlcohol(thing.def);
        }

        public static bool IsHardDrug(Thing thing)
        {
            return thing.def.IsNonMedicalDrug && thing.def.ingestible.drugCategory == DrugCategory.Hard;
        }

        public static IsRelevantDrug GetDrugTestDelegate(Pawn pawn)
        {
            if(pawn.Ideo == null)
                return null;
            if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Prohibited))
                return IsDrug;
            else if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Alcohol))
                return IsNonAlcohol;
            else if(pawn.Ideo.HasPrecept(PreceptDefOf.DrugPossession_Social))
                return IsHardDrug;
            return null;
        }
    }

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
                DrugPossessionHelper.IsRelevantDrug isRelevantDrug = DrugPossessionHelper.GetDrugTestDelegate(pawn);
                if( isRelevantDrug != null )
                {
                    Thing dummy = null;
                    if(DrugPossessionHelper.CaravanHasDrugs(pawn, isRelevantDrug, ref dummy)
                        || DrugPossessionHelper.MapPawnsHaveDrugs(pawn, isRelevantDrug, ref dummy)
                        || DrugPossessionHelper.HomeMapHasDrugs(pawn, isRelevantDrug, ref dummy))
                    {
                        PawnComp.SetNoticedDrugsTickIfNotSet(pawn, Find.TickManager.TicksGame);
                        return ThoughtState.ActiveAtStage(0);
                    }
                }
                PawnComp.SetNoticedDrugsTick(pawn, -1);
                return false;
            }
            return false;
        }

        public override float MoodMultiplier(Pawn pawn)
        {
            float x = (float)(Find.TickManager.TicksGame - PawnComp.GetNoticedDrugsTick(pawn)) / GenDate.TicksPerHour;
            return MoodOffsetFromHoursSinceNoticedDrugsCurve.Evaluate(x);
        }
    }

    public class ThoughtWorker_Precept_DrugPossession_Prohibited : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing) => DrugPossessionHelper.IsDrug(thing);
    }

    public class ThoughtWorker_Precept_DrugPossession_Alcohol : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing) => DrugPossessionHelper.IsNonAlcohol(thing);
    }

    public class ThoughtWorker_Precept_DrugPossession_Social : ThoughtWorker_Precept_DrugPossession_Base
    {
        protected override bool IsRelevantDrug(Thing thing) => DrugPossessionHelper.IsHardDrug(thing);
    }

    public class ThoughtWorker_Precept_DrugPossession_HasDrugs : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (!pawn.RaceProps.Humanlike || !other.RaceProps.Humanlike)
                return false;
            DrugPossessionHelper.IsRelevantDrug isRelevantDrug = DrugPossessionHelper.GetDrugTestDelegate(pawn);
            if( isRelevantDrug == null )
                return false;
            if (!RelationsUtility.PawnsKnowEachOther(pawn, other))
                return false;
            Thing dummy = null;
            return DrugPossessionHelper.PawnHasDrugs(other, isRelevantDrug, ref dummy);
        }
    }

    public class Alert_DrugPossession : Alert
    {
        // Pawns disliking drugs.
        private List<Pawn> affectedPawns = new List<Pawn>();
        // Disliked drugs (not necessarily all of them).
        private List<Thing> affectedThings = new List<Thing>();

        public Alert_DrugPossession()
        {
            defaultLabel = "MorePrecepts.AlertDrugsPresent".Translate();
        }

        private void Update()
        {
            affectedPawns.Clear();
            affectedThings.Clear();
            foreach(Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoLodgers)
            {
                DrugPossessionHelper.IsRelevantDrug isRelevantDrug = DrugPossessionHelper.GetDrugTestDelegate(pawn);
                if(isRelevantDrug == null)
                    continue;
                Thing drug = null;
                if(DrugPossessionHelper.CaravanHasDrugs(pawn, isRelevantDrug, ref drug)
                    || DrugPossessionHelper.MapPawnsHaveDrugs(pawn, isRelevantDrug, ref drug)
                    || DrugPossessionHelper.HomeMapHasDrugs(pawn, isRelevantDrug, ref drug))
                {
                    affectedPawns.Add(pawn);
                    if(!affectedThings.Contains(drug))
                        affectedThings.Add(drug);
                }
            }
        }

        public override TaggedString GetExplanation()
        {
            return "MorePrecepts.AlertDrugsPresentDesc"
                .Translate(
                    affectedThings.Select(v => v.LabelShort).ToLineList(" - "),
                    affectedPawns.Select(v => v.NameShortColored.Resolve()).ToLineList(" - "));
        }

        public override AlertReport GetReport()
        {
            Update();
            return AlertReport.CulpritsAre(affectedThings.Concat(affectedPawns).ToList());
        }
    }

}
