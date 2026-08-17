using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Advancement.Editor
{
    /// <summary>Provides a concise entry point to the visual progression authoring workflow.</summary>
    [CustomEditor(typeof(ProgressionGraphDefinition))]
    public sealed class ProgressionGraphDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ProgressionGraphDefinition graph = (ProgressionGraphDefinition)target;
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Branches", graph.Branches.Count);
                EditorGUILayout.IntField("Nodes", graph.Nodes.Count);
            }

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(graph);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open Progression Graph", GUILayout.Height(28f)))
                ProgressionGraphEditorWindow.Open(graph);

            DrawValidation(graph);
        }

        private static void DrawValidation(ProgressionGraphDefinition graph)
        {
            IReadOnlyList<ProgressionValidationIssue> issues = ProgressionGraphValidator.Validate(graph);
            for (int index = 0; index < issues.Count; index++)
            {
                ProgressionValidationIssue issue = issues[index];
                MessageType type = issue.Severity switch
                {
                    ProgressionValidationSeverity.Error => MessageType.Error,
                    ProgressionValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }
    }
}
