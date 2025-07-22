using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

/*
Cases to test:
- burning a dead body on the pyre
- burning a corpse to lose the body, then doing the ritual
*/

namespace MorePrecepts
{

    // Basing this on Building_Grave should save a lot of code, as the pyre is a "grave" that gets burned.
    public class Building_FuneralPyre : Building_Grave
    {
        public override int OpenTicks => 10;

        public override void Notify_HauledTo(Pawn hauler, Thing thing, int count)
        {
            // Count this as burying, but ignore all the other grave things.
            hauler.records.Increment(RimWorld.RecordDefOf.CorpsesBuried);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt( phase, drawLoc, flip );
            if (base.HasCorpse)
            {
                Vector3 drawLoc2 = base.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) + def.building.gibbetCorposeDrawOffset;
                float corpseRotation = 0;
                base.Corpse.InnerPawn.Drawer.renderer.wiggler.SetToCustomRotation(corpseRotation);
                base.Corpse.DynamicDrawPhaseAt(phase, drawLoc2);
            }
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
                    PawnComp.SetBurnedOnPyre(pyre.Corpse.InnerPawn);
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
            return target.HasThing && (building_FuneralPyre = target.Thing as Building_FuneralPyre) != null && (building_FuneralPyre.Corpse == obligation.targetA.Thing || building_FuneralPyre.Corpse?.InnerPawn == obligation.targetA.Thing);
        }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            if (obligation == null)
            {
                yield return "MorePrecepts.RitualTargetFuneralPyreInfoAbstract".Translate(parent.ideo.Named("IDEO"));
                yield break;
            }
            Pawn pawn = obligation.targetA.Thing as Pawn;
            if (pawn == null)
                pawn = ((Corpse)obligation.targetA.Thing).InnerPawn;
            yield return "MorePrecepts.RitualTargetFuneralPyreInfo".Translate(pawn.Named("PAWN"));
        }

        public override List<string> MissingTargetBuilding(Ideo ideo)
        {
            for (int i = 0; i < ideo.PreceptsListForReading.Count; i++)
            {
                Precept_Building precept_Building = ideo.PreceptsListForReading[i] as Precept_Building;
                if (precept_Building != null && precept_Building.ThingDef.defName == "FuneralPyre")
                    return null;
            }
            return new List<string> { "MorePrecepts.FuneralPyre".Translate() };
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
            Corpse corpse = (obligation.targetA.Thing as Pawn)?.Corpse;
            if (obligation.targetA.HasThing)
            {
                Pawn pawn = obligation.targetA.Thing as Pawn;
                if(pawn != null)
                {
                    if(PawnComp.GetBurnedOnPyre(pawn))
                        return false;
                }
                if (corpse != null)
                    return !corpse.Destroyed;
                return true;
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
                yield return "MorePrecepts.RitualTargetEmptyFuneralPyreInfoAbstract".Translate(parent.ideo.Named("IDEO"));
                yield break;
                }
            Pawn arg = (Pawn)obligation.targetA.Thing;
            yield return "MorePrecepts.RitualTargetEmptyFuneralPyreInfo".Translate(arg.Named("PAWN"));
        }

        public override List<string> MissingTargetBuilding(Ideo ideo)
        {
            for (int i = 0; i < ideo.PreceptsListForReading.Count; i++)
            {
                Precept_Building precept_Building = ideo.PreceptsListForReading[i] as Precept_Building;
                if (precept_Building != null && precept_Building.ThingDef.defName == "FuneralPyre")
                    return null;
            }
            return new List<string> { "MorePrecepts.FuneralPyre".Translate() };
        }
    }

    public class RitualObligationTrigger_MemberCorpseDestroyedNoPyreProperties : RitualObligationTrigger_MemberCorpseDestroyedProperties
    {
        public RitualObligationTrigger_MemberCorpseDestroyedNoPyreProperties()
            : base()
        {
            triggerClass = typeof(RitualObligationTrigger_MemberCorpseDestroyedNoPyre);
        }
    }

    public class RitualObligationTrigger_MemberCorpseDestroyedNoPyre : RitualObligationTrigger_MemberCorpseDestroyed
    {
        public override void Notify_MemberCorpseDestroyed(Pawn p)
        {
            if(PawnComp.GetBurnedOnPyre( p ))
                return;
            base.Notify_MemberCorpseDestroyed( p );
        }
    }

    // Prevent the pyre to be usable with vanilla grave funeral ritual (Building_Pyre inherits from Building_Grave).
    [HarmonyPatch(typeof(RitualObligationTargetWorker_GraveWithTarget))]
    public class RitualObligationTargetWorker_GraveWithTarget_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GetTargets))]
        public static void GetTargets(ref IEnumerable<TargetInfo> __result, RitualObligation obligation, Map map)
        {
            Thing thing = __result.FirstOrDefault().Thing;
            if( thing != null && thing is Building_FuneralPyre )
                __result = null;
            else
                __result = new TargetInfo[]{ thing };
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanUseTargetInternal))]
        public static bool CanUseTargetInternal(ref RitualTargetUseReport __result, TargetInfo target, RitualObligation obligation)
        {
            if( target.HasThing && target.Thing is Building_FuneralPyre )
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

}
