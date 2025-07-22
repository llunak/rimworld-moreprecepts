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

    // Like JobDriver_StandAndBeSociallyActive, but also reserve the seat.
    public class JobDriver_SitAndBeSociallyActive : JobDriver_StandAndBeSociallyActive
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.ReserveSittableOrSpot(pawn.Position, job, errorOnFailed);
        }
    }

}
