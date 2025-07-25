using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

// We track last violence as the tick from the last time the pawn killed somebody.
// Causing damage to a pawn increases the tick somewhat.
namespace MorePrecepts
{

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
                && !new HistoryEvent(HistoryEventDefOf.Violence_AttackedNonHostilePerson, attacker.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                return true;
            }
            return false;
        }

        public static bool IsConsideredNonHostile(Pawn attacker, Pawn victim)
        {
            // I think kidnappers are considered a threat, but explicitly allow targeting them
            // in defence, just in case.
            if(victim.mindState?.duty?.def == DutyDefOf.Kidnap && victim.carryTracker.CarriedThing is Pawn)
                return false;
            if(attacker.IsColonist && GenHostility.IsActiveThreatToPlayer(victim))
                return false;
            // Give grace period for when the pawn has just started fleeing.
            if(victim.MentalStateDef == MentalStateDefOf.PanicFlee && victim.MentalState?.Age.TicksToSeconds() < 2)
                return false;
            // Allow when the pawn has an aggressive mental state.
            if(victim.MentalStateDef != null && victim.MentalStateDef.category == MentalStateCategory.Aggro
                // This is Aggro even though it's not aggresive, it should be Malicious, filter it out.
                && victim.MentalStateDef != MentalStateDefOf.Tantrum)
                return false;
            return true;
        }

        public static bool WillingToAttack(Pawn attacker, Pawn victim)
        {
            if( attacker.RaceProps.Humanlike && victim.RaceProps.Humanlike )
            {
                if(NotWillingToAttackAny(attacker))
                    return false;
                if(IsConsideredNonHostile(attacker, victim) && NotWillingToAttackNonHostile(attacker))
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
            Pawn pawn = __instance.pawn;
            if( __result && !ViolenceHelper.WillingToAttack(pawn, otherPawn))
                __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(SocialFightChance))]
        public static void SocialFightChance(ref float __result, Pawn_InteractionsTracker __instance, InteractionDef interaction, Pawn initiator)
        {
            if( __result > 0 )
            {
                Pawn pawn = __instance.pawn;
                // Reduce social fight chance for violence-avoiding pawns, but still keep at least a small chance.
                if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Pacifism))
                    __result = Mathf.Max( 0.01f, __result / 8 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Avoided))
                    __result = Mathf.Max( 0.01f, __result / 8 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Horrible))
                    __result = Mathf.Max( 0.01f, __result / 4 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Disapproved))
                    __result = Mathf.Max( 0.01f, __result / 2 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Defense))
                    __result = Mathf.Max( 0.01f, __result / 4 );
                else if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Violence_DefenseMild))
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
                            if(blockNonHostile && ViolenceHelper.IsConsideredNonHostile(pawn, target))
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
    public static class Pawn3_Patch
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
                    pawn.Named(HistoryEventArgsNames.Doer), otherPawn.Named(HistoryEventArgsNames.Victim));
                Find.HistoryEventsManager.RecordEvent(historyEvent);
                if(ViolenceHelper.IsConsideredNonHostile(pawn, otherPawn))
                {
                    HistoryEvent historyEventNonHostile = new HistoryEvent(HistoryEventDefOf.Violence_AttackedNonHostilePerson,
                        pawn.Named(HistoryEventArgsNames.Doer), otherPawn.Named(HistoryEventArgsNames.Victim));
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
            {
                // There are 60000 ticks in a day. Body parts usually have 25-30 hp, so killing a pawn could
                // average let's say 100-200 damage? Add 600 ticks per damage, so that damage worth killing
                // one enemy postpones by 1-2 days.
                PawnComp.AddToLastViolenceTick(pawn, (int)(totalDamageDealt * 600));
            }
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
                    PawnComp.SetLastViolenceTickToNow(pawn);
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

    // Apparently enabledForNPCFactions disables only the effects, but not the generation itself,
    // so e.g. quest lodgers still could get pacifism, which would make the betrayal case broken.
    // Disable generating violence-avoiding memes and precepts.
    [HarmonyPatch(typeof(IdeoGenerationParms))]
    public static class IdeoGenerationParms_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof( FactionDef ), typeof( bool ), typeof( List<PreceptDef> ), typeof( List<MemeDef> ),
            typeof( List<MemeDef> ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( bool ), typeof( string ),
            typeof( List<StyleCategoryDef> ), typeof( List<DeityPreset> ), typeof( bool ), typeof( string ), typeof( bool ) })]
        public static void Constructor(ref IdeoGenerationParms __instance)
        {
            if( __instance.forFaction.isPlayer )
                return;
            if( __instance.disallowedPrecepts != null )
                __instance.disallowedPrecepts = new List<PreceptDef>(__instance.disallowedPrecepts);
            else
                __instance.disallowedPrecepts = new List<PreceptDef>();
            if( __instance.disallowedMemes != null )
                __instance.disallowedMemes = new List<MemeDef>(__instance.disallowedMemes);
            else
                __instance.disallowedMemes = new List<MemeDef>();
            __instance.disallowedPrecepts.Add(PreceptDefOf.Violence_Pacifism);
            __instance.disallowedPrecepts.Add(PreceptDefOf.Violence_Avoided);
            __instance.disallowedPrecepts.Add(PreceptDefOf.Violence_Horrible);
            __instance.disallowedPrecepts.Add(PreceptDefOf.Violence_Disapproved);
            __instance.disallowedMemes.Add(MemeDefOf.Pacifism);
        }
    }

    public abstract class ThoughtWorker_Precept_Violence_Base : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        protected abstract float DaysSatisfied();
        protected abstract float DaysNoBonus();
        protected abstract float DaysMissing();
        protected abstract float DaysMissing_Major();
        protected abstract SimpleCurve MoodOffsetFromDaysSinceLastAttackCurve();

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.WorkTagIsDisabled(WorkTags.Violent))
                return false;
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                float num = (float)(Find.TickManager.TicksGame - PawnComp.GetLastViolenceTick(p)) / 60000f;
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
            float num = (float)(Find.TickManager.TicksGame - PawnComp.GetLastViolenceTick(pawn)) / 60000f;
            return MoodOffsetFromDaysSinceLastAttackCurve().Evaluate(num);
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing().Named("DAYSSATISIFED");
        }
    }

    // The Wanted class is less demanding.
    public class ThoughtWorker_Precept_Violence_Wanted : ThoughtWorker_Precept_Violence_Base
    {
        protected override float DaysSatisfied() => 5f;
        protected override float DaysNoBonus() => 7f;
        protected override float DaysMissing() => 8f;
        protected override float DaysMissing_Major() => 15f;
        protected override SimpleCurve MoodOffsetFromDaysSinceLastAttackCurve() => StaticMoodOffsetFromDaysSinceLastAttackCurve;

        public static readonly SimpleCurve StaticMoodOffsetFromDaysSinceLastAttackCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(5f, 1f),
            new CurvePoint(7f, 0f),
            new CurvePoint(8f, 1f),
            new CurvePoint(15f, 10f)
        };
    }

    // Essential gets unhappy more quickly.
    public class ThoughtWorker_Precept_Violence_Essential : ThoughtWorker_Precept_Violence_Base
    {
        protected override float DaysSatisfied() => 2f;
        protected override float DaysNoBonus() => 5f;
        protected override float DaysMissing() => 7f;
        protected override float DaysMissing_Major() => 8f;
        protected override SimpleCurve MoodOffsetFromDaysSinceLastAttackCurve() => StaticMoodOffsetFromDaysSinceLastAttackCurve;

        public static readonly SimpleCurve StaticMoodOffsetFromDaysSinceLastAttackCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(2f, 1f),
            new CurvePoint(5f, 0f),
            new CurvePoint(7f, 1f),
            new CurvePoint(8f, 10f)
        };
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
            float num = (float)(Find.TickManager.TicksGame - PawnComp.GetLastViolenceTick(other)) / 60000f;
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
