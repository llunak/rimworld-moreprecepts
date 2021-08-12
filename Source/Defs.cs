using RimWorld;
using Verse;

namespace MorePrecepts
{
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

//        public static PreceptDef Alcohol_Prohibited;

        static PreceptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PreceptDefOf));
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

        public static HistoryEventDef AttackedPerson;

        [MayRequireIdeology]
        public static HistoryEventDef BuiltTrap;

        static HistoryEventDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HistoryEventDefOf));
        }
    }

}
