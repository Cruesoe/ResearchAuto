using Verse;

namespace ResearchAuto
{
    public class ResearchAutoSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool ignoreTechLevel = false;
        public bool restrictToPlayerTech = false;
        public bool showMessages = true;
        public bool includeAnomaly = true;
        public bool includeGravship = true;
        public bool includeDivinitech = true;
        public bool prioritizeExpensive = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref ignoreTechLevel, "ignoreTechLevel", false);
            Scribe_Values.Look(ref restrictToPlayerTech, "restrictToPlayerTech", false);
            Scribe_Values.Look(ref showMessages, "showMessages", true);
            Scribe_Values.Look(ref includeAnomaly, "includeAnomaly", true);
            Scribe_Values.Look(ref includeGravship, "includeGravship", true);
            Scribe_Values.Look(ref includeDivinitech, "includeDivinitech", true);
            Scribe_Values.Look(ref prioritizeExpensive, "prioritizeExpensive", false);
        }
    }
}
