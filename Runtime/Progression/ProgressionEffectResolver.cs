using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    /// <summary>Contains one resolved progression effect for gameplay and UI consumers.</summary>
    public readonly struct ResolvedProgressionEffect
    {
        public ResolvedProgressionEffect(ProgressionEffectKind kind, string key, float value)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            Value = value;
        }

        public ProgressionEffectKind Kind { get; }
        public string Key { get; }
        public float Value { get; }
        public bool IsUnlocked => Kind == ProgressionEffectKind.Unlock && Value > 0f;
    }

    /// <summary>Projects purchased nodes into deterministic values for other systems.</summary>
    public static class ProgressionEffectResolver
    {
        public static IReadOnlyList<ResolvedProgressionEffect> Resolve(
            ProgressionGraphDefinition graph,
            ProgressionProgramState state)
        {
            return Resolve(graph, state, null, 0);
        }

        /// <summary>Resolves graph effects and all level milestone effects reached by the player.</summary>
        public static IReadOnlyList<ResolvedProgressionEffect> Resolve(
            ProgressionGraphDefinition graph,
            ProgressionProgramState state,
            ProgressionLevelTrackDefinition levelTrack,
            int currentLevel)
        {
            List<ResolvedProgressionEffect> results = new();
            if (state == null)
                return results;

            Dictionary<EffectKey, EffectAccumulator> accumulators = new();
            if (graph != null)
            {
                for (int nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
                {
                    ProgressionNodeDefinition node = graph.Nodes[nodeIndex];
                    int rank = node != null ? state.GetNodeRank(node.StableId) : 0;
                    if (rank <= 0)
                        continue;

                    AddEffects(accumulators, node.Effects, rank);
                }
            }

            if (levelTrack != null)
            {
                for (int stepIndex = 0; stepIndex < levelTrack.Steps.Count; stepIndex++)
                {
                    ProgressionLevelStepDefinition step = levelTrack.Steps[stepIndex];
                    if (step == null || step.TargetLevel > currentLevel)
                        continue;

                    AddEffects(accumulators, step.Effects, 1);
                }
            }

            foreach (KeyValuePair<EffectKey, EffectAccumulator> pair in accumulators)
            {
                results.Add(new ResolvedProgressionEffect(
                    pair.Key.Kind,
                    pair.Key.Key,
                    pair.Value.Value));
            }

            return results;
        }

        private static void AddEffects(
            IDictionary<EffectKey, EffectAccumulator> accumulators,
            IReadOnlyList<ProgressionEffectDefinition> effects,
            int rank)
        {
            if (effects == null)
                return;

            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                ProgressionEffectDefinition effect = effects[effectIndex];
                if (effect == null)
                    continue;

                EffectKey key = new(effect.Kind, effect.Key);
                accumulators.TryGetValue(key, out EffectAccumulator accumulator);
                accumulators[key] = Apply(accumulator, effect, rank);
            }
        }

        public static bool TryGetValue(
            IReadOnlyList<ResolvedProgressionEffect> effects,
            ProgressionEffectKind kind,
            string key,
            out float value)
        {
            if (effects != null)
            {
                for (int index = 0; index < effects.Count; index++)
                {
                    ResolvedProgressionEffect effect = effects[index];
                    if (effect.Kind == kind &&
                        string.Equals(effect.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = effect.Value;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }

        private static EffectAccumulator Apply(
            EffectAccumulator accumulator,
            ProgressionEffectDefinition effect,
            int rank)
        {
            float rankedValue = effect.StackMode switch
            {
                ProgressionEffectStackMode.Add => effect.Value * rank,
                ProgressionEffectStackMode.Multiply => Mathf.Pow(effect.Value, rank),
                _ => effect.Value
            };
            if (!accumulator.HasValue)
                return new EffectAccumulator(true, rankedValue);

            float value = effect.StackMode switch
            {
                ProgressionEffectStackMode.Add => accumulator.Value + rankedValue,
                ProgressionEffectStackMode.Multiply => accumulator.Value * rankedValue,
                ProgressionEffectStackMode.Highest => Mathf.Max(accumulator.Value, rankedValue),
                ProgressionEffectStackMode.Lowest => Mathf.Min(accumulator.Value, rankedValue),
                ProgressionEffectStackMode.Override => rankedValue,
                _ => accumulator.Value
            };
            return new EffectAccumulator(true, value);
        }

        private readonly struct EffectKey : IEquatable<EffectKey>
        {
            public EffectKey(ProgressionEffectKind kind, string key)
            {
                Kind = kind;
                Key = key ?? string.Empty;
            }

            public ProgressionEffectKind Kind { get; }
            public string Key { get; }

            public bool Equals(EffectKey other)
            {
                return Kind == other.Kind &&
                       string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is EffectKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(Key));
            }
        }

        private readonly struct EffectAccumulator
        {
            public EffectAccumulator(bool hasValue, float value)
            {
                HasValue = hasValue;
                Value = value;
            }

            public bool HasValue { get; }
            public float Value { get; }
        }
    }
}
