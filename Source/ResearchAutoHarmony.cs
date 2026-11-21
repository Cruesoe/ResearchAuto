using HarmonyLib;
using Verse;

namespace ResearchAuto
{
    [StaticConstructorOnStartup]
    public static class ResearchAutoHarmony
    {
        static ResearchAutoHarmony()
        {
            new Harmony("com.researchauto.mod").PatchAll(typeof(ResearchAutoHarmony).Assembly);
        }
    }
}
