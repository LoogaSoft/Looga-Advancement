using System;
using System.Collections.Generic;

namespace LoogaSoft.Advancement
{
    public enum AbilityActivationStatus
    {
        Activated,
        MissingDefinition,
        MissingExecutor,
        MissingRequiredTag,
        BlockedByTag,
        NoCharges,
        OnCooldown,
        Rejected
    }

    /// <summary>Provides game-owned data to an ability executor.</summary>
    public readonly struct AbilityActivationContext
    {
        public AbilityActivationContext(object owner, object target = null, object payload = null)
        {
            Owner = owner;
            Target = target;
            Payload = payload;
        }

        public object Owner { get; }
        public object Target { get; }
        public object Payload { get; }
    }

    public readonly struct AbilityActivationResult
    {
        public AbilityActivationResult(AbilityActivationStatus status, string reason = "")
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public AbilityActivationStatus Status { get; }
        public string Reason { get; }
        public bool Succeeded => Status == AbilityActivationStatus.Activated;
    }

    /// <summary>Connects an authored ability to game-specific behavior.</summary>
    public interface IAbilityExecutor
    {
        bool CanActivate(
            AbilityDefinition ability,
            in AbilityActivationContext context,
            out string rejectionReason);

        void Activate(AbilityDefinition ability, in AbilityActivationContext context);
    }

    /// <summary>Stores cooldown and charge data for one granted ability.</summary>
    [Serializable]
    public sealed class AbilityRuntimeState
    {
        private readonly string _abilityId;
        private int _charges;
        private double _cooldownEndsAt;
        private double _nextChargeAt;

        public AbilityRuntimeState(AbilityDefinition ability, double currentTime)
        {
            _abilityId = ability?.StableId ?? string.Empty;
            _charges = ability?.MaximumCharges ?? 0;
            _nextChargeAt = currentTime;
        }

        public string AbilityId => _abilityId;
        public int Charges => _charges;
        public double CooldownEndsAt => _cooldownEndsAt;

        public void Refresh(AbilityDefinition ability, double currentTime)
        {
            if (ability == null || _charges >= ability.MaximumCharges)
                return;

            float recoverySeconds = ability.ChargeRecoverySeconds;
            if (recoverySeconds <= 0f)
            {
                _charges = ability.MaximumCharges;
                return;
            }

            while (_charges < ability.MaximumCharges && currentTime >= _nextChargeAt)
            {
                _charges++;
                _nextChargeAt += recoverySeconds;
            }
        }

        internal void Consume(AbilityDefinition ability, double currentTime)
        {
            _charges = Math.Max(0, _charges - 1);
            _cooldownEndsAt = currentTime + ability.CooldownSeconds;
            if (_charges < ability.MaximumCharges && _nextChargeAt <= currentTime)
                _nextChargeAt = currentTime + Math.Max(0f, ability.ChargeRecoverySeconds);
        }
    }

    /// <summary>Applies shared ability limits before game-owned execution.</summary>
    public sealed class AbilityController
    {
        private readonly Dictionary<string, AbilityRuntimeState> _states =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<AbilityTagDefinition> _activeTags = new();

        public IReadOnlyCollection<AbilityTagDefinition> ActiveTags => _activeTags;

        public void SetTag(AbilityTagDefinition tag, bool active)
        {
            if (tag == null)
                return;

            if (active)
                _activeTags.Add(tag);
            else
                _activeTags.Remove(tag);
        }

        public AbilityRuntimeState GetOrCreateState(AbilityDefinition ability, double currentTime)
        {
            if (ability == null)
                return null;

            if (_states.TryGetValue(ability.StableId, out AbilityRuntimeState state))
                return state;

            state = new AbilityRuntimeState(ability, currentTime);
            _states.Add(ability.StableId, state);
            return state;
        }

        public AbilityActivationResult TryActivate(
            AbilityDefinition ability,
            IAbilityExecutor executor,
            in AbilityActivationContext context,
            double currentTime)
        {
            if (ability == null)
                return new AbilityActivationResult(AbilityActivationStatus.MissingDefinition);

            if (executor == null)
                return new AbilityActivationResult(AbilityActivationStatus.MissingExecutor);

            AbilityActivationStatus tagStatus = ValidateTags(ability);
            if (tagStatus != AbilityActivationStatus.Activated)
                return new AbilityActivationResult(tagStatus);

            AbilityRuntimeState state = GetOrCreateState(ability, currentTime);
            state.Refresh(ability, currentTime);
            if (state.Charges <= 0)
                return new AbilityActivationResult(AbilityActivationStatus.NoCharges);

            if (currentTime < state.CooldownEndsAt)
                return new AbilityActivationResult(AbilityActivationStatus.OnCooldown);

            if (!executor.CanActivate(ability, context, out string reason))
                return new AbilityActivationResult(AbilityActivationStatus.Rejected, reason);

            state.Consume(ability, currentTime);
            executor.Activate(ability, context);
            ApplyGrantedTags(ability);
            return new AbilityActivationResult(AbilityActivationStatus.Activated);
        }

        private AbilityActivationStatus ValidateTags(AbilityDefinition ability)
        {
            for (int index = 0; index < ability.RequiredTags.Count; index++)
            {
                AbilityTagDefinition tag = ability.RequiredTags[index];
                if (tag != null && !_activeTags.Contains(tag))
                    return AbilityActivationStatus.MissingRequiredTag;
            }

            for (int index = 0; index < ability.BlockedTags.Count; index++)
            {
                AbilityTagDefinition tag = ability.BlockedTags[index];
                if (tag != null && _activeTags.Contains(tag))
                    return AbilityActivationStatus.BlockedByTag;
            }

            return AbilityActivationStatus.Activated;
        }

        private void ApplyGrantedTags(AbilityDefinition ability)
        {
            for (int index = 0; index < ability.GrantedTags.Count; index++)
            {
                AbilityTagDefinition tag = ability.GrantedTags[index];
                if (tag != null)
                    _activeTags.Add(tag);
            }
        }
    }
}
