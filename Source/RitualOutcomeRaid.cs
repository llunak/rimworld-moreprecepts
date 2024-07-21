using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using RimWorld;

namespace MorePrecepts
{
    public class RitualAttachableOutcomeEffectWorker_Raid : RitualAttachableOutcomeEffectWorker
    {
        public override void Apply(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual, RitualOutcomePossibility outcome,
            out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            extraOutcomeDesc = null;
            float chance = 0.5f;
            // Rituals with violent precepts are more likely to cause a raid.
            foreach(Pawn pawn in jobRitual.assignments.Participants)
            {
                if(pawn.Ideo != null && (pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Essential) || pawn.Ideo.HasPrecept(PreceptDefOf.Violence_Wanted)))
                {
                    chance = outcome.BestPositiveOutcome(jobRitual) ? 0.9f : 0.8f;
                    break;
                }
            }
            if (!Rand.Chance(chance))
                return;
            IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, jobRitual.Map);
            incidentParms.forced = true;
            if (IncidentDefOf.RaidEnemy.Worker.CanFireNow(incidentParms))
            {
                IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms);
                extraOutcomeDesc = def.letterInfoText;
            }
        }
    }
}
