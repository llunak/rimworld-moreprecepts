using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MorePrecepts
{
    // Store mod's per-pawn data.
    // This used to be a ThingComp, but those need to be patched into game's XML
    // in order to be added to pawn, which does not work with some mods that create custom pawns,
    // so instead this is a custom class that loads/saves them by patching Pawn class.
    public class PawnComp
    {
        // For alcohol  precept, alcohol version of lastTakeRecreationalDrugTick.
        private int lastTakeAlcoholTick = -99999;

        // For violence precept.
        private int lastViolenceTick = -99999;

        // For compassion precept.
        private int lastDownedTick = -99999;
        private int lastDownedTicksUntilDeath = -99999;

        // For funeral pyre.
        private bool burnedOnPyre = false;

        // For drugpossession, the first tick the pawn became aware of drugs present.
        private int noticedDrugsTick = -99999;

        private static Dictionary< Pawn, PawnComp > dict = new Dictionary< Pawn, PawnComp >();

        public static PawnComp GetComp( Pawn pawn )
        {
            PawnComp comp;
            if( dict.TryGetValue( pawn, out comp ))
                return comp;
            comp = new PawnComp();
            dict[ pawn ] = comp;
            return comp;
        }

        public static int GetLastTakeAlcoholTick(Pawn pawn)
        {
            return GetComp( pawn ).lastTakeAlcoholTick;
        }

        public static void SetLastTakeAlcoholTickToNow(Pawn pawn)
        {
            GetComp( pawn ).lastTakeAlcoholTick = Find.TickManager.TicksGame;
        }

        public static int GetLastViolenceTick(Pawn pawn)
        {
            return GetComp( pawn ).lastViolenceTick;
        }

        public static void SetLastViolenceTickToNow(Pawn pawn)
        {
            GetComp( pawn ).lastViolenceTick = Find.TickManager.TicksGame;
        }

        public static void AddToLastViolenceTick(Pawn pawn, int add)
        {
            PawnComp comp = GetComp( pawn );
            comp.lastViolenceTick = Math.Min(comp.lastViolenceTick + add, Find.TickManager.TicksGame);
        }

        public static ( int, int ) GetLastDownedTicks(Pawn pawn)
        {
            PawnComp comp = GetComp( pawn );
            return ( comp.lastDownedTick, comp.lastDownedTicksUntilDeath );
        }

        public static void SetLastDownedTicks(Pawn pawn, int lastDownedTick, int lastDownedTickUntilDeath)
        {
            PawnComp comp = GetComp( pawn );
            comp.lastDownedTick = lastDownedTick;
            comp.lastDownedTicksUntilDeath = lastDownedTickUntilDeath;
        }

        public static void ResetOnlyLastDownedTick(Pawn pawn)
        {
            GetComp( pawn ).lastDownedTick = -99999;
        }

        public static void SetBurnedOnPyre(Pawn pawn)
        {
            GetComp( pawn ).burnedOnPyre = true;
        }

        public static bool GetBurnedOnPyre(Pawn pawn)
        {
            return GetComp( pawn ).burnedOnPyre;
        }

        public static int GetNoticedDrugsTick(Pawn pawn)
        {
            return GetComp( pawn ).noticedDrugsTick;
        }

        public static void SetNoticedDrugsTick(Pawn pawn, int tick)
        {
            GetComp( pawn ).noticedDrugsTick = tick;
        }

        public static void SetNoticedDrugsTickIfNotSet(Pawn pawn, int tick)
        {
            PawnComp comp = GetComp( pawn );
            if(comp.noticedDrugsTick < 0)
                comp.noticedDrugsTick = tick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref lastTakeAlcoholTick, "MorePrecepts.LastTakeAlcoholTick", -99999);
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecepts.LastViolenceTick", -99999);
            Scribe_Values.Look(ref lastDownedTick, "MorePrecepts.LastDownedTick", -99999);
            Scribe_Values.Look(ref lastDownedTicksUntilDeath, "MorePrecepts.LastDownedTicksUntilDeath", -99999);
            Scribe_Values.Look(ref burnedOnPyre, "MorePrecepts.BurnedOnPyre", false);
            Scribe_Values.Look(ref noticedDrugsTick, "MorePrecepts.NoticedDrugsTick", -99999);
        }

        public static void ClearAll()
        {
            dict.Clear();
        }
    }

    // Patching Pawn.ExposeData() makes the data to be saved per-pawn, and that also makes
    // this backwards compatible with old saves that used ThingComp-based PawnComp,
    // as that's where those were saving.
    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ExposeData))]
        public static void ExposeData( Pawn __instance )
        {
            PawnComp.GetComp( __instance ).ExposeData();
        }
    }

    [HarmonyPatch(typeof(Game))]
    public static class Game_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(InitNewGame))]
        public static void InitNewGame()
        {
            PawnComp.ClearAll();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(LoadGame))]
        public static void LoadGame()
        {
            PawnComp.ClearAll();
        }
    }
}
