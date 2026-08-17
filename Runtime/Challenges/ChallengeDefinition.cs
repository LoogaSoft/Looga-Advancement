using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    public enum ChallengeObjectiveMode
    {
        All,
        Any,
        AtLeast
    }

    public enum ChallengeProgressAggregation
    {
        Add,
        Highest,
        Latest
    }

    public enum ChallengeRepeatMode
    {
        Once,
        Repeatable,
        Daily,
        Weekly,
        Seasonal
    }

    /// <summary>Identifies one measurable event without coupling it to a game event bus.</summary>
    [CreateAssetMenu(fileName = "Challenge Metric", menuName = "LoogaSoft/Advancement/Challenges/Metric")]
    public sealed class ChallengeMetricDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;

        public string StableId => _stableId;

        private void OnValidate()
        {
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "challenge-metric");
        }
    }

    /// <summary>Defines a reward payload that a game adapter can grant.</summary>
    public abstract class ChallengeRewardDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;

        public string StableId => _stableId;

        protected virtual void OnValidate()
        {
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "challenge-reward");
        }
    }

    [System.Serializable]
    public sealed class ChallengeObjectiveDefinition
    {
        [SerializeField] private ChallengeMetricDefinition _metric;
        [SerializeField] private string _matchKey = string.Empty;
        [SerializeField, Min(1)] private int _targetAmount = 1;
        [SerializeField] private ChallengeProgressAggregation _aggregation;

        public ChallengeMetricDefinition Metric => _metric;
        public string MatchKey => _matchKey;
        public int TargetAmount => _targetAmount;
        public ChallengeProgressAggregation Aggregation => _aggregation;
    }

    /// <summary>Defines objectives and rewards for one challenge.</summary>
    [CreateAssetMenu(fileName = "Challenge", menuName = "LoogaSoft/Advancement/Challenges/Challenge")]
    public sealed class ChallengeDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;
        [SerializeField, TextArea(2, 6)] private string _description = string.Empty;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ChallengeRepeatMode _repeatMode;
        [SerializeField] private ChallengeObjectiveMode _objectiveMode;
        [SerializeField, Min(1)] private int _requiredObjectiveCount = 1;
        [SerializeField] private List<ChallengeObjectiveDefinition> _objectives = new();
        [SerializeField] private List<ChallengeRewardDefinition> _rewards = new();

        public string StableId => _stableId;
        public string DisplayName => name;
        public string Description => _description;
        public Sprite Icon => _icon;
        public ChallengeRepeatMode RepeatMode => _repeatMode;
        public ChallengeObjectiveMode ObjectiveMode => _objectiveMode;
        public IReadOnlyList<ChallengeObjectiveDefinition> Objectives => _objectives;
        public IReadOnlyList<ChallengeRewardDefinition> Rewards => _rewards;
        public int RequiredObjectiveCount => _objectiveMode switch
        {
            ChallengeObjectiveMode.All => _objectives.Count,
            ChallengeObjectiveMode.Any => _objectives.Count > 0 ? 1 : 0,
            _ => Mathf.Clamp(_requiredObjectiveCount, 1, Mathf.Max(1, _objectives.Count))
        };

        private void OnValidate()
        {
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "challenge");
            _objectives ??= new List<ChallengeObjectiveDefinition>();
            _rewards ??= new List<ChallengeRewardDefinition>();
            _requiredObjectiveCount = Mathf.Clamp(
                _requiredObjectiveCount,
                1,
                Mathf.Max(1, _objectives.Count));
        }
    }

    [CreateAssetMenu(fileName = "Challenge Catalog", menuName = "LoogaSoft/Advancement/Challenges/Catalog")]
    public sealed class ChallengeCatalog : ScriptableObject
    {
        [SerializeField] private List<ChallengeDefinition> _challenges = new();

        public IReadOnlyList<ChallengeDefinition> Challenges => _challenges;

        private void OnValidate()
        {
            _challenges ??= new List<ChallengeDefinition>();
        }
    }
}
