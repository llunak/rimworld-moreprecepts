using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MorePrecepts
{

// this is complicated, disbable for now
#if false

/*
Alcohol is normally considered to be a drug, and HistoryEventDef events include it. Patch code that sends
those and duplicate them for our alcohol-specific events. This means the events will be duplicated, so
it must be ensured that handling of those events does not conflict. For that reason, alcohol precept
should completely override the drug precept (i.e. it's possible to set alcohol as neutral even if
drug use is set to medical only).
*/

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
// TODO: strange? why true below?
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

/*    [HarmonyPatch(typeof(PawnUtility))]
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
*/
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

    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    public static class FloatMenuMakerMap_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AddHumanlikeOrders))]
        public static void AddHumanlikeOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            // The functions may block the menu entry for drinking alcohol. It's quite nested in the function, making it hard(?)
            // to replace, so just post-process the menu entries.
            List<Pair<string,Thing>> alcoholItems = new List<Pair<string,Thing>>();
            IntVec3 c = IntVec3.FromVector3(clickPos);
            foreach (Thing thing7 in c.GetThingList(pawn.Map))
            {
                Thing t2 = thing7;
                CompProperties_Drug compDrug = (CompProperties_Drug)t2.def.CompDefFor<CompDrug>();
                if (compDrug == null || compDrug.chemical != ChemicalDefOf.Alcohol)
                    continue; // not alcohol, ignore
                if (t2.def.ingestible == null || !pawn.RaceProps.CanEverEat(t2) || !t2.IngestibleNow)
                    continue;
                string text = ((!t2.def.ingestible.ingestCommandString.NullOrEmpty()) ? string.Format(t2.def.ingestible.ingestCommandString, t2.LabelShort) : ((string)"ConsumeThing".Translate(t2.LabelShort, t2)));
                alcoholItems.Add(new Pair<String,Thing>(text, t2));
            }
            if(alcoholItems.Count == 0)
                return;
            Log.Message("M1:" + alcoholItems.Count);
            for( int i = 0; i < opts.Count; ++i )
            {
                Log.Message("M2:" + opts[i].Label);
                for( int j = 0; j < alcoholItems.Count; ++j )
                {
                    string text = alcoholItems[j].First;
                    if(opts[i].Label == text || opts[i].Label.StartsWith(text + ":"))
                    {
                        Thing t2 = alcoholItems[j].Second;
                        // Big scary copy&paste&modify from the function. Only the one if statement is modified and the alcohol block is added.
				if (!t2.IsSociallyProper(pawn))
				{
					text = text + ": " + "ReservedForPrisoners".Translate().CapitalizeFirst();
				}
				Log.Message("M21:" + text);
				if (!t2.def.IsDrug || !ModsConfig.IdeologyActive || new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out var opt, text))
				{
					if (t2.def.IsNonMedicalDrug && pawn.IsTeetotaler())
					{
					    Log.Message("M221");
						opt = new FloatMenuOption(text + ": " + TraitDefOf.DrugDesire.DataAtDegree(-1).GetLabelCapFor(pawn), null);
					}
					else if (FoodUtility.InappropriateForTitle(t2.def, pawn, allowIfStarving: true))
					{
					    Log.Message("M223");
						opt = new FloatMenuOption(text + ": " + "FoodBelowTitleRequirements".Translate(pawn.royalty.MostSeniorTitle.def.GetLabelFor(pawn).CapitalizeFirst()), null);
					}
					else if (!pawn.CanReach(t2, PathEndMode.OnCell, Danger.Deadly))
					{
					    Log.Message("M224");
						opt = new FloatMenuOption(text + ": " + "NoPath".Translate().CapitalizeFirst(), null);
					}
					else
					{
					    Log.Message("M225");
						MenuOptionPriority priority = ((t2 is Corpse) ? MenuOptionPriority.Low : MenuOptionPriority.Default);
						int maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(t2, pawn, FoodUtility.WillIngestStackCountOf(pawn, t2.def, t2.GetStatValue(StatDefOf.Nutrition)));
						opt = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
						{
							int maxAmountToPickup2 = FoodUtility.GetMaxAmountToPickup(t2, pawn, FoodUtility.WillIngestStackCountOf(pawn, t2.def, t2.GetStatValue(StatDefOf.Nutrition)));
							if (maxAmountToPickup2 != 0)
							{
								t2.SetForbidden(value: false);
								Job job23 = JobMaker.MakeJob(JobDefOf.Ingest, t2);
								job23.count = maxAmountToPickup2;
								pawn.jobs.TryTakeOrderedJob(job23, JobTag.Misc);
							}
						}, priority), pawn, t2);
						if (maxAmountToPickup == 0)
						{
							opt.action = null;
						}
					}
				}
				else
				    Log.Message("M22X");
                        // End of big scary copy&paste&modify from the function.
                        // Now replace the menu option.
                        Log.Message("M3:" + alcoholItems[j].First + "->" + opt.Label);
                        opts[i] = opt;
                    }
                }
            }
        }
    }

// These are basically copy&paste&modify of HighLife classes, split into two classes based on the constants.
// The wanted class more or less matches HighLife settings, the Essential gets unhappy more quickly.
	public class ThoughtWorker_Precept_Alcohol_Wanted : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
	{
		// The values are also hardcoded in the XML.
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
		// The values are also hardcoded in the XML.
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

#endif

}
