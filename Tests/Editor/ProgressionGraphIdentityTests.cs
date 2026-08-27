using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Advancement.Tests
{
    public sealed class ProgressionGraphIdentityTests
    {
        [Test]
        public void ValidationPreservesAuthoredIdsDuringIdentityMigration()
        {
            ProgressionGraphDefinition graph = ScriptableObject.CreateInstance<ProgressionGraphDefinition>();
            try
            {
                SerializedObject serializedGraph = new(graph);
                serializedGraph.FindProperty("_stableId").stringValue = "personal-bay";
                serializedGraph.FindProperty("_identityVersion").intValue = 0;

                SerializedProperty branches = serializedGraph.FindProperty("_branches");
                branches.arraySize = 1;
                branches.GetArrayElementAtIndex(0).FindPropertyRelative("_stableId").stringValue =
                    "generator-rate";

                SerializedProperty nodes = serializedGraph.FindProperty("_nodes");
                nodes.arraySize = 1;
                nodes.GetArrayElementAtIndex(0).FindPropertyRelative("_stableId").stringValue =
                    "generator-rate-1";
                serializedGraph.ApplyModifiedPropertiesWithoutUndo();

                InvokeOnValidate(graph);
                InvokeOnValidate(graph);

                serializedGraph.Update();
                Assert.That(graph.StableId, Is.EqualTo("personal-bay"));
                Assert.That(graph.Branches[0].StableId, Is.EqualTo("generator-rate"));
                Assert.That(graph.Nodes[0].StableId, Is.EqualTo("generator-rate-1"));
                Assert.That(serializedGraph.FindProperty("_identityVersion").intValue, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ValidationGeneratesMissingIdsOnlyOnce()
        {
            ProgressionGraphDefinition graph = ScriptableObject.CreateInstance<ProgressionGraphDefinition>();
            try
            {
                InvokeOnValidate(graph);
                string stableId = graph.StableId;

                InvokeOnValidate(graph);

                Assert.That(stableId, Is.Not.Empty);
                Assert.That(graph.StableId, Is.EqualTo(stableId));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static void InvokeOnValidate(ProgressionGraphDefinition graph)
        {
            MethodInfo method = typeof(ProgressionGraphDefinition).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(graph, null);
        }
    }
}
