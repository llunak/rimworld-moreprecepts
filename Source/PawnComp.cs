using RimWorld;
using Verse;
using System;
using HarmonyLib;

namespace MorePrecepts
{
    // A ThingComp attached to pawns, to contain all pawn extra data for this mod.
    [HarmonyPatch]
    public sealed class PawnComp : ThingComp
    {
        // For alcohol  precept, alcohol version of lastTakeRecreationalDrugTick.
        private int lastTakeAlcoholTick;

        // For violence precept.
        private int lastViolenceTick;

        // For compassion precept.
        private int lastDownedTick;
        private int lastDownedTicksUntilDeath;

        // For funeral pyre.
        private bool burnedOnPyre;

        // For drugpossession, the first tick the pawn became aware of drugs present.
        private int noticedDrugsTick;

        public static int GetLastTakeAlcoholTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
                return WarnBrokenPawn(pawn);
            return comp.lastTakeAlcoholTick;
        }

        public static void SetLastTakeAlcoholTickToNow(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.lastTakeAlcoholTick = Find.TickManager.TicksGame;
        }

        public static int GetLastViolenceTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
                return WarnBrokenPawn(pawn);
            return comp.lastViolenceTick;
        }

        public static void SetLastViolenceTickToNow(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.lastViolenceTick = Find.TickManager.TicksGame;
        }

        public static void AddToLastViolenceTick(Pawn pawn, int add)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.lastViolenceTick = Math.Min(comp.lastViolenceTick + add, Find.TickManager.TicksGame);
        }

        public static ( int, int ) GetLastDownedTicks(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return (-99999, -99999);
            }
            return ( comp.lastDownedTick, comp.lastDownedTicksUntilDeath );
        }

        public static void SetLastDownedTicks(Pawn pawn, int lastDownedTick, int lastDownedTickUntilDeath)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.lastDownedTick = lastDownedTick;
            comp.lastDownedTicksUntilDeath = lastDownedTickUntilDeath;
        }

        public static void ResetOnlyLastDownedTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.lastDownedTick = -99999;
        }

        public static void SetBurnedOnPyre(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.burnedOnPyre = true;
        }

        public static bool GetBurnedOnPyre(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return false;
            }
            return comp.burnedOnPyre;
        }

        public static int GetNoticedDrugsTick(Pawn pawn)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
                return WarnBrokenPawn(pawn);
            return comp.noticedDrugsTick;
        }

        public static void SetNoticedDrugsTick(Pawn pawn, int tick)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            comp.noticedDrugsTick = tick;
        }

        public static void SetNoticedDrugsTickIfNotSet(Pawn pawn, int tick)
        {
            PawnComp comp = pawn.GetComp<PawnComp>();
            if(comp == null)
            {
                WarnBrokenPawn(pawn);
                return;
            }
            if(comp.noticedDrugsTick < 0)
                comp.noticedDrugsTick = tick;
        }

        private static int WarnBrokenPawn(Pawn pawn)
        {
            // We patch the BasePawn ThingDef, so the PawnComp should always be there.
            // If not, then unless proven otherwise assume a mod that creates pawns without basing
            // them on the BasePawn ThingDef, and just ignore them.
            // Actually do not log, it appears that there are such broken(?) mods, e.g. the zombies or crystalloid mods.
            // Do not bother further until somebody actually complains.
            // Log.Warning("Pawn " + pawn + " lacks MorePrecepts.PawnComp, not based on BasePawn ThingDef?");
            return -99999;
        }

        public override void Initialize(CompProperties props)
        {
            lastTakeAlcoholTick = -99999;
            lastViolenceTick = -99999;
            lastDownedTick = -99999;
            lastDownedTicksUntilDeath = -99999;
            burnedOnPyre = false;
            noticedDrugsTick = -99999;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastTakeAlcoholTick, "MorePrecepts.LastTakeAlcoholTick", -99999);
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecepts.LastViolenceTick", -99999);
            Scribe_Values.Look(ref lastDownedTick, "MorePrecepts.LastDownedTick", -99999);
            Scribe_Values.Look(ref lastDownedTicksUntilDeath, "MorePrecepts.LastDownedTicksUntilDeath", -99999);
            Scribe_Values.Look(ref burnedOnPyre, "MorePrecepts.BurnedOnPyre", false);
            Scribe_Values.Look(ref noticedDrugsTick, "MorePrecepts.NoticedDrugsTick", -99999);
        }

        [HarmonyPatch(typeof(ThingWithComps))]
        [HarmonyPatch(nameof(InitializeComps))]
        [HarmonyPrefix]
        public static void InitializeComps()
        {
        }

        [HarmonyPatch(typeof(Game))]
        [HarmonyPatch(nameof(ClearCaches))]
        [HarmonyPostfix]
        public static void ClearCaches()
        {
        }
    }
}
