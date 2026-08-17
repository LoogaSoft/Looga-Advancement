using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    /// <summary>Identifies a capability state that can allow or block abilities.</summary>
    [CreateAssetMenu(fileName = "Ability Tag", menuName = "LoogaSoft/Advancement/Abilities/Tag")]
    public sealed class AbilityTagDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;

        public string StableId => _stableId;

        private void OnValidate()
        {
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "ability-tag");
        }
    }

    /// <summary>Defines one game-neutral ability and its activation limits.</summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "LoogaSoft/Advancement/Abilities/Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _stableId = string.Empty;
        [SerializeField, TextArea(2, 5)] private string _description = string.Empty;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private float _cooldownSeconds;
        [SerializeField, Min(1)] private int _maximumCharges = 1;
        [SerializeField, Min(0f)] private float _chargeRecoverySeconds;
        [SerializeField] private List<AbilityTagDefinition> _grantedTags = new();
        [SerializeField] private List<AbilityTagDefinition> _requiredTags = new();
        [SerializeField] private List<AbilityTagDefinition> _blockedTags = new();

        public string StableId => _stableId;
        public string DisplayName => name;
        public string Description => _description;
        public Sprite Icon => _icon;
        public float CooldownSeconds => _cooldownSeconds;
        public int MaximumCharges => _maximumCharges;
        public float ChargeRecoverySeconds => _chargeRecoverySeconds;
        public IReadOnlyList<AbilityTagDefinition> GrantedTags => _grantedTags;
        public IReadOnlyList<AbilityTagDefinition> RequiredTags => _requiredTags;
        public IReadOnlyList<AbilityTagDefinition> BlockedTags => _blockedTags;

        private void OnValidate()
        {
            _stableId = ProgressionIdUtility.EnsureGenerated(_stableId, "ability");
            _cooldownSeconds = Mathf.Max(0f, _cooldownSeconds);
            _maximumCharges = Mathf.Max(1, _maximumCharges);
            _chargeRecoverySeconds = Mathf.Max(0f, _chargeRecoverySeconds);
            _grantedTags ??= new List<AbilityTagDefinition>();
            _requiredTags ??= new List<AbilityTagDefinition>();
            _blockedTags ??= new List<AbilityTagDefinition>();
        }
    }

    /// <summary>Collects the abilities available to one project or game mode.</summary>
    [CreateAssetMenu(fileName = "Ability Catalog", menuName = "LoogaSoft/Advancement/Abilities/Catalog")]
    public sealed class AbilityCatalog : ScriptableObject
    {
        [SerializeField] private List<AbilityDefinition> _abilities = new();

        public IReadOnlyList<AbilityDefinition> Abilities => _abilities;

        public AbilityDefinition Find(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return null;

            for (int index = 0; index < _abilities.Count; index++)
            {
                AbilityDefinition ability = _abilities[index];
                if (ability != null &&
                    string.Equals(ability.StableId, stableId, System.StringComparison.OrdinalIgnoreCase))
                    return ability;
            }

            return null;
        }

        private void OnValidate()
        {
            _abilities ??= new List<AbilityDefinition>();
        }
    }
}
