using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MorePrecepts
{

/*
A big complication of a separate precept for alcohol is that the game treats alcohol (beer) as a recreational
drug, so it gets handled by the druguse precept. That means that having DrugUse:MedicalOnly together
with Alcohol:Wanted would normally conflict, as core game would forbid alcohol based on the first precept.

So the most code here tries to disable core from including alcohol in recreational drugs, which is not
trivial, as even IsTeetotaler() returns true if DrugUse:MedicalOnly is set, thus normally blocking beer use.
So patch all drug-related code to follow the alcohol precept instead of the drug use one. This includes
avoiding drug HistoryEventDef events and sending alcohol ones, otherwise pawns would get thoughts
from both alcohol and drugs precepts. That may possibly break mods that react to those events, but oh well.
*/

    public static class AlcoholHelper
    {
        public static bool NeedsAlcoholOverride(ThingDef thing, Pawn pawn)
        {
            if(!HasAlcoholPrecept(pawn))
                return false;
            if(!IsAlcohol(thing))
                return false;
            return true;
        }
        public static bool HasAlcoholPrecept(Pawn pawn)
        {
            // Override DrugUse only if Alcohol precept is actually active.
            if(pawn.Ideo == null )
                return false;
            return pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Prohibited)
                || pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Disapproved)
                || pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Neutral)
                || pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Wanted)
                || pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Essential);
        }
        public static bool HasDislikingAlcoholPrecept(Pawn pawn)
        {
            if(pawn.Ideo == null )
                return false;
            return pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Prohibited)
                || pawn.Ideo.HasPrecept(PreceptDefOf.Alcohol_Disapproved);
        }
        // This only checks if the thing is alcohol, normally we need to call NeedsAlcoholOverride()
        // to also check if alcohol should be treated specially.
        public static bool IsAlcohol(ThingDef thing)
        {
            if (thing.IsDrug)
            {
                CompProperties_Drug compDrug = (CompProperties_Drug)thing.CompDefFor<CompDrug>();
                if (compDrug != null && compDrug.chemical == ChemicalDefOf.Alcohol)
                    return true;
            }
            return false;
        }
        // See PawnUtility_Patch below.
        public static int overrideCounter = 0;
        public static void AddOverride( bool doIt = true )
        {
            if(!doIt)
                return;
            ++overrideCounter;
            if(overrideCounter > 10)
                Log.Error("MorePrecepts: IsTeetotaler() broken override add");
        }
        public static void RemoveOverride(bool doIt = true)
        {
            if(!doIt)
                return;
            --overrideCounter;
            if(overrideCounter < 0)
                Log.Error("MorePrecepts: IsTeetotaler() broken override remove");
        }
    }

// We need to patch IsTeetotaler() to not return true if only DrugUse:MedicalOnly is set (in which
// the case function would normally return true). That means we also need to check all calls
// to IsTeetotaler() and add a drug use check if needed. If the item is actually alcohol, we'll
// temporarily tell IsTeetotaler() to return false if drug use would be the only reason it would
// return true.
    [HarmonyPatch(typeof(PawnUtility))]
    public static class PawnUtility_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(IsTeetotaler))]
        public static bool IsTeetotaler(ref bool __result, Pawn pawn)
        {
            if(AlcoholHelper.overrideCounter == 0)
                return true; // proceed normally
            // Our functionality that ignores drug settings.
            if (!new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                __result = true;
            if (pawn.story != null)
                __result = pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) < 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(Bill_Medical))]
    public static class Bill_Medical_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PawnAllowedToStartAnew))]
        public static void PawnAllowedToStartAnew(ref bool __result, out RecipeDef __state, Bill_Medical __instance, Pawn pawn)
        {
            __state = null;
            if (__instance.recipe.Worker is Recipe_AdministerIngestible)
            {
                ThingDef singleDef = __instance.recipe.ingredients[0].filter.BestThingRequest.singleDef;
                if(AlcoholHelper.NeedsAlcoholOverride(singleDef, pawn))
                {
                    // We need to also call base.PawnAllowedToStartAnew(), which is complicated with patching.
                    // So temporarily unset the recipe and call the original version, which will then just call
                    // the base function.
                    __state = __instance.recipe;
                    __instance.recipe = null;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnAllowedToStartAnew))]
        public static void PawnAllowedToStartAnew(ref bool __result, RecipeDef __state, Bill_Medical __instance, Pawn pawn)
        {
            if(__state == null)
                return;
            __instance.recipe = __state;
            if (__instance.recipe.Worker is Recipe_AdministerIngestible)
            {
                ThingDef singleDef = __instance.recipe.ingredients[0].filter.BestThingRequest.singleDef;
                if(AlcoholHelper.NeedsAlcoholOverride(singleDef, pawn))
                {
                    __result = new HistoryEvent(HistoryEventDefOf.AdministeredAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job();
                }
            }
        }
    }

// These places to patch are those that have 'IngestedDrug', 'AdministeredDrug' or 'IsTeetotaler'.

    [HarmonyPatch(typeof(Recipe_AdministerIngestible))]
    public static class Recipe_AdministerIngestible_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void ApplyOnPawn(Pawn pawn, BodyPartRecord part, ref Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (AlcoholHelper.NeedsAlcoholOverride(ingredients[0].def, billDoer))
            {
                // If alcohol, send alcohol event.
                if(billDoer != null)
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.AdministeredAlcohol, billDoer.Named(HistoryEventArgsNames.Doer)));
                // And block sending drug events, which is inside 'billDoer' check, but rest of the original function will still do its work.
                billDoer = null;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void GetLabelWhenUsedOn(out bool __state, Recipe_AdministerIngestible __instance, Pawn pawn, BodyPartRecord part)
        {
            ThingDef singleDef = __instance.recipe.ingredients[0].filter.BestThingRequest.singleDef;
            __state = AlcoholHelper.NeedsAlcoholOverride(singleDef, pawn);
            AlcoholHelper.AddOverride( __state );
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void GetLabelWhenUsedOn(bool __state, Pawn pawn, BodyPartRecord part)
        {
            AlcoholHelper.RemoveOverride( __state );
        }
    }

    [HarmonyPatch(typeof(CaravanPawnsNeedsUtility))]
    public static class CaravanPawnsNeedsUtility_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CanEatForNutritionEver))]
        public static void CanEatForNutritionEver(ref bool __result, ThingDef food, Pawn pawn)
        {
            if(AlcoholHelper.NeedsAlcoholOverride(food, pawn))
            {
                // Override return value for alcohol.
                AlcoholHelper.AddOverride();
                if (food.IsNutritionGivingIngestible && pawn.WillEat_NewTemp(food, null, careIfNotAcceptableForTitle: false)
                    && (int)food.ingestible.preferability > 1 && !pawn.IsTeetotaler() && food.ingestible.canAutoSelectAsFoodForCaravan)
                {
                    __result = new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
                }
                AlcoholHelper.RemoveOverride();
            }
        }
    }

    [HarmonyPatch(typeof(CompDrug))]
    public static class CompDrug_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PostIngested))]
        public static bool PostIngested(CompDrug __instance, Pawn ingester)
        {
            if(AlcoholHelper.IsAlcohol(__instance.parent.def))
            {
                // Do unconditionally alcohol-only things.
                if (!ingester.Dead)
                    PawnComp.SetLastTakeAlcoholTickToNow(ingester);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, ingester.Named(HistoryEventArgsNames.Doer)));
            }
            if(!AlcoholHelper.NeedsAlcoholOverride(__instance.parent.def, ingester))
                return true; // original processing
            // Big scary copy&paste&modify to remove drug events and use alcohol event instead.
			if (__instance.Props.Addictive && ingester.RaceProps.IsFlesh)
			{
				float num = 1f;
				if (ModsConfig.BiotechActive && ingester.genes != null)
				{
					foreach (Gene item in ingester.genes.GenesListForReading)
					{
						num *= item.def.overdoseChanceFactor;
					}
				}
				if (Rand.Chance(num))
				{
					float num2 = ingester.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;
					bool flag = false;
					if (ModsConfig.BiotechActive && ingester.genes != null)
					{
						foreach (Gene item2 in ingester.genes.GenesListForReading)
						{
							Gene_ChemicalDependency gene_ChemicalDependency;
							if ((gene_ChemicalDependency = item2 as Gene_ChemicalDependency) != null && gene_ChemicalDependency.def.chemical == __instance.Props.chemical)
							{
								flag = true;
								break;
							}
						}
					}
					if (num2 < 0.9f && !flag && Rand.Value < __instance.Props.largeOverdoseChance)
					{
						float num3 = Rand.Range(0.85f, 0.99f);
						HealthUtility.AdjustSeverity(ingester, HediffDefOf.DrugOverdose, num3 - num2);
						if (ingester.Faction == Faction.OfPlayer)
						{
							Messages.Message("MessageAccidentalOverdose".Translate(ingester.Named("INGESTER"), __instance.parent.LabelNoCount, __instance.parent.Named("DRUG")), ingester, MessageTypeDefOf.NegativeHealthEvent);
						}
					}
					else
					{
						float num4 = __instance.Props.overdoseSeverityOffset.RandomInRange / ingester.BodySize;
						if (num4 > 0f)
						{
							HealthUtility.AdjustSeverity(ingester, HediffDefOf.DrugOverdose, num4);
						}
					}
				}
			}
			if (ingester.drugs != null)
			{
				ingester.drugs.Notify_DrugIngested(__instance.parent);
			}
            // End of big scary copy&paste&modify.
            return false; // no original processing
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    public static class FloatMenuMakerMap_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AddHumanlikeOrders))]
        public static void AddHumanlikeOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            // The functions may block the menu entry for drinking alcohol. It's quite nested in the function, making it hard(?)
            // to replace, so just post-process the menu entries.
            List<Pair<string,Thing>> alcoholItems = new List<Pair<string,Thing>>();
            IntVec3 c = IntVec3.FromVector3(clickPos);
            foreach (Thing thing7 in c.GetThingList(pawn.Map))
            {
                Thing t2 = thing7;
                if(!AlcoholHelper.NeedsAlcoholOverride(t2.def, pawn))
                    continue; // not alcohol, ignore
                if (t2.def.ingestible == null || !pawn.RaceProps.CanEverEat(t2) || !t2.IngestibleNow)
                    continue;
                string text = ((!t2.def.ingestible.ingestCommandString.NullOrEmpty()) ? string.Format(t2.def.ingestible.ingestCommandString, t2.LabelShort) : ((string)"ConsumeThing".Translate(t2.LabelShort, t2)));
                alcoholItems.Add(new Pair<String,Thing>(text, t2));
            }
            if(alcoholItems.Count == 0)
                return;
            AlcoholHelper.AddOverride();
            for( int i = 0; i < opts.Count; ++i )
            {
                for( int j = 0; j < alcoholItems.Count; ++j )
                {
                    string text = alcoholItems[j].First;
                    if(opts[i].Label == text || opts[i].Label.StartsWith(text + ":"))
                    {
                        Thing t2 = alcoholItems[j].Second;
                        // Big scary copy&paste&modify from the function. Only the one if statement is modified and the alcohol block is added.
                        // Some of the code is useless, because we know the thing is a drug.
				if (!t2.IsSociallyProper(pawn))
				{
					text = text + ": " + "ReservedForPrisoners".Translate().CapitalizeFirst();
				}
				else if (FoodUtility.MoodFromIngesting(pawn, t2, t2.def) < 0f)
				{
					text = string.Format("{0}: ({1})", text, "WarningFoodDisliked".Translate());
				}
				if (!t2.def.IsDrug || !ModsConfig.IdeologyActive || new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out var opt, text) || PawnUtility.CanTakeDrugForDependency(pawn, t2.def))
				{
					if (t2.def.IsNonMedicalDrug && !pawn.CanTakeDrug(t2.def))
					{
						opt = new FloatMenuOption(text + ": " + TraitDefOf.DrugDesire.DataAtDegree(-1).GetLabelCapFor(pawn), null);
					}
					else if (FoodUtility.InappropriateForTitle(t2.def, pawn, allowIfStarving: true))
					{
						opt = new FloatMenuOption(text + ": " + "FoodBelowTitleRequirements".Translate(pawn.royalty.MostSeniorTitle.def.GetLabelFor(pawn).CapitalizeFirst()).CapitalizeFirst(), null);
					}
					else if (!pawn.CanReach(t2, PathEndMode.OnCell, Danger.Deadly))
					{
						opt = new FloatMenuOption(text + ": " + "NoPath".Translate().CapitalizeFirst(), null);
					}
					else
					{
						MenuOptionPriority priority = ((t2 is Corpse) ? MenuOptionPriority.Low : MenuOptionPriority.Default);
						int maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(t2, pawn, FoodUtility.WillIngestStackCountOf(pawn, t2.def, FoodUtility.NutritionForEater(pawn, t2)));
						opt = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
						{
							int maxAmountToPickup2 = FoodUtility.GetMaxAmountToPickup(t2, pawn, FoodUtility.WillIngestStackCountOf(pawn, t2.def, FoodUtility.NutritionForEater(pawn, t2)));
							if (maxAmountToPickup2 != 0)
							{
								t2.SetForbidden(value: false);
								Job job30 = JobMaker.MakeJob(RimWorld.JobDefOf.Ingest, t2);
								job30.count = maxAmountToPickup2;
								pawn.jobs.TryTakeOrderedJob(job30, JobTag.Misc);
							}
						}, priority), pawn, t2);
						if (maxAmountToPickup == 0)
						{
							opt.action = null;
						}
					}
				}
                        // End of big scary copy&paste&modify from the function.
                        // Now replace the menu option.
                        opts[i] = opt;
                    }
                }
            }
            AlcoholHelper.RemoveOverride();
        }
    }

    [HarmonyPatch(typeof(Pawn_DrugPolicyTracker))]
    public static class Pawn_DrugPolicyTracker_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(AllowedToTakeScheduledEver))]
        public static void AllowedToTakeScheduledEver(out bool __state, Pawn_DrugPolicyTracker __instance, ThingDef thingDef)
        {
            __state = AlcoholHelper.NeedsAlcoholOverride(thingDef, __instance.pawn);
            AlcoholHelper.AddOverride( __state );
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AllowedToTakeScheduledEver))]
        public static void AllowedToTakeScheduledEver(bool __state, ThingDef thingDef)
        {
            AlcoholHelper.RemoveOverride( __state );
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(AllowedToTakeToInventory))]
        public static void AllowedToTakeToInventory(out bool __state, Pawn_DrugPolicyTracker __instance, ThingDef thingDef)
        {
            __state = AlcoholHelper.NeedsAlcoholOverride(thingDef, __instance.pawn);
            AlcoholHelper.AddOverride( __state );
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AllowedToTakeToInventory))]
        public static void AllowedToTakeToInventory(bool __state, ThingDef thingDef)
        {
            AlcoholHelper.RemoveOverride( __state );
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_Drunk))]
    public static class ThoughtWorker_Drunk_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CurrentSocialStateInternal))]
        public static void CurrentSocialStateInternal(out bool __state, Pawn p, Pawn other)
        {
            // We don't know the thing, but this is about being drunk, so our functionality if precept is active.
            __state = AlcoholHelper.HasAlcoholPrecept(p);
            AlcoholHelper.AddOverride( __state );
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CurrentSocialStateInternal))]
        public static void CurrentSocialStateInternal(bool __state, Pawn p, Pawn other)
        {
            AlcoholHelper.RemoveOverride( __state );
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_TeetotalerVsAddict))]
    public static class ThoughtWorker_TeetotalerVsAddict_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CurrentSocialStateInternal))]
        public static void CurrentSocialStateInternal(ref ThoughtState __result, Pawn p, Pawn other)
        {
            if(__result.Active)
                return;
            // A pawn with precept that dislikes alcohol will dislike pawn that has an alcohol addiction (and not others).
            // Mostly copy&paste&modify from the original function.
            if (!p.RaceProps.Humanlike)
                return;
            if( !AlcoholHelper.HasDislikingAlcoholPrecept(p))   // (!p.IsTeetotaler())
                return;
            if (!other.RaceProps.Humanlike)
                return;
            if (!RelationsUtility.PawnsKnowEachOther(p, other))
                return;
            List<Hediff> hediffs = other.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i].def.IsAddiction)
                {
                    Hediff_Addiction addiction = hediffs[i] as Hediff_Addiction;
                    if( addiction.Chemical == ChemicalDefOf.Alcohol )
                    {
                        __result = true;
                        return;
                    }
                }
            }
        }
    }

    // ThoughtWorker_TeetotalerVsChemicalInterest is primarily about the traits, but precepts
    // also set IsTeetotaler(). The only case when we'd need to patch would be the somewhat
    // unusual case of drug precept being fine with drugs but alcohol precept not being fine with alcohol.
    // But let's say that people who don't like only alcohol are fine with chemical interest.

    // PawnInventoryGenerator, Pawn_InventoryTracker and JobGiver_TakeCombatEnhancingDrug are about combat
    // enhancing drugs, which alcohol is not, so let's ignore it.

    // The BestFoodSourceOnMap_NewTemp() function gets called from several other places (FoodUtility.TryFindBestFoodSourceFor(),
    // JobGiver_GetFood, WorkGiver_InteractAnimal), but they all basically pass !pawn.IsTeetotaler() to allowDrug
    // (or false for animals), so just ignore the argument and check pawn settings ourselves.
    [HarmonyPatch(typeof(FoodUtility))]
    public static class FoodUtility_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(BestFoodInInventory_NewTemp))]
        public static IEnumerable<CodeInstruction> BestFoodInInventory_NewTemp(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // .. && (allowDrug || !thing.def.IsDrug) && ..
                // Replace it with:
                // .. && (!BestFoodInInventory_Hook(eater, thing.def)) && ..
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:45:ldarg.s::5
                // T:46:brtrue.s::System.Reflection.Emit.Label
                // T:47:ldloc.2::
                // T:48:ldfld::Verse.ThingDef def
                // T:49:callvirt::Boolean get_IsDrug()
                // T:50:brtrue.s::System.Reflection.Emit.Label
                if(codes[i].opcode == OpCodes.Ldarg_S && codes[i].operand.ToString() == "5"
                    && i + 5 < codes.Count && codes[i+1].opcode == OpCodes.Brtrue_S
                    && codes[i+4].opcode == OpCodes.Callvirt && codes[i+4].operand.ToString() == "Boolean get_IsDrug()")
                {
                    codes[i] = new CodeInstruction(OpCodes.Nop);
                    codes[i+1] = new CodeInstruction(OpCodes.Ldarg_1);
                    codes[i+4] = new CodeInstruction(OpCodes.Call, typeof(FoodUtility_Patch).GetMethod(nameof(BestFoodInInventory_Hook)));
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch FoodUtility.BestFoodInInventory");
            return codes;
        }

        public static bool BestFoodInInventory_Hook(Pawn eater, ThingDef thing)
        {
            // If this returns true, the thing will not be used.
            if(AlcoholHelper.NeedsAlcoholOverride(thing, eater))
            {
                if(AlcoholHelper.HasDislikingAlcoholPrecept(eater))
                    return true; // block
                else
                    return false; // allow
            }
            // The original case.
            bool allowDrug = !eater.IsTeetotaler();
            return !(allowDrug || !thing.IsDrug); // Negate, since the transpilled code negates.
        }

        // This transpiller is set up manually, as Harmony cannot find the method to patch (or I don't know how to set it up).
        // The catch is that the code to patch is an internal predicate function in FoodUtility.BestFoodSourceOnMap_NewTemp(),
        // which is implemented as a method in a nested private class.
        public static IEnumerable<CodeInstruction> BestFoodSourceOnMap_NewTemp_foodValidator(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // .. || (!allowDrug && thing.def.IsDrug) || ..
                // Replace it with:
                // .. || (BestFoodSourceOnMap_NewTemp_Hook(eater, thing.def)) || ..
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:165:ldarg.0::
                // T:166:ldfld::System.Boolean allowDrug
                // T:167:brtrue.s::System.Reflection.Emit.Label
                // T:168:ldarg.1::
                // T:169:ldfld::Verse.ThingDef def
                // T:170:callvirt::Boolean get_IsDrug()
                // T:171:brtrue.s::System.Reflection.Emit.Label
                if(codes[i].opcode == OpCodes.Ldarg_0 && i + 6 < codes.Count
                    && codes[i+1].opcode == OpCodes.Ldfld && codes[i+1].operand.ToString() == "System.Boolean allowDrug"
                    && codes[i+2].opcode == OpCodes.Brtrue_S
                    && codes[i+5].opcode == OpCodes.Callvirt && codes[i+5].operand.ToString() == "Boolean get_IsDrug()")
                {
                    Type nestedClass = typeof(FoodUtility).GetNestedType("<>c__DisplayClass19_0", BindingFlags.NonPublic);
                    if(nestedClass != null)
                    {
                        FieldInfo eaterField = AccessTools.Field(nestedClass, "eater");
                        if(eaterField != null)
                        {
                            codes[i+1] = new CodeInstruction(OpCodes.Ldfld, eaterField);
                            codes[i+2] = new CodeInstruction(OpCodes.Nop);
                            codes[i+5] = new CodeInstruction(OpCodes.Call, typeof(FoodUtility_Patch).GetMethod(nameof(BestFoodSourceOnMap_NewTemp_Hook)));
                            found = true;
                            break;
                        }
                    }
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch FoodUtility.BestFoodSourceOnMap_NewTemp() validator");
            return codes;
        }

        public static bool BestFoodSourceOnMap_NewTemp_Hook(Pawn eater, ThingDef thing)
        {
            // If this returns true, the thing will not be used.
            if(AlcoholHelper.NeedsAlcoholOverride(thing, eater))
            {
                if(AlcoholHelper.HasDislikingAlcoholPrecept(eater))
                    return true; // block
                else
                    return false; // allow
            }
            // The original case.
            bool allowDrug = !eater.IsTeetotaler();
            return !allowDrug && thing.IsDrug;
        }
    }

    [HarmonyPatch(typeof(PawnAddictionHediffsGenerator))]
    public static class PawnAddictionHediffsGenerator_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(GenerateAddictionsAndTolerancesFor))]
        public static IEnumerable<CodeInstruction> GenerateAddictionsAndTolerancesFor(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // if(.. || pawn.IsTeetotaler()) return;
                // Patch out the return for the IsTeetotaler() case.
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:16:ldloc.0::
                // T:17:ldfld::Verse.Pawn pawn
                // T:18:call::Boolean IsTeetotaler(Verse.Pawn)
                // T:19:brfalse.s::System.Reflection.Emit.Label
                // T:20:ret::
                if(i + 4 < codes.Count && codes[i+2].opcode == OpCodes.Call
                    && codes[i+2].operand.ToString() == "Boolean IsTeetotaler(Verse.Pawn)"
                    && codes[i+4].opcode == OpCodes.Ret)
                {
                    codes[i+4] = new CodeInstruction(OpCodes.Nop);
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch PawnAddictionHediffsGenerator.GenerateAddictionsAndTolerancesFor()");
            return codes;
        }
    }

    // The TryFindIngestibleToNurse() function has several checks related to drugs that are located at the start,
    // patch them out and then patch the predicate to check them depending on the item.
    [HarmonyPatch(typeof(JoyGiver_SocialRelax))]
    public static class JoyGiver_SocialRelax_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(TryFindIngestibleToNurse))]
        public static IEnumerable<CodeInstruction> TryFindIngestibleToNurse(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int foundCount = 0;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // if(.. || pawn.IsTeetotaler()) return;
                // Patch out the return for the IsTeetotaler() case.
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:7:call::Boolean IsTeetotaler(Verse.Pawn)
                // T:8:brfalse.s::System.Reflection.Emit.Label
                // T:9:ldarg.2::
                if(i + 2 < codes.Count
                    && codes[i].opcode == OpCodes.Call && codes[i].operand.ToString() == "Boolean IsTeetotaler(Verse.Pawn)"
                    && codes[i+1].opcode == OpCodes.Brfalse_S
                    && codes[i+2].opcode == OpCodes.Ldarg_2)
                {
                    codes[i+2] = codes[i+1];
                    codes[i+2].opcode = OpCodes.Br_S;
                    codes[i+1] = new CodeInstruction(OpCodes.Pop); // need to pop the conditional branch argument
                    ++foundCount;
                }
                // T:14:ldsfld::RimWorld.HistoryEventDef IngestedRecreationalDrug
                // T:15:ldloc.0::
                // T:16:ldfld::Verse.Pawn ingester
                // T:17:ldsfld::System.String Doer
                // T:18:call::Verse.NamedArgument Named(System.Object, System.String)
                // T:19:newobj::Void .ctor(HistoryEventDef, NamedArgument)
                // T:20:call::Boolean DoerWillingToDo(RimWorld.HistoryEvent)
                // T:21:brtrue.s::System.Reflection.Emit.Label
                // T:22:ldarg.2::
                if(i + 8 < codes.Count
                    && codes[i].opcode == OpCodes.Ldsfld && codes[i].operand.ToString() == "RimWorld.HistoryEventDef IngestedRecreationalDrug"
                    && codes[i+6].opcode == OpCodes.Call && codes[i+6].operand.ToString() == "Boolean DoerWillingToDo(RimWorld.HistoryEvent)"
                    && codes[i+7].opcode == OpCodes.Brtrue_S
                    && codes[i+8].opcode == OpCodes.Ldarg_2)
                {
                    codes[i+8] = codes[i+7];
                    codes[i+8].opcode = OpCodes.Br_S;
                    codes[i+7] = new CodeInstruction(OpCodes.Pop); // need to pop the conditional branch argument
                    ++foundCount;
                }
            }
            if(foundCount != 2)
                Log.Error("MorePrecepts: Failed to patch JoyGiver_SocialRelax.TryFindIngestibleToNurse()");
            return codes;
        }

        // This transpiller is set up manually, as Harmony cannot find the method to patch (or I don't know how to set it up).
        // The catch is that the code to patch is an internal predicate function in JoyGiver_SocialRelax.TryFindIngestibleToNurse(),
        // which is implemented as a method in a nested private class.
        public static IEnumerable<CodeInstruction> TryFindIngestibleToNurse_validator(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // ingester.CanReserve(t) && !t.IsForbidden(ingester)
                // Replace it with:
                // ingester.CanReserve(t) && !TryFindIngestibleToNurse_Hook(t,ingester)
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // T:10:ldarg.1::
                // T:11:ldarg.0::
                // T:12:ldfld::Verse.Pawn ingester
                // T:13:call::Boolean IsForbidden(Verse.Thing, Verse.Pawn)
                if(codes[i].opcode == OpCodes.Call && codes[i].operand.ToString() == "Boolean IsForbidden(Verse.Thing, Verse.Pawn)")
                {
                    codes[i] = new CodeInstruction(OpCodes.Call, typeof(JoyGiver_SocialRelax_Patch).GetMethod(nameof(TryFindIngestibleToNurse_Hook)));
                    found = true;
                    break;
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch JoyGiver_SocialRelax.TryFindIngestibleToNurse() validator");
            return codes;
        }

        public static bool TryFindIngestibleToNurse_Hook(Thing thing, Pawn ingester)
        {
            // If this returns true, the thing will be ignored.
            // Original code first.
            if(thing.IsForbidden(ingester))
                return true;
            // Now the patched out code, depending on the thing.
            if(AlcoholHelper.NeedsAlcoholOverride(thing.def, ingester))
            {
                AlcoholHelper.AddOverride();
                bool block = false;
                if(ingester.IsTeetotaler())
                    block = true;
                if (!new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, ingester.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                    block = true;
                AlcoholHelper.RemoveOverride();
                return block;
            }
            else // normal drug, the original code
            {
                if(ingester.IsTeetotaler())
                    return true; // block
                if (!new HistoryEvent(HistoryEventDefOf.IngestedRecreationalDrug, ingester.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                    return true; // block
                return false;
            }
        }
    }

    public abstract class ThoughtWorker_Precept_Alcohol_Base : ThoughtWorker_Precept, IPreceptCompDescriptionArgs
    {
        protected abstract float DaysSatisfied();
        protected abstract float DaysNoBonus();
        protected abstract float DaysMissing();
        protected abstract float DaysMissing_Major();
        protected abstract SimpleCurve MoodOffsetFromDaysSinceLastAlcoholCurve();

        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!ThoughtUtility.ThoughtNullified(p, def))
            {
                float num = (float)(Find.TickManager.TicksGame - PawnComp.GetLastTakeAlcoholTick(p)) / GenDate.TicksPerDay;
                if (num > DaysNoBonus() && def.minExpectationForNegativeThought != null
                    && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < def.minExpectationForNegativeThought.order)
                    return false;
                if (num < DaysNoBonus())
                    return ThoughtState.ActiveAtStage(0);
                if (num < DaysMissing())
                    return ThoughtState.ActiveAtStage(1);
                return ThoughtState.ActiveAtStage(2);
            }
            return false;
        }

        public override float MoodMultiplier(Pawn pawn)
        {
            float num = (float)(Find.TickManager.TicksGame - PawnComp.GetLastTakeAlcoholTick(pawn)) / GenDate.TicksPerDay;
            return MoodOffsetFromDaysSinceLastAlcoholCurve().Evaluate(num);
        }

        public IEnumerable<NamedArgument> GetDescriptionArgs()
        {
            yield return DaysMissing().Named("DAYSSATISIFED");
        }
    }

    public class ThoughtWorker_Precept_Alcohol_Wanted : ThoughtWorker_Precept_Alcohol_Base
    {
        protected override float DaysSatisfied() => 0.75f;
        protected override float DaysNoBonus() => 1f;
        protected override float DaysMissing() => 2f;
        protected override float DaysMissing_Major() => 11f;
        protected override SimpleCurve MoodOffsetFromDaysSinceLastAlcoholCurve() => StaticMoodOffsetFromDaysSinceLastAlcoholCurve;

        private static readonly SimpleCurve StaticMoodOffsetFromDaysSinceLastAlcoholCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(0.75f, 1f),
            new CurvePoint(1f, 0f),
            new CurvePoint(2f, 1f),
            new CurvePoint(11f, 10f)
        };
    }

    public class ThoughtWorker_Precept_Alcohol_Essential : ThoughtWorker_Precept_Alcohol_Base
    {
        protected override float DaysSatisfied() => 0.75f;
        protected override float DaysNoBonus() => 1f;
        protected override float DaysMissing() => 1.2f;
        protected override float DaysMissing_Major() => 3f;
        protected override SimpleCurve MoodOffsetFromDaysSinceLastAlcoholCurve() => StaticMoodOffsetFromDaysSinceLastAlcoholCurve;

        private static readonly SimpleCurve StaticMoodOffsetFromDaysSinceLastAlcoholCurve = new SimpleCurve
        {
            // First values are times from above, second values are mood multipliers for the XML value.
            new CurvePoint(0.75f, 1f),
            new CurvePoint(1f, 0f),
            new CurvePoint(1.2f, 1f),
            new CurvePoint(3f, 10f)
        };
    }
}
