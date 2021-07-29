using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace MorePrecepts
{

// TODO decide what to do when drugs vs alcohol conflict (such as drugs totaly disabled, alcohol allowed)

// Alcohol is normally considered to be a drug, and HistoryEventDef events include it. Patch code that sends
// those and duplicate them for our alcohol-specific events. They'll be duplicated, which should be fine.

    [HarmonyPatch(typeof(Bill_Medical))]
    public static class Bill_Medical_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnAllowedToStartAnew))]
        public static void PawnAllowedToStartAnew(ref bool __result, Bill_Medical __instance, Pawn pawn)
        {
            if( __result )
            {
                if (__instance.recipe.Worker is Recipe_AdministerIngestible)
                {
                    ThingDef singleDef = __instance.recipe.ingredients[0].filter.BestThingRequest.singleDef;
                    if (singleDef.IsDrug)
                    {
                        CompProperties_Drug compDrug = (CompProperties_Drug)singleDef.CompDefFor<CompDrug>();
                        // Disallow also if alcohol is prohibited.
                        if (compDrug != null && compDrug.chemical == ChemicalDefOf.Alcohol
                            && !new HistoryEvent(HistoryEventDefOf.AdministeredAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
                        {
                            __result = false;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_AdministerIngestible))]
    public static class Recipe_AdministerIngestible_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if(billDoer != null)
            {
                if (ingredients[0].def.IsDrug)
                {
                    CompProperties_Drug compDrug = (CompProperties_Drug)ingredients[0].def.CompDefFor<CompDrug>();
                    // Send also the notification about administering alcohol.
                    if (compDrug != null && compDrug.chemical == ChemicalDefOf.Alcohol)
                        Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.AdministeredAlcohol, billDoer.Named(HistoryEventArgsNames.Doer)));
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaravanPawnsNeedsUtility))]
    public static class CaravanPawnsNeedsUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CanEatForNutritionEver))]
        public static void CanEatForNutritionEver(ref bool __result, ThingDef food, Pawn pawn)
        {
            if( !__result )
                return;
            if (food.IsNutritionGivingIngestible && pawn.WillEat(food, null, careIfNotAcceptableForTitle: false)
                && (int)food.ingestible.preferability > 1 && (!food.IsDrug || !pawn.IsTeetotaler())
                && food.ingestible.canAutoSelectAsFoodForCaravan && food.IsDrug)
            {
                CompProperties_Drug compDrug = (CompProperties_Drug)food.CompDefFor<CompDrug>();
                // Allow also drinking alcohol unless prohibited.
                if (compDrug != null && compDrug.chemical == ChemicalDefOf.Alcohol
                    && new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    __result = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnUtility))]
    public static class PawnUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(IsTeetotaler))]
        public static void IsTeetotaler(ref bool __result, Pawn pawn)
        {
            if (!new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(CompDrug))]
    public static class CompDrug_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PostIngested))]
        public static void PostIngested(CompDrug __instance, Pawn ingester)
        {
            CompProperties_Drug compDrug = (CompProperties_Drug)__instance.parent.def.CompDefFor<CompDrug>();
            if (compDrug != null && compDrug.chemical == ChemicalDefOf.Alcohol)
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, ingester.Named(HistoryEventArgsNames.Doer)));
        }
    }

// TODO FloatMenuMakerMap.AddHumanlikeOrders()

// These are basically copy&paste&modify of HighLife classes, split into two classes based on the constants.
// The wanted class more or less matches HighLife settings, the Essential gets unhappy more quickly.
	public class ThoughtWorker_Precept_Alcohol_Wanted : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
	{
		private const float DaysSatisfied = 0.75f;

		private const float DaysNoBonus = 1f;

		private const float DaysMissing = 2f;

		private const float DaysMissing_Major = 11f;

		public static readonly SimpleCurve MoodOffsetFromDaysSinceLastDrugCurve = new SimpleCurve
		{
			new CurvePoint(0.75f, 3f),
			new CurvePoint(1f, 0f),
			new CurvePoint(2f, -1f),
			new CurvePoint(11f, -10f)
		};

		protected override ThoughtState ShouldHaveThought(Pawn p)
		{
			if (!ThoughtUtility.ThoughtNullified(p, def))
			{
				float num = (float)(Find.TickManager.TicksGame - p.mindState.lastTakeRecreationalDrugTick) / 60000f;
				if (num > 1f && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
				{
					return false;
				}
				if (num < 1f)
				{
					return ThoughtState.ActiveAtStage(0);
				}
				if (num < 2f)
				{
					return ThoughtState.ActiveAtStage(1);
				}
				if (num < 11f)
				{
					return ThoughtState.ActiveAtStage(2);
				}
				return ThoughtState.ActiveAtStage(3);
			}
			return false;
		}

		public IEnumerable<NamedArgument> GetDescriptionArgs()
		{
			yield return 0.75f.Named("DAYSSATISIFED");
		}
	}

	public class ThoughtWorker_Precept_Alcohol_Essential : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
	{
		private const float DaysSatisfied = 0.75f;

		private const float DaysNoBonus = 1f;

		private const float DaysMissing = 1.2f;

		private const float DaysMissing_Major = 3f;

		public static readonly SimpleCurve MoodOffsetFromDaysSinceLastDrugCurve = new SimpleCurve
		{
			new CurvePoint(0.75f, 3f),
			new CurvePoint(1f, 0f),
			new CurvePoint(1.2f, -1f),
			new CurvePoint(3f, -10f)
		};

		protected override ThoughtState ShouldHaveThought(Pawn p)
		{
			if (!ThoughtUtility.ThoughtNullified(p, def))
			{
				float num = (float)(Find.TickManager.TicksGame - p.mindState.lastTakeRecreationalDrugTick) / 60000f;
				if (num > 1f && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
				{
					return false;
				}
				if (num < 1f)
				{
					return ThoughtState.ActiveAtStage(0);
				}
				if (num < 1.2f)
				{
					return ThoughtState.ActiveAtStage(1);
				}
				if (num < 3f)
				{
					return ThoughtState.ActiveAtStage(2);
				}
				return ThoughtState.ActiveAtStage(3);
			}
			return false;
		}

		public IEnumerable<NamedArgument> GetDescriptionArgs()
		{
			yield return 0.75f.Named("DAYSSATISIFED");
		}
	}

// Again copy&paste&modify from HighLife.
	public class Thought_Situational_Precept_Alcohol_Wanted : Thought_Situational
	{
		protected override float BaseMoodOffset
		{
			get
			{
				if (ThoughtUtility.ThoughtNullified(pawn, def))
				{
					return 0f;
				}
				float x = (float)(Find.TickManager.TicksGame - pawn.mindState.lastTakeRecreationalDrugTick) / 60000f;
				return Mathf.RoundToInt(ThoughtWorker_Precept_Alcohol_Wanted.MoodOffsetFromDaysSinceLastDrugCurve.Evaluate(x));
			}
		}
	}

	public class Thought_Situational_Precept_Alcohol_Essential : Thought_Situational
	{
		protected override float BaseMoodOffset
		{
			get
			{
				if (ThoughtUtility.ThoughtNullified(pawn, def))
				{
					return 0f;
				}
				float x = (float)(Find.TickManager.TicksGame - pawn.mindState.lastTakeRecreationalDrugTick) / 60000f;
				return Mathf.RoundToInt(ThoughtWorker_Precept_Alcohol_Essential.MoodOffsetFromDaysSinceLastDrugCurve.Evaluate(x));
			}
		}
	}

}
