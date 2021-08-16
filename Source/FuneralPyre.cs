using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MorePrecepts
{

    // Basing this on Building_Grave should save a lot of code, as the pyre is a "grave" that gets burned.
    public class Building_FuneralPyre : Building_Grave
    {
        public override int OpenTicks => 10;

        public override void Notify_CorpseBuried(Pawn worker)
        {
            // Count this as burying, but ignore all the other grave things.
            worker.records.Increment(RecordDefOf.CorpsesBuried);
        }

        public override void Draw()
        {
            base.Draw();
            if (base.HasCorpse)
                base.Corpse.DrawAt(base.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) + def.building.gibbetCorposeDrawOffset);
        }
    }

    public class RitualOutcomeEffectWorker_FuneralPyre : RitualOutcomeEffectWorker_RemoveConsumableBuilding
    {
        public RitualOutcomeEffectWorker_FuneralPyre()
        {
        }

        public RitualOutcomeEffectWorker_FuneralPyre(RitualOutcomeEffectDef def)
            : base(def)
        {
        }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            if (jobRitual.selectedTarget.HasThing)
            {
                Building_FuneralPyre pyre = jobRitual.selectedTarget.Thing as Building_FuneralPyre;
                if(pyre != null && jobRitual.Organizer != null)
                    TaleRecorder.RecordTale(TaleDefOf.BurnedCorpse, jobRitual.Organizer, (pyre.Corpse != null) ? pyre.Corpse.InnerPawn : null);
                if(pyre.Corpse != null)
                {
                    PawnComp comp = pyre.Corpse.InnerPawn.GetComp<PawnComp>();
                    comp.burnedOnPyre = true;
                }
            }
            base.Apply(progress, totalPresence, jobRitual);
        }
    }

    public class RitualObligationTargetWorker_FuneralPyreWithTarget : RitualObligationTargetWorker_GraveWithTarget
    {
        public RitualObligationTargetWorker_FuneralPyreWithTarget()
        {
        }

        public RitualObligationTargetWorker_FuneralPyreWithTarget(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            Thing thing = map.listerThings.ThingsInGroup(ThingRequestGroup.Grave).FirstOrDefault((Thing t) => t is Building_FuneralPyre && ((Building_FuneralPyre)t).Corpse == obligation.targetA.Thing);
            if (thing != null)
                yield return thing;
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            Building_FuneralPyre building_FuneralPyre;
            return target.HasThing && (building_FuneralPyre = target.Thing as Building_FuneralPyre) != null && building_FuneralPyre.Corpse == obligation.targetA.Thing;
        }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            if (obligation == null)
            {
                yield return "RitualTargetFuneralPyreInfoAbstract".Translate(parent.ideo.Named("IDEO"));
                yield break;
            }
            bool num = obligation.targetA.Thing.ParentHolder is Building_FuneralPyre;
            Pawn innerPawn = ((Corpse)obligation.targetA.Thing).InnerPawn;
            TaggedString taggedString = "RitualTargetFuneralPyreInfo".Translate(innerPawn.Named("PAWN"));
            if (!num)
            {
                taggedString += " (" + "RitualTargetFuneralPyreInfoMustBePlaced".Translate(innerPawn.Named("PAWN")) + ")";
            }
            yield return taggedString;
        }

        public override List<string> MissingTargetBuilding(Ideo ideo)
        {
            for (int i = 0; i < ideo.PreceptsListForReading.Count; i++)
            {
                Precept_Building precept_Building = ideo.PreceptsListForReading[i] as Precept_Building;
                if (precept_Building != null && precept_Building.ThingDef.defName == "FuneralPyre")
                    return null;
            }
            return new List<string> { "FuneralPyre".Translate() };
        }
    }

    public class RitualObligationTargetWorker_AnyEmptyFuneralPyre : RitualObligationTargetWorker_AnyEmptyGrave
    {
        public RitualObligationTargetWorker_AnyEmptyFuneralPyre()
        {
        }

        public RitualObligationTargetWorker_AnyEmptyFuneralPyre(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            Thing thing = map.listerThings.ThingsInGroup(ThingRequestGroup.Grave).FirstOrDefault((Thing t) => t is Building_FuneralPyre && ((Building_FuneralPyre)t).Corpse == null);
            if (thing != null)
                yield return thing;
        }

        public override bool ObligationTargetsValid(RitualObligation obligation)
        {
            if (obligation.targetA.HasThing)
            {
                Pawn pawn = obligation.targetA.Thing as Pawn;
                if(pawn != null)
                {
                    PawnComp comp = pawn.GetComp<PawnComp>();
                    if(comp.burnedOnPyre)
                        return false;
                }
                return obligation.targetA.ThingDestroyed;
            }
            return false;
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            Building_FuneralPyre building_FuneralPyre;
            return target.HasThing && (building_FuneralPyre = target.Thing as Building_FuneralPyre) != null && building_FuneralPyre.Corpse == null;
        }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            if (obligation == null)
                {
                yield return "RitualTargetEmptyFuneralPyreInfoAbstract".Translate(parent.ideo.Named("IDEO"));
                yield break;
                }
            Pawn arg = (Pawn)obligation.targetA.Thing;
            TaggedString taggedString = "RitualTargetEmptyFuneralPyreInfo".Translate(arg.Named("PAWN"));
            yield return taggedString;
        }

        public override List<string> MissingTargetBuilding(Ideo ideo)
        {
            for (int i = 0; i < ideo.PreceptsListForReading.Count; i++)
            {
                Precept_Building precept_Building = ideo.PreceptsListForReading[i] as Precept_Building;
                if (precept_Building != null && precept_Building.ThingDef.defName == "FuneralPyre")
                    return null;
            }
            return new List<string> { "FuneralPyre".Translate() };
        }
    }

    [HarmonyPatch(typeof(IdeoUIUtility))]
    public static class IdeoUIUtility_Patch2
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanAddPrecept))]
        public static bool CanAddPrecept(ref AcceptanceReport __result, ref PreceptDef def, RitualPatternDef pat, Ideo ideo)
        {
            // Do not allow more than one funeral type. This primarily blocks usage of standard funeral rituals
            // with our funeral pyres, but it probably doesn't make much sense to have more than one funeral type anyway.
            // The exclusionTags tag is supported here, but for some reason it blocks duplicates only when
            // creating an ideology, not when editing it.
            if( pat != null && pat.defName.Contains("Funeral"))
            {
                foreach (Precept item in ideo.PreceptsListForReading)
                {
                    if (item is Precept_Ritual && item.def.defName.Contains("Funeral"))
                    {
                        __result = new AcceptanceReport("IdeoAlreadyHasFuneral".Translate());
                        return false;
                    }
                }
            }
            return true;
        }
    }

}
