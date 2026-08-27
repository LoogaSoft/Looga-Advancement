using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    /// <summary>Defines how node positions are resolved for presentation.</summary>
    public enum ProgressionGraphLayoutMode
    {
        Automatic,
        Manual
    }

    /// <summary>Defines how many prerequisite entries a node must satisfy.</summary>
    public enum ProgressionPrerequisiteMode
    {
        All,
        Any,
        AtLeast
    }

    /// <summary>Defines the authored nodes and links for one progression graph.</summary>
    [CreateAssetMenu(fileName = "Progression Graph", menuName = "LoogaSoft/Advancement/Progression/Graph")]
    public sealed class ProgressionGraphDefinition : ScriptableObject
    {
        public static event Action<ProgressionGraphDefinition> DefinitionChanged;

        [SerializeField, HideInInspector] private string _stableId = string.Empty;
        [SerializeField, HideInInspector] private int _identityVersion;
        [SerializeField] private ProgressionGraphLayoutMode _layoutMode;
        [SerializeField, Min(80f)] private float _tierSpacing = 250f;
        [SerializeField, Min(60f)] private float _nodeSpacing = 110f;
        [SerializeField, Min(80f)] private float _branchSpacing = 150f;
        [SerializeField] private List<ProgressionBranchDefinition> _branches = new();
        [SerializeField] private List<ProgressionNodeDefinition> _nodes = new();

        public string StableId => _stableId;
        public ProgressionGraphLayoutMode LayoutMode => _layoutMode;
        public IReadOnlyList<ProgressionBranchDefinition> Branches => _branches;
        public IReadOnlyList<ProgressionNodeDefinition> Nodes => _nodes;

        /// <summary>Returns the authored or automatically arranged position for one node.</summary>
        public Vector2 GetNodePosition(ProgressionNodeDefinition node)
        {
            if (node == null || _layoutMode == ProgressionGraphLayoutMode.Manual)
                return node?.GraphPosition ?? Vector2.zero;

            string layoutBranchId = GetLayoutBranchId(node);
            int branchIndex = FindBranchIndex(layoutBranchId);
            int laneIndex = FindLaneIndex(node);
            float branchOffset = CalculateBranchOffset(branchIndex);
            return new Vector2(
                branchOffset + laneIndex * _nodeSpacing,
                Mathf.Max(0, node.Tier - 1) * _tierSpacing);
        }

        public bool TryGetNode(string nodeId, out ProgressionNodeDefinition node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodeId))
                return false;

            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeDefinition candidate = _nodes[index];
                if (candidate != null &&
                    string.Equals(candidate.StableId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    node = candidate;
                    return true;
                }
            }

            return false;
        }

        public ProgressionBranchDefinition FindBranch(string branchId)
        {
            for (int index = 0; index < _branches.Count; index++)
            {
                ProgressionBranchDefinition branch = _branches[index];
                if (branch != null &&
                    string.Equals(branch.StableId, branchId, StringComparison.OrdinalIgnoreCase))
                {
                    return branch;
                }
            }

            return null;
        }

        /// <summary>Returns every branch inherited through the node's prerequisite lineage.</summary>
        public IReadOnlyList<ProgressionBranchDefinition> GetNodeBranches(ProgressionNodeDefinition node)
        {
            List<ProgressionBranchDefinition> result = new();
            if (node == null)
                return result;

            HashSet<string> branchIds = new(StringComparer.OrdinalIgnoreCase);
            ResolveNodeBranchIds(node, branchIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            for (int index = 0; index < _branches.Count; index++)
            {
                ProgressionBranchDefinition branch = _branches[index];
                if (branch != null && branchIds.Contains(branch.StableId))
                    result.Add(branch);
            }

            return result;
        }

        /// <summary>Returns the node's only inherited branch, or null for shared and unresolved nodes.</summary>
        public ProgressionBranchDefinition GetSingleNodeBranch(ProgressionNodeDefinition node)
        {
            IReadOnlyList<ProgressionBranchDefinition> branches = GetNodeBranches(node);
            return branches.Count == 1 ? branches[0] : null;
        }

        public bool NodeBelongsToBranch(ProgressionNodeDefinition node, string branchId)
        {
            if (node == null || string.IsNullOrWhiteSpace(branchId))
                return false;

            IReadOnlyList<ProgressionBranchDefinition> branches = GetNodeBranches(node);
            for (int index = 0; index < branches.Count; index++)
            {
                if (string.Equals(branches[index].StableId, branchId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Blends inherited branch colors for shared nodes.</summary>
        public Color GetNodeBranchColor(ProgressionNodeDefinition node, Color fallback)
        {
            IReadOnlyList<ProgressionBranchDefinition> branches = GetNodeBranches(node);
            if (branches.Count == 0)
                return fallback;

            Color color = Color.clear;
            for (int index = 0; index < branches.Count; index++)
                color += branches[index].Color;

            return color / branches.Count;
        }

        private void OnValidate()
        {
            _tierSpacing = Mathf.Max(80f, _tierSpacing);
            _nodeSpacing = Mathf.Max(60f, _nodeSpacing);
            _branchSpacing = Mathf.Max(80f, _branchSpacing);
            _branches ??= new List<ProgressionBranchDefinition>();
            _nodes ??= new List<ProgressionNodeDefinition>();

            if (_identityVersion < 1)
                _identityVersion = 1;

            EnsureUniqueInternalIds();
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "graph");

            for (int index = 0; index < _branches.Count; index++)
                _branches[index]?.Normalize(index);

            for (int index = 0; index < _nodes.Count; index++)
                _nodes[index]?.Normalize(index);

            DefinitionChanged?.Invoke(this);
        }

        private void EnsureUniqueInternalIds()
        {
            HashSet<string> branchIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < _branches.Count; index++)
            {
                ProgressionBranchDefinition branch = _branches[index];
                if (branch == null)
                    continue;

                if (string.IsNullOrWhiteSpace(branch.StableId) || !branchIds.Add(branch.StableId))
                {
                    branch.AssignStableId(ProgressionIdUtility.CreateGenerated("branch"));
                    branchIds.Add(branch.StableId);
                }
            }

            HashSet<string> nodeIds = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeDefinition node = _nodes[index];
                if (node == null)
                    continue;

                if (string.IsNullOrWhiteSpace(node.StableId) || !nodeIds.Add(node.StableId))
                {
                    node.AssignStableId(ProgressionIdUtility.CreateGenerated("node"));
                    nodeIds.Add(node.StableId);
                }
            }
        }

        private int FindBranchIndex(string branchId)
        {
            for (int index = 0; index < _branches.Count; index++)
            {
                ProgressionBranchDefinition branch = _branches[index];
                if (branch != null &&
                    string.Equals(branch.StableId, branchId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return _branches.Count;
        }

        private void ResolveNodeBranchIds(
            ProgressionNodeDefinition node,
            HashSet<string> branchIds,
            HashSet<string> visitingNodeIds)
        {
            if (node == null || !visitingNodeIds.Add(node.StableId))
                return;

            IReadOnlyList<ProgressionPrerequisiteEntryDefinition> entries = node.Prerequisites.Entries;
            if (entries.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(node.OriginBranchId) && FindBranch(node.OriginBranchId) != null)
                    branchIds.Add(node.OriginBranchId);
            }
            else
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    ProgressionPrerequisiteEntryDefinition entry = entries[index];
                    if (entry != null && TryGetNode(entry.NodeId, out ProgressionNodeDefinition prerequisite))
                        ResolveNodeBranchIds(prerequisite, branchIds, visitingNodeIds);
                }
            }

            visitingNodeIds.Remove(node.StableId);
        }

        private string GetLayoutBranchId(ProgressionNodeDefinition node)
        {
            ProgressionBranchDefinition branch = GetSingleNodeBranch(node);
            return branch?.StableId ?? string.Empty;
        }

        private int FindLaneIndex(ProgressionNodeDefinition node)
        {
            int lane = 0;
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeDefinition candidate = _nodes[index];
                if (ReferenceEquals(candidate, node))
                    return lane;

                if (candidate != null &&
                    candidate.Tier == node.Tier &&
                    string.Equals(
                        GetLayoutBranchId(candidate),
                        GetLayoutBranchId(node),
                        StringComparison.OrdinalIgnoreCase))
                {
                    lane++;
                }
            }

            return lane;
        }

        private float CalculateBranchOffset(int targetBranchIndex)
        {
            float offset = 0f;
            int branchCount = Mathf.Min(targetBranchIndex, _branches.Count);
            for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
            {
                string branchId = _branches[branchIndex]?.StableId ?? string.Empty;
                offset += CalculateBranchWidth(branchId) + _branchSpacing;
            }

            if (targetBranchIndex >= _branches.Count)
                offset += _branchSpacing;

            return offset;
        }

        private float CalculateBranchWidth(string branchId)
        {
            int maximumLanes = 1;
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeDefinition node = _nodes[index];
                if (node == null ||
                    !string.Equals(GetLayoutBranchId(node), branchId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int laneCount = 0;
                for (int candidateIndex = 0; candidateIndex < _nodes.Count; candidateIndex++)
                {
                    ProgressionNodeDefinition candidate = _nodes[candidateIndex];
                    if (candidate != null &&
                        candidate.Tier == node.Tier &&
                        string.Equals(GetLayoutBranchId(candidate), branchId, StringComparison.OrdinalIgnoreCase))
                    {
                        laneCount++;
                    }
                }

                maximumLanes = Mathf.Max(maximumLanes, laneCount);
            }

            return (maximumLanes - 1) * _nodeSpacing;
        }
    }

    [Serializable]
    public sealed class ProgressionBranchDefinition
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;
        [SerializeField] private string _displayName = "Branch";
        [SerializeField] private Color _color = new(0.32f, 0.62f, 0.92f, 1f);
        [SerializeField] private string _associatedTrackId = string.Empty;

        public string StableId => _stableId;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public string AssociatedTrackId => _associatedTrackId;

        internal void Normalize(int index)
        {
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? $"Branch {index + 1}" : _displayName.Trim();
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "branch");
            _associatedTrackId = ProgressionIdUtility.NormalizeOptional(_associatedTrackId);
        }

        internal void AssignStableId(string stableId)
        {
            _stableId = stableId;
        }
    }

    [Serializable]
    public sealed class ProgressionNodeDefinition
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;
        [SerializeField] private string _displayName = "Node";
        [SerializeField, TextArea(2, 5)] private string _description = string.Empty;
        [SerializeField] private Sprite _icon;
        [SerializeField] private string _branchId = string.Empty;
        [SerializeField, Min(1)] private int _tier = 1;
        [SerializeField] private Vector2 _graphPosition;
        [SerializeField, Min(1)] private int _maxRank = 1;
        [SerializeField, Min(0)] private int _pointCostPerRank;
        [SerializeField] private ProgressionPrerequisiteDefinition _prerequisites = new();
        [SerializeField, HideInInspector] private List<string> _prerequisiteNodeIds = new();
        [SerializeField] private List<ProgressionRequirementDefinition> _requirements = new();
        [SerializeField] private List<ProgressionCostDefinition> _costs = new();
        [SerializeField] private List<ProgressionEffectDefinition> _effects = new();

        public string StableId => _stableId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        /// <summary>Gets the authored origin branch used only when this is a root node.</summary>
        public string OriginBranchId => _branchId;
        public int Tier => _tier;
        public Vector2 GraphPosition => _graphPosition;
        public int MaxRank => _maxRank;
        public int PointCostPerRank => _pointCostPerRank;
        public ProgressionPrerequisiteDefinition Prerequisites
        {
            get
            {
                _prerequisites ??= new ProgressionPrerequisiteDefinition();
                _prerequisiteNodeIds ??= new List<string>();
                MigrateLegacyPrerequisites();
                return _prerequisites;
            }
        }
        public IReadOnlyList<ProgressionRequirementDefinition> Requirements => _requirements;
        public IReadOnlyList<ProgressionCostDefinition> Costs => _costs;
        public IReadOnlyList<ProgressionEffectDefinition> Effects => _effects;

        internal void Normalize(int index)
        {
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? $"Node {index + 1}" : _displayName.Trim();
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "node");
            _branchId = ProgressionIdUtility.NormalizeOptional(_branchId);
            _tier = Mathf.Max(1, _tier);
            _maxRank = Mathf.Max(1, _maxRank);
            _pointCostPerRank = Mathf.Max(0, _pointCostPerRank);
            _prerequisites ??= new ProgressionPrerequisiteDefinition();
            _prerequisiteNodeIds ??= new List<string>();
            _requirements ??= new List<ProgressionRequirementDefinition>();
            _costs ??= new List<ProgressionCostDefinition>();
            _effects ??= new List<ProgressionEffectDefinition>();

            MigrateLegacyPrerequisites();
            _prerequisites.Normalize();
        }

        private void MigrateLegacyPrerequisites()
        {
            if (_prerequisites.Entries.Count > 0 || _prerequisiteNodeIds.Count == 0)
                return;

            for (int index = 0; index < _prerequisiteNodeIds.Count; index++)
            {
                string nodeId = ProgressionIdUtility.NormalizeOptional(_prerequisiteNodeIds[index]);
                if (!string.IsNullOrEmpty(nodeId))
                    _prerequisites.AddMigratedEntry(nodeId);
            }

            _prerequisiteNodeIds.Clear();
        }

        internal void AssignStableId(string stableId)
        {
            _stableId = stableId;
        }

        internal void RemapInternalIds(
            IReadOnlyDictionary<string, string> branchIds,
            IReadOnlyDictionary<string, string> nodeIds)
        {
            if (!string.IsNullOrWhiteSpace(_branchId) && branchIds.TryGetValue(_branchId, out string branchId))
                _branchId = branchId;

            _prerequisites?.RemapNodeIds(nodeIds);
            _prerequisiteNodeIds ??= new List<string>();
            for (int index = 0; index < _prerequisiteNodeIds.Count; index++)
            {
                string nodeId = _prerequisiteNodeIds[index];
                if (!string.IsNullOrWhiteSpace(nodeId) && nodeIds.TryGetValue(nodeId, out string replacement))
                    _prerequisiteNodeIds[index] = replacement;
            }
        }
    }

    /// <summary>Defines the prerequisite rule for one progression node.</summary>
    [Serializable]
    public sealed class ProgressionPrerequisiteDefinition
    {
        [SerializeField] private ProgressionPrerequisiteMode _mode = ProgressionPrerequisiteMode.All;
        [SerializeField, Min(1)] private int _requiredCount = 1;
        [SerializeField] private List<ProgressionPrerequisiteEntryDefinition> _entries = new();

        public ProgressionPrerequisiteMode Mode => _mode;
        public int AuthoredRequiredCount => _requiredCount;
        public int RequiredCount => _mode switch
        {
            ProgressionPrerequisiteMode.All => Entries.Count,
            ProgressionPrerequisiteMode.Any => Entries.Count > 0 ? 1 : 0,
            _ => Mathf.Clamp(_requiredCount, 0, Entries.Count)
        };
        public IReadOnlyList<ProgressionPrerequisiteEntryDefinition> Entries =>
            _entries ??= new List<ProgressionPrerequisiteEntryDefinition>();

        internal void Normalize()
        {
            _entries ??= new List<ProgressionPrerequisiteEntryDefinition>();
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                ProgressionPrerequisiteEntryDefinition entry = _entries[index];
                if (entry == null)
                {
                    _entries.RemoveAt(index);
                    continue;
                }

                entry.Normalize();
            }

            _requiredCount = _entries.Count == 0
                ? 1
                : Mathf.Clamp(_requiredCount, 1, _entries.Count);
        }

        internal void AddMigratedEntry(string nodeId)
        {
            _entries.Add(new ProgressionPrerequisiteEntryDefinition(nodeId, 1));
        }

        internal void RemapNodeIds(IReadOnlyDictionary<string, string> nodeIds)
        {
            if (_entries == null)
                return;

            for (int index = 0; index < _entries.Count; index++)
                _entries[index]?.RemapNodeId(nodeIds);
        }
    }

    /// <summary>Requires one node to reach a specified rank.</summary>
    [Serializable]
    public sealed class ProgressionPrerequisiteEntryDefinition
    {
        [SerializeField] private string _nodeId = string.Empty;
        [SerializeField, Min(1)] private int _requiredRank = 1;

        public ProgressionPrerequisiteEntryDefinition()
        {
        }

        internal ProgressionPrerequisiteEntryDefinition(string nodeId, int requiredRank)
        {
            _nodeId = nodeId;
            _requiredRank = requiredRank;
        }

        public string NodeId => _nodeId;
        public int RequiredRank => _requiredRank;

        internal void Normalize()
        {
            _nodeId = ProgressionIdUtility.NormalizeOptional(_nodeId);
            _requiredRank = Mathf.Max(1, _requiredRank);
        }

        internal void RemapNodeId(IReadOnlyDictionary<string, string> nodeIds)
        {
            if (!string.IsNullOrWhiteSpace(_nodeId) && nodeIds.TryGetValue(_nodeId, out string replacement))
                _nodeId = replacement;
        }
    }

    internal static class ProgressionIdUtility
    {
        public static string EnsureGenerated(string value, string prefix)
        {
            return string.IsNullOrWhiteSpace(value)
                ? $"{prefix}-{Guid.NewGuid():N}"
                : NormalizeOptional(value);
        }

        public static string CreateGenerated(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid():N}";
        }

        public static string Normalize(string value, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(value) ? fallback : value;
            return NormalizeOptional(source);
        }

        public static string NormalizeOptional(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            Span<char> buffer = stackalloc char[value.Length];
            int length = 0;
            bool pendingSeparator = false;

            foreach (char character in value.Trim())
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && length > 0)
                        buffer[length++] = '-';

                    buffer[length++] = char.ToLowerInvariant(character);
                    pendingSeparator = false;
                }
                else
                {
                    pendingSeparator = true;
                }
            }

            return length > 0 ? new string(buffer[..length]) : string.Empty;
        }
    }
}
