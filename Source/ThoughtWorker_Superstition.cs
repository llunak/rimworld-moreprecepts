using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

namespace MorePrecepts
{
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
                        bool wasEvent = false;
                        if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Strong))
                        {
                            HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Superstition_Superstitious_Strong_Plus,
                                pawn.Named(HistoryEventArgsNames.Doer));
                            wasEvent = wasEvent || historyEvent.DoerWillingToDo();
                            Find.HistoryEventsManager.RecordEvent(historyEvent);
                        }
                        if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Weak))
                        {
                            HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Superstition_Superstitious_Weak_Plus,
                                pawn.Named(HistoryEventArgsNames.Doer));
                            wasEvent = wasEvent || historyEvent.DoerWillingToDo();
                            Find.HistoryEventsManager.RecordEvent(historyEvent);
                        }
                        // Need a separate event for notifying pawns disgusted by superstition, using several PreceptComp_KnowsMemoryThought
                        // in PreceptDef each reacting to the specific events would list each of them in the tooltip.
                        // TODO: Fix this in Core?
                        if(wasEvent)
                        {
                            Log.Message("P2");
                            HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Superstition_Superstitious_Generic,
                                pawn.Named(HistoryEventArgsNames.Doer));
                            Find.HistoryEventsManager.RecordEvent(historyEvent);
                        }
                    }
                }
            }
        }
    }
}
