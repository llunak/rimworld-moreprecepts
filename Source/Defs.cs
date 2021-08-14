using RimWorld;
using Verse;

namespace MorePrecepts
{
    [DefOf]
    public static class EffecterDefOf
    {
            public static EffecterDef EatVegetarian;

        static EffecterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EffecterDefOf));
        }
    }

    [DefOf]
    public static class JobDefOf
    {
            [MayRequireIdeology]
            public static JobDef EatAtFeast;

        static JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf));
        }
    }

    [DefOf]
    public static class PreceptDefOf
    {
        public static PreceptDef Superstition_Strong;

        public static PreceptDef Superstition_Mild;

        public static PreceptDef Violence_Pacifism;

        public static PreceptDef Violence_Horrible;

        public static PreceptDef Violence_Disapproved;

        public static PreceptDef Violence_Wanted;

        public static PreceptDef Violence_Essential;

        public static PreceptDef Alcohol_Prohibited;

        public static PreceptDef Alcohol_Disapproved;

        public static PreceptDef Alcohol_Neutral;

        public static PreceptDef Alcohol_Wanted;

        public static PreceptDef Alcohol_Essential;

        static PreceptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PreceptDefOf));
        }
    }

    [DefOf]
    public static class SoundDefOf
    {
            public static SoundDef Meal_Eat;

        static SoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }
    }

    [DefOf]
    public static class ThoughtDefOf
    {
        public static ThoughtDef Superstition_Superstitious_Strong_Plus;

        public static ThoughtDef Superstition_Superstitious_Strong_Minus;

        public static ThoughtDef Superstition_Superstitious_Mild_Plus;

        public static ThoughtDef Superstition_Superstitious_Mild_Minus;

        static ThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThoughtDefOf));
        }
    }

    [DefOf]
    public static class HistoryEventDefOf
    {
        public static HistoryEventDef Superstition_Superstitious_Generic;

        public static HistoryEventDef Superstition_Superstitious_Strong_Plus;

        public static HistoryEventDef Superstition_Superstitious_Strong_Minus;

        public static HistoryEventDef Superstition_Superstitious_Mild_Plus;

        public static HistoryEventDef Superstition_Superstitious_Mild_Minus;

        public static HistoryEventDef IngestedAlcohol;

        public static HistoryEventDef AdministeredAlcohol;

        public static HistoryEventDef IngestedRecreationalDrug;

        public static HistoryEventDef Violence_AttackedPerson;

        [MayRequireIdeology]
        public static HistoryEventDef BuiltTrap;

        static HistoryEventDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HistoryEventDefOf));
        }
    }

}
