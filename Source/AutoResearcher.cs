using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchAuto
{
    public class AutoResearcher : GameComponent
    {
        private const string TabAnomaly = "Anomaly";
        private const string TabGravtech = "VGE_Gravtech";
        private const string TabGravShip = "VGE_GravShip";
        private const string KnowledgeDivinitech = "Information";
        private const string VgeGravtechSlotType = "VanillaGravshipExpanded.World_ExposeData_Patch";
        private const string VgeGravtechSlotField = "currentGravtechProject";
        private const int IdlePollInterval = 2500;
        private const int ResearchTabDeferTicks = 300;

        private static ResearchTabDef tabAnomalyDef;
        private static ResearchTabDef tabGravtechDef;
        private static ResearchTabDef tabGravShipDef;
        private static KnowledgeCategoryDef knowledgeDivinitechDef;
        // Vanilla Gravship Expanded's own gravtech slot; null without it, when gravtech uses the standard slot
        private static FieldInfo vgeGravtechSlot;
        private static bool tabsResolved;

        private bool everythingFinishedLetterSent;
        private int delayTicks = -1;
        private readonly List<ResearchProjectDef> anomalyCandidates = new List<ResearchProjectDef>();
        private readonly List<ResearchProjectDef> divinitechCandidates = new List<ResearchProjectDef>();
        private readonly List<ResearchProjectDef> gravshipCandidates = new List<ResearchProjectDef>();
        private readonly List<ResearchProjectDef> standardCandidates = new List<ResearchProjectDef>();

        public AutoResearcher(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref everythingFinishedLetterSent, "everythingFinishedLetterSent", false);
            Scribe_Values.Look(ref delayTicks, "delayTicks", -1);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % IdlePollInterval == 0 && ResearchAutoMod.settings.modEnabled)
                AssignNextResearchIfIdle();

            if (delayTicks > 0 && --delayTicks == 0)
            {
                // Cleared first so a deferral set by the call below is kept
                delayTicks = -1;
                AssignNextResearchIfIdle();
            }
        }

        public void TriggerDelayedCheck(int ticks) => delayTicks = ticks;

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
            ClassifyProjects(out bool anomalyActive, out bool gravshipActive, out bool divinitechActive);

            if (settings.includeAnomaly && !anomalyActive)
                TryStartResearch(ResearchCategory.Anomaly);

            if (settings.includeDivinitech && !divinitechActive)
                TryStartResearch(ResearchCategory.Divinitech);

            if (settings.includeGravship && !gravshipActive)
                TryStartResearch(ResearchCategory.Gravship);

            if (Find.ResearchManager.GetProject() == null)
            {
                if (TryStartResearch(ResearchCategory.Standard))
                {
                    everythingFinishedLetterSent = false;
                }
                else if (!everythingFinishedLetterSent)
                {
                    Find.LetterStack.ReceiveLetter(
                        "ResearchAuto_AllDoneLabel".Translate(),
                        "ResearchAuto_AllDoneText".Translate(),
                        LetterDefOf.NeutralEvent);
                    everythingFinishedLetterSent = true;
                }
            }
            else
            {
                everythingFinishedLetterSent = false;
            }
        }

        private static void EnsureTabsResolved()
        {
            if (tabsResolved)
                return;

            tabAnomalyDef = DefDatabase<ResearchTabDef>.GetNamedSilentFail(TabAnomaly);
            tabGravtechDef = DefDatabase<ResearchTabDef>.GetNamedSilentFail(TabGravtech);
            tabGravShipDef = DefDatabase<ResearchTabDef>.GetNamedSilentFail(TabGravShip);
            knowledgeDivinitechDef = DefDatabase<KnowledgeCategoryDef>.GetNamedSilentFail(KnowledgeDivinitech);
            if (tabGravtechDef != null)
            {
                var slotType = AccessTools.TypeByName(VgeGravtechSlotType);
                vgeGravtechSlot = slotType != null ? AccessTools.Field(slotType, VgeGravtechSlotField) : null;
            }
            tabsResolved = true;
        }

        private static bool VgeGravtechSlotBusy()
        {
            return vgeGravtechSlot?.GetValue(null) is ResearchProjectDef project && !project.IsFinished;
        }

        private void ClassifyProjects(out bool anomalyActive, out bool gravshipActive, out bool divinitechActive)
        {
            anomalyActive = false;
            gravshipActive = false;
            divinitechActive = false;

            anomalyCandidates.Clear();
            divinitechCandidates.Clear();
            gravshipCandidates.Clear();
            standardCandidates.Clear();

            EnsureTabsResolved();

            var research = Find.ResearchManager;
            TechLevel playerTech = Faction.OfPlayer.def.techLevel;
            bool restrict = ResearchAutoMod.settings.restrictToPlayerTech;
            var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                bool isAnomaly = IsAnomaly(p);
                bool isGravship = !isAnomaly && IsGravship(p);
                bool isDivinitech = !isAnomaly && !isGravship && IsDivinitech(p);

                if (p.knowledgeCategory != null && research.GetProject(p.knowledgeCategory) == p)
                {
                    if (isAnomaly)
                        anomalyActive = true;
                    else if (isGravship)
                        gravshipActive = true;
                    else if (isDivinitech)
                        divinitechActive = true;
                }

                if (p.IsFinished || !p.CanStartNow)
                    continue;

                if (isAnomaly)
                    anomalyCandidates.Add(p);
                else if (isGravship)
                    gravshipCandidates.Add(p);
                else if (isDivinitech)
                    divinitechCandidates.Add(p);
                else if (!restrict || p.techLevel <= playerTech)
                    standardCandidates.Add(p);
            }

            if (VgeGravtechSlotBusy())
                gravshipActive = true;
        }

        private List<ResearchProjectDef> GetCandidates(ResearchCategory category)
        {
            switch (category)
            {
                case ResearchCategory.Anomaly:
                    return anomalyCandidates;
                case ResearchCategory.Gravship:
                    return gravshipCandidates;
                case ResearchCategory.Divinitech:
                    return divinitechCandidates;
                default:
                    return standardCandidates;
            }
        }

        private bool TryStartResearch(ResearchCategory category)
        {
            var candidates = GetCandidates(category);
            // Vanilla Gravship Expanded moves gravtech projects into its own slot, so the standard slot doesn't block them
            bool ownSlot = category == ResearchCategory.Gravship && vgeGravtechSlot != null;
            RemoveProjectsWithOccupiedSlots(candidates, ownSlot);
            if (candidates.Count == 0)
                return false;

            PreferProjectsWithProgress(candidates);
            var selected = SelectCandidate(category, candidates);
            if (selected == null)
                return false;

            Find.ResearchManager.SetCurrentProject(selected);
            if (ResearchAutoMod.settings.showMessages)
                Messages.Message("ResearchAuto_ResearchStarted".Translate(selected.LabelCap), MessageTypeDefOf.SilentInput, false);
            return true;
        }

        private static void RemoveProjectsWithOccupiedSlots(List<ResearchProjectDef> candidates, bool ownSlot)
        {
            var research = Find.ResearchManager;
            bool standardBusy = !ownSlot && research.GetProject() != null;
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                bool standardSlotOccupied = standardBusy && candidate.baseCost > 0f;
                bool knowledgeSlotOccupied = candidate.knowledgeCategory != null
                    && research.GetProject(candidate.knowledgeCategory) != null;

                if (standardSlotOccupied || knowledgeSlotOccupied)
                    candidates.RemoveAt(i);
            }
        }

        private static void PreferProjectsWithProgress(List<ResearchProjectDef> candidates)
        {
            var research = Find.ResearchManager;
            bool anyPartial = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (research.GetProgress(candidates[i]) > 0f)
                {
                    anyPartial = true;
                    break;
                }
            }

            if (!anyPartial)
                return;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (research.GetProgress(candidates[i]) <= 0f)
                    candidates.RemoveAt(i);
            }
        }

        private static ResearchProjectDef SelectCandidate(ResearchCategory category, List<ResearchProjectDef> candidates)
        {
            bool matchTechLevel = category == ResearchCategory.Standard && !ResearchAutoMod.settings.ignoreTechLevel;
            bool prioritizeExpensive = ResearchAutoMod.settings.prioritizeExpensive;

            TechLevel targetTech = TechLevel.Undefined;
            if (matchTechLevel)
            {
                targetTech = candidates[0].techLevel;
                for (int i = 1; i < candidates.Count; i++)
                {
                    if (candidates[i].techLevel < targetTech)
                        targetTech = candidates[i].techLevel;
                }
            }

            float targetCost = prioritizeExpensive ? float.MinValue : float.MaxValue;
            bool foundCost = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
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
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
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

        // A def is null when its DLC or mod isn't loaded; never match on null,
        // or every project with no tab/knowledge category lands in that queue.
        private static bool IsAnomaly(ResearchProjectDef p)
        {
            return tabAnomalyDef != null && p.tab == tabAnomalyDef;
        }

        private static bool IsGravship(ResearchProjectDef p)
        {
            return (tabGravtechDef != null && p.tab == tabGravtechDef)
                || (tabGravShipDef != null && p.tab == tabGravShipDef);
        }

        private static bool IsDivinitech(ResearchProjectDef p)
        {
            return knowledgeDivinitechDef != null && p.knowledgeCategory == knowledgeDivinitechDef;
        }
    }
}
