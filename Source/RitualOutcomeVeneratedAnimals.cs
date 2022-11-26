using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

// This is basically copy&paste of FarmAnimalsWanderIn code (can't quite just subclass,
// some relevant code is private).
namespace MorePrecepts
{
    public class IncidentWorker_VeneratedAnimalsWanderIn : IncidentWorker
    {
        private const float TotalBodySizeToSpawn = 2.5f;

        private static SimpleCurve BaseChanceFactorByAnimalBodySizePerCapitaCurve = new SimpleCurve
        {
            new CurvePoint(0f, 1.5f),
            new CurvePoint(4f, 1f)
        };

        private const float SelectionChanceFactorIfExistingMatingPair = 0.5f;

        public override float BaseChanceThisGame
        {
            get
            {
                if (!ModsConfig.IdeologyActive /*|| !IdeoUtility.AnyColonistWithRanchingIssue()*/)
                {
                    return base.BaseChanceThisGame;
                }
                return base.BaseChanceThisGame
                    * BaseChanceFactorByAnimalBodySizePerCapitaCurve.Evaluate(PawnUtility.PlayerAnimalBodySizePerCapita());
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }
            Map map = (Map)parms.target;
            PawnKindDef kind;
            if (RCellFinder.TryFindRandomPawnEntryCell(out var _, map, CellFinder.EdgeRoadChance_Animal))
            {
                return TryFindRandomPawnKind(map, out kind, parms.pawnIdeo);
            }
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }
            if (!TryFindRandomPawnKind(map, out var kind, parms.pawnIdeo))
            {
                return false;
            }
            int num = Mathf.Clamp(GenMath.RoundRandom(((parms.totalBodySize > 0f) ? parms.totalBodySize : 2.5f)
                / kind.RaceProps.baseBodySize), 2, 10);
            if (num >= 2)
            {
                SpawnAnimal(result, map, kind, Gender.Female);
                SpawnAnimal(result, map, kind, Gender.Male);
                num -= 2;
            }
            for (int i = 0; i < num; i++)
            {
                SpawnAnimal(result, map, kind);
            }
            SendStandardLetter(parms.customLetterLabel
                ?? ((string)"MorePrecepts.LetterLabelVeneratedAnimalsWanderIn".Translate(kind.GetLabelPlural()).CapitalizeFirst()),
                parms.customLetterText ?? ((string)"MorePrecepts.LetterVeneratedAnimalsWanderIn".Translate(kind.GetLabelPlural())),
                LetterDefOf.PositiveEvent, parms, new TargetInfo(result, map));
            return true;
        }

        private void SpawnAnimal(IntVec3 location, Map map, PawnKindDef pawnKind, Gender? gender = null)
        {
            IntVec3 loc = CellFinder.RandomClosewalkCellNear(location, map, 12);
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pawnKind, null, PawnGenerationContext.NonPlayer,
                -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false,
                allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null,
                null, null, null, null, null, null, gender));
            GenSpawn.Spawn(pawn, loc, map, Rot4.Random);
            /*pawn.SetFaction(Faction.OfPlayer);*/
        }

        private bool TryFindRandomPawnKind(Map map, out PawnKindDef kind, Ideo ideo)
        {
            return DefDatabase<PawnKindDef>.AllDefs.Where(
                (PawnKindDef x) => x.RaceProps.Animal /*&& x.RaceProps.wildness < 0.35f*/
                && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(x.race)
                /*&& !x.race.tradeTags.NullOrEmpty() && x.race.tradeTags.Contains("AnimalFarm")*/ && !x.RaceProps.Dryad
                && (ideo != null ? ideo.IsVeneratedAnimal(x.race) : false)) // added
                .TryRandomElementByWeight((PawnKindDef k) => SelectionChance(k), out kind);
        }

        private float SelectionChance(PawnKindDef pawnKind)
        {
            float num = 1;/*0.42000002f - pawnKind.RaceProps.wildness;*/
            if (PawnUtility.PlayerHasReproductivePair(pawnKind))
            {
                num *= 0.5f;
            }
            return num;
        }
    }

    public class RitualAttachableOutcomeEffectWorker_VeneratedAnimalsWanderIn : RitualAttachableOutcomeEffectWorker
    {
        public const float PositiveOutcomeBodysize = 2f;

        public const float BestOutcomeBodysize = 3f;

        public override void Apply(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
            OutcomeChance outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            extraOutcomeDesc = null;
            IncidentParms parms = new IncidentParms
            {
                target = jobRitual.Map,
                totalBodySize = (outcome.BestPositiveOutcome(jobRitual) ? 3f : 2f),
                customLetterText = "MorePrecepts.RitualAttachedOutcome_VeneratedAnimalsWanderIn_Desc".Translate(jobRitual.RitualLabel),
                pawnIdeo = jobRitual.Ritual?.ideo
            };
            if (IncidentDefOf.MP_VeneratedAnimalsWanderIn.Worker.TryExecute(parms))
            {
                extraOutcomeDesc = def.letterInfoText;
            }
        }
    }

    [HarmonyPatch(typeof(RitualAttachableOutcomeEffectDef))]
    public static class RitualAttachableOutcomeEffectDef_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanAttachToRitual))]
        public static bool CanAttachToRitual(ref AcceptanceReport __result, RitualAttachableOutcomeEffectDef __instance, Precept_Ritual ritual)
        {
            if( __instance == RitualAttachableOutcomeEffectDefOf.MP_VeneratedAnimalsWanderIn
                && ritual.ideo.VeneratedAnimals.Count == 0)
            {
                __result = "MorePrecepts.RitualAttachedRewardRequiredVeneratedAnimal".Translate();
                return false;
            }
            return true;
        }
    }
}
