using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MorePrecepts
{

    // temporary
    public static class AlcoholHelper
    {
        public static bool NeedsAlcoholOverride(ThingDef thing, Pawn pawn)
        {
            return false;
        }
        public static void AddOverride( bool doIt = true )
        {
        }
        public static void RemoveOverride(bool doIt = true)
        {
        }
    }

    [HarmonyPatch(typeof(Thing))]
    public static class Thing3_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Ingested))]
        public static void Ingested(Thing __instance, Pawn ingester, float nutritionWanted)
        {
            LordJob_Ritual_DrinkingParty lordJob_Ritual = ingester.GetLord()?.LordJob as LordJob_Ritual_DrinkingParty;
            if (lordJob_Ritual != null)
                lordJob_Ritual.AddDrink(__instance);
        }
    }

    // Extra worker and lordjob for keeping track of drinks drunk during the party.
    public class RitualBehaviorWorker_DrinkingParty : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_DrinkingParty()
        {
        }

        public RitualBehaviorWorker_DrinkingParty(RitualBehaviorDef def)
            : base(def)
        {
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            return new LordJob_Ritual_DrinkingParty(target, ritual, obligation, def.stages, assignments, organizer);
        }
    }

    public class LordJob_Ritual_DrinkingParty : LordJob_Ritual
    {
        public float joySum = 0;
        public int drinksCount = 0;

        public LordJob_Ritual_DrinkingParty()
        {
        }

        public LordJob_Ritual_DrinkingParty(TargetInfo selectedTarget, Precept_Ritual ritual, RitualObligation obligation, List<RitualStage> allStages, RitualRoleAssignments assignments, Pawn organizer = null)
            : base(selectedTarget, ritual, obligation, allStages, assignments, organizer)
        {
        }

        public void AddDrink(Thing thing)
        {
            joySum += thing.def.ingestible.joy;
            ++drinksCount;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref joySum, "joySum", 0);
            Scribe_Values.Look(ref drinksCount, "drinksCount", 0);
        }
    }

    public class RitualOutcomeComp_DrinkingPartyDrinks : RitualOutcomeComp_Quality
    {
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            LordJob_Ritual_DrinkingParty lordJob = ritual as LordJob_Ritual_DrinkingParty;
            if(lordJob == null)
                return 0;
            int participantCount = 0;
            foreach (Pawn item in ritual.PawnsToCountTowardsPresence)
            {
                if (ritual.Ritual != null)
                {
                    RitualRole ritualRole = ritual.RoleFor(item, includeForced: true);
                    if (ritualRole != null && !ritualRole.countsAsParticipant)
                        continue;
                }
                ++participantCount;
            }
            if(lordJob.drinksCount < participantCount)
                return 0; // Not enough drinks.
            return lordJob.joySum * 100 / participantCount; // map to average joy per pawn (17 is one beer, XML counts 50 as max)
        }

        public override ExpectedOutcomeDesc GetExpectedOutcomeDesc(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
        {
            return new ExpectedOutcomeDesc
            {
                label = label.CapitalizeFirst(),
                present = false,
                uncertainOutcome = true,
                effect = ExpectedOffsetDesc(positive: true, 0),
                positive = true
            };
        }
    }

    // Based on JobGiver_EatInGatheringArea.
    public class JobGiver_DrinkInGatheringArea : ThinkNode_JobGiver
    {
        private static List<ThingDef> nurseableDrugs = new List<ThingDef>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            PawnDuty duty = pawn.mindState.duty;
            if (duty == null)
                return null;
            int delay = GenDate.TicksPerHour / 2;
            if(pawn.story != null)
            {
                Trait trait = pawn.story.traits.GetTrait(TraitDefOf.DrugDesire);
                if (trait != null)
                {   // Consume more often for drug desire.
                    if (trait.Degree == 1)
                        delay /= 2;
                    if (trait.Degree == 2)
                        delay /= 3;
                }
            }
            if (Find.TickManager.TicksGame - PawnComp.GetLastTakeAlcoholTick(pawn) < delay
                || Find.TickManager.TicksGame - pawn.mindState.lastTakeRecreationalDrugTick < delay)
            {
                return null;
            }
            IntVec3 cell = duty.focus.Cell;
            Thing thing = FindDrink(pawn, cell);
            if (thing == null)
                return null;
            Job job = JobMaker.MakeJob(RimWorld.JobDefOf.Ingest, thing);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, thing.def, thing.def.GetStatValueAbstract(StatDefOf.Nutrition));
            return job;
        }

        private Thing FindDrink(Pawn pawn, IntVec3 gatheringSpot)
        {
            Predicate<Thing> validator = delegate(Thing x)
            {
                if (!x.IngestibleNow)
                    return false;
                if (!GatheringsUtility.InGatheringArea(x.Position, gatheringSpot, pawn.Map))
                    return false;
                if (x.def.ingestible == null || (x.def.ingestible.foodType & FoodTypeFlags.Fluid) != FoodTypeFlags.Fluid
                    || !x.def.IsDrug)
                    return false;
                bool alcoholOverride = AlcoholHelper.NeedsAlcoholOverride(x.def,pawn);
                AlcoholHelper.AddOverride(alcoholOverride);
                if (!pawn.WillEat(x) || pawn.IsTeetotaler())
                {
                    AlcoholHelper.RemoveOverride(alcoholOverride);
                    return false;
                }
                AlcoholHelper.RemoveOverride(alcoholOverride);
                if (x.IsForbidden(pawn))
                    return false;
                if (!x.IsSociallyProper(pawn))
                    return false;
                return pawn.CanReserve(x) ? true : false;
            };
            // Based on JoyGiver_SocialRelax.TryFindIngestibleToNurse().
            nurseableDrugs.Clear();
            DrugPolicy currentPolicy = pawn.drugs.CurrentPolicy;
            for (int i = 0; i < currentPolicy.Count; i++)
            {
                if (currentPolicy[i].allowedForJoy && currentPolicy[i].drug.ingestible.nurseable)
                    nurseableDrugs.Add(currentPolicy[i].drug);
            }

            nurseableDrugs.Shuffle();
            for (int j = 0; j < nurseableDrugs.Count; j++)
            {
                List<Thing> list = pawn.Map.listerThings.ThingsOfDef(nurseableDrugs[j]);
                if (list.Count > 0)
                {
                    Thing ingestible = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                        ThingRequest.ForGroup(ThingRequestGroup.Drug), PathEndMode.ClosestTouch,
                        TraverseParms.For(TraverseMode.NoPassClosedDoors), 14f, validator, null, 0, 12);
                    if (ingestible != null)
                        return ingestible;
                }
            }
            return null;
        }
    }

}
