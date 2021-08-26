using RimWorld;
using Verse;
using System;

namespace MorePrecepts
{
    // A ThingComp attached to pawns, to contain all pawn extra data for this mod.
    public class PawnComp : ThingComp
    {
        // For alcohol  precept, alcohol version of lastTakeRecreationalDrugTick.
        private int lastTakeAlcoholTick;

        // For violence precept.
        public int lastViolenceTick;

        // For funeral pyre.
        private bool burnedOnPyre;

        // For drugpossession, the first tick the pawn became aware of drugs present.
        private int noticedDrugsTick;

        public static int GetLastTakeAlcoholTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            return comp.lastTakeAlcoholTick;
        }

        public static void SetLastTakeAlcoholTickToNow(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.lastTakeAlcoholTick = Find.TickManager.TicksGame;
        }

        public static int GetLastViolenceTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            return comp.lastViolenceTick;
        }

        public static void SetLastViolenceTickToNow(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.lastViolenceTick = Find.TickManager.TicksGame;
        }

        public static void AddToLastViolenceTick(Pawn pawn, int add)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.lastViolenceTick = Math.Min(comp.lastViolenceTick + add, Find.TickManager.TicksGame);
        }

        public static void SetBurnedOnPyre(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.burnedOnPyre = true;
        }

        public static bool GetBurnedOnPyre(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            return comp.burnedOnPyre;
        }

        public static int GetNoticedDrugsTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            return comp.noticedDrugsTick;
        }

        public static void SetNoticedDrugsTick(Pawn pawn, int tick)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            comp.noticedDrugsTick = tick;
        }

        public static void SetNoticedDrugsTickIfNotSet(Pawn pawn, int tick)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp.noticedDrugsTick < 0)
                comp.noticedDrugsTick = tick;
        }

        public override void Initialize(CompProperties props)
        {
            lastTakeAlcoholTick = -99999;
            lastViolenceTick = -99999;
            burnedOnPyre = false;
            noticedDrugsTick = -99999;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastTakeAlcoholTick, "MorePrecepts.LastTakeAlcoholTick", -99999);
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecepts.LastViolenceTick", -99999);
            Scribe_Values.Look(ref burnedOnPyre, "MorePrecepts.BurnedOnPyre", false);
            Scribe_Values.Look(ref noticedDrugsTick, "MorePrecepts.NoticedDrugsTick", -99999);
        }
    }
}
