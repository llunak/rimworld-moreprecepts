using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MorePrecepts
{

    public class JobGiver_SitAndBeSociallyActive : JobGiver_StandAndBeSociallyActive
    {
        protected override Job TryGiveJob(Pawn pawn)
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

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = new Toil();
            toil.tickAction = delegate
            {
                if (job.lookDirection != Direction8Way.Invalid)
                    base.pawn.rotationTracker.Face(base.pawn.Position.ToVector3() + job.lookDirection.AsVector());
                base.pawn.GainComfortFromCellIfPossible();
            };
            toil.socialMode = RandomSocialMode.SuperActive;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            yield return toil;
        }
    }

}
