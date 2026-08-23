#if LOOGA_ADVANCEMENT_R3_SUPPORT
using System;
using global::R3;

namespace LoogaSoft.Advancement.R3
{
    /// <summary>Exposes confirmed Advancement state changes as R3 observables.</summary>
    public static class LoogaAdvancementR3Extensions
    {
        public static Observable<ProgressionProgramChange> ChangesAsObservable(
            this ProgressionProgramState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.FromEvent<Action<ProgressionProgramChange>, ProgressionProgramChange>(
                handler => new Action<ProgressionProgramChange>(handler),
                handler => state.Changed += handler,
                handler => state.Changed -= handler);
        }

        public static Observable<int> NodeRankAsObservable(
            this ProgressionProgramState state,
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Provide a node ID.", nameof(nodeId));

            return Observable.Create<int, (ProgressionProgramState State, string NodeId)>(
                (state, nodeId),
                static (observer, context) =>
                {
                    observer.OnNext(context.State.GetNodeRank(context.NodeId));
                    return context.State.ChangesAsObservable()
                        .Where(change => change.Kind == ProgressionProgramChangeKind.NodeRank &&
                                         string.Equals(
                                             change.NodeId,
                                             context.NodeId,
                                             StringComparison.OrdinalIgnoreCase))
                        .Select(change => change.CurrentValue)
                        .Subscribe(observer);
                })
                .DistinctUntilChanged();
        }

        public static Observable<int> ProgramLevelAsObservable(this ProgressionProgramState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.Create<int, ProgressionProgramState>(state, static (observer, value) =>
            {
                observer.OnNext(value.ProgramLevel);
                return value.ChangesAsObservable()
                    .Where(change => change.Kind == ProgressionProgramChangeKind.ProgramLevel)
                    .Select(change => change.CurrentValue)
                    .Subscribe(observer);
            }).DistinctUntilChanged();
        }

        public static Observable<int> EarnedPointsAsObservable(this ProgressionProgramState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.Create<int, ProgressionProgramState>(state, static (observer, value) =>
            {
                observer.OnNext(value.EarnedPoints);
                return value.ChangesAsObservable()
                    .Where(change => change.Kind == ProgressionProgramChangeKind.EarnedPoints)
                    .Select(change => change.CurrentValue)
                    .Subscribe(observer);
            }).DistinctUntilChanged();
        }

        public static Observable<ProgressionNodeAvailability> NodeAvailabilityAsObservable(
            this ProgressionProgramState state,
            Func<ProgressionNodeAvailability> evaluate)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (evaluate == null)
                throw new ArgumentNullException(nameof(evaluate));

            return Observable.Create<
                    ProgressionNodeAvailability,
                    (ProgressionProgramState State, Func<ProgressionNodeAvailability> Evaluate)>(
                    (state, evaluate),
                    static (observer, context) =>
                    {
                        observer.OnNext(context.Evaluate());
                        return context.State.ChangesAsObservable()
                            .Select(_ => context.Evaluate())
                            .Subscribe(observer);
                    })
                .DistinctUntilChanged();
        }

        public static Observable<ChallengeProgressChange> ChangesAsObservable(
            this ChallengeProgressState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.FromEvent<Action<ChallengeProgressChange>, ChallengeProgressChange>(
                handler => new Action<ChallengeProgressChange>(handler),
                handler => state.Changed += handler,
                handler => state.Changed -= handler);
        }

        public static Observable<int> ObjectiveProgressAsObservable(
            this ChallengeProgressState state,
            int objectiveIndex)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.Create<int, (ChallengeProgressState State, int ObjectiveIndex)>(
                (state, objectiveIndex),
                static (observer, context) =>
                {
                    observer.OnNext(context.State.GetAmount(context.ObjectiveIndex));
                    return context.State.ChangesAsObservable()
                        .Where(change => change.Kind == ChallengeProgressChangeKind.SnapshotLoaded ||
                                         (change.Kind == ChallengeProgressChangeKind.ObjectiveProgress &&
                                          change.ObjectiveIndex == context.ObjectiveIndex))
                        .Select(_ => context.State.GetAmount(context.ObjectiveIndex))
                        .Subscribe(observer);
                })
                .DistinctUntilChanged();
        }

        public static Observable<bool> CompletionAsObservable(
            this ChallengeProgressState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Observable.Create<bool, ChallengeProgressState>(state, static (observer, value) =>
            {
                observer.OnNext(value.CompletionCount > 0);
                return value.ChangesAsObservable()
                    .Where(change => change.Kind == ChallengeProgressChangeKind.Completed ||
                                     change.Kind == ChallengeProgressChangeKind.SnapshotLoaded)
                    .Select(_ => value.CompletionCount > 0)
                    .Subscribe(observer);
            }).DistinctUntilChanged();
        }
    }
}
#endif
