using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;

// TODO:
// use xml for blocking thoughts, like comfort:ignored blocks atewithouttable?
// je potreba mit thoughts zvlast, at je tooltip pro precept ukaze vsechny (neukazuje stages)
// slight disrespect of teenagers when elders respected a lot
// disrespect of young leaders
// constants instead of hardcoded values
// todo: disable for npc factions? or modify them to not spawn "wrong" pawns
// when respected, have a debuf when there are no younger people

namespace MorePrecepts
{
    // Generic good-opinion class.
    public class ThoughtWorker_Precept_Elderly_Plus : ThoughtWorker_Precept
    {
        protected static int countOld(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist)
                return -1;
            List< Pawn > list = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            int num = 0;
            for (int i = 0; i < list.Count; ++i)
            {
                Pawn other = list[i];
                if (other != pawn && other.RaceProps.Humanlike && !other.IsSlave && !other.IsQuestLodger())
                {
                    // Count old enough pawns. If the pawn counts as old, count only older pawns.
                    if( other.ageTracker.AgeBiologicalYears >= 50
                        && other.ageTracker.AgeBiologicalYears > pawn.ageTracker.AgeBiologicalYears )
                    {
                        ++num;
                    }
                }
            }
            return num;
        }

        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            int num = countOld(pawn);
            if( num < 0 )
                return false;
            if( num == 0 )
            {
                if(pawn.ageTracker.AgeBiologicalYears < 50)
                    return ThoughtState.ActiveAtStage( 0 ); // no elder => penalty
                else
                    return ThoughtState.Inactive; // this pawn is an elder, no penalty
            }
            if( num > 2 )
                return ThoughtState.ActiveAtStage( 3 );
            else
                return ThoughtState.ActiveAtStage( num );
        }
    }

    // For respected, which has only 1 stage.
    public class ThoughtWorker_Precept_Elderly_Respected : ThoughtWorker_Precept_Elderly_Plus
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            int num = countOld(pawn);
            if( num < 0 )
                return false;
            if( num == 0 )
                return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage( 0 );
        }
    }

    public class ThoughtWorker_Precept_Elderly_Self_Plus : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if( pawn.ageTracker.AgeBiologicalYears >= 80 )
                return ThoughtState.ActiveAtStage( 1 );
            if( pawn.ageTracker.AgeBiologicalYears >= 50 )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_Precept_Elderly_Social_Plus : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn, Pawn otherPawn)
        {
            if( otherPawn.ageTracker.AgeBiologicalYears < pawn.ageTracker.AgeBiologicalYears )
                return ThoughtState.Inactive; // give social bonus only to older than the pawn
            if( otherPawn.ageTracker.AgeBiologicalYears >= 80 )
                return ThoughtState.ActiveAtStage( 1 );
            if( otherPawn.ageTracker.AgeBiologicalYears >= 50 )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_Precept_Elderly_NoYoung : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist)
                return false;
            List< Pawn > list = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < list.Count; ++i)
            {
                Pawn other = list[i];
                // Make even quest lodgers count here.
                if (other != pawn && other.RaceProps.Humanlike && !other.IsSlave)
                {
                    if( other.ageTracker.AgeBiologicalYears < 50 )
                        return ThoughtState.Inactive;
                }
            }
            return ThoughtState.ActiveAtStage( 0 ); // no young people exist
        }
    }

    // Disrespect young people a bit, teenagers more.
    public class ThoughtWorker_Precept_Elderly_Social_Young : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn, Pawn otherPawn)
        {
            if( pawn.ageTracker.AgeBiologicalYears < 50 ) // only elders view young ones a bit poorly
                return ThoughtState.Inactive;
            if( otherPawn.ageTracker.AgeBiologicalYears < 18 )
                return ThoughtState.ActiveAtStage( 1 );
            if( otherPawn.ageTracker.AgeBiologicalYears < 25 )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

    // Generic bad-opinion class.
    public class ThoughtWorker_Precept_Elderly_Minus : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist)
                return false;
            List< Pawn > list = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            int num = 0;
            for (int i = 0; i < list.Count; ++i)
            {
                Pawn other = list[i];
                // Count everybody, even slaves and quest lodgers.
                if (other != pawn && other.RaceProps.Humanlike)
                {
                    // Count other old people even for old pawns, as long as the other one is older.
                    if( other.ageTracker.AgeBiologicalYears >= 50
                        && other.ageTracker.AgeBiologicalYears > pawn.ageTracker.AgeBiologicalYears )
                    {
                        ++num;
                    }
                }
            }
            if( num == 0 )
                return ThoughtState.Inactive; // no elder => don't care
            if( num > 2 )
                return ThoughtState.ActiveAtStage( 3 - 1 );
            else
                return ThoughtState.ActiveAtStage( num - 1 );
        }
    }

    public class ThoughtWorker_Precept_Elderly_Self_Minus : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if( pawn.ageTracker.AgeBiologicalYears >= 60 )
                return ThoughtState.ActiveAtStage( 1 );
            if( pawn.ageTracker.AgeBiologicalYears >= 50 )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_Precept_Elderly_Social_Minus : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn, Pawn otherPawn)
        {
            if( otherPawn.ageTracker.AgeBiologicalYears < pawn.ageTracker.AgeBiologicalYears )
                return ThoughtState.Inactive; // give social penaly only to older than the pawn
            if( otherPawn.ageTracker.AgeBiologicalYears >= 60 )
                return ThoughtState.ActiveAtStage( 1 );
            if( otherPawn.ageTracker.AgeBiologicalYears >= 50 )
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

}
