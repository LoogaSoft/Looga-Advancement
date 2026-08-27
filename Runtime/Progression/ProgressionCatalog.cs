using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Advancement
{
    /// <summary>Collects the progression programs used by the project.</summary>
    [CreateAssetMenu(fileName = "Progression Catalog", menuName = "LoogaSoft/Advancement/Progression/Catalog")]
    public sealed class ProgressionCatalog : ScriptableObject
    {
        private static ProgressionCatalog _active;

        [SerializeField] private List<ProgressionProgramDefinition> _programs = new();
        [SerializeField] private string _defaultSeasonId = "season-01";
        [SerializeField] private string _defaultSeasonStartUtc = "2026-01-01T00:00:00Z";
        [SerializeField] private string _seasonIdLiveConfigKey = "progression-season-id";
        [SerializeField] private string _seasonStartLiveConfigKey = "progression-season-start-utc";

        public static ProgressionCatalog Active => _active;
        public IReadOnlyList<ProgressionProgramDefinition> Programs => _programs;
        public string DefaultSeasonId => _defaultSeasonId;
        public string SeasonIdLiveConfigKey => _seasonIdLiveConfigKey;
        public string SeasonStartLiveConfigKey => _seasonStartLiveConfigKey;

        public DateTime DefaultSeasonStartUtc
        {
            get
            {
                return DateTime.TryParse(
                    _defaultSeasonStartUtc,
                    null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime value)
                    ? value.ToUniversalTime()
                    : DateTime.UnixEpoch;
            }
        }

        public static void SetActive(ProgressionCatalog catalog)
        {
            _active = catalog;
        }

        public ProgressionProgramDefinition FindProgram(string programId)
        {
            for (int index = 0; index < _programs.Count; index++)
            {
                ProgressionProgramDefinition program = _programs[index];
                if (program != null &&
                    string.Equals(program.StableId, programId, StringComparison.OrdinalIgnoreCase))
                {
                    return program;
                }
            }

            return null;
        }

        /// <summary>Finds the first program with the specified semantic role.</summary>
        public ProgressionProgramDefinition FindProgram(ProgressionProgramKind kind)
        {
            for (int index = 0; index < _programs.Count; index++)
            {
                ProgressionProgramDefinition program = _programs[index];
                if (program != null && program.Kind == kind)
                {
                    return program;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            _programs ??= new List<ProgressionProgramDefinition>();
            _defaultSeasonId = string.IsNullOrWhiteSpace(_defaultSeasonId)
                ? "season-01"
                : _defaultSeasonId.Trim();
            _seasonIdLiveConfigKey = _seasonIdLiveConfigKey?.Trim() ?? string.Empty;
            _seasonStartLiveConfigKey = _seasonStartLiveConfigKey?.Trim() ?? string.Empty;
        }
    }
}
