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

        [TestCase(ProgressionGraphLatticeType.Rectangular)]
        [TestCase(ProgressionGraphLatticeType.Diamond)]
        [TestCase(ProgressionGraphLatticeType.Hexagonal)]
        [TestCase(ProgressionGraphLatticeType.Triangular)]
        [TestCase(ProgressionGraphLatticeType.Staggered)]
        public void LatticeCoordinatesRoundTrip(ProgressionGraphLatticeType latticeType)
        {
            ProgressionGraphDefinition graph = ScriptableObject.CreateInstance<ProgressionGraphDefinition>();
            try
            {
                SerializedObject serializedGraph = new(graph);
                serializedGraph.FindProperty("_latticeType").enumValueIndex = (int)latticeType;
                serializedGraph.FindProperty("_latticeSpacing").vector2Value = new Vector2(230f, 240f);
                serializedGraph.ApplyModifiedPropertiesWithoutUndo();

                Vector2Int[] coordinates =
                {
                    new(-3, -2),
                    new(0, 0),
                    new(2, 1),
                    new(4, 5)
                };
                for (int index = 0; index < coordinates.Length; index++)
                {
                    Vector2 position = graph.GetLatticePosition(coordinates[index]);
                    Assert.That(graph.GetLatticeCoordinate(position), Is.EqualTo(coordinates[index]));
                }
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void LatticeAuthoringUsesNodeCoordinateForResolvedPosition()
        {
            ProgressionGraphDefinition graph = ScriptableObject.CreateInstance<ProgressionGraphDefinition>();
            try
            {
                SerializedObject serializedGraph = new(graph);
                serializedGraph.FindProperty("_authoringMode").enumValueIndex =
                    (int)ProgressionGraphAuthoringMode.Lattice;
                SerializedProperty nodes = serializedGraph.FindProperty("_nodes");
                nodes.arraySize = 1;
                SerializedProperty node = nodes.GetArrayElementAtIndex(0);
                node.FindPropertyRelative("_hasLatticeCoordinate").boolValue = true;
                node.FindPropertyRelative("_latticeCoordinate").vector2IntValue = new Vector2Int(3, 2);
                node.FindPropertyRelative("_graphPosition").vector2Value = new Vector2(999f, 999f);
                serializedGraph.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    graph.GetNodePosition(graph.Nodes[0]),
                    Is.EqualTo(graph.GetLatticePosition(new Vector2Int(3, 2))));
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
