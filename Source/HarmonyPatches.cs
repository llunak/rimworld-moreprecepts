using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Reflection;

namespace MorePrecepts
{
    [StaticConstructorOnStartup]
    public class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("llunak.MorePrecepts");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // FoodUtility.BestFoodSourceOnMap() needs special handling, see the transpiller.
            bool done = false;
            MethodInfo oldMethod;
            MethodInfo newMethod;
            Type nestedClass = typeof(FoodUtility).GetNestedType("<>c__DisplayClass14_0", BindingFlags.NonPublic);
            if(nestedClass != null)
            {
                oldMethod = AccessTools.Method(nestedClass, "<BestFoodSourceOnMap>b__0");
                newMethod = typeof(FoodUtility_Patch).GetMethod("BestFoodSourceOnMap_foodValidator");
                if(oldMethod != null)
                {
                    harmony.Patch(oldMethod, transpiler: new HarmonyMethod(newMethod));
                    done = true;
                }
            }
            if(!done)
                Log.Error("MorePrecepts: Failed to find BestFoodSourceOnMap_foodValidator for patching");

            // JoyGiver_SocialRelax.TryFindIngestibleToNurse() needs special handling, see the transpiller.
            done = false;
            nestedClass = typeof(JoyGiver_SocialRelax).GetNestedType("<>c__DisplayClass8_0", BindingFlags.NonPublic);
            if(nestedClass != null)
            {
                oldMethod = AccessTools.Method(nestedClass, "<TryFindIngestibleToNurse>b__0");
                newMethod = typeof(JoyGiver_SocialRelax_Patch).GetMethod("TryFindIngestibleToNurse_validator");
                if(oldMethod != null)
                {
                    harmony.Patch(oldMethod, transpiler: new HarmonyMethod(newMethod));
                    done = true;
                }
            }
            if(!done)
                Log.Error("MorePrecepts: Failed to find TryFindIngestibleToNurse_validator for patching");

            // Toils_Ingest.CarryIngestibleToChewSpot() needs special handling, see the transpiller.
            done = false;
            nestedClass = typeof(Toils_Ingest).GetNestedType("<>c__DisplayClass3_0", BindingFlags.NonPublic);
            if(nestedClass != null)
            {
                oldMethod = AccessTools.Method(nestedClass, "<CarryIngestibleToChewSpot>b__0");
                newMethod = typeof(Toils_Ingest_Patch).GetMethod("CarryIngestibleToChewSpot_delegate");
                if(oldMethod != null)
                {
                    harmony.Patch(oldMethod, transpiler: new HarmonyMethod(newMethod));
                    done = true;
                }
            }
            if(!done)
                Log.Error("MorePrecepts: Failed to find CarryIngestibleToChewSpot_delegate for patching");

            // JobDriver_Strip.MakeNewToils() needs special handling, see the transpiller.
            done = false;
            oldMethod = AccessTools.Method(typeof(JobDriver_Strip), "<MakeNewToils>b__2_2");
            newMethod = typeof(JobDriver_Strip_Patch).GetMethod("MakeNewToils_delegate");
            if(oldMethod != null)
            {
                harmony.Patch(oldMethod, transpiler: new HarmonyMethod(newMethod));
                done = true;
            }
            if(!done)
                Log.Error("MorePrecepts: Failed to find JobDriver_Strip.MakeNewToils() delegate for patching");
        }
    }
}
