using HarmonyLib;
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

// This is basically copy&paste&replace of the automatedturrets code.
// Modifications are needed to get ResetStaticData() working properly in a mod.

namespace MorePrecepts
{
	[DefOf]
	public static class HistoryEventDefOf
	{
		[MayRequireIdeology]
		public static HistoryEventDef BuiltTrap;
	}

	public class ThoughtWorker_Precept_HasTraps : ThoughtWorker_Precept
	{
		private static List<ThingDef> trapDefs = new List<ThingDef>();
		
		private static bool needReset = true;

		public static void ResetStaticData()
		{
			trapDefs.Clear();
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				if (allDefsListForReading[i].building != null && allDefsListForReading[i].building.isTrap)
				{
					trapDefs.Add(allDefsListForReading[i]);
				}
			}
			needReset = false;
		}
		
		public static void SetNeedReset()
		{
		    needReset = true;
		}

		protected override ThoughtState ShouldHaveThought(Pawn p)
		{
			if (p.Faction == null || p.IsSlave)
			{
				return false;
			}
			if(needReset)
			    ResetStaticData();
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				for (int j = 0; j < trapDefs.Count; j++)
				{
					List<Thing> list = maps[i].listerThings.ThingsOfDef(trapDefs[j]);
					for (int k = 0; k < list.Count; k++)
					{
						if (list[k].Faction == p.Faction)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
	}

    [HarmonyPatch(typeof(GenConstruct))]
    public static class GenConstruct_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CanConstruct))]
        [HarmonyPatch(new Type[] { typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool) } )]
        public static void CanConstruct(ref bool __result, Thing t, Pawn p, bool checkSkills = true, bool forced = false)
        {
		ThingDef thingDef;
		if ((t.def.IsBlueprint || t.def.IsFrame) && (thingDef = t.def.entityDefToBuild as ThingDef) != null && thingDef.building != null && thingDef.building.isTrap && !new HistoryEvent(HistoryEventDefOf.BuiltTrap, p.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
		{
		        __result = false;
		}
	}
    }

    // The place that calls ThoughtWorker_Precept_HasAutomatedTurrets.ResetStaticData() is called when loading game
    // data, which on the first load is before mods are loaded. So our code has a flag whether reset is needed,
    // which will do it on-demand the first time. Still patch the turret function to get notifications when re-loading data
    // (e.g. when changing language).
    [HarmonyPatch(typeof(ThoughtWorker_Precept_HasAutomatedTurrets))]
    public class ThoughtWorker_Precept_HasAutomatedTurrets_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ResetStaticData))]
        public static void ResetStaticData()
        {
            ThoughtWorker_Precept_HasTraps.ResetStaticData();
        }
    }

}
