using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using System;

// Alter the code that gives the core thoughts about sleeping in a bedroom/barracks.
namespace MorePrecepts
{
    [HarmonyPatch(typeof(Toils_LayDown))]
    public static class Toils_LayDown2_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ApplyBedThoughts))]
        public static void ApplyBedThoughts_Prefix(Pawn actor)
        {
            if( actor.needs.mood == null )
                return;
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.Barracks_Preferred_SleptInBedroom);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.Barracks_Preferred_SleptInBarracks);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.Barracks_Despised_SleptInBarracks);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(ApplyBedThoughts))]
        public static IEnumerable<CodeInstruction> ApplyBedThoughts(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found1 = false;
            bool found2 = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // if (bed.GetRoom().Role == RoomRoleDefOf.Bedroom)
                //     thoughtDef = ThoughtDefOf.SleptInBedroom;
                // Change to:
                // if (bed.GetRoom().Role == RoomRoleDefOf.Bedroom)
                //     thoughtDef = ApplyBedThoughts_Hook1( ThoughtDefOf.SleptInBedroom, actor );
                if( codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString() == "Verse.RoomRoleDef Bedroom"
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Bne_Un_S
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString() == "RimWorld.ThoughtDef SleptInBedroom" )
                {
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Ldarg_0 )); // load 'actor' (function is static, so 0)
                    codes.Insert( i + 4, new CodeInstruction( OpCodes.Call,
                        typeof( Toils_LayDown2_Patch ).GetMethod( nameof( ApplyBedThoughts_Hook1 ))));
                    found1 = true;
                }
                // The function has code:
                // if (bed.GetRoom().Role == RoomRoleDefOf.Barracks)
                //     thoughtDef = ThoughtDefOf.SleptInBarracks;
                // Change to:
                // if (bed.GetRoom().Role == RoomRoleDefOf.Barracks)
                //     thoughtDef = ApplyBedThoughts_Hook2( ThoughtDefOf.SleptInBarracks, actor );
                if( codes[ i ].opcode == OpCodes.Ldsfld && codes[ i ].operand.ToString() == "Verse.RoomRoleDef Barracks"
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Bne_Un_S
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld && codes[ i + 2 ].operand.ToString() == "RimWorld.ThoughtDef SleptInBarracks" )
                {
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Ldarg_0 )); // load 'actor' (function is static, so 0)
                    codes.Insert( i + 4, new CodeInstruction( OpCodes.Call,
                        typeof( Toils_LayDown2_Patch ).GetMethod( nameof( ApplyBedThoughts_Hook2 ))));
                    found2 = true;
                }
            }
            if( !found1 || !found2 )
                Log.Error("MorePrecepts: Failed to patch Toils_LayDown.ApplyBedThoughts()");
            return codes;
        }

        public static ThoughtDef ApplyBedThoughts_Hook1( ThoughtDef thoughtDef, Pawn actor )
        {
            if( actor.Ideo?.HasPrecept( PreceptDefOf.Barracks_Preferred ) ?? false )
                thoughtDef = ThoughtDefOf.Barracks_Preferred_SleptInBedroom;
            return thoughtDef;
        }

        public static ThoughtDef ApplyBedThoughts_Hook2( ThoughtDef thoughtDef, Pawn actor )
        {
            if( actor.Ideo?.HasPrecept( PreceptDefOf.Barracks_Preferred ) ?? false )
                thoughtDef = ThoughtDefOf.Barracks_Preferred_SleptInBarracks;
            else if( actor.Ideo?.HasPrecept( PreceptDefOf.Barracks_Despised ) ?? false )
                thoughtDef = ThoughtDefOf.Barracks_Despised_SleptInBarracks;
            return thoughtDef;
        }
    }

    [HarmonyPatch(typeof(ThoughtUtility))]
    public static class ThoughtUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(RemovePositiveBedroomThoughts))]
        public static void RemovePositiveBedroomThoughts(Pawn pawn)
        {
            if( pawn?.needs?.mood != null )
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefIf(ThoughtDefOf.Barracks_Preferred_SleptInBedroom,
                    (Thought_Memory thought) => thought.MoodOffset() > 0f);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefIf(ThoughtDefOf.Barracks_Preferred_SleptInBarracks,
                    (Thought_Memory thought) => thought.MoodOffset() > 0f);
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefIf(ThoughtDefOf.Barracks_Despised_SleptInBarracks,
                    (Thought_Memory thought) => thought.MoodOffset() > 0f);
            }
        }
    }
}
