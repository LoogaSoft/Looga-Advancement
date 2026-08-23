using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    public enum ProgressionProgramChangeKind
    {
        NodeRank,
        ProgramLevel,
        EarnedPoints
    }

    /// <summary>Describes one confirmed change to a progression program.</summary>
    public readonly struct ProgressionProgramChange
    {
        public ProgressionProgramChange(
            ProgressionProgramChangeKind kind,
            int previousValue,
            int currentValue,
            string nodeId = "")
        {
            Kind = kind;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            NodeId = nodeId ?? string.Empty;
        }

        public ProgressionProgramChangeKind Kind { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public string NodeId { get; }
    }

    /// <summary>Stores account-wide progression that must not follow a save profile.</summary>
    [Serializable]
    public sealed class AccountCareerProgressionState
    {
        [SerializeField] private int _revision;
        [SerializeField] private ProgressionLevelState _accountLevel = new();
        [SerializeField] private List<ProgressionProgramState> _programs = new();
        [SerializeField] private List<string> _unlockedRewardIds = new();

        public int Revision => _revision;
        public ProgressionLevelState AccountLevel => _accountLevel;
        public IReadOnlyList<ProgressionProgramState> Programs => _programs;
        public IReadOnlyList<string> UnlockedRewardIds => _unlockedRewardIds;

        public AccountCareerProgressionSnapshot CreateSnapshot()
        {
            AccountCareerProgressionSnapshot snapshot = new()
            {
                revision = _revision,
                accountLevel = _accountLevel?.Level ?? 1,
                accountExperience = _accountLevel?.Progress ?? 0,
                programs = ProgressionSnapshotUtility.CreateProgramSnapshots(_programs),
                unlockedRewardIds = _unlockedRewardIds != null
                    ? new List<string>(_unlockedRewardIds)
                    : new List<string>()
            };
            return snapshot;
        }

        public void LoadSnapshot(AccountCareerProgressionSnapshot snapshot)
        {
            snapshot ??= new AccountCareerProgressionSnapshot();
            _revision = Mathf.Max(0, snapshot.revision);
            _accountLevel ??= new ProgressionLevelState();
            _accountLevel.ApplyConfirmedProgress(snapshot.accountLevel, snapshot.accountExperience);
            _programs = ProgressionSnapshotUtility.CreateProgramStates(snapshot.programs);
            _unlockedRewardIds = snapshot.unlockedRewardIds != null
                ? new List<string>(snapshot.unlockedRewardIds)
                : new List<string>();
        }

        public ProgressionProgramState GetOrCreateProgram(string programId, int startingLevel = 1)
        {
            _programs ??= new List<ProgressionProgramState>();
            for (int index = 0; index < _programs.Count; index++)
            {
                ProgressionProgramState state = _programs[index];
                if (state != null && string.Equals(state.ProgramId, programId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            ProgressionProgramState created = new(programId, string.Empty, startingLevel);
            _programs.Add(created);
            return created;
        }

        /// <summary>Applies a revision that an authoritative persistence service confirmed.</summary>
        public void ApplyConfirmedRevision(int revision)
        {
            _revision = Mathf.Max(_revision, revision);
        }
    }

    /// <summary>Stores progression that belongs to one seasonal save profile.</summary>
    [Serializable]
    public sealed class SeasonalProfileProgressionState
    {
        [SerializeField] private string _seasonId = string.Empty;
        [SerializeField] private int _revision;
        [SerializeField] private List<ProgressionProgramState> _programs = new();

        public string SeasonId => _seasonId;
        public int Revision => _revision;
        public IReadOnlyList<ProgressionProgramState> Programs => _programs;

        public SeasonalProfileProgressionSnapshot CreateSnapshot()
        {
            return new SeasonalProfileProgressionSnapshot
            {
                seasonId = _seasonId,
                revision = _revision,
                programs = ProgressionSnapshotUtility.CreateProgramSnapshots(_programs)
            };
        }

        public void LoadSnapshot(SeasonalProfileProgressionSnapshot snapshot)
        {
            snapshot ??= new SeasonalProfileProgressionSnapshot();
            _seasonId = snapshot.seasonId ?? string.Empty;
            _revision = Mathf.Max(0, snapshot.revision);
            _programs = ProgressionSnapshotUtility.CreateProgramStates(snapshot.programs);
        }

        public void BeginSeason(string seasonId)
        {
            string normalizedSeasonId = seasonId?.Trim() ?? string.Empty;
            if (string.Equals(_seasonId, normalizedSeasonId, StringComparison.Ordinal))
                return;

            _seasonId = normalizedSeasonId;
            _revision = 0;
            _programs.Clear();
        }

        public ProgressionProgramState GetOrCreateProgram(string programId, int startingLevel = 1)
        {
            _programs ??= new List<ProgressionProgramState>();
            for (int index = 0; index < _programs.Count; index++)
            {
                ProgressionProgramState state = _programs[index];
                if (state != null && string.Equals(state.ProgramId, programId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            ProgressionProgramState created = new(programId, _seasonId, startingLevel);
            _programs.Add(created);
            return created;
        }

        /// <summary>Applies a revision that an authoritative persistence service confirmed.</summary>
        public void ApplyConfirmedRevision(int revision)
        {
            _revision = Mathf.Max(_revision, revision);
        }
    }

    [Serializable]
    public sealed class ProgressionLevelState
    {
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _progress;

        public int Level => _level;
        public int Progress => _progress;

        /// <summary>Applies account progress that an authoritative service confirmed.</summary>
        public void ApplyConfirmedProgress(int level, int progress)
        {
            _level = Mathf.Max(1, level);
            _progress = Mathf.Max(0, progress);
        }
    }

    [Serializable]
    public sealed class ProgressionProgramState
    {
        [SerializeField] private string _programId = string.Empty;
        [SerializeField] private string _seasonId = string.Empty;
        [SerializeField, Min(1)] private int _programLevel = 1;
        [SerializeField, Min(0)] private int _earnedPoints;
        [SerializeField] private List<ProgressionNodeState> _nodes = new();

        public ProgressionProgramState(string programId, string seasonId, int startingLevel)
        {
            _programId = programId ?? string.Empty;
            _seasonId = seasonId ?? string.Empty;
            _programLevel = Mathf.Max(1, startingLevel);
        }

        public string ProgramId => _programId;
        public string SeasonId => _seasonId;
        public int ProgramLevel => _programLevel;
        public int EarnedPoints => _earnedPoints;
        public IReadOnlyList<ProgressionNodeState> Nodes => _nodes;

        public event Action<ProgressionProgramChange> Changed;

        public int GetNodeRank(string nodeId)
        {
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeState node = _nodes[index];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                    return node.Rank;
            }

            return 0;
        }

        public int GetSpentPoints(ProgressionGraphDefinition graph)
        {
            if (graph == null)
                return 0;

            int spent = 0;
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeState state = _nodes[index];
                if (state != null && graph.TryGetNode(state.NodeId, out ProgressionNodeDefinition definition))
                    spent += definition.PointCostPerRank * state.Rank;
            }

            return Mathf.Max(0, spent);
        }

        /// <summary>Applies a node rank that an authoritative service confirmed.</summary>
        public void ApplyConfirmedNodeRank(string nodeId, int rank, string confirmedAtUtc)
        {
            for (int index = 0; index < _nodes.Count; index++)
            {
                ProgressionNodeState node = _nodes[index];
                if (node == null || !string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                int previousRank = node.Rank;
                if (!node.SetConfirmedRank(rank, confirmedAtUtc))
                    return;

                if (node.Rank != previousRank)
                {
                    Changed?.Invoke(new ProgressionProgramChange(
                        ProgressionProgramChangeKind.NodeRank,
                        previousRank,
                        node.Rank,
                        node.NodeId));
                }
                return;
            }

            ProgressionNodeState created = new(nodeId, rank, confirmedAtUtc);
            _nodes.Add(created);
            Changed?.Invoke(new ProgressionProgramChange(
                ProgressionProgramChangeKind.NodeRank,
                0,
                created.Rank,
                created.NodeId));
        }

        /// <summary>Applies a program level that an authoritative service confirmed.</summary>
        public void ApplyConfirmedLevel(int level)
        {
            int previousLevel = _programLevel;
            _programLevel = Mathf.Max(_programLevel, level);
            if (_programLevel != previousLevel)
            {
                Changed?.Invoke(new ProgressionProgramChange(
                    ProgressionProgramChangeKind.ProgramLevel,
                    previousLevel,
                    _programLevel));
            }
        }

        /// <summary>Applies an earned-point total that an authoritative service confirmed.</summary>
        public void ApplyConfirmedPoints(int earnedPoints)
        {
            int previousPoints = _earnedPoints;
            _earnedPoints = Mathf.Max(_earnedPoints, earnedPoints);
            if (_earnedPoints != previousPoints)
            {
                Changed?.Invoke(new ProgressionProgramChange(
                    ProgressionProgramChangeKind.EarnedPoints,
                    previousPoints,
                    _earnedPoints));
            }
        }
    }

    [Serializable]
    public sealed class ProgressionNodeState
    {
        [SerializeField] private string _nodeId = string.Empty;
        [SerializeField, Min(0)] private int _rank;
        [SerializeField] private string _confirmedAtUtc = string.Empty;

        public ProgressionNodeState(string nodeId, int rank, string confirmedAtUtc)
        {
            _nodeId = nodeId ?? string.Empty;
            _rank = Mathf.Max(0, rank);
            _confirmedAtUtc = confirmedAtUtc ?? string.Empty;
        }

        public string NodeId => _nodeId;
        public int Rank => _rank;
        public string ConfirmedAtUtc => _confirmedAtUtc;

        internal bool SetConfirmedRank(int rank, string confirmedAtUtc)
        {
            int confirmedRank = Mathf.Max(_rank, rank);
            string timestamp = confirmedAtUtc ?? string.Empty;
            if (_rank == confirmedRank && string.Equals(_confirmedAtUtc, timestamp, StringComparison.Ordinal))
                return false;

            _rank = confirmedRank;
            _confirmedAtUtc = timestamp;
            return true;
        }
    }

    internal static class ProgressionSnapshotUtility
    {
        public static List<ProgressionProgramSnapshot> CreateProgramSnapshots(
            IReadOnlyList<ProgressionProgramState> states)
        {
            List<ProgressionProgramSnapshot> snapshots = new();
            if (states == null)
                return snapshots;

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                ProgressionProgramState state = states[stateIndex];
                if (state == null)
                    continue;

                ProgressionProgramSnapshot snapshot = new()
                {
                    programId = state.ProgramId,
                    seasonId = state.SeasonId,
                    programLevel = state.ProgramLevel,
                    earnedPoints = state.EarnedPoints
                };
                for (int nodeIndex = 0; nodeIndex < state.Nodes.Count; nodeIndex++)
                {
                    ProgressionNodeState node = state.Nodes[nodeIndex];
                    if (node == null)
                        continue;

                    snapshot.nodes.Add(new ProgressionNodeSnapshot
                    {
                        nodeId = node.NodeId,
                        rank = node.Rank,
                        confirmedAtUtc = node.ConfirmedAtUtc
                    });
                }

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        public static List<ProgressionProgramState> CreateProgramStates(
            IReadOnlyList<ProgressionProgramSnapshot> snapshots)
        {
            List<ProgressionProgramState> states = new();
            if (snapshots == null)
                return states;

            for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            {
                ProgressionProgramSnapshot snapshot = snapshots[snapshotIndex];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.programId))
                    continue;

                ProgressionProgramState state = new(
                    snapshot.programId,
                    snapshot.seasonId,
                    snapshot.programLevel);
                state.ApplyConfirmedPoints(snapshot.earnedPoints);
                if (snapshot.nodes != null)
                {
                    for (int nodeIndex = 0; nodeIndex < snapshot.nodes.Count; nodeIndex++)
                    {
                        ProgressionNodeSnapshot node = snapshot.nodes[nodeIndex];
                        if (node != null && !string.IsNullOrWhiteSpace(node.nodeId))
                        {
                            state.ApplyConfirmedNodeRank(node.nodeId, node.rank, node.confirmedAtUtc);
                        }
                    }
                }

                states.Add(state);
            }

            return states;
        }
    }
}
