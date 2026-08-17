using System;
using System.Collections.Generic;

namespace LoogaSoft.Advancement
{
    public enum ProgressionNodeAvailability
    {
        Available,
        Owned,
        MaximumRank,
        Locked,
        Unaffordable
    }

    public enum ProgressionLevelAvailability
    {
        Available,
        Disabled,
        MaximumLevel,
        Locked,
        Unaffordable
    }

    public interface IProgressionEvaluationContext
    {
        DateTime UtcNow { get; }
        DateTime SeasonStartUtc { get; }
        int AccountLevel { get; }
        int GetExternalLevel(string trackId);
        int GetCurrencyAmount(string currencyId);
        int GetItemAmount(string itemId);
        int GetSeasonTierOverride(string key);
        bool IsChallengeComplete(string challengeId);
        bool HasFlag(string flagId);
    }

    public sealed class ProgressionNodeEvaluation
    {
        private readonly List<string> _blockers = new();

        public ProgressionNodeAvailability Availability { get; internal set; }
        public int CurrentRank { get; internal set; }
        public int NextRank { get; internal set; }
        public IReadOnlyList<string> Blockers => _blockers;

        internal void AddBlocker(string blocker)
        {
            if (!string.IsNullOrWhiteSpace(blocker))
                _blockers.Add(blocker);
        }
    }

    public sealed class ProgressionLevelEvaluation
    {
        private readonly List<string> _blockers = new();

        public ProgressionLevelAvailability Availability { get; internal set; }
        public int CurrentLevel { get; internal set; }
        public int TargetLevel { get; internal set; }
        public IReadOnlyList<string> Blockers => _blockers;

        internal void AddBlocker(string blocker)
        {
            if (!string.IsNullOrWhiteSpace(blocker))
                _blockers.Add(blocker);
        }
    }

    /// <summary>Evaluates authored progression without changing player state.</summary>
    public static class ProgressionEvaluator
    {
        public static int CalculateEarnedPoints(
            ProgressionProgramDefinition program,
            IProgressionEvaluationContext context)
        {
            if (program?.PointPolicy == null || context == null || !program.PointPolicy.UsesPoints)
                return 0;

            int earned = program.PointPolicy.StartingPoints;
            IReadOnlyList<ProgressionPointAwardDefinition> awards = program.PointPolicy.Awards;
            for (int index = 0; index < awards.Count; index++)
            {
                ProgressionPointAwardDefinition award = awards[index];
                if (award != null && IsPointAwardEarned(award, context))
                    earned += award.Points;
            }

            return Math.Min(earned, program.PointPolicy.MaximumEarnedPoints);
        }

        public static ProgressionLevelEvaluation EvaluateNextLevel(
            ProgressionProgramDefinition program,
            ProgressionProgramState state,
            IProgressionEvaluationContext context)
        {
            ProgressionLevelEvaluation result = new();
            if (program == null || state == null || context == null)
            {
                result.Availability = ProgressionLevelAvailability.Disabled;
                result.AddBlocker("Progression data is incomplete.");
                return result;
            }

            result.CurrentLevel = state.ProgramLevel;
            result.TargetLevel = result.CurrentLevel + 1;
            ProgressionLevelTrackDefinition track = program.LevelTrack;
            if (track == null || track.Mode != ProgressionLevelMode.PurchasedSteps)
            {
                result.Availability = ProgressionLevelAvailability.Disabled;
                return result;
            }

            ProgressionLevelStepDefinition step = track.FindStep(result.TargetLevel);
            if (step == null)
            {
                result.Availability = ProgressionLevelAvailability.MaximumLevel;
                return result;
            }

            bool locked = false;
            for (int index = 0; index < step.Requirements.Count; index++)
            {
                ProgressionRequirementDefinition requirement = step.Requirements[index];
                if (requirement == null || IsRequirementMet(requirement, state, context, int.MaxValue))
                    continue;

                result.AddBlocker(DescribeRequirement(requirement));
                locked = true;
            }

            if (locked)
            {
                result.Availability = ProgressionLevelAvailability.Locked;
                return result;
            }

            bool affordable = true;
            for (int index = 0; index < step.Costs.Count; index++)
            {
                ProgressionCostDefinition cost = step.Costs[index];
                if (cost == null)
                    continue;

                int owned = cost.Kind == ProgressionCostKind.Currency
                    ? context.GetCurrencyAmount(cost.Key)
                    : context.GetItemAmount(cost.Key);
                if (owned >= cost.Amount)
                    continue;

                result.AddBlocker($"Requires {cost.Amount} {cost.Key}.");
                affordable = false;
            }

            result.Availability = affordable
                ? ProgressionLevelAvailability.Available
                : ProgressionLevelAvailability.Unaffordable;
            return result;
        }

        public static ProgressionNodeEvaluation EvaluateNode(
            ProgressionProgramDefinition program,
            ProgressionNodeDefinition node,
            ProgressionProgramState state,
            IProgressionEvaluationContext context)
        {
            ProgressionNodeEvaluation result = new();
            if (program == null || node == null || state == null || context == null)
            {
                result.Availability = ProgressionNodeAvailability.Locked;
                result.AddBlocker("Progression data is incomplete.");
                return result;
            }

            result.CurrentRank = state.GetNodeRank(node.StableId);
            result.NextRank = result.CurrentRank + 1;
            if (result.CurrentRank >= node.MaxRank)
            {
                result.Availability = ProgressionNodeAvailability.MaximumRank;
                return result;
            }

            bool locked = !EvaluatePrerequisites(program.Graph, node, state, result) |
                          !EvaluateRequirements(program, node, state, context, result);
            if (locked)
            {
                result.Availability = ProgressionNodeAvailability.Locked;
                return result;
            }

            if (!CanAfford(program, node, state, context, result))
            {
                result.Availability = ProgressionNodeAvailability.Unaffordable;
                return result;
            }

            result.Availability = result.CurrentRank > 0
                ? ProgressionNodeAvailability.Owned
                : ProgressionNodeAvailability.Available;
            return result;
        }

        private static bool EvaluatePrerequisites(
            ProgressionGraphDefinition graph,
            ProgressionNodeDefinition node,
            ProgressionProgramState state,
            ProgressionNodeEvaluation result)
        {
            ProgressionPrerequisiteDefinition prerequisites = node.Prerequisites;
            if (prerequisites == null || prerequisites.Entries.Count == 0)
            {
                return true;
            }

            int satisfiedCount = 0;
            List<string> unmetLabels = new();
            HashSet<string> evaluatedNodeIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < prerequisites.Entries.Count; index++)
            {
                ProgressionPrerequisiteEntryDefinition entry = prerequisites.Entries[index];
                if (entry == null || !evaluatedNodeIds.Add(entry.NodeId))
                {
                    continue;
                }

                if (state.GetNodeRank(entry.NodeId) >= entry.RequiredRank)
                {
                    satisfiedCount++;
                    continue;
                }

                string label = graph != null && graph.TryGetNode(entry.NodeId, out ProgressionNodeDefinition prerequisite)
                    ? prerequisite.DisplayName
                    : entry.NodeId;
                unmetLabels.Add(entry.RequiredRank > 1
                    ? $"{label} at rank {entry.RequiredRank}"
                    : label);
            }

            if (satisfiedCount >= prerequisites.RequiredCount)
            {
                return true;
            }

            if (prerequisites.Mode == ProgressionPrerequisiteMode.All)
            {
                for (int index = 0; index < unmetLabels.Count; index++)
                {
                    result.AddBlocker($"Requires {unmetLabels[index]}.");
                }

                return false;
            }

            int remainingCount = prerequisites.RequiredCount - satisfiedCount;
            string alternatives = string.Join(", ", unmetLabels);
            result.AddBlocker(prerequisites.Mode == ProgressionPrerequisiteMode.Any
                ? $"Requires any one of: {alternatives}."
                : $"Requires any {remainingCount} more of: {alternatives}.");
            return false;
        }

        private static bool EvaluateRequirements(
            ProgressionProgramDefinition program,
            ProgressionNodeDefinition node,
            ProgressionProgramState state,
            IProgressionEvaluationContext context,
            ProgressionNodeEvaluation result)
        {
            bool passed = true;
            int availableSeasonTier = program.SeasonPolicy.GetAvailableTier(
                context.UtcNow,
                context.SeasonStartUtc,
                context.GetSeasonTierOverride);
            if (node.Tier > availableSeasonTier)
            {
                result.AddBlocker($"Tier {node.Tier} is not available yet.");
                passed = false;
            }

            for (int index = 0; index < node.Requirements.Count; index++)
            {
                ProgressionRequirementDefinition requirement = node.Requirements[index];
                if (requirement == null || IsRequirementMet(requirement, state, context, availableSeasonTier))
                    continue;

                result.AddBlocker(DescribeRequirement(requirement));
                passed = false;
            }

            return passed;
        }

        private static bool IsRequirementMet(
            ProgressionRequirementDefinition requirement,
            ProgressionProgramState state,
            IProgressionEvaluationContext context,
            int availableSeasonTier)
        {
            return requirement.Kind switch
            {
                ProgressionRequirementKind.AccountLevel => context.AccountLevel >= requirement.RequiredValue,
                ProgressionRequirementKind.ProgramLevel => state.ProgramLevel >= requirement.RequiredValue,
                ProgressionRequirementKind.ExternalLevel =>
                    context.GetExternalLevel(requirement.Key) >= requirement.RequiredValue,
                ProgressionRequirementKind.ChallengeComplete => context.IsChallengeComplete(requirement.Key),
                ProgressionRequirementKind.SeasonTier => availableSeasonTier >= requirement.RequiredValue,
                ProgressionRequirementKind.CustomFlag => context.HasFlag(requirement.Key),
                _ => false
            };
        }

        private static bool CanAfford(
            ProgressionProgramDefinition program,
            ProgressionNodeDefinition node,
            ProgressionProgramState state,
            IProgressionEvaluationContext context,
            ProgressionNodeEvaluation result)
        {
            bool passed = true;
            if (program.PointPolicy.UsesPoints)
            {
                int availablePoints = Math.Min(
                    Math.Max(state.EarnedPoints, CalculateEarnedPoints(program, context)),
                    program.PointPolicy.MaximumEarnedPoints) - state.GetSpentPoints(program.Graph);
                if (availablePoints < node.PointCostPerRank)
                {
                    result.AddBlocker($"Requires {node.PointCostPerRank} specialization point(s).");
                    passed = false;
                }
            }

            for (int index = 0; index < node.Costs.Count; index++)
            {
                ProgressionCostDefinition cost = node.Costs[index];
                if (cost == null)
                    continue;

                int owned = cost.Kind == ProgressionCostKind.Currency
                    ? context.GetCurrencyAmount(cost.Key)
                    : context.GetItemAmount(cost.Key);
                if (owned >= cost.Amount)
                    continue;

                result.AddBlocker($"Requires {cost.Amount} {cost.Key}.");
                passed = false;
            }

            return passed;
        }

        private static string DescribeRequirement(ProgressionRequirementDefinition requirement)
        {
            return requirement.Kind switch
            {
                ProgressionRequirementKind.AccountLevel => $"Requires account level {requirement.RequiredValue}.",
                ProgressionRequirementKind.ProgramLevel => $"Requires facility level {requirement.RequiredValue}.",
                ProgressionRequirementKind.ExternalLevel =>
                    $"Requires {requirement.Key} level {requirement.RequiredValue}.",
                ProgressionRequirementKind.ChallengeComplete => $"Requires challenge {requirement.Key}.",
                ProgressionRequirementKind.SeasonTier => $"Requires season tier {requirement.RequiredValue}.",
                ProgressionRequirementKind.CustomFlag => $"Requires {requirement.Key}.",
                _ => "A requirement is not met."
            };
        }

        private static bool IsPointAwardEarned(
            ProgressionPointAwardDefinition award,
            IProgressionEvaluationContext context)
        {
            return award.Source switch
            {
                ProgressionRequirementKind.AccountLevel => context.AccountLevel >= award.RequiredValue,
                ProgressionRequirementKind.ExternalLevel =>
                    context.GetExternalLevel(award.SourceId) >= award.RequiredValue,
                ProgressionRequirementKind.ChallengeComplete => context.IsChallengeComplete(award.SourceId),
                ProgressionRequirementKind.SeasonTier =>
                    context.GetSeasonTierOverride(award.SourceId) >= award.RequiredValue,
                ProgressionRequirementKind.CustomFlag => context.HasFlag(award.SourceId),
                _ => false
            };
        }
    }
}
