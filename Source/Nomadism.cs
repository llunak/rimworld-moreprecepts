using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace MorePrecepts
{

    public static class SettlementCreationTick
    {
        public static int Get(Pawn pawn, out bool inSettlement)
        {
            inSettlement = false;
            if(pawn.Map == null)
                return -1;
            Settlement settlement = Find.WorldObjects.WorldObjectAt<Settlement>(pawn.Map.Tile);
            if(settlement == null)
                return -1;
            inSettlement = true;
            return settlement.creationGameTicks;
        }
    }

    [HarmonyPatch(typeof(Settlement))]
    public static class Settlement_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Abandon))]
        public static bool Abandon(Settlement __instance)
        {
            if(__instance.Faction == Faction.OfPlayer)
            {
                foreach(Pawn pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_Colonists)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Nomadism_AbandonedSettlement,
                        pawn.Named(HistoryEventArgsNames.Doer)));
                }
            }
            return true;
        }
    }

    public abstract class ThoughtWorker_Precept_Nomadism_Base : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        protected abstract float DaysSatisfied();
        protected abstract float DaysNoBonus();
        protected abstract float DaysMissing();
        protected abstract float DaysMissing_Minor();
        protected abstract float DaysMissing_Major();
        protected abstract SimpleCurve MoodOffsetFromDaysSinceSettledCurve();

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                bool inSettlement;
                float num = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(p, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return ThoughtState.ActiveAtStage(1);
                if (num > DaysNoBonus() && def.minExpectationForNegativeThought != null
                    && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysNoBonus())
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysMissing())
                    return ThoughtState.ActiveAtStage(1);
                return ThoughtState.ActiveAtStage(2);
            }
            return false;
        }

        public override float MoodMultiplier(Pawn pawn)
        {
            bool inSettlement;
            float num = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(pawn, out inSettlement)) / 60000f;
            if(!inSettlement)
                return 0;
            return Mathf.RoundToInt(MoodOffsetFromDaysSinceSettledCurve().Evaluate(num));
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing_Minor().Named("DAYSCHANGED");
        }
    }

    // Wanted aims to move once a year.
    public class ThoughtWorker_Precept_Nomadism_Wanted : ThoughtWorker_Precept_Nomadism_Base
    {
        protected override float DaysSatisfied() => 5f;
        protected override float DaysNoBonus() => 10f;
        protected override float DaysMissing() => 46f;
        protected override float DaysMissing_Minor() => 60f;
        protected override float DaysMissing_Major() => 65f;
        protected override SimpleCurve MoodOffsetFromDaysSinceSettledCurve() => StaticMoodOffsetFromDaysSinceSettledCurve;
        public const float StaticDaysMissing = 46f;
        public const float StaticDaysMissing_Minor = 60f;

        public static readonly SimpleCurve StaticMoodOffsetFromDaysSinceSettledCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(5f, 1f),
            new CurvePoint(10f, 0f),
            new CurvePoint(46f, 1f),
            new CurvePoint(60f, 1f),
            new CurvePoint(65f, 10f)
        };
    }

    // Important aims to move once every half a year.
    public class ThoughtWorker_Precept_Nomadism_Important : ThoughtWorker_Precept_Nomadism_Base
    {
        protected override float DaysSatisfied() => 5f;
        protected override float DaysNoBonus() => 10f;
        protected override float DaysMissing() => 25f;
        protected override float DaysMissing_Minor() => 30f;
        protected override float DaysMissing_Major() => 35f;
        protected override SimpleCurve MoodOffsetFromDaysSinceSettledCurve() => StaticMoodOffsetFromDaysSinceSettledCurve;
        public const float StaticDaysMissing = 25f;
        public const float StaticDaysMissing_Minor = 30f;

        public static readonly SimpleCurve StaticMoodOffsetFromDaysSinceSettledCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(5f, 1f),
            new CurvePoint(10f, 0f),
            new CurvePoint(25f, 1f),
            new CurvePoint(30f, 1f),
            new CurvePoint(35f, 10f)
        };
    }

    // Essential aims to move once every quadrum.
    public class ThoughtWorker_Precept_Nomadism_Essential : ThoughtWorker_Precept_Nomadism_Base
    {
        protected override float DaysSatisfied() => 3f;
        protected override float DaysNoBonus() => 5f;
        protected override float DaysMissing() => 10f;
        protected override float DaysMissing_Minor() => 15f;
        protected override float DaysMissing_Major() => 17f;
        protected override SimpleCurve MoodOffsetFromDaysSinceSettledCurve() => StaticMoodOffsetFromDaysSinceSettledCurve;
        public const float StaticDaysMissing = 10f;
        public const float StaticDaysMissing_Minor = 15f;

        public static readonly SimpleCurve StaticMoodOffsetFromDaysSinceSettledCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(3f, 1f),
            new CurvePoint(5f, 0f),
            new CurvePoint(10f, 1f),
            new CurvePoint(15f, 1f),
            new CurvePoint(17f, 10f)
        };
    }

    // Block happy thought about having abandoned a settlement when in a settlement.
    public class Thought_Nomadism_BlockInSettlement : Thought_Memory
    {
        private bool IsInSettlement()
        {
                bool inSettlement;
                SettlementCreationTick.Get(pawn, out inSettlement);
                return inSettlement;
        }

        public override bool VisibleInNeedsTab
        {
            get
            {
                if(base.VisibleInNeedsTab)
                    return !IsInSettlement();
                return false;
            }
        }

        public override float MoodOffset()
        {
            if(ThoughtUtility.ThoughtNullified(pawn, def))
                return 0f;
            if(IsInSettlement())
                return 0f;
            return base.MoodOffset();
        }
    }

    public class Alert_SettlementToLeave : Alert
    {
        // Pawns and description of how long they've been in the settlement they're in.
        private Dictionary<Pawn, string> affectedPawns = new Dictionary<Pawn,string>();

        public Alert_SettlementToLeave()
        {
            defaultLabel = "MorePrecepts.AlertSettlementToLeave".Translate();
        }

        private void UpdatePawns()
        {
            affectedPawns.Clear();
            foreach(Pawn pawn in PawnsFinder.HomeMaps_FreeColonistsSpawned)
            {
                if(pawn.Ideo == null)
                    continue;
                ThoughtDef thought = null;
                if(pawn.Ideo.HasPrecept(PreceptDefOf.Nomadism_Wanted) && !ThoughtUtility.ThoughtNullified(pawn, ThoughtDefOf.Nomadism_Wanted))
                    thought = ThoughtDefOf.Nomadism_Wanted;
                if(pawn.Ideo.HasPrecept(PreceptDefOf.Nomadism_Important) && !ThoughtUtility.ThoughtNullified(pawn, ThoughtDefOf.Nomadism_Important))
                    thought = ThoughtDefOf.Nomadism_Important;
                if(pawn.Ideo.HasPrecept(PreceptDefOf.Nomadism_Essential) && !ThoughtUtility.ThoughtNullified(pawn, ThoughtDefOf.Nomadism_Essential))
                    thought = ThoughtDefOf.Nomadism_Essential;
                if(thought == null)
                    continue;
                 if (thought.minExpectationForNegativeThought != null && pawn.MapHeld != null
                    && ExpectationsUtility.CurrentExpectationFor(pawn.MapHeld).order < thought.minExpectationForNegativeThought.order)
                    continue;
                bool inSettlement;
                int days = (Find.TickManager.TicksGame - SettlementCreationTick.Get(pawn, out inSettlement)) / 60000;
                if(!inSettlement)
                    continue;
                int max = -1;
                if(thought == ThoughtDefOf.Nomadism_Wanted && days > ThoughtWorker_Precept_Nomadism_Wanted.StaticDaysMissing)
                    max = (int)ThoughtWorker_Precept_Nomadism_Wanted.StaticDaysMissing_Minor;
                if(thought == ThoughtDefOf.Nomadism_Important && days > ThoughtWorker_Precept_Nomadism_Important.StaticDaysMissing)
                    max = (int)ThoughtWorker_Precept_Nomadism_Important.StaticDaysMissing_Minor;
                if(thought == ThoughtDefOf.Nomadism_Essential && days > ThoughtWorker_Precept_Nomadism_Essential.StaticDaysMissing)
                    max = (int)ThoughtWorker_Precept_Nomadism_Essential.StaticDaysMissing_Minor;
                if(max > 0)
                    affectedPawns.Add(pawn, string.Format(
                        "MorePrecepts.AlertSettlementToLeaveColonistInfo".Translate(),
                        pawn.NameShortColored.Resolve(), days, max));
            }
        }

        public override TaggedString GetExplanation()
        {
            return "MorePrecepts.AlertSettlementToLeaveDesc"
                .Translate(affectedPawns.Select(v => v.Value).ToLineList(" - "));
        }

        public override AlertReport GetReport()
        {
            UpdatePawns();
            return AlertReport.CulpritsAre(affectedPawns.Keys.ToList());
        }
    }

}
