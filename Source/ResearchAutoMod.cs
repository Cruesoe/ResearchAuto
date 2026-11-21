using UnityEngine;
using Verse;

namespace ResearchAuto
{
    public class ResearchAutoMod : Mod
    {
        public static ResearchAutoSettings settings;

        public ResearchAutoMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ResearchAutoSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            DrawSectionHeader(listing, "ResearchAuto_SectionCore".Translate());
            listing.CheckboxLabeled("ResearchAuto_ModEnabled".Translate(), ref settings.modEnabled);
            listing.CheckboxLabeled("ResearchAuto_ShowMessages".Translate(), ref settings.showMessages);

            listing.Gap(12f);
            DrawSectionHeader(listing, "ResearchAuto_SectionSorting".Translate());
            listing.CheckboxLabeled("ResearchAuto_RestrictToPlayerTech".Translate(), ref settings.restrictToPlayerTech);
            if (!settings.restrictToPlayerTech)
                listing.CheckboxLabeled("ResearchAuto_IgnoreTechLevel".Translate(), ref settings.ignoreTechLevel);
            listing.CheckboxLabeled("ResearchAuto_PrioritizeExpensive".Translate(), ref settings.prioritizeExpensive);

            listing.Gap(12f);
            DrawSectionHeader(listing, "ResearchAuto_SectionParallel".Translate());
            listing.CheckboxLabeled("ResearchAuto_IncludeAnomaly".Translate(), ref settings.includeAnomaly);
            listing.CheckboxLabeled("ResearchAuto_IncludeGravship".Translate(), ref settings.includeGravship);
            listing.CheckboxLabeled("ResearchAuto_IncludeDivinitech".Translate(), ref settings.includeDivinitech);

            listing.End();
        }

        private static void DrawSectionHeader(Listing_Standard listing, string label)
        {
            Text.Font = GameFont.Medium;
            listing.Label(label);
            Text.Font = GameFont.Small;
            listing.GapLine();
        }

        public override string SettingsCategory() => "Research: Auto";
    }
}
