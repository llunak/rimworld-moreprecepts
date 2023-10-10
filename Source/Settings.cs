using Verse;
using UnityEngine;

namespace MorePrecepts
{
    public class Settings : ModSettings
    {
        public bool showNomadsWantToLeave = true;

        public override void ExposeData()
        {
            Scribe_Values.Look( ref showNomadsWantToLeave, "ShowNomadsWantToLeave", true );
        }
    }

    public class MorePreceptsMod : Mod
    {
        private static Settings _settings;
        public static Settings settings { get { return _settings; }}

        public MorePreceptsMod( ModContentPack content )
            : base( content )
        {
            _settings = GetSettings< Settings >();
        }

        public override string SettingsCategory()
        {
            return "MorePrecepts.ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin( rect );
            listing.CheckboxLabeled( "MorePrecepts.ShowNomadsWantToLeave".Translate(),
                ref settings.showNomadsWantToLeave, "MorePrecepts.ShowNomadsWantToLeaveTooltip".Translate());
            listing.End();
            base.DoSettingsWindowContents(rect);
        }
    }
}
