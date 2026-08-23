using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace LoogaSoft.Advancement.Editor
{
    internal static class LoogaAdvancementOptionalSupportUtility
    {
        public static bool AllAssembliesAreAvailable(
            IReadOnlyList<string> assemblyNames,
            out string missingAssemblies)
        {
            string[] missing = assemblyNames
                .Where(assemblyName => !AssemblyIsAvailable(assemblyName))
                .ToArray();

            missingAssemblies = string.Join(", ", missing);
            return missing.Length == 0;
        }

        public static bool DefineIsEnabled(string defineSymbol)
        {
            return GetDefines().Contains(defineSymbol);
        }

        public static void SetDefineSymbol(string defineSymbol, bool enabled)
        {
            List<string> defines = GetDefines();
            bool changed = enabled ? AddDistinct(defines, defineSymbol) : defines.Remove(defineSymbol);
            if (!changed)
                return;

            PlayerSettings.SetScriptingDefineSymbols(
                GetNamedBuildTarget(),
                string.Join(";", defines.Distinct()));
            AssetDatabase.Refresh();
        }

        private static bool AssemblyIsAvailable(string assemblyName)
        {
            return CompilationPipeline.GetAssemblies().Any(assembly => assembly.name == assemblyName) ||
                   AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == assemblyName) ||
                   AssetDatabase.FindAssets($"{assemblyName} t:AssemblyDefinitionAsset").Length > 0;
        }

        private static bool AddDistinct(List<string> values, string value)
        {
            if (values.Contains(value))
                return false;

            values.Add(value);
            return true;
        }

        private static List<string> GetDefines()
        {
            return PlayerSettings.GetScriptingDefineSymbols(GetNamedBuildTarget())
                .Split(';')
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct()
                .ToList();
        }

        private static NamedBuildTarget GetNamedBuildTarget()
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            return NamedBuildTarget.FromBuildTargetGroup(group);
        }
    }
}
