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

        public static PreceptDef Violence_Defense;

        public static PreceptDef Violence_Wanted;

        public static PreceptDef Violence_Essential;

        public static PreceptDef Alcohol_Prohibited;

        public static PreceptDef Alcohol_Disapproved;

        public static PreceptDef Alcohol_Neutral;

        public static PreceptDef Alcohol_Wanted;

        public static PreceptDef Alcohol_Essential;

        public static PreceptDef Feast;

        public static PreceptDef Feast_Meat;

        public static PreceptDef Feast_Veg;

        public static PreceptDef Nomadism_Wanted;

        public static PreceptDef Nomadism_Important;

        public static PreceptDef Nomadism_Essential;

        public static PreceptDef DrugPossession_Prohibited;

        public static PreceptDef DrugPossession_Alcohol;

        public static PreceptDef DrugPossession_Social;

        static PreceptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PreceptDefOf));
        }
    }

    [DefOf]
    public static class RitualPatternDefOf
    {
            public static RitualPatternDef Feast;

            public static RitualPatternDef Feast_Meat;

            public static RitualPatternDef Feast_Veg;

        static RitualPatternDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RitualPatternDefOf));
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
    public static class TaleDefOf
    {
            public static TaleDef BurnedCorpse;

        static TaleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TaleDefOf));
        }
    }

    [DefOf]
    public static class ThoughtDefOf
    {
        public static ThoughtDef Superstition_Superstitious_Strong_Plus;

        public static ThoughtDef Superstition_Superstitious_Strong_Minus;

        public static ThoughtDef Superstition_Superstitious_Mild_Plus;

        public static ThoughtDef Superstition_Superstitious_Mild_Minus;

        public static ThoughtDef Nomadism_Wanted;

        public static ThoughtDef Nomadism_Important;

        public static ThoughtDef Nomadism_Essential;

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

        public static HistoryEventDef Violence_AttackedHostilePerson;

        public static HistoryEventDef Nomadism_AbandonedSettlement;

        public static HistoryEventDef DrugPossession_TradedDrug;

        public static HistoryEventDef DrugPossession_TradedNonAlcoholDrug;

        public static HistoryEventDef DrugPossession_TradedHardDrug;

        public static HistoryEventDef Compassion_IncapacitatedPawnLeftToDie_All;

        public static HistoryEventDef Compassion_IncapacitatedPawnLeftToDie_NonGuiltyEnemies;

        public static HistoryEventDef Compassion_IncapacitatedPawnLeftToDie_NonHostile;

        public static HistoryEventDef Compassion_IncapacitatedPawnLeftToDie_Allies;

        [MayRequireIdeology]
        public static HistoryEventDef BuiltTrap;

        static HistoryEventDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HistoryEventDefOf));
        }
    }

}
