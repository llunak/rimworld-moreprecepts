using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

namespace MorePrecepts
{
    [DefOf]
    public static class PreceptDefOf
    {
        public static PreceptDef Superstition_Strong;

        public static PreceptDef Superstition_Weak;
    }

    [DefOf]
    public static class ThoughtDefOf
    {
        public static ThoughtDef Superstition_WasSuperstitious_Strong_Plus;

        public static ThoughtDef Superstition_WasSuperstitious_Strong_Minus;

        public static ThoughtDef Superstition_WasSuperstitious_Weak_Plus;

        public static ThoughtDef Superstition_WasSuperstitious_Weak_Minus;
    }

    [HarmonyPatch(typeof(GameConditionManager))]
    public static class GameConditionManager_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(RegisterCondition))]
        public static void RegisterCondition(GameCondition cond)
        {
            // TODO: Make configurable in XML?
            Log.Message("XX:" + cond.def);
            if(cond.def == GameConditionDefOf.Flashstorm)
            {
                foreach( Map map in cond.AffectedMaps )
                {
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        Log.Message("P:" + pawn);
                        if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Strong))
                        {
                            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.Superstition_WasSuperstitious_Strong_Plus);
                            if (pawn.needs.mood != null)
                                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                        }
                        if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Weak))
                        {
                            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.Superstition_WasSuperstitious_Weak_Plus);
                            if (pawn.needs.mood != null)
                                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                        }
                    }
                }
            }
        }
    }
}
