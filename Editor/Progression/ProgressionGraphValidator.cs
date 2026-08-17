using System;
using System.Collections.Generic;

namespace LoogaSoft.Advancement.Editor
{
    internal enum ProgressionValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal readonly struct ProgressionValidationIssue
    {
        public ProgressionValidationIssue(ProgressionValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public ProgressionValidationSeverity Severity { get; }
        public string Message { get; }
    }

    internal static class ProgressionGraphValidator
    {
        public static List<ProgressionValidationIssue> Validate(ProgressionGraphDefinition graph)
        {
            List<ProgressionValidationIssue> issues = new();
            if (graph == null)
            {
                issues.Add(new ProgressionValidationIssue(
                    ProgressionValidationSeverity.Error,
                    "Assign a progression graph."));
                return issues;
            }

            ValidateStableIds(graph, issues);
            ValidateBranchOrigins(graph, issues);
            ValidatePrerequisites(graph, issues);
            ValidateResolvablePrerequisites(graph, issues);

            if (issues.Count == 0)
            {
                issues.Add(new ProgressionValidationIssue(
                    ProgressionValidationSeverity.Info,
                    $"{graph.Nodes.Count} node(s) passed validation."));
            }

            return issues;
        }

        private static void ValidateStableIds(
            ProgressionGraphDefinition graph,
            List<ProgressionValidationIssue> issues)
        {
            HashSet<string> branchIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < graph.Branches.Count; index++)
            {
                ProgressionBranchDefinition branch = graph.Branches[index];
                if (branch == null || string.IsNullOrWhiteSpace(branch.StableId))
                {
                    issues.Add(Error($"Branch {index + 1} has no stable ID."));
                    continue;
                }

                if (!branchIds.Add(branch.StableId))
                    issues.Add(Error($"Branch ID '{branch.StableId}' is duplicated."));
            }

            HashSet<string> nodeIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                ProgressionNodeDefinition node = graph.Nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.StableId))
                {
                    issues.Add(Error($"Node {index + 1} has no stable ID."));
                    continue;
                }

                if (!nodeIds.Add(node.StableId))
                    issues.Add(Error($"Node ID '{node.StableId}' is duplicated."));
            }
        }

        private static void ValidateBranchOrigins(
            ProgressionGraphDefinition graph,
            List<ProgressionValidationIssue> issues)
        {
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                ProgressionNodeDefinition node = graph.Nodes[index];
                if (node == null)
                    continue;

                if (node.Prerequisites.Entries.Count > 0)
                    continue;

                if (string.IsNullOrWhiteSpace(node.OriginBranchId))
                {
                    issues.Add(Error($"Root node '{node.DisplayName}' has no origin branch."));
                    continue;
                }

                if (graph.FindBranch(node.OriginBranchId) == null)
                    issues.Add(Error($"Root node '{node.DisplayName}' references a missing origin branch."));
            }
        }

        private static void ValidatePrerequisites(
            ProgressionGraphDefinition graph,
            List<ProgressionValidationIssue> issues)
        {
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                ProgressionNodeDefinition node = graph.Nodes[index];
                if (node == null)
                    continue;

                ProgressionPrerequisiteDefinition definition = node.Prerequisites;
                if (definition == null)
                    continue;

                if (definition.Mode == ProgressionPrerequisiteMode.AtLeast &&
                    definition.AuthoredRequiredCount > definition.Entries.Count)
                {
                    issues.Add(Error(
                        $"Node '{node.DisplayName}' requires more prerequisites than it defines."));
                }

                HashSet<string> prerequisites = new(StringComparer.OrdinalIgnoreCase);
                for (int prerequisiteIndex = 0;
                     prerequisiteIndex < definition.Entries.Count;
                     prerequisiteIndex++)
                {
                    ProgressionPrerequisiteEntryDefinition entry = definition.Entries[prerequisiteIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.NodeId))
                    {
                        issues.Add(Error($"Node '{node.DisplayName}' has an empty prerequisite."));
                        continue;
                    }

                    string prerequisiteId = entry.NodeId;
                    if (!prerequisites.Add(prerequisiteId))
                        issues.Add(Warning($"Node '{node.DisplayName}' repeats prerequisite '{prerequisiteId}'."));

                    if (string.Equals(node.StableId, prerequisiteId, StringComparison.OrdinalIgnoreCase))
                        issues.Add(Error($"Node '{node.DisplayName}' requires itself."));
                    else if (!graph.TryGetNode(prerequisiteId, out ProgressionNodeDefinition prerequisite))
                        issues.Add(Error($"Node '{node.DisplayName}' references missing node '{prerequisiteId}'."));
                    else if (entry.RequiredRank > prerequisite.MaxRank)
                    {
                        issues.Add(Error(
                            $"Node '{node.DisplayName}' requires rank {entry.RequiredRank} of " +
                            $"'{prerequisite.DisplayName}', but that node has only {prerequisite.MaxRank} rank(s)."));
                    }
                }
            }
        }

        private static void ValidateResolvablePrerequisites(
            ProgressionGraphDefinition graph,
            List<ProgressionValidationIssue> issues)
        {
            HashSet<string> resolvableNodeIds = new(StringComparer.OrdinalIgnoreCase);
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < graph.Nodes.Count; index++)
                {
                    ProgressionNodeDefinition node = graph.Nodes[index];
                    if (node == null || resolvableNodeIds.Contains(node.StableId))
                        continue;

                    ProgressionPrerequisiteDefinition definition = node.Prerequisites;
                    if (definition == null || definition.RequiredCount == 0)
                    {
                        changed |= resolvableNodeIds.Add(node.StableId);
                        continue;
                    }

                    int resolvedCount = CountResolvedPrerequisites(definition, resolvableNodeIds);
                    if (resolvedCount >= definition.RequiredCount)
                        changed |= resolvableNodeIds.Add(node.StableId);
                }
            }
            while (changed);

            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                ProgressionNodeDefinition node = graph.Nodes[index];
                if (node == null || resolvableNodeIds.Contains(node.StableId))
                    continue;

                issues.Add(Error(
                    $"Node '{node.DisplayName}' has prerequisites that cannot be resolved. " +
                    "Check for a dependency cycle or an impossible threshold."));
            }
        }

        private static int CountResolvedPrerequisites(
            ProgressionPrerequisiteDefinition definition,
            HashSet<string> resolvableNodeIds)
        {
            int resolvedCount = 0;
            HashSet<string> countedNodeIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < definition.Entries.Count; index++)
            {
                ProgressionPrerequisiteEntryDefinition entry = definition.Entries[index];
                if (entry != null &&
                    countedNodeIds.Add(entry.NodeId) &&
                    resolvableNodeIds.Contains(entry.NodeId))
                    resolvedCount++;
            }

            return resolvedCount;
        }

        private static ProgressionValidationIssue Error(string message)
        {
            return new ProgressionValidationIssue(ProgressionValidationSeverity.Error, message);
        }

        private static ProgressionValidationIssue Warning(string message)
        {
            return new ProgressionValidationIssue(ProgressionValidationSeverity.Warning, message);
        }
    }
}
