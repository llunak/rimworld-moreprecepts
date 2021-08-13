using RimWorld;
using Verse;

namespace MorePrecepts
{
    // A ThingComp attached to pawns, to contain all pawn extra data for this mod.
    public class PawnComp : ThingComp
    {
        // For violence precept.
        public int lastViolenceTick;

        public override void Initialize(CompProperties props)
        {
            lastViolenceTick = -99999;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastViolenceTick, "MorePrecents.LastViolenceTick", -99999);
        }
    }
}
