using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    public enum ChallengeProgressChangeKind
    {
        ObjectiveProgress,
        Completed,
        SnapshotLoaded
    }

    /// <summary>Describes one confirmed challenge progress change.</summary>
    public readonly struct ChallengeProgressChange
    {
        public ChallengeProgressChange(
            ChallengeProgressChangeKind kind,
            int objectiveIndex = -1,
            int previousAmount = 0,
            int currentAmount = 0)
        {
            Kind = kind;
            ObjectiveIndex = objectiveIndex;
            PreviousAmount = previousAmount;
            CurrentAmount = currentAmount;
        }

        public ChallengeProgressChangeKind Kind { get; }
        public int ObjectiveIndex { get; }
        public int PreviousAmount { get; }
        public int CurrentAmount { get; }
    }

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

        internal bool Apply(int amount, ChallengeProgressAggregation aggregation)
        {
            int nextAmount = aggregation switch
            {
                ChallengeProgressAggregation.Add => Math.Max(0, _amount + amount),
                ChallengeProgressAggregation.Highest => Math.Max(_amount, amount),
                ChallengeProgressAggregation.Latest => Math.Max(0, amount),
                _ => _amount
            };

            if (nextAmount == _amount)
                return false;

            _amount = nextAmount;
            return true;
        }

        internal void LoadAmount(int amount)
        {
            _amount = Math.Max(0, amount);
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

        public event Action<ChallengeProgressChange> Changed;

        public ChallengeProgressSnapshot CreateSnapshot()
        {
            ChallengeProgressSnapshot snapshot = new()
            {
                challengeId = _challengeId,
                completionCount = _completionCount,
                completedAtUtc = _completedAtUtc
            };

            for (int index = 0; index < _objectives.Count; index++)
            {
                ChallengeObjectiveState objective = _objectives[index];
                if (objective == null)
                    continue;

                snapshot.objectives.Add(new ChallengeObjectiveSnapshot
                {
                    objectiveIndex = objective.ObjectiveIndex,
                    amount = objective.Amount
                });
            }

            return snapshot;
        }

        public void LoadSnapshot(ChallengeProgressSnapshot snapshot)
        {
            snapshot ??= new ChallengeProgressSnapshot();
            _challengeId = snapshot.challengeId ?? string.Empty;
            _completionCount = Math.Max(0, snapshot.completionCount);
            _completedAtUtc = snapshot.completedAtUtc ?? string.Empty;
            _objectives = new List<ChallengeObjectiveState>();

            if (snapshot.objectives != null)
            {
                for (int index = 0; index < snapshot.objectives.Count; index++)
                {
                    ChallengeObjectiveSnapshot objective = snapshot.objectives[index];
                    if (objective == null)
                        continue;

                    ChallengeObjectiveState state = new(objective.objectiveIndex);
                    state.LoadAmount(objective.amount);
                    _objectives.Add(state);
                }
            }

            Changed?.Invoke(new ChallengeProgressChange(ChallengeProgressChangeKind.SnapshotLoaded));
        }

        public int GetAmount(int objectiveIndex)
        {
            for (int index = 0; index < _objectives.Count; index++)
            {
                ChallengeObjectiveState state = _objectives[index];
                if (state != null && state.ObjectiveIndex == objectiveIndex)
                    return state.Amount;
            }

            return 0;
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
            Changed?.Invoke(new ChallengeProgressChange(ChallengeProgressChangeKind.Completed));
        }

        internal void NotifyObjectiveChanged(int objectiveIndex, int previousAmount, int currentAmount)
        {
            Changed?.Invoke(new ChallengeProgressChange(
                ChallengeProgressChangeKind.ObjectiveProgress,
                objectiveIndex,
                previousAmount,
                currentAmount));
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
                if (!objectiveState.Apply(signal.Amount, objective.Aggregation))
                    continue;

                changed = true;
                state.NotifyObjectiveChanged(index, previousAmount, objectiveState.Amount);
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
