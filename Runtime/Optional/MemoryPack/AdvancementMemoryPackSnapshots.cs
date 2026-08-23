#if LOOGA_ADVANCEMENT_MEMORYPACK_SUPPORT
using System;
using System.Collections.Generic;
using global::MemoryPack;

namespace LoogaSoft.Advancement.MemoryPack
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public sealed partial class ProgressionProgramSnapshotDto
    {
        [MemoryPackOrder(0)] public int SchemaVersion { get; set; } = ProgressionProgramSnapshot.CurrentSchemaVersion;
        [MemoryPackOrder(1)] public string ProgramId { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public string SeasonId { get; set; } = string.Empty;
        [MemoryPackOrder(3)] public int ProgramLevel { get; set; }
        [MemoryPackOrder(4)] public int EarnedPoints { get; set; }
        [MemoryPackOrder(5)] public List<ProgressionNodeSnapshotDto> Nodes { get; set; } = new();
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public sealed partial class ProgressionNodeSnapshotDto
    {
        [MemoryPackOrder(0)] public string NodeId { get; set; } = string.Empty;
        [MemoryPackOrder(1)] public int Rank { get; set; }
        [MemoryPackOrder(2)] public string ConfirmedAtUtc { get; set; } = string.Empty;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public sealed partial class ChallengeProgressSnapshotDto
    {
        [MemoryPackOrder(0)] public int SchemaVersion { get; set; } = ChallengeProgressSnapshot.CurrentSchemaVersion;
        [MemoryPackOrder(1)] public string ChallengeId { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public int CompletionCount { get; set; }
        [MemoryPackOrder(3)] public string CompletedAtUtc { get; set; } = string.Empty;
        [MemoryPackOrder(4)] public List<ChallengeObjectiveSnapshotDto> Objectives { get; set; } = new();
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public sealed partial class ChallengeObjectiveSnapshotDto
    {
        [MemoryPackOrder(0)] public int ObjectiveIndex { get; set; }
        [MemoryPackOrder(1)] public int Amount { get; set; }
    }

    /// <summary>Converts serializer-neutral Advancement snapshots to versioned MemoryPack DTOs.</summary>
    public static class AdvancementMemoryPackSnapshotConverter
    {
        public static ProgressionProgramSnapshotDto ToMemoryPack(this ProgressionProgramSnapshot source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ProgressionProgramSnapshotDto target = new()
            {
                SchemaVersion = source.schemaVersion,
                ProgramId = source.programId ?? string.Empty,
                SeasonId = source.seasonId ?? string.Empty,
                ProgramLevel = source.programLevel,
                EarnedPoints = source.earnedPoints
            };

            if (source.nodes == null)
                return target;

            for (int index = 0; index < source.nodes.Count; index++)
            {
                ProgressionNodeSnapshot node = source.nodes[index];
                if (node == null)
                    continue;

                target.Nodes.Add(new ProgressionNodeSnapshotDto
                {
                    NodeId = node.nodeId ?? string.Empty,
                    Rank = node.rank,
                    ConfirmedAtUtc = node.confirmedAtUtc ?? string.Empty
                });
            }

            return target;
        }

        public static ProgressionProgramSnapshot ToSnapshot(this ProgressionProgramSnapshotDto source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ProgressionProgramSnapshot target = new()
            {
                schemaVersion = source.SchemaVersion,
                programId = source.ProgramId ?? string.Empty,
                seasonId = source.SeasonId ?? string.Empty,
                programLevel = source.ProgramLevel,
                earnedPoints = source.EarnedPoints
            };

            if (source.Nodes == null)
                return target;

            for (int index = 0; index < source.Nodes.Count; index++)
            {
                ProgressionNodeSnapshotDto node = source.Nodes[index];
                if (node == null)
                    continue;

                target.nodes.Add(new ProgressionNodeSnapshot
                {
                    nodeId = node.NodeId ?? string.Empty,
                    rank = node.Rank,
                    confirmedAtUtc = node.ConfirmedAtUtc ?? string.Empty
                });
            }

            return target;
        }

        public static ChallengeProgressSnapshotDto ToMemoryPack(this ChallengeProgressSnapshot source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ChallengeProgressSnapshotDto target = new()
            {
                SchemaVersion = source.schemaVersion,
                ChallengeId = source.challengeId ?? string.Empty,
                CompletionCount = source.completionCount,
                CompletedAtUtc = source.completedAtUtc ?? string.Empty
            };

            if (source.objectives == null)
                return target;

            for (int index = 0; index < source.objectives.Count; index++)
            {
                ChallengeObjectiveSnapshot objective = source.objectives[index];
                if (objective == null)
                    continue;

                target.Objectives.Add(new ChallengeObjectiveSnapshotDto
                {
                    ObjectiveIndex = objective.objectiveIndex,
                    Amount = objective.amount
                });
            }

            return target;
        }

        public static ChallengeProgressSnapshot ToSnapshot(this ChallengeProgressSnapshotDto source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ChallengeProgressSnapshot target = new()
            {
                schemaVersion = source.SchemaVersion,
                challengeId = source.ChallengeId ?? string.Empty,
                completionCount = source.CompletionCount,
                completedAtUtc = source.CompletedAtUtc ?? string.Empty
            };

            if (source.Objectives == null)
                return target;

            for (int index = 0; index < source.Objectives.Count; index++)
            {
                ChallengeObjectiveSnapshotDto objective = source.Objectives[index];
                if (objective == null)
                    continue;

                target.objectives.Add(new ChallengeObjectiveSnapshot
                {
                    objectiveIndex = objective.ObjectiveIndex,
                    amount = objective.Amount
                });
            }

            return target;
        }
    }
}
#endif
