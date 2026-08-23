using UnityEngine;

namespace LoogaSoft.Advancement.Editor
{
    internal static class LoogaAdvancementR3SupportProvider
    {
        private const string DefineSymbol = "LOOGA_ADVANCEMENT_R3_SUPPORT";
        private static readonly string[] RequiredAssemblies = { "R3" };

        public static string ProviderId => "looga-advancement.r3";
        public static string PackageName => "Looga Advancement";
        public static string IntegrationName => "R3";
        public static string Description => "Adds observable progression and challenge state adapters.";

        public static bool IsEnabled() => LoogaAdvancementOptionalSupportUtility.DefineIsEnabled(DefineSymbol);

        public static string GetUnavailableReason()
        {
            return LoogaAdvancementOptionalSupportUtility.AllAssembliesAreAvailable(
                RequiredAssemblies,
                out string missing)
                ? string.Empty
                : "Install R3. Missing assemblies: " + missing;
        }

        public static void SetEnabled(bool enabled)
        {
            LoogaAdvancementOptionalSupportUtility.SetDefineSymbol(DefineSymbol, enabled);
            Debug.Log($"Looga Advancement R3 support {(enabled ? "enabled" : "disabled")}.");
        }
    }

    internal static class LoogaAdvancementMemoryPackSupportProvider
    {
        private const string DefineSymbol = "LOOGA_ADVANCEMENT_MEMORYPACK_SUPPORT";
        private static readonly string[] RequiredAssemblies = { "MemoryPack.Unity" };

        public static string ProviderId => "looga-advancement.memorypack";
        public static string PackageName => "Looga Advancement";
        public static string IntegrationName => "MemoryPack";
        public static string Description => "Adds versioned progression and challenge snapshot DTOs.";

        public static bool IsEnabled() => LoogaAdvancementOptionalSupportUtility.DefineIsEnabled(DefineSymbol);

        public static string GetUnavailableReason()
        {
            return LoogaAdvancementOptionalSupportUtility.AllAssembliesAreAvailable(
                RequiredAssemblies,
                out string missing)
                ? string.Empty
                : "Install MemoryPack. Missing assemblies: " + missing;
        }

        public static void SetEnabled(bool enabled)
        {
            LoogaAdvancementOptionalSupportUtility.SetDefineSymbol(DefineSymbol, enabled);
            Debug.Log($"Looga Advancement MemoryPack support {(enabled ? "enabled" : "disabled")}.");
        }
    }
}
