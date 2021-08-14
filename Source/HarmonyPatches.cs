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
            Type nestedClass = typeof(FoodUtility).GetNestedType("<>c__DisplayClass12_0", BindingFlags.NonPublic);
            if(nestedClass != null)
            {
                MethodInfo oldMethod = AccessTools.Method(nestedClass, "<BestFoodSourceOnMap>b__0");
                MethodInfo newMethod = typeof(FoodUtility_Patch).GetMethod("BestFoodSourceOnMap_foodValidator");
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
                MethodInfo oldMethod = AccessTools.Method(nestedClass, "<TryFindIngestibleToNurse>b__0");
                MethodInfo newMethod = typeof(JoyGiver_SocialRelax_Patch).GetMethod("TryFindIngestibleToNurse_validator");
                if(oldMethod != null)
                {
                    harmony.Patch(oldMethod, transpiler: new HarmonyMethod(newMethod));
                    done = true;
                }
            }
            if(!done)
                Log.Error("MorePrecepts: Failed to find TryFindIngestibleToNurse_validator for patching");
        }
    }
}
