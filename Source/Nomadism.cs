using HarmonyLib;
using System;
using System.Collections.Generic;
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

    // Wanted aims to move once a year.
    public class ThoughtWorker_Precept_Nomadism_Wanted : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        // The values are also hardcoded in the XML.
        private const float DaysSatisfied = 5f;
        private const float DaysNoBonus = 10f;
        private const float DaysMissing = 46f;
        private const float DaysMissing_Minor = 60f;
        private const float DaysMissing_Major = 65f;

        public static readonly SimpleCurve MoodOffsetFromDaysSinceLastDrugCurve = new SimpleCurve
        {
            new CurvePoint(DaysSatisfied, 5f),
            new CurvePoint(DaysNoBonus, 0f),
            new CurvePoint(DaysMissing, 0f),
            new CurvePoint(DaysMissing_Minor, -1f),
            new CurvePoint(DaysMissing_Major, -10f)
        };

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                bool inSettlement;
                float num = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(p, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return ThoughtState.ActiveAtStage(1);
                if (num > DaysSatisfied && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysSatisfied)
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysNoBonus)
                    return ThoughtState.ActiveAtStage(1);
                if (num < DaysMissing)
                    return ThoughtState.ActiveAtStage(2);
                if (num < DaysMissing_Minor)
                    return ThoughtState.ActiveAtStage(3);
                return ThoughtState.ActiveAtStage(4);
            }
            return false;
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing_Minor.Named("DAYSCHANGED");
        }
    }

    // Important aims to move once every half a year.
    public class ThoughtWorker_Precept_Nomadism_Important : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        // The values are also hardcoded in the XML.
        private const float DaysSatisfied = 5f;
        private const float DaysNoBonus = 10f;
        private const float DaysMissing = 25f;
        private const float DaysMissing_Minor = 30f;
        private const float DaysMissing_Major = 35f;

        public static readonly SimpleCurve MoodOffsetFromDaysSinceLastDrugCurve = new SimpleCurve
        {
            new CurvePoint(DaysSatisfied, 7f),
            new CurvePoint(DaysNoBonus, 0f),
            new CurvePoint(DaysMissing, -1f),
            new CurvePoint(DaysMissing_Minor, -1f),
            new CurvePoint(DaysMissing_Major, -10f)
        };

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                bool inSettlement;
                float num = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(p, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return ThoughtState.ActiveAtStage(1);
                if (num > DaysSatisfied && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysSatisfied)
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysNoBonus)
                    return ThoughtState.ActiveAtStage(1);
                if (num < DaysMissing)
                    return ThoughtState.ActiveAtStage(2);
                if (num < DaysMissing_Minor)
                    return ThoughtState.ActiveAtStage(3);
                return ThoughtState.ActiveAtStage(4);
            }
            return false;
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing_Minor.Named("DAYSCHANGED");
        }
    }

    // Essential aims to move once every quadrum.
    public class ThoughtWorker_Precept_Nomadism_Essential : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        // The values are also hardcoded in the XML.
        private const float DaysSatisfied = 3f;
        private const float DaysNoBonus = 5f;
        private const float DaysMissing = 10f;
        private const float DaysMissing_Minor = 15f;
        private const float DaysMissing_Major = 17f;

        public static readonly SimpleCurve MoodOffsetFromDaysSinceLastDrugCurve = new SimpleCurve
        {
            new CurvePoint(DaysSatisfied, 10f),
            new CurvePoint(DaysNoBonus, 0f),
            new CurvePoint(DaysMissing, 0f),
            new CurvePoint(DaysMissing_Minor, -1f),
            new CurvePoint(DaysMissing_Major, -10f)
        };

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                bool inSettlement;
                float num = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(p, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return ThoughtState.ActiveAtStage(1);
                if (num > DaysSatisfied && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysSatisfied)
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysNoBonus)
                    return ThoughtState.ActiveAtStage(1);
                if (num < DaysMissing)
                    return ThoughtState.ActiveAtStage(2);
                if (num < DaysMissing_Minor)
                    return ThoughtState.ActiveAtStage(3);
                return ThoughtState.ActiveAtStage(4);
            }
            return false;
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing_Minor.Named("DAYSCHANGED");
        }
    }

// Again copy&paste&modify from HighLife.
    public class Thought_Situational_Precept_Nomadism_Wanted : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                if (ThoughtUtility.ThoughtNullified(pawn, def))
                    return 0f;
                bool inSettlement;
                float x = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(pawn, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return 0;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Nomadism_Wanted.MoodOffsetFromDaysSinceLastDrugCurve.Evaluate(x));
            }
        }
    }

    public class Thought_Situational_Precept_Nomadism_Important : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                if (ThoughtUtility.ThoughtNullified(pawn, def))
                    return 0f;
                bool inSettlement;
                float x = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(pawn, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return 0;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Nomadism_Important.MoodOffsetFromDaysSinceLastDrugCurve.Evaluate(x));
            }
        }
    }

    public class Thought_Situational_Precept_Nomadism_Essential : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                if (ThoughtUtility.ThoughtNullified(pawn, def))
                    return 0f;
                bool inSettlement;
                float x = (float)(Find.TickManager.TicksGame - SettlementCreationTick.Get(pawn, out inSettlement)) / 60000f;
                if(!inSettlement)
                    return 0;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Nomadism_Essential.MoodOffsetFromDaysSinceLastDrugCurve.Evaluate(x));
            }
        }
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
}
