using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;

// TODO:
// use xml for blocking thoughts, like comfort:ignored blocks atewithouttable?
// when using thoughts stages, the precept tooltip does not show them all
// constants instead of hardcoded values
// todo: disable for npc factions? or modify them to not spawn "wrong" pawns
// when respected, disrespected somebody too young with royalty title

namespace MorePrecepts
{
    // Helper to calculate age limits. For humans old should be 50+, very old 80+,
    // teenager below 18 and young adult below 25. Basing it on race properties
    // is more friendly to other mods.
    // Default human lifeExpectancy is 80, teenager age is 18.
    // Use of these functions should be ordered by age (somebody very old counts also as old).
    public static class Ages
    {
        public static bool IsOld(Pawn pawn)
        {
            return pawn.ageTracker.AgeBiologicalYears >= pawn.RaceProps.lifeExpectancy * 50 / 80;
        }
        public static bool IsVeryOld(Pawn pawn)
        {
            return pawn.ageTracker.AgeBiologicalYears >= pawn.RaceProps.lifeExpectancy;
        }
        public static bool IsVeryOldMinus(Pawn pawn)
        {   // When elders are dispespected, a pawn is seen as very old sooner.
            return pawn.ageTracker.AgeBiologicalYears >= pawn.RaceProps.lifeExpectancy * 60 / 80;
        }
        public static bool IsVeryYoung(Pawn pawn)
        {
            foreach(LifeStageAge stage in pawn.RaceProps.lifeStageAges)
            {
                Log.Message("Y1:" + stage.def.defName );
                if( stage.def.defName.EndsWith("Adult"))
                {
                    return pawn.ageTracker.AgeBiologicalYears < stage.minAge;
                }
            }
            return false;
        }
        public static bool IsYoung(Pawn pawn)
        {
            foreach(LifeStageAge stage in pawn.RaceProps.lifeStageAges)
            {
                if( stage.def.defName.EndsWith("Adult"))
                {
                    return pawn.ageTracker.AgeBiologicalYears < stage.minAge * 25 / 18;
                }
            }
            return false;
        }
    }

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
                    if( Ages.IsOld(other) && other.ageTracker.AgeBiologicalYears > pawn.ageTracker.AgeBiologicalYears )
                        ++num;
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
                if(!Ages.IsOld(pawn))
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
            if( Ages.IsVeryOld(pawn))
                return ThoughtState.ActiveAtStage( 1 );
            if( Ages.IsOld(pawn))
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
            if( Ages.IsVeryOld(otherPawn))
                return ThoughtState.ActiveAtStage( 1 );
            if( Ages.IsOld(otherPawn))
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
            if(!Ages.IsOld(pawn))
                return ThoughtState.Inactive;
            List< Pawn > list = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < list.Count; ++i)
            {
                Pawn other = list[i];
                // Make even quest lodgers count here.
                if (other != pawn && other.RaceProps.Humanlike && !other.IsSlave)
                {
                    if( !Ages.IsOld(other))
                        return ThoughtState.Inactive;
                }
            }
            return ThoughtState.ActiveAtStage( 0 ); // no young people exist
        }
    }

    // Somebody else too young for a role.
    public class ThoughtWorker_Precept_Elderly_Role : ThoughtWorker_Precept
    {
        protected enum YoungType { NoYoung, HasYoung, HasVeryYoung };
        protected static YoungType hasYoungWithRole(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist || !ModsConfig.IdeologyActive)
                return YoungType.NoYoung;
            List< Pawn > list = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < list.Count; ++i)
            {
                Pawn other = list[i];
                if (other != pawn && other.RaceProps.Humanlike && !other.IsSlave && !other.IsQuestLodger()
                    && other.Ideo != null && other.Ideo.GetRole(other) != null)
                {
                    if( Ages.IsVeryYoung(other))
                        return YoungType.HasVeryYoung;
                    if( Ages.IsYoung(other))
                        return YoungType.HasYoung;
                }
            }
            return YoungType.NoYoung;
        }
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            YoungType young = hasYoungWithRole( pawn );
            if( young == YoungType.NoYoung )
                return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage( young == YoungType.HasVeryYoung ? 0 : 1 );
        }
    }

    public class ThoughtWorker_Precept_Elderly_Role_Single : ThoughtWorker_Precept_Elderly_Role
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            YoungType young = hasYoungWithRole( pawn );
            if( young == YoungType.NoYoung )
                return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage( 0 );
        }
    }

    // "I feel too young for the role"
    public class ThoughtWorker_Precept_Elderly_Role_Self : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist || !ModsConfig.IdeologyActive || pawn.Ideo == null || pawn.Ideo.GetRole(pawn) == null)
                return false;
            if( Ages.IsVeryYoung(pawn))
                return ThoughtState.ActiveAtStage( 0 );
            if( Ages.IsYoung(pawn))
                return ThoughtState.ActiveAtStage( 1 );
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_Precept_Elderly_Role_Self_Single : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn)
        {
            if (pawn.Faction == null || !pawn.IsColonist || !ModsConfig.IdeologyActive || pawn.Ideo == null || pawn.Ideo.GetRole(pawn) == null)
                return false;
            if( Ages.IsYoung(pawn))
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

    // Disrespect young people a bit, teenagers more.
    public class ThoughtWorker_Precept_Elderly_Social_Young : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn pawn, Pawn otherPawn)
        {
            if( !Ages.IsOld(pawn)) // only elders view young ones a bit poorly
                return ThoughtState.Inactive;
            if( Ages.IsVeryYoung(otherPawn))
                return ThoughtState.ActiveAtStage( 1 );
            if( Ages.IsYoung(otherPawn))
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
                    if( Ages.IsOld(other) && other.ageTracker.AgeBiologicalYears > pawn.ageTracker.AgeBiologicalYears )
                        ++num;
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
            if( Ages.IsVeryOldMinus(pawn))
                return ThoughtState.ActiveAtStage( 1 );
            if( Ages.IsOld(pawn))
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
            if( Ages.IsVeryOldMinus(otherPawn))
                return ThoughtState.ActiveAtStage( 1 );
            if( Ages.IsOld(otherPawn))
                return ThoughtState.ActiveAtStage( 0 );
            return ThoughtState.Inactive;
        }
    }

}
