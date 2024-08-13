using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;
using UnityEngine;

// This is about new members of the colony (even temporary), not about visiting pawns.
namespace MorePrecepts
{
    public class NewcomerAttitudeHelper
    {
        private const int ExcitedDays = 7;
        private const int CarefulDays = 7;
        private const int WaryDays = 15;
        private const int FearfulDays = 30;
        private static readonly SimpleCurve factorCurve = new SimpleCurve
        {
            new CurvePoint(1f, 1f),
            new CurvePoint(0.5f, 1f),
            new CurvePoint(0.2f, 0.5f),
            new CurvePoint(0, 0)
        };
        private static readonly SimpleCurve factorCurveExcited = new SimpleCurve
        {   // This one is based on days, not 0.0-1.0 factor like above.
            new CurvePoint(0, 1f), // total newcomers are 100% interesting
            new CurvePoint(ExcitedDays * 0.5f, 1f), // until here
            new CurvePoint(ExcitedDays * 0.8f, 0.5f), // now goes down faster
            new CurvePoint(ExcitedDays, 0), // not interesting
            new CurvePoint(ExcitedDays * 2, 0), // until now, start getting bored
            new CurvePoint(ExcitedDays * 2.8f, -0.5f), // now go down faster
            new CurvePoint(ExcitedDays * 3f, -1f) // maximum
        };
        public static float daysLimit(Pawn pawn)
        {
            if(pawn == null || pawn.Ideo == null)
                return 0;
            if( pawn.Ideo.HasPrecept(PreceptDefOf.MP_NewcomerAttitude_Excited))
                return ExcitedDays;
            if( pawn.Ideo.HasPrecept(PreceptDefOf.MP_NewcomerAttitude_Careful))
                return CarefulDays;
            if( pawn.Ideo.HasPrecept(PreceptDefOf.MP_NewcomerAttitude_Wary))
                return WaryDays;
            if( pawn.Ideo.HasPrecept(PreceptDefOf.MP_NewcomerAttitude_Fearful))
                return FearfulDays;
            return 0;
        }
        public static float daysFactor(Pawn pawn, Pawn other)
        {
            float ticks = other.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal);
            if( ticks > Find.TickManager.TicksGame - GenDate.TicksPerHour )
                return 0; // ignore initial colonists
            float days = ticks / GenDate.TicksPerDay;
            float limit = daysLimit( pawn );
            return factorCurve.Evaluate( ( limit - days ) / limit );
        }
        public static float adjustDaysFactorForStatus(Pawn pawn, Pawn other, float factor)
        {
            if( other.IsSlave )
                factor /= 2;
            DevelopmentalStage developmentalStage = other.ageTracker.CurLifeStage.developmentalStage;
            if( developmentalStage.Juvenile())
                factor /= 2;
            else if( developmentalStage.Baby())
                factor /= 4;
            else if( developmentalStage.Newborn())
                factor /= 8;
            if( other.Downed )
                factor /= 3;
            return factor;
        }
        public static bool calculateHasArmed(Pawn pawn)
        {
            if(pawn == null || pawn.Ideo == null || pawn.Faction == null)
                return false;
            foreach (Pawn other in PawnsFinder.AllMaps_SpawnedPawnsInFaction(pawn.Faction))
            {
                if( !other.IsColonist || other == pawn )
                    continue;
                if( other.equipment.Primary != null && other.equipment.Primary.def.IsWeapon )
                {
                    if( daysFactor( pawn, other ) > 0 ) // is newcomer
                        return true;
                }
            }
            return false;
        }
        public static float calculateMood(Pawn pawn, ThoughtDef def, bool hasAnyArmed)
        {
            if(pawn == null || pawn.Ideo == null || pawn.Faction == null)
                return 0;
            float total = 0;
            foreach (Pawn other in PawnsFinder.AllMaps_SpawnedPawnsInFaction(pawn.Faction))
            {
                if( !other.IsColonist || other == pawn )
                    continue;
                float factor = daysFactor( pawn, other );
                if( factor <= 0 )
                    continue;
                factor = adjustDaysFactorForStatus( pawn, other, factor );
                if(hasAnyArmed)
                {
                    // If there are any armed and this one is not, reduce its weight by the ratio
                    // between the mood effects.
                    bool armed = other.equipment.Primary != null && other.equipment.Primary.def.IsWeapon;
                    if(!armed)
                        factor = factor * def.stages[ 1 ].baseMoodEffect / def.stages[ 0 ].baseMoodEffect;
                }
                total += factor * 0.25f; // give full mood at 4 recent newcomers
            }
            if( total > 1 )
                total = 1;
            return def.stages[ hasAnyArmed ? 0 : 1 ].baseMoodEffect * total;
        }
        // Return 1.0 (=just recent newcomer) to -1.0 (=no newcomers for maximum time).
        public static float calculateExcitedFactor(Pawn pawn)
        {
            if(pawn == null || pawn.Ideo == null || pawn.Faction == null)
                return 0;
            if( Find.TickManager.TicksGame / GenDate.TicksPerDay < ExcitedDays )
                return 0; // no reaction during the initial time
            float bestFactor = -1;
            foreach (Pawn other in PawnsFinder.AllMaps_SpawnedPawnsInFaction(pawn.Faction))
            {
                if( !other.IsColonist || other == pawn )
                    continue;
                float ticks = other.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal);
                if( ticks > Find.TickManager.TicksGame - GenDate.TicksPerHour )
                    continue; // ignore initial colonists
                float days = ticks / GenDate.TicksPerDay;
                float factor = factorCurveExcited.Evaluate( days );
                factor = NewcomerAttitudeHelper.adjustDaysFactorForStatus( pawn, other, factor );
                bestFactor = Mathf.Max( bestFactor, factor );
            }
            return bestFactor;
        }
    }

    public class ThoughtWorker_Precept_NewcomerAttitude : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if(!p.RaceProps.Humanlike)
                return false;
            if(p.DevelopmentalStage.Baby())
                return false;
            float factor = NewcomerAttitudeHelper.daysFactor( p, p );
            if( factor > 0 )
                return false; // do not give the thought if the pawn considers himself a newcomer
            bool anyNewcomer = false;
            foreach (Pawn other in PawnsFinder.AllMaps_SpawnedPawnsInFaction(p.Faction))
            {
                if( !other.IsColonist || other == p )
                    continue;
                factor = NewcomerAttitudeHelper.daysFactor( p, other );
                anyNewcomer = factor > 0;
                if( anyNewcomer )
                    break;
            }
            if( !anyNewcomer )
                return ThoughtState.Inactive;
            if( NewcomerAttitudeHelper.calculateHasArmed( p ))
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.ActiveAtStage( 1 );
        }
    }

    public class Thought_NewcomerAttitude : Thought_Situational
    {
        public override float MoodOffset()
        {
            return Mathf.Ceil( NewcomerAttitudeHelper.calculateMood( pawn, def, CurStageIndex == 0 ));
        }
    }

    public class ThoughtWorker_Precept_NewcomerAttitude_Excited : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if(!p.RaceProps.Humanlike)
                return false;
            if(p.DevelopmentalStage.Baby())
                return false;
            float selfFactor = NewcomerAttitudeHelper.daysFactor( p, p );
            if( selfFactor > 0 )
                return false; // do not give the thought if the pawn considers himself a newcomer
            float factor = NewcomerAttitudeHelper.calculateExcitedFactor( p );
            if( factor > 0 )
                return ThoughtState.ActiveAtStage( 0 );
            else if( factor < 0 )
                return ThoughtState.ActiveAtStage( 1 );
            else
                return ThoughtState.Inactive;
        }
    }

    public class Thought_NewcomerAttitude_Excited : Thought_Situational
    {
        public override float MoodOffset()
        {
            return Mathf.Ceil( BaseMoodOffset * Mathf.Abs( NewcomerAttitudeHelper.calculateExcitedFactor( pawn )));
        }
    }

    public class ThoughtWorker_Precept_NewcomerAttitude_Social : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn p, Pawn otherPawn)
        {
            if(!p.RaceProps.Humanlike || !otherPawn.RaceProps.Humanlike)
                return false;
            if(p.Faction != otherPawn.Faction)
                return false;
            if(!RelationsUtility.PawnsKnowEachOther(p, otherPawn))
                return false;
            if(p.DevelopmentalStage.Baby() || otherPawn.DevelopmentalStage.Baby())
                return false;
            float factor = NewcomerAttitudeHelper.daysFactor( p, otherPawn );
            if( factor <= 0 )
                return false;
            if( otherPawn.equipment.Primary != null && otherPawn.equipment.Primary.def.IsWeapon )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.ActiveAtStage( 1 );
        }
    }

    public class Thought_NewcomerAttitude_Social : Thought_SituationalSocial
    {
        public override float OpinionOffset()
        {
            if (ThoughtUtility.ThoughtNullified(pawn, def))
                return 0;
            if (!Active)
                return 0;
            float factor = NewcomerAttitudeHelper.daysFactor(pawn, otherPawn);
            factor = NewcomerAttitudeHelper.adjustDaysFactorForStatus( pawn, otherPawn, factor );
            return Mathf.Ceil( CurStage.baseOpinionOffset * factor );
        }
    }

}
