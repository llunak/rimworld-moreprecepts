using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MorePrecepts
{

    public class JobGiver_SitAndBeSociallyActive : JobGiver_StandAndBeSociallyActive
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            Job job = base.TryGiveJob(pawn);
            job.def = JobDefOf.SitAndBeSociallyActive;
            return job;
        }
    }

    // Like JobDriver_StandAndBeSociallyActive, but also reserve the seat, and don't
    // turn towards other pawns (=keep facing the table).
    public class JobDriver_SitAndBeSociallyActive : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.ReserveSittableOrSpot(pawn.Position, job, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            if (base.TargetLocA.IsValid)
                yield return Toils_Goto.GotoCell(base.TargetLocA, PathEndMode.OnCell);
            Toil toil = new Toil();
            toil.tickIntervalAction = delegate(int delta)
            {
                if (job.lookDirection != Direction8Way.Invalid)
                    base.pawn.rotationTracker.Face(base.pawn.Position.ToVector3() + job.lookDirection.AsVector());
                base.pawn.GainComfortFromCellIfPossible(delta);
            };
            toil.socialMode = RandomSocialMode.SuperActive;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            yield return toil;
        }
    }

}
