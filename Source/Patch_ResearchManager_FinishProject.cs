using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchAuto
{
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public static class Patch_ResearchManager_FinishProject
    {
        // Picks the next project on the following tick, after FinishProject and other mods' postfixes have run
        public static void Postfix() => Current.Game.GetComponent<AutoResearcher>()?.TriggerDelayedCheck(1);
    }
}
