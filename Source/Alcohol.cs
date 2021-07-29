using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace MorePrecepts
{

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
