using RimWorld;
using Verse;

namespace MorePrecepts
{
    // A ThingComp attached to pawns, to contain all pawn extra data for this mod.
    public class PawnComp : ThingComp
    {
        // For alcohol  precept.
        public int lastTakeAlcoholTick;

        // For violence precept.
        public int lastViolenceTick;

        // For funeral pyre.
        public bool burnedOnPyre;

        public override void Initialize(CompProperties props)
        {
            lastTakeAlcoholTick = -99999;
            lastViolenceTick = -99999;
            burnedOnPyre = false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastTakeAlcoholTick, "MorePrecepts.LastTakeAlcoholTick", -99999);
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecepts.LastViolenceTick", -99999);
            Scribe_Values.Look(ref burnedOnPyre, "MorePrecepts.BurnedOnPyre", false);
        }
    }
}
