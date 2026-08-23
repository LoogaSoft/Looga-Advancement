using System;
using System.Collections.Generic;

namespace LoogaSoft.Advancement
{
    /// <summary>Stores persistent progression without selecting a serialization technology.</summary>
    [Serializable]
    public sealed class AccountCareerProgressionSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int revision;
        public int accountLevel = 1;
        public int accountExperience;
        public List<ProgressionProgramSnapshot> programs = new();
        public List<string> unlockedRewardIds = new();
    }

    /// <summary>Stores progression that can reset for a season or profile.</summary>
    [Serializable]
    public sealed class SeasonalProfileProgressionSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string seasonId = string.Empty;
        public int revision;
        public List<ProgressionProgramSnapshot> programs = new();
    }

    [Serializable]
    public sealed class ProgressionProgramSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string programId = string.Empty;
        public string seasonId = string.Empty;
        public int programLevel = 1;
        public int earnedPoints;
        public List<ProgressionNodeSnapshot> nodes = new();
    }

    [Serializable]
    public sealed class ProgressionNodeSnapshot
    {
        public string nodeId = string.Empty;
        public int rank;
        public string confirmedAtUtc = string.Empty;
    }

    /// <summary>Stores challenge progress without selecting a serialization technology.</summary>
    [Serializable]
    public sealed class ChallengeProgressSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string challengeId = string.Empty;
        public int completionCount;
        public string completedAtUtc = string.Empty;
        public List<ChallengeObjectiveSnapshot> objectives = new();
    }

    [Serializable]
    public sealed class ChallengeObjectiveSnapshot
    {
        public int objectiveIndex;
        public int amount;
    }
}
