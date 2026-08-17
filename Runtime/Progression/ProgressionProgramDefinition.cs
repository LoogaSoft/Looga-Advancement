using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    public enum ProgressionProgramKind
    {
        AccountLevel,
        Specialization,
        Facility,
        Custom
    }

    public enum ProgressionPersistenceScope
    {
        GlobalAccount,
        PersistentProfile,
        SeasonalProfile
    }

    public enum ProgressionLevelMode
    {
        None,
        Experience,
        PurchasedSteps
    }

    /// <summary>Defines the policy, schedule, and graph for one progression program.</summary>
    [CreateAssetMenu(fileName = "Progression Program", menuName = "LoogaSoft/Advancement/Program")]
    public sealed class ProgressionProgramDefinition : ScriptableObject
    {
        [SerializeField] private string _stableId = string.Empty;
        [SerializeField] private ProgressionProgramKind _kind;
        [SerializeField] private ProgressionPersistenceScope _persistenceScope;
        [SerializeField] private ProgressionGraphDefinition _graph;
        [SerializeField] private ProgressionPointPolicy _pointPolicy = new();
        [SerializeField] private ProgressionSeasonPolicy _seasonPolicy = new();
        [SerializeField] private ProgressionLevelTrackDefinition _levelTrack = new();

        public string StableId => _stableId;
        public ProgressionProgramKind Kind => _kind;
        public ProgressionPersistenceScope PersistenceScope => _persistenceScope;
        public ProgressionGraphDefinition Graph => _graph;
        public ProgressionPointPolicy PointPolicy => _pointPolicy;
        public ProgressionSeasonPolicy SeasonPolicy => _seasonPolicy;
        public ProgressionLevelTrackDefinition LevelTrack => _levelTrack;

        private void OnValidate()
        {
            _stableId = ProgressionIdUtility.Normalize(_stableId, name);
            _pointPolicy ??= new ProgressionPointPolicy();
            _seasonPolicy ??= new ProgressionSeasonPolicy();
            _levelTrack ??= new ProgressionLevelTrackDefinition();
            _pointPolicy.Normalize();
            _seasonPolicy.Normalize();
            _levelTrack.Normalize();
        }
    }

    [Serializable]
    public sealed class ProgressionPointPolicy
    {
        [SerializeField] private bool _usesPoints;
        [SerializeField, Min(0)] private int _maximumEarnedPoints;
        [SerializeField, Min(0)] private int _startingPoints;
        [SerializeField] private List<ProgressionPointAwardDefinition> _awards = new();

        public bool UsesPoints => _usesPoints;
        public int MaximumEarnedPoints => _maximumEarnedPoints;
        public int StartingPoints => _startingPoints;
        public IReadOnlyList<ProgressionPointAwardDefinition> Awards => _awards;

        internal void Normalize()
        {
            _maximumEarnedPoints = Mathf.Max(0, _maximumEarnedPoints);
            _startingPoints = Mathf.Clamp(_startingPoints, 0, _maximumEarnedPoints);
            _awards ??= new List<ProgressionPointAwardDefinition>();
        }
    }

    [Serializable]
    public sealed class ProgressionPointAwardDefinition
    {
        [SerializeField] private ProgressionRequirementKind _source = ProgressionRequirementKind.AccountLevel;
        [SerializeField] private string _sourceId = string.Empty;
        [SerializeField, Min(1)] private int _requiredValue = 1;
        [SerializeField, Min(1)] private int _points = 1;

        public ProgressionRequirementKind Source => _source;
        public string SourceId => _sourceId;
        public int RequiredValue => _requiredValue;
        public int Points => _points;
    }

    [Serializable]
    public sealed class ProgressionSeasonPolicy
    {
        [SerializeField] private bool _usesTierSchedule;
        [SerializeField] private List<ProgressionTierReleaseDefinition> _tierReleases = new();

        public bool UsesTierSchedule => _usesTierSchedule;
        public IReadOnlyList<ProgressionTierReleaseDefinition> TierReleases => _tierReleases;

        public int GetAvailableTier(
            DateTime utcNow,
            DateTime seasonStartUtc,
            Func<string, int> resolveOverride = null)
        {
            if (!_usesTierSchedule || _tierReleases.Count == 0)
                return int.MaxValue;

            double elapsedDays = Math.Max(0d, (utcNow - seasonStartUtc).TotalDays);
            int availableTier = 0;
            for (int index = 0; index < _tierReleases.Count; index++)
            {
                ProgressionTierReleaseDefinition release = _tierReleases[index];
                if (release == null)
                    continue;

                bool releasedBySchedule = elapsedDays >= release.ReleaseAfterDays;
                bool releasedByOverride = !string.IsNullOrWhiteSpace(release.LiveOverrideKey) &&
                                          resolveOverride?.Invoke(release.LiveOverrideKey) >= release.Tier;
                if (releasedBySchedule || releasedByOverride)
                    availableTier = Math.Max(availableTier, release.Tier);
            }

            return availableTier;
        }

        internal void Normalize()
        {
            _tierReleases ??= new List<ProgressionTierReleaseDefinition>();
            _tierReleases.Sort((left, right) =>
                (left?.ReleaseAfterDays ?? int.MaxValue).CompareTo(right?.ReleaseAfterDays ?? int.MaxValue));
        }
    }

    [Serializable]
    public sealed class ProgressionTierReleaseDefinition
    {
        [SerializeField, Min(1)] private int _tier = 1;
        [SerializeField, Min(0)] private int _releaseAfterDays;
        [SerializeField] private string _liveOverrideKey = string.Empty;

        public int Tier => _tier;
        public int ReleaseAfterDays => _releaseAfterDays;
        public string LiveOverrideKey => _liveOverrideKey;
    }

    [Serializable]
    public sealed class ProgressionLevelTrackDefinition
    {
        [SerializeField] private ProgressionLevelMode _mode;
        [SerializeField, Min(1)] private int _startingLevel = 1;
        [SerializeField, Min(1)] private int _baseExperience = 1000;
        [SerializeField, Min(0)] private int _experienceGrowth = 250;
        [SerializeField] private List<ProgressionLevelStepDefinition> _steps = new();

        public ProgressionLevelMode Mode => _mode;
        public bool Enabled => _mode != ProgressionLevelMode.None;
        public int StartingLevel => _startingLevel;
        public int BaseExperience => _baseExperience;
        public int ExperienceGrowth => _experienceGrowth;
        public IReadOnlyList<ProgressionLevelStepDefinition> Steps => _steps;

        public int GetExperienceForNextLevel(int currentLevel)
        {
            int levelOffset = Mathf.Max(0, currentLevel - _startingLevel);
            return Mathf.Max(1, _baseExperience + _experienceGrowth * levelOffset);
        }

        public ProgressionLevelStepDefinition FindStep(int targetLevel)
        {
            for (int index = 0; index < _steps.Count; index++)
            {
                ProgressionLevelStepDefinition step = _steps[index];
                if (step != null && step.TargetLevel == targetLevel)
                    return step;
            }

            return null;
        }

        internal void Normalize()
        {
            _startingLevel = Mathf.Max(1, _startingLevel);
            _baseExperience = Mathf.Max(1, _baseExperience);
            _experienceGrowth = Mathf.Max(0, _experienceGrowth);
            _steps ??= new List<ProgressionLevelStepDefinition>();
            _steps.Sort((left, right) =>
                (left?.TargetLevel ?? int.MaxValue).CompareTo(right?.TargetLevel ?? int.MaxValue));
        }
    }

    [Serializable]
    public sealed class ProgressionLevelStepDefinition
    {
        [SerializeField, Min(2)] private int _targetLevel = 2;
        [SerializeField] private List<ProgressionRequirementDefinition> _requirements = new();
        [SerializeField] private List<ProgressionCostDefinition> _costs = new();
        [SerializeField] private List<ProgressionEffectDefinition> _effects = new();

        public int TargetLevel => _targetLevel;
        public IReadOnlyList<ProgressionRequirementDefinition> Requirements => _requirements;
        public IReadOnlyList<ProgressionCostDefinition> Costs => _costs;
        public IReadOnlyList<ProgressionEffectDefinition> Effects => _effects;
    }
}
