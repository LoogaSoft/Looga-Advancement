using System;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    public enum ProgressionRequirementKind
    {
        AccountLevel,
        ProgramLevel,
        ExternalLevel,
        ChallengeComplete,
        SeasonTier,
        CustomFlag
    }

    public enum ProgressionCostKind
    {
        Currency,
        Item
    }

    public enum ProgressionEffectKind
    {
        Unlock,
        AdditiveModifier,
        MultiplicativeModifier,
        GeneratorRate,
        GeneratorCapacity,
        Custom
    }

    public enum ProgressionEffectStackMode
    {
        Add,
        Multiply,
        Highest,
        Lowest,
        Override
    }

    [Serializable]
    public sealed class ProgressionRequirementDefinition
    {
        [SerializeField] private ProgressionRequirementKind _kind;
        [SerializeField] private string _key = string.Empty;
        [SerializeField, Min(1)] private int _requiredValue = 1;

        public ProgressionRequirementKind Kind => _kind;
        public string Key => _key;
        public int RequiredValue => _requiredValue;
    }

    [Serializable]
    public sealed class ProgressionCostDefinition
    {
        [SerializeField] private ProgressionCostKind _kind;
        [SerializeField] private string _key = string.Empty;
        [SerializeField, Min(1)] private int _amount = 1;

        public ProgressionCostKind Kind => _kind;
        public string Key => _key;
        public int Amount => _amount;
    }

    [Serializable]
    public sealed class ProgressionEffectDefinition
    {
        [SerializeField] private ProgressionEffectKind _kind;
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private float _value = 1f;
        [SerializeField] private ProgressionEffectStackMode _stackMode;

        public ProgressionEffectKind Kind => _kind;
        public string Key => _key;
        public float Value => _value;
        public ProgressionEffectStackMode StackMode => _stackMode;
    }
}
