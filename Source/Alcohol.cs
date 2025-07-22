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

So most of the code here tries to prevent core from including alcohol in recreational drugs, which is not
trivial, as even IsTeetotaler() returns true if DrugUse:MedicalOnly is set, thus normally blocking beer use.
So patch all drug-related code to follow the alcohol precept instead of the drug use one. This includes
avoiding drug HistoryEventDef events and sending alcohol ones, otherwise pawns would get thoughts
from both alcohol and drugs precepts. That may possibly break mods that react to those events, but oh well.

Note that the teetotaler trait (DrugDesire < 0) still prevents alcohol.
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
        public static bool WillingToIngestAlcohol(Pawn pawn)
        {
            return ( pawn.story == null || pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) >= 0 )
                && new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
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
            if(!AlcoholHelper.WillingToIngestAlcohol(pawn))
                __result = true;
            return false;
        }

        // Handle the IsTeetotaler() call in CanTakeDrug() (teetotalerCanConsume is never used, so it's always false).
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CanTakeDrug))]
        public static void CanTakeDrug(out bool __state, Pawn pawn, ThingDef drug)
        {
            __state = AlcoholHelper.NeedsAlcoholOverride(drug, pawn);
            AlcoholHelper.AddOverride( __state );
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CanTakeDrug))]
        public static void CanTakeDrug(bool __state)
        {
            AlcoholHelper.RemoveOverride( __state );
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(CanTakeDrug))]
        public static IEnumerable<CodeInstruction> CanTakeDrug(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // if (ModsConfig.IdeologyActive)
                // As the first code in the block, insert:
                // int code = CanTakeDrug_Hook(pawn, drug);
                // if(code == 0)
                //     return false;
                // if(code != 1) // before the (whole) original body of the if statement
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if(codes[i].opcode == OpCodes.Call && codes[i].operand.ToString() == "Boolean get_IdeologyActive()"
                    && i + 1 < codes.Count && codes[i+1].opcode == OpCodes.Brfalse_S)
                {
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_0)); // load 'pawn'
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_1)); // load 'drug'
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, typeof(PawnUtility_Patch).GetMethod(nameof(CanTakeDrug_Hook))));
                    codes.Insert(i + 5, new CodeInstruction(OpCodes.Dup)); // duplicate the return value
                    Label label = generator.DefineLabel();
                    codes.Insert(i + 6, new CodeInstruction(OpCodes.Brtrue_S, label)); // jump after if(code == 0) if false (==0), i.e. it's !=0
                    codes.Insert(i + 7, new CodeInstruction(OpCodes.Ret)); // return false, use the duped 0 value as false
                    // These two convert the duped value on the stack so that the condition changes from code!=1 to code!=0.
                    codes.Insert(i + 8, new CodeInstruction(OpCodes.Ldc_I4_1));
                    codes.Insert(i + 9, new CodeInstruction(OpCodes.Sub));
                    codes.Insert(i + 10, codes[ i + 1 ].Clone()); // skip the original block if code!=0 (originally code!=1)
                    codes[ i + 8 ].labels.Add( label ); // add label for the false part of the if(code == 0)
                    found = true;
                    break;
                }
            }

            if(!found)
                Log.Error("MorePrecepts: Failed to patch PawnUtility.CanTakeDrug");
            return codes;
        }
        public static int CanTakeDrug_Hook(Pawn pawn, ThingDef drug)
        {
            if(!AlcoholHelper.NeedsAlcoholOverride(drug, pawn))
                return 2; // Normal execution.
            if(AlcoholHelper.WillingToIngestAlcohol(pawn))
                return 1; // Skip the rest of execution, allow taking.
            return 0; // Make return false.
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PawnWouldBeUnhappyTakingDrug))]
        public static bool PawnWouldBeUnhappyTakingDrug(bool result, Pawn pawn, ThingDef drug)
        {
            if(AlcoholHelper.NeedsAlcoholOverride(drug, pawn))
                return !AlcoholHelper.WillingToIngestAlcohol(pawn);
            return result;
        }
    }

    // This class needs adjusting in a very way that's very similar to CanTakeDrug() (although the same thing is written in different way).
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Ingest))]
    public static class FloatMenuOptionProvider_Ingest_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(GetSingleOptionFor))]
        public static IEnumerable<CodeInstruction> GetSingleOptionFor(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            int textLoad = -1;
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // ... text + ": " ...
                // Save the load instruction of 'text'.
                if(codes[i].IsLdloc() && i + 1 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldstr && codes[ i + 1 ].operand.ToString() == ": ")
                {
                    textLoad = i;
                }
                // The function has code:
                // if (ModsConfig.IdeologyActive)
                // As the first code in the block, insert:
                // int code = GetSingleOptionFor_Hook1(clickedThing, context);
                // if(code == 0)
                //     return GetSingleOptionFor_Hook2(clickedThing, context, text);
                // if(code != 1) // before the (whole) original body of the if statement
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if(textLoad != -1 && codes[i].opcode == OpCodes.Call && codes[i].operand.ToString() == "Boolean get_IdeologyActive()"
                    && i + 1 < codes.Count && codes[i+1].opcode == OpCodes.Brfalse)
                {
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1)); // load 'clickedThing'
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_2)); // load 'context'
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Call,
                        typeof(FloatMenuOptionProvider_Ingest_Patch).GetMethod(nameof(GetSingleOptionFor_Hook1))));
                    codes.Insert(i + 5, new CodeInstruction(OpCodes.Dup)); // duplicate the return value
                    Label label = generator.DefineLabel();
                    codes.Insert(i + 6, new CodeInstruction(OpCodes.Brtrue_S, label)); // jump after if(code == 0) if false (==0), i.e. it's !=0
                    codes.Insert(i + 7, new CodeInstruction(OpCodes.Pop)); // pop the dupped return value
                    codes.Insert(i + 8, new CodeInstruction(OpCodes.Ldarg_1)); // load 'clickedThing'
                    codes.Insert(i + 9, new CodeInstruction(OpCodes.Ldarg_2)); // load 'context'
                    codes.Insert(i + 10, codes[ textLoad ].Clone()); // load 'text'
                    codes.Insert(i + 11, new CodeInstruction(OpCodes.Call,
                        typeof(FloatMenuOptionProvider_Ingest_Patch).GetMethod(nameof(GetSingleOptionFor_Hook2))));
                    codes.Insert(i + 12, new CodeInstruction(OpCodes.Ret)); // return 'opt'
                    // These two convert the duped value on the stack so that the condition changes from code!=1 to code!=0.
                    codes.Insert(i + 13, new CodeInstruction(OpCodes.Ldc_I4_1));
                    codes.Insert(i + 14, new CodeInstruction(OpCodes.Sub));
                    codes.Insert(i + 15, codes[ i + 1 ].Clone()); // skip the original block if code!=0 (originally code!=1)
                    codes[ i + 13 ].labels.Add( label ); // add label for the false part of the if(code == 0)
                    found = true;
                    break;
                }
            }

            if(!found)
                Log.Error("MorePrecepts: Failed to patch FloatMenuOptionProvider_Ingest.GetSingleOptionFor");
            return codes;
        }
        public static int GetSingleOptionFor_Hook1(Thing drug, FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            if(!AlcoholHelper.NeedsAlcoholOverride(drug.def, pawn))
                return 2; // Normal execution.
            if(!AlcoholHelper.WillingToIngestAlcohol(pawn) && !PawnUtility.CanTakeDrugForDependency(pawn, drug.def))
                return 0; // Make return 'opt' (created by the second hook).
            return 1; // Skip the rest of execution, allow taking.
        }

        public static FloatMenuOption GetSingleOptionFor_Hook2(Thing drug, FloatMenuContext context, string text)
        {
            Pawn pawn = context.FirstSelectedPawn;
            new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out var opt, text);
            return opt;
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

// These places to patch are those that have 'IngestedDrug', 'IngestedRecreationalDrug', 'AdministeredDrug' or 'IsTeetotaler'.

    // PawnUtility.CanTakeDrug() is already done above.

    [HarmonyPatch(typeof(Recipe_AdministerIngestible))]
    public static class Recipe_AdministerIngestible_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void ApplyOnPawn(out bool __state, Pawn pawn, BodyPartRecord part, ref Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            __state = AlcoholHelper.NeedsAlcoholOverride(ingredients[0].def, billDoer);
            if (__state )
            {
                AlcoholHelper.AddOverride(); // For the IsTeetotaler() check.
                // If alcohol, send alcohol event.
                if(billDoer != null)
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.AdministeredAlcohol,
                        billDoer.Named(HistoryEventArgsNames.Doer), pawn.Named(HistoryEventArgsNames.Victim)));
                // And block sending drug events, which is inside 'billDoer' check, but rest of the original function will still do its work.
                billDoer = null;
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ApplyOnPawn))]
        public static void ApplyOnPawn(bool __state)
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
                if (food.IsNutritionGivingIngestible && pawn.WillEat(food, null, careIfNotAcceptableForTitle: false)
                    && (int)food.ingestible.preferability > 1 && !pawn.IsTeetotaler() && food.ingestible.canAutoSelectAsFoodForCaravan)
                {
                    __result = AlcoholHelper.WillingToIngestAlcohol(pawn);
                }
                AlcoholHelper.RemoveOverride();
            }
        }
    }

    [HarmonyPatch(typeof(CompDrug))]
    public static class CompDrug_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(PostIngested))]
        public static IEnumerable<CodeInstruction> PostIngested(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found1 = false;
            bool found2 = false;
            int lastSkip = -1;
            Label label = generator.DefineLabel();
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code starting with:
                // Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.IngestedDrug, ingester.Named(HistoryEventArgsNames.Doer)));
                // In front of the entire code remaining, add:
                // if(!PostIngested_Hook(this, ingester))
                //      { ... the block ... }
                // Log.Message("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if(codes[i].opcode == OpCodes.Call && codes[i].operand.ToString() == "Void Notify_DrugIngested(Verse.Thing)"
                    && i + 1 < codes.Count
                    && codes[i + 1].opcode == OpCodes.Call
                    && codes[i + 1].operand.ToString() == "RimWorld.HistoryEventsManager get_HistoryEventsManager()")
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0)); // load 'this'
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1)); // load 'ingester'
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Call,
                        typeof(CompDrug_Patch).GetMethod(nameof(PostIngested_Hook))));
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Brtrue_S, label));
                    found1 = true;
                }
                if(found1 && codes[i].opcode == OpCodes.Callvirt && codes[i].operand.ToString() == "Void RecordEvent(RimWorld.HistoryEvent, Boolean)")
                    lastSkip = i;
            }

            // Add label to the first instruction after the block (the last 'ret' as of now).
            if(found1 && lastSkip != -1 && lastSkip + 1 < codes.Count)
            {
                codes[ lastSkip + 1 ].labels.Add( label );
                found2 = true;
            }

            if(!found1 || !found2)
                Log.Error("MorePrecepts: Failed to patch CompDrug.PostIngested()");
            return codes;
        }
        public static bool PostIngested_Hook(CompDrug comp, Pawn ingester)
        {
            if(!AlcoholHelper.NeedsAlcoholOverride(comp.parent.def, ingester))
                return false;
            if (!ingester.Dead)
                PawnComp.SetLastTakeAlcoholTickToNow(ingester);
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.IngestedAlcohol, ingester.Named(HistoryEventArgsNames.Doer)));
            return true;
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
        public static void AllowedToTakeScheduledEver(bool __state)
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
        public static void AllowedToTakeToInventory(bool __state)
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
        public static void CurrentSocialStateInternal(bool __state)
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
            if( AlcoholHelper.WillingToIngestAlcohol(p))   // (!p.IsTeetotaler()) - TODO core now only checks trait, why?
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

    // The BestFoodSourceOnMap() function gets called from several other places (FoodUtility.TryFindBestFoodSourceFor(),
    // JobGiver_GetFood, WorkGiver_InteractAnimal), but they all basically pass !pawn.IsTeetotaler() to allowDrug
    // (or false for animals), so just ignore the argument and check pawn settings ourselves.
    [HarmonyPatch(typeof(FoodUtility))]
    public static class FoodUtility_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(BestFoodInInventory))]
        public static IEnumerable<CodeInstruction> BestFoodInInventory(IEnumerable<CodeInstruction> instructions)
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
                if(AlcoholHelper.WillingToIngestAlcohol(eater))
                    return true; // block
                else
                    return false; // allow
            }
            // The original case.
            bool allowDrug = !eater.IsTeetotaler();
            return !(allowDrug || !thing.IsDrug); // Negate, since the transpilled code negates.
        }

        // This transpiller is set up manually, as Harmony cannot find the method to patch (or I don't know how to set it up).
        // The catch is that the code to patch is an internal predicate function in FoodUtility.BestFoodSourceOnMap(),
        // which is implemented as a method in a nested private class.
        public static IEnumerable<CodeInstruction> BestFoodSourceOnMap_foodValidator(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // The function has code:
                // .. || (!allowDrug && thing.def.IsDrug) || ..
                // Replace it with:
                // .. || (BestFoodSourceOnMap_Hook(eater, thing.def)) || ..
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
                    Type nestedClass = typeof(FoodUtility).GetNestedType("<>c__DisplayClass16_0", BindingFlags.NonPublic);
                    if(nestedClass != null)
                    {
                        FieldInfo eaterField = AccessTools.Field(nestedClass, "eater");
                        if(eaterField != null)
                        {
                            codes[i+1] = new CodeInstruction(OpCodes.Ldfld, eaterField);
                            codes[i+2] = new CodeInstruction(OpCodes.Nop);
                            codes[i+5] = new CodeInstruction(OpCodes.Call, typeof(FoodUtility_Patch).GetMethod(nameof(BestFoodSourceOnMap_Hook)));
                            found = true;
                            break;
                        }
                    }
                }
            }
            if(!found)
                Log.Error("MorePrecepts: Failed to patch FoodUtility.BestFoodSourceOnMap() validator");
            return codes;
        }

        public static bool BestFoodSourceOnMap_Hook(Pawn eater, ThingDef thing)
        {
            // If this returns true, the thing will not be used.
            if(AlcoholHelper.NeedsAlcoholOverride(thing, eater))
            {
                if(AlcoholHelper.WillingToIngestAlcohol(eater))
                    return true; // block
                else
                    return false; // allow
            }
            // The original case.
            bool allowDrug = !eater.IsTeetotaler();
            return !allowDrug && thing.IsDrug;
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
                    codes.Insert(i+1, new CodeInstruction(OpCodes.Pop)); // need to pop the conditional branch argument
                    codes[i+1+1].opcode = OpCodes.Br_S;
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
                    codes.Insert(i+7, new CodeInstruction(OpCodes.Pop)); // need to pop the conditional branch argument
                    codes[i+7+1].opcode = OpCodes.Br_S;
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
                bool block = !AlcoholHelper.WillingToIngestAlcohol(ingester);
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
