using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MorePrecepts
{

    // We track last violence as the tick from the last time the pawn killed somebody.
    // Causing damage to a pawn increases the tick somewhat.
    public class LastViolenceTickComp : ThingComp
    {
        public int lastViolenceTick;

        public override void Initialize(CompProperties props)
        {
            lastViolenceTick = -99999;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecents.LastViolenceTick", -99999);
        }

        public static int Get(Pawn pawn)
        {
            LastViolenceTickComp comp = pawn.GetComp<LastViolenceTickComp>();
            return comp.lastViolenceTick;
        }

        public static void SetToNow(Pawn pawn)
        {
            LastViolenceTickComp comp = pawn.GetComp<LastViolenceTickComp>();
            comp.lastViolenceTick = Find.TickManager.TicksGame;
        }

        public static void Add(Pawn pawn, float add)
        {
            LastViolenceTickComp comp = pawn.GetComp<LastViolenceTickComp>();
            // There are 60000 ticks in a day. Body parts usually have 25-30 hp, so killing a pawn could
            // average let's say 100-200 damage? Add 100 ticks per damage, so that 600 damage postpones by one day.
            comp.lastViolenceTick = Math.Min(comp.lastViolenceTick + (int)(add * 100), Find.TickManager.TicksGame);
        }
    }

// Basically all this code patches all relevant WorkTags.Violent places to disable violence only between pawns.

    [HarmonyPatch(typeof(FloatMenuUtility))]
    public static class FloatMenuUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GetRangedAttackAction))]
        public static void GetRangedAttackAction(ref Action __result, Pawn pawn, LocalTargetInfo target, ref string failStr)
        {
            if( __result == null )
                return;
            Pawn victim = target.Thing as Pawn;
            if( victim != null && pawn.RaceProps.Humanlike && victim.RaceProps.Humanlike
                && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                failStr = "IdeoligionForbids".Translate();
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker))]
    public static class Pawn_InteractionsTracker_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(SocialFightPossible))]
        public static void SocialFightPossible(ref bool __result, Pawn_InteractionsTracker __instance, Pawn otherPawn)
        {
            FieldInfo fi = AccessTools.Field(typeof(Pawn_InteractionsTracker),"pawn");
            Pawn pawn = (Pawn)fi.GetValue(__instance);
            if( __result && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(SocialFightChance))]
        public static void SocialFightChance(ref float __result, Pawn_InteractionsTracker __instance, InteractionDef interaction, Pawn initiator)
        {
            if( __result > 0 )
            {
                FieldInfo fi = AccessTools.Field(typeof(Pawn_InteractionsTracker),"pawn");
                Pawn pawn = (Pawn)fi.GetValue(__instance);
                // Reduce social fight chance for violence-avoiding pawns, but still keep at least a small chance.
                if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Pacifism))
                    __result = Mathf.Max( 0.01f, __result / 8 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Horrible))
                    __result = Mathf.Max( 0.01f, __result / 4 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Disapproved))
                    __result = Mathf.Max( 0.01f, __result / 2 );
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat))]
    public static class JobGiver_ReactToCloseMeleeThreat_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TryGiveJob))]
        public static void TryGiveJob(ref Job __result, Pawn pawn)
        {
            if( __result != null )
            {
                Pawn meleeThreat = pawn.mindState.meleeThreat;
                if( pawn.RaceProps.Humanlike && meleeThreat.RaceProps.Humanlike
                     && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    __result = null;
                }
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_SocialFighting))]
    public static class JobGiver_SocialFighting_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TryGiveJob))]
        public static void TryGiveJob(ref Job __result, Pawn pawn)
        {
            if( __result != null )
            {
                Pawn otherPawn = ((MentalState_SocialFighting)pawn.MentalState).otherPawn;
                if( pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike
                     && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    __result = null;
                }
            }
        }
    }

    [HarmonyPatch(typeof(AttackTargetFinder))]
    public static class AttackTargetFinder_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BestAttackTarget))]
        public static void BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags, ref Predicate<Thing> validator,
            float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange,
            bool canBashFences = false)
        {
            Pawn pawn = searcher as Pawn;
            if( pawn != null && pawn.RaceProps.Humanlike
                && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                // Use a wrapper validator that'll also ignore other pawns and pass that to the actual function.
                Predicate<Thing> oldValidator = validator;
                Predicate<Thing> validatorWrapper = delegate(Thing thing)
                {
                    Pawn target = thing as Pawn;
                    if( target != null && target.RaceProps.Humanlike)
                        return false;
                    if(oldValidator != null && !oldValidator(thing))
                        return false;
                    return true;
                };
                validator = validatorWrapper;
            }
        }
    }

    [HarmonyPatch(typeof(RitualRolePrisonerOrSlave))]
    public static class RitualRolePrisonerOrSlave_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AppliesToPawn))]
        public static void AppliesToPawn(ref bool __result, Pawn p, ref string reason, LordJob_Ritual ritual, RitualRoleAssignments assignments,
            Precept_Ritual precept, bool skipReason)
        {
            if( !new HistoryEvent(HistoryEventDefOf.AttackedPerson, p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                if (!skipReason)
                    reason = "MessageRitualRoleMustBeCapableOfFighting".Translate(p);
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TryStartAttack))]
        public static bool TryStartAttack(ref bool __result, Pawn __instance, LocalTargetInfo targ)
        {
            Pawn pawn = __instance;
            Pawn otherPawn = targ.Thing as Pawn;
            if( otherPawn != null && pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike
                 && !new HistoryEvent(HistoryEventDefOf.AttackedPerson, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PreApplyDamage))]
        public static void PreApplyDamage(Pawn __instance, DamageInfo dinfo, ref bool absorbed)
        {
            Pawn pawn = dinfo.Instigator as Pawn;
            Pawn otherPawn = __instance;
            if(pawn != null && pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike)
            {
                HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.AttackedPerson,
                    pawn.Named(HistoryEventArgsNames.Doer));
                Find.HistoryEventsManager.RecordEvent(historyEvent);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PostApplyDamage))]
        public static void PostApplyDamage(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            Pawn pawn = dinfo.Instigator as Pawn;
            Pawn otherPawn = __instance;
            if(pawn != null && pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike)
                LastViolenceTickComp.Add(pawn, totalDamageDealt);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Kill))]
        public static void Kill(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if(dinfo.HasValue && dinfo.Value.Instigator != null)
            {
                Pawn pawn = dinfo.Value.Instigator as Pawn;
                Pawn otherPawn = __instance;
                if(pawn != null && pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike)
                    LastViolenceTickComp.SetToNow(pawn);
            }
        }
    }

    // These are basically copy&paste&modify of HighLife classes, split into two classes based on the constants.
    // The Wanted class is less demanding, the Essential gets unhappy more quickly.
    public class ThoughtWorker_Precept_Violence_Wanted : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        // The values are also hardcoded in the XML.
        private const float DaysSatisfied = 5f;
        private const float DaysNoBonus = 7f;
        private const float DaysMissing = 8f;
        private const float DaysMissing_Major = 15f;

        public static readonly SimpleCurve MoodOffsetFromDaysSinceLastAttackCurve = new SimpleCurve
        {
            new CurvePoint(DaysSatisfied, 2f),
            new CurvePoint(DaysNoBonus, 0f),
            new CurvePoint(DaysMissing, -2f),
            new CurvePoint(DaysMissing_Major, -10f)
        };

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.WorkTagIsDisabled(WorkTags.Violent))
                return false;
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                float num = (float)(Find.TickManager.TicksGame - LastViolenceTickComp.Get(p)) / 60000f;
                if (num > DaysSatisfied && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysSatisfied)
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysNoBonus)
                    return ThoughtState.ActiveAtStage(1);
                if (num < DaysMissing)
                    return ThoughtState.ActiveAtStage(2);
                return ThoughtState.ActiveAtStage(3);
            }
            return false;
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysSatisfied.Named("DAYSSATISIFED");
        }
    }

    public class ThoughtWorker_Precept_Violence_Essential : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        // The values are also hardcoded in the XML.
        private const float DaysSatisfied = 2f;
        private const float DaysNoBonus = 5f;
        private const float DaysMissing = 7f;
        private const float DaysMissing_Major = 10f;

        public static readonly SimpleCurve MoodOffsetFromDaysSinceLastAttackCurve = new SimpleCurve
        {
            new CurvePoint(DaysSatisfied, 3f),
            new CurvePoint(DaysNoBonus, 0f),
            new CurvePoint(DaysMissing, -2f),
            new CurvePoint(DaysMissing_Major, -10f)
        };

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.WorkTagIsDisabled(WorkTags.Violent))
                return false;
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                float num = (float)(Find.TickManager.TicksGame - LastViolenceTickComp.Get(p)) / 60000f;
                if (num > DaysSatisfied && def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysSatisfied)
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysNoBonus)
                    return ThoughtState.ActiveAtStage(1);
                if (num < DaysMissing)
                    return ThoughtState.ActiveAtStage(2);
                return ThoughtState.ActiveAtStage(3);
            }
            return false;
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysSatisfied.Named("DAYSSATISIFED");
        }
    }

    // Again copy&paste&modify from HighLife.
    public class Thought_Situational_Precept_Violence_Wanted : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                if (ThoughtUtility.ThoughtNullified(pawn, def))
                    return 0f;
                float x = (float)(Find.TickManager.TicksGame - LastViolenceTickComp.Get(pawn)) / 60000f;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Violence_Wanted.MoodOffsetFromDaysSinceLastAttackCurve.Evaluate(x));
            }
        }
    }

    public class Thought_Situational_Precept_Violence_Essential : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                if (ThoughtUtility.ThoughtNullified(pawn, def))
                    return 0f;
                float x = (float)(Find.TickManager.TicksGame - LastViolenceTickComp.Get(pawn)) / 60000f;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Violence_Essential.MoodOffsetFromDaysSinceLastAttackCurve.Evaluate(x));
            }
        }
    }

}
