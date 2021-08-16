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
    public static class LastViolenceTick
    {
        public static int Get(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            return comp.lastViolenceTick;
        }

        public static void SetToNow(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.lastViolenceTick = Find.TickManager.TicksGame;
        }

        public static void Add(Pawn pawn, float add)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            // There are 60000 ticks in a day. Body parts usually have 25-30 hp, so killing a pawn could
            // average let's say 100-200 damage? Add 100 ticks per damage, so that 600 damage postpones by one day.
            comp.lastViolenceTick = Math.Min(comp.lastViolenceTick + (int)(add * 100), Find.TickManager.TicksGame);
        }
    }

    public static class ViolenceHelper
    {
        public static bool NotWillingToAttackAny(Pawn attacker)
        {
            if( attacker.RaceProps.Humanlike
                && !new HistoryEvent(HistoryEventDefOf.Violence_AttackedPerson, attacker.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                return true;
            }
            return false;
        }

        public static bool NotWillingToAttackNonHostile(Pawn attacker)
        {
            if( attacker.RaceProps.Humanlike
                && !new HistoryEvent(HistoryEventDefOf.Violence_AttackedHostilePerson, attacker.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                return true;
            }
            return false;
        }

        public static bool WillingToAttack(Pawn attacker, Pawn victim)
        {
            if( attacker.RaceProps.Humanlike && victim.RaceProps.Humanlike )
            {
                if(NotWillingToAttackAny(attacker))
                    return false;
                if(!victim.HostileTo(attacker) && NotWillingToAttackNonHostile(attacker))
                    return false;
            }
            return true;
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
            if( victim != null && !ViolenceHelper.WillingToAttack( pawn, victim ))
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
            if( __result && !ViolenceHelper.WillingToAttack(pawn, otherPawn))
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
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Defense))
                    __result = Mathf.Max( 0.01f, __result / 4 );
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
                if( meleeThreat != null && !ViolenceHelper.WillingToAttack(pawn, meleeThreat))
                    __result = null;
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
                if( otherPawn != null && !ViolenceHelper.WillingToAttack(pawn, otherPawn))
                    __result = null;
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
            if( pawn != null )
            {
                bool blockAll = ViolenceHelper.NotWillingToAttackAny(pawn);
                bool blockNonHostile = ViolenceHelper.NotWillingToAttackNonHostile(pawn);
                if(blockAll || blockNonHostile)
                {
                    // Use a wrapper validator that'll also ignore other pawns and pass that to the actual function.
                    Predicate<Thing> oldValidator = validator;
                    Predicate<Thing> validatorWrapper = delegate(Thing thing)
                    {
                        Pawn target = thing as Pawn;
                        if( target != null && target.RaceProps.Humanlike)
                        {
                            if(blockAll)
                                return false;
                            if(blockNonHostile && !target.HostileTo(pawn))
                                return false;
                        }
                        if(oldValidator != null && !oldValidator(thing))
                            return false;
                        return true;
                    };
                    validator = validatorWrapper;
                }
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
            // Block also self-defense pawns, this is selecting for the duel, so it's not known who the opponent would be,
            // and it's simpler to assume they wouldn't want to enter the duel.
            if( ViolenceHelper.NotWillingToAttackAny(p) || ViolenceHelper.NotWillingToAttackNonHostile(p))
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
            if( otherPawn != null && !ViolenceHelper.WillingToAttack(pawn, otherPawn))
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
                HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Violence_AttackedPerson,
                    pawn.Named(HistoryEventArgsNames.Doer));
                Find.HistoryEventsManager.RecordEvent(historyEvent);
                if(!otherPawn.HostileTo(pawn))
                {
                    HistoryEvent historyEventNonHostile = new HistoryEvent(HistoryEventDefOf.Violence_AttackedHostilePerson,
                        pawn.Named(HistoryEventArgsNames.Doer));
                    Find.HistoryEventsManager.RecordEvent(historyEventNonHostile);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PostApplyDamage))]
        public static void PostApplyDamage(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            Pawn pawn = dinfo.Instigator as Pawn;
            Pawn otherPawn = __instance;
            if(pawn != null && pawn.RaceProps.Humanlike && otherPawn.RaceProps.Humanlike)
                LastViolenceTick.Add(pawn, totalDamageDealt);
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
                    LastViolenceTick.SetToNow(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs))]
    public static class Pawn_MeleeVerbs_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TryMeleeAttack))]
        public static bool TryMeleeAttack(ref bool __result, Pawn_MeleeVerbs __instance, Thing target, Verb verbToUse, bool surpriseAttack)
        {
            Pawn pawn = __instance.Pawn;
            Pawn otherPawn = target as Pawn;
            if( otherPawn != null && !ViolenceHelper.WillingToAttack( pawn, otherPawn ))
            {
                __result = false;
                return false;
            }
            return true;
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
                float num = (float)(Find.TickManager.TicksGame - LastViolenceTick.Get(p)) / 60000f;
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
                float num = (float)(Find.TickManager.TicksGame - LastViolenceTick.Get(p)) / 60000f;
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
                float x = (float)(Find.TickManager.TicksGame - LastViolenceTick.Get(pawn)) / 60000f;
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
                float x = (float)(Find.TickManager.TicksGame - LastViolenceTick.Get(pawn)) / 60000f;
                return Mathf.RoundToInt(ThoughtWorker_Precept_Violence_Essential.MoodOffsetFromDaysSinceLastAttackCurve.Evaluate(x));
            }
        }
    }

    // Give pro-violence pawns a negative social thought against pawns that don't have a recent violence.
    public class ThoughtWorker_NoViolence : ThoughtWorker
    {
        private const float DaysShortWanted = 7f;
        private const float DaysLongWanted = 15f;
        private const float DaysShortEssential = 5f;
        private const float DaysLongEssential = 10f;
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if(!pawn.RaceProps.Humanlike || !other.RaceProps.Humanlike)
                return false;
            if(!RelationsUtility.PawnsKnowEachOther(pawn, other))
                return false;
            if(pawn.Ideo == null)
                return false;
            bool wanted = pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Wanted);
            bool essential = pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Essential);
            if(!wanted && !essential)
                return false;
            if(def.minExpectationForNegativeThought != null && pawn.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(pawn.MapHeld).order < def.minExpectationForNegativeThought.order)
                return false;
            float num = (float)(Find.TickManager.TicksGame - LastViolenceTick.Get(other)) / 60000f;
            if(wanted)
            {
                if(num < DaysShortWanted)
                    return false;
                return ThoughtState.ActiveAtStage(num < DaysLongWanted ? 0 : 1);
            }
            else // essential
            {
                if(num < DaysShortEssential)
                    return false;
                return ThoughtState.ActiveAtStage(num < DaysLongEssential ? 2 : 3);
            }
        }
    }
}
