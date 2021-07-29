using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

namespace MorePrecepts
{
    public class PreceptComp_SuperstitionGood : PreceptComp
    {
        public List<GameConditionDef> gameConditionDefs;
    }
    public class PreceptComp_SuperstitionBad : PreceptComp
    {
        public List<GameConditionDef> gameConditionDefs;
    }

    [HarmonyPatch(typeof(GameConditionManager))]
    public static class GameConditionManager_Patch
    {
        private enum SuperstitionType { None, Good, Bad };

        private static SuperstitionType GetSuperstitionType(GameCondition cond)
        {
            // It seems it's not possible to extend GameCondition, it doesn't seem to take <comps>,
            // but PreceptDef does, so cheat and store the list of conditions there.
            PreceptDef data = PreceptDefOf.Superstition_Strong;
            foreach(PreceptComp comp in data.comps)
            {
                PreceptComp_SuperstitionGood good = comp as PreceptComp_SuperstitionGood;
                if( good != null )
                    foreach(GameConditionDef def in good.gameConditionDefs)
                        if(cond.def == def)
                            return SuperstitionType.Good;
                PreceptComp_SuperstitionBad bad = comp as PreceptComp_SuperstitionBad;
                if( bad != null )
                    foreach(GameConditionDef def in bad.gameConditionDefs)
                        if(cond.def == def)
                            return SuperstitionType.Bad;
            }
            return SuperstitionType.None;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(RegisterCondition))]
        public static void RegisterCondition(GameCondition cond)
        {
            SuperstitionType type = GetSuperstitionType(cond);
            Log.Message("XX:" + cond.def + "/" + type);
            if( type == SuperstitionType.None )
                return;
            foreach( Map map in cond.AffectedMaps )
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    Log.Message("P:" + pawn);
                    bool wasEvent = false;
                    if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Strong))
                    {
                        HistoryEvent historyEvent = new HistoryEvent(type == SuperstitionType.Good
                            ? HistoryEventDefOf.Superstition_Superstitious_Strong_Plus
                            : HistoryEventDefOf.Superstition_Superstitious_Strong_Minus,
                            pawn.Named(HistoryEventArgsNames.Doer));
                        wasEvent = wasEvent || historyEvent.DoerWillingToDo();
                        Find.HistoryEventsManager.RecordEvent(historyEvent);
                    }
                    if(pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.Superstition_Weak))
                    {
                        HistoryEvent historyEvent = new HistoryEvent(type == SuperstitionType.Good
                            ? HistoryEventDefOf.Superstition_Superstitious_Weak_Plus
                            : HistoryEventDefOf.Superstition_Superstitious_Weak_Minus,
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
