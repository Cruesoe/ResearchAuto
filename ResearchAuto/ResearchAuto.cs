using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using HarmonyLib;

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

            DrawSectionHeader(listing, "Core Settings");
            listing.CheckboxLabeled("Enable Auto-Research", ref settings.modEnabled);
            listing.CheckboxLabeled("Show Notifications", ref settings.showMessages);

            listing.Gap(12f);
            DrawSectionHeader(listing, "Sorting & Filtering");
            listing.CheckboxLabeled("Restrict to Current Era", ref settings.restrictToPlayerTech);
            if (!settings.restrictToPlayerTech)
                listing.CheckboxLabeled("Ignore Tech Level", ref settings.ignoreTechLevel);
            listing.CheckboxLabeled("Prioritize Expensive Projects", ref settings.prioritizeExpensive);

            listing.Gap(12f);
            DrawSectionHeader(listing, "Parallel Queues");
            listing.CheckboxLabeled("Include Anomaly Techs", ref settings.includeAnomaly);
            listing.CheckboxLabeled("Include Gravship Techs", ref settings.includeGravship);
            listing.CheckboxLabeled("Include Monolyn Race Technologies", ref settings.includeDivinitech);

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

    [StaticConstructorOnStartup]
    public static class ResearchAutoHarmony
    {
        static ResearchAutoHarmony()
        {
            new Harmony("com.researchauto.mod").PatchAll();
        }
    }

    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public static class ResearchManager_FinishProject_Patch
    {
        public static void Postfix() => Current.Game.GetComponent<AutoResearcher>()?.TriggerDelayedCheck(300);
    }

    [HarmonyPatch(typeof(ResearchManager), "SetCurrentProject")]
    public static class ResearchManager_SetCurrentProject_Patch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj != null)
                Current.Game.GetComponent<AutoResearcher>()?.Notify_ProjectStarted(proj);
        }
    }

    [HarmonyPatch(typeof(ResearchManager), "StopProject")]
    public static class ResearchManager_StopProject_Patch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj != null)
                Current.Game.GetComponent<AutoResearcher>()?.Notify_ProjectStopped(proj);
        }
    }

    public enum ResearchCategory { Standard, Anomaly, Gravship, Divinitech }

    public class AutoResearcher : GameComponent
    {
        private const string TabAnomaly = "Anomaly";
        private const string TabGravtech = "VGE_Gravtech";
        private const string TabGravShip = "VGE_GravShip";
        private const string KnowledgeDivinitech = "Information";
        private const int IdlePollInterval = 2500;
        private const int ResearchTabDeferTicks = 300;

        private bool everythingFinishedLetterSent;
        private int delayTicks = -1;
        private ResearchProjectDef lastAssignedGravtech;
        private readonly List<ResearchProjectDef> candidateCache = new List<ResearchProjectDef>();

        public AutoResearcher(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref everythingFinishedLetterSent, "everythingFinishedLetterSent", false);
            Scribe_Values.Look(ref delayTicks, "delayTicks", -1);
            Scribe_Defs.Look(ref lastAssignedGravtech, "lastAssignedGravtech");
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % IdlePollInterval == 0 && ResearchAutoMod.settings.modEnabled)
                AssignNextResearchIfIdle();

            if (delayTicks > 0 && --delayTicks == 0)
            {
                AssignNextResearchIfIdle();
                delayTicks = -1;
            }
        }

        public void TriggerDelayedCheck(int ticks) => delayTicks = ticks;

        public void Notify_ProjectStarted(ResearchProjectDef proj)
        {
            if (IsGravship(proj))
                lastAssignedGravtech = proj;
        }

        public void Notify_ProjectStopped(ResearchProjectDef proj)
        {
            if (lastAssignedGravtech == proj)
                lastAssignedGravtech = null;
        }

        public void AssignNextResearchIfIdle()
        {
            if (!ResearchAutoMod.settings.modEnabled)
                return;

            if (Find.MainTabsRoot != null && Find.MainTabsRoot.OpenTab == MainButtonDefOf.Research)
            {
                delayTicks = ResearchTabDeferTicks;
                return;
            }

            var settings = ResearchAutoMod.settings;
            GetActiveParallelProjects(out bool anomalyActive, out bool gravshipActive, out bool divinitechActive);

            if (settings.includeAnomaly && !anomalyActive)
                TryStartResearch(ResearchCategory.Anomaly);

            if (settings.includeDivinitech && !divinitechActive)
                TryStartResearch(ResearchCategory.Divinitech);

            if (settings.includeGravship)
            {
                if (!gravshipActive && lastAssignedGravtech != null && !lastAssignedGravtech.IsFinished)
                {
                    if (lastAssignedGravtech.CanStartNow)
                        gravshipActive = true;
                    else
                        lastAssignedGravtech = null;
                }

                if (!gravshipActive)
                    TryStartResearch(ResearchCategory.Gravship);
            }

            if (Find.ResearchManager.GetProject() == null)
            {
                if (TryStartResearch(ResearchCategory.Standard))
                {
                    everythingFinishedLetterSent = false;
                }
                else if (!everythingFinishedLetterSent)
                {
                    Find.LetterStack.ReceiveLetter(
                        "Auto-Research Complete",
                        "All available standard research projects for your current era and settings have been completed. Advance your tech level or adjust your mod settings to continue.",
                        LetterDefOf.NeutralEvent);
                    everythingFinishedLetterSent = true;
                }
            }
            else
            {
                everythingFinishedLetterSent = false;
            }
        }

        private void GetActiveParallelProjects(out bool anomalyActive, out bool gravshipActive, out bool divinitechActive)
        {
            anomalyActive = false;
            gravshipActive = false;
            divinitechActive = false;

            var research = Find.ResearchManager;
            var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                if (p.knowledgeCategory == null || research.GetProject(p.knowledgeCategory) != p)
                    continue;

                if (IsAnomaly(p))
                    anomalyActive = true;
                else if (IsGravship(p))
                    gravshipActive = true;
                else if (IsDivinitech(p))
                    divinitechActive = true;
            }
        }

        private bool TryStartResearch(ResearchCategory category)
        {
            CollectCandidates(category);
            if (candidateCache.Count == 0)
                return false;

            PreferProjectsWithProgress();
            var selected = SelectCandidate(category);
            if (selected == null)
                return false;

            Find.ResearchManager.SetCurrentProject(selected);
            if (ResearchAutoMod.settings.showMessages)
                Messages.Message($"Research started: {selected.LabelCap}", MessageTypeDefOf.SilentInput, false);
            return true;
        }

        private void CollectCandidates(ResearchCategory category)
        {
            candidateCache.Clear();

            TechLevel playerTech = Faction.OfPlayer.def.techLevel;
            bool restrict = ResearchAutoMod.settings.restrictToPlayerTech;
            var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                if (p.IsFinished || !p.CanStartNow)
                    continue;
                if (GetCategory(p) != category)
                    continue;
                if (category == ResearchCategory.Standard && restrict && p.techLevel > playerTech)
                    continue;

                candidateCache.Add(p);
            }
        }

        private void PreferProjectsWithProgress()
        {
            var research = Find.ResearchManager;
            bool anyPartial = false;
            for (int i = 0; i < candidateCache.Count; i++)
            {
                if (research.GetProgress(candidateCache[i]) > 0f)
                {
                    anyPartial = true;
                    break;
                }
            }

            if (!anyPartial)
                return;

            for (int i = candidateCache.Count - 1; i >= 0; i--)
            {
                if (research.GetProgress(candidateCache[i]) <= 0f)
                    candidateCache.RemoveAt(i);
            }
        }

        private ResearchProjectDef SelectCandidate(ResearchCategory category)
        {
            bool matchTechLevel = category == ResearchCategory.Standard && !ResearchAutoMod.settings.ignoreTechLevel;
            bool prioritizeExpensive = ResearchAutoMod.settings.prioritizeExpensive;

            TechLevel targetTech = TechLevel.Undefined;
            if (matchTechLevel)
            {
                targetTech = candidateCache[0].techLevel;
                for (int i = 1; i < candidateCache.Count; i++)
                {
                    if (candidateCache[i].techLevel < targetTech)
                        targetTech = candidateCache[i].techLevel;
                }
            }

            float targetCost = prioritizeExpensive ? float.MinValue : float.MaxValue;
            bool foundCost = false;
            for (int i = 0; i < candidateCache.Count; i++)
            {
                var p = candidateCache[i];
                if (matchTechLevel && p.techLevel != targetTech)
                    continue;

                foundCost = true;
                if (prioritizeExpensive)
                {
                    if (p.baseCost > targetCost)
                        targetCost = p.baseCost;
                }
                else if (p.baseCost < targetCost)
                {
                    targetCost = p.baseCost;
                }
            }

            if (!foundCost)
                return null;

            ResearchProjectDef selected = null;
            int matchCount = 0;
            for (int i = 0; i < candidateCache.Count; i++)
            {
                var p = candidateCache[i];
                if (matchTechLevel && p.techLevel != targetTech)
                    continue;
                if (p.baseCost != targetCost)
                    continue;

                matchCount++;
                if (selected == null || Rand.Chance(1f / matchCount))
                    selected = p;
            }

            return selected;
        }

        private static ResearchCategory GetCategory(ResearchProjectDef p)
        {
            if (IsAnomaly(p))
                return ResearchCategory.Anomaly;
            if (IsGravship(p))
                return ResearchCategory.Gravship;
            if (IsDivinitech(p))
                return ResearchCategory.Divinitech;
            return ResearchCategory.Standard;
        }

        private static bool IsAnomaly(ResearchProjectDef p) => p.tab?.defName == TabAnomaly;

        private static bool IsGravship(ResearchProjectDef p)
        {
            string tab = p.tab?.defName;
            return tab == TabGravtech || tab == TabGravShip;
        }

        private static bool IsDivinitech(ResearchProjectDef p) =>
            p.knowledgeCategory?.defName == KnowledgeDivinitech;
    }
}
