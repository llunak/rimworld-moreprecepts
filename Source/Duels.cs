using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace MorePrecepts
{

    public class RitualOutcomeEffectWorker_AnimalDuel : RitualOutcomeEffectWorker_Duel
    {
        public RitualOutcomeEffectWorker_AnimalDuel()
        {
        }

        public RitualOutcomeEffectWorker_AnimalDuel(RitualOutcomeEffectDef def)
            : base(def)
        {
        }

        protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual, OutcomeChance outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            // Outcome adds melee skill, which animals do not have, so filter the animal out.
            Dictionary<Pawn, int> presenceWithoutAnimals
                = totalPresence.Where( f => !f.Key.RaceProps.Animal ).ToDictionary( f => f.Key, f => f.Value );
            base.ApplyExtraOutcome(presenceWithoutAnimals, jobRitual, outcome, out extraOutcomeDesc, ref letterLookTargets);
        }
    }

}
