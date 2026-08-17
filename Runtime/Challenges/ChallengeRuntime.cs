using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    /// <summary>Reports one game event to the challenge evaluator.</summary>
    public readonly struct ChallengeSignal
    {
        public ChallengeSignal(ChallengeMetricDefinition metric, int amount = 1, string matchKey = "")
        {
            Metric = metric;
            Amount = amount;
            MatchKey = matchKey ?? string.Empty;
        }

        public ChallengeMetricDefinition Metric { get; }
        public int Amount { get; }
        public string MatchKey { get; }
    }

    [Serializable]
    public sealed class ChallengeObjectiveState
    {
        [SerializeField] private int _objectiveIndex;
        [SerializeField] private int _amount;

        public ChallengeObjectiveState(int objectiveIndex)
        {
            _objectiveIndex = objectiveIndex;
        }

        public int ObjectiveIndex => _objectiveIndex;
        public int Amount => _amount;

        internal void Apply(int amount, ChallengeProgressAggregation aggregation)
        {
            _amount = aggregation switch
            {
                ChallengeProgressAggregation.Add => Math.Max(0, _amount + amount),
                ChallengeProgressAggregation.Highest => Math.Max(_amount, amount),
                ChallengeProgressAggregation.Latest => Math.Max(0, amount),
                _ => _amount
            };
        }
    }

    [Serializable]
    public sealed class ChallengeProgressState
    {
        [SerializeField] private string _challengeId = string.Empty;
        [SerializeField] private int _completionCount;
        [SerializeField] private string _completedAtUtc = string.Empty;
        [SerializeField] private List<ChallengeObjectiveState> _objectives = new();

        public ChallengeProgressState(string challengeId)
        {
            _challengeId = challengeId ?? string.Empty;
        }

        public string ChallengeId => _challengeId;
        public int CompletionCount => _completionCount;
        public string CompletedAtUtc => _completedAtUtc;
        public IReadOnlyList<ChallengeObjectiveState> Objectives => _objectives;

        public int GetAmount(int objectiveIndex)
        {
            ChallengeObjectiveState state = GetOrCreateObjective(objectiveIndex);
            return state.Amount;
        }

        internal ChallengeObjectiveState GetOrCreateObjective(int objectiveIndex)
        {
            for (int index = 0; index < _objectives.Count; index++)
            {
                ChallengeObjectiveState state = _objectives[index];
                if (state != null && state.ObjectiveIndex == objectiveIndex)
                    return state;
            }

            ChallengeObjectiveState created = new(objectiveIndex);
            _objectives.Add(created);
            return created;
        }

        internal void MarkCompleted(DateTime utcNow)
        {
            _completionCount++;
            _completedAtUtc = utcNow.ToUniversalTime().ToString("O");
        }
    }

    public readonly struct ChallengeEvaluationResult
    {
        public ChallengeEvaluationResult(bool changed, bool completedNow)
        {
            Changed = changed;
            CompletedNow = completedNow;
        }

        public bool Changed { get; }
        public bool CompletedNow { get; }
    }

    /// <summary>Updates challenge progress without granting game-specific rewards.</summary>
    public static class ChallengeEvaluator
    {
        public static ChallengeEvaluationResult Apply(
            ChallengeDefinition definition,
            ChallengeProgressState state,
            in ChallengeSignal signal,
            DateTime utcNow)
        {
            if (definition == null || state == null || signal.Metric == null)
                return default;

            if (definition.RepeatMode == ChallengeRepeatMode.Once && state.CompletionCount > 0)
                return default;

            bool changed = false;
            for (int index = 0; index < definition.Objectives.Count; index++)
            {
                ChallengeObjectiveDefinition objective = definition.Objectives[index];
                if (!Matches(objective, signal))
                    continue;

                ChallengeObjectiveState objectiveState = state.GetOrCreateObjective(index);
                int previousAmount = objectiveState.Amount;
                objectiveState.Apply(signal.Amount, objective.Aggregation);
                changed |= previousAmount != objectiveState.Amount;
            }

            bool completed = changed && IsComplete(definition, state);
            if (completed)
                state.MarkCompleted(utcNow);

            return new ChallengeEvaluationResult(changed, completed);
        }

        public static bool IsComplete(ChallengeDefinition definition, ChallengeProgressState state)
        {
            if (definition == null || state == null)
                return false;

            int completedObjectives = 0;
            for (int index = 0; index < definition.Objectives.Count; index++)
            {
                ChallengeObjectiveDefinition objective = definition.Objectives[index];
                if (objective != null && state.GetAmount(index) >= objective.TargetAmount)
                    completedObjectives++;
            }

            return completedObjectives >= definition.RequiredObjectiveCount;
        }

        private static bool Matches(ChallengeObjectiveDefinition objective, in ChallengeSignal signal)
        {
            if (objective?.Metric != signal.Metric)
                return false;

            return string.IsNullOrWhiteSpace(objective.MatchKey) ||
                   string.Equals(objective.MatchKey, signal.MatchKey, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Allows a game adapter to grant package-neutral reward definitions.</summary>
    public interface IChallengeRewardHandler
    {
        bool CanGrant(ChallengeRewardDefinition reward);
        void Grant(ChallengeRewardDefinition reward, ChallengeDefinition source);
    }
}
