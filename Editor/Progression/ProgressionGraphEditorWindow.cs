using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Advancement.Editor
{
    /// <summary>Authors progression nodes and prerequisite links on a zoomable canvas.</summary>
    public sealed class ProgressionGraphEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 300f;
        private const float NodeWidth = 210f;
        private const float NodeHeight = 104f;
        private const float PortSize = 16f;

        [SerializeField]
        private ProgressionGraphDefinition _graph;
        private SerializedObject _serializedGraph;
        private ProgressionGraphCanvas _canvas;
        private IMGUIContainer _inspector;
        private ObjectField _graphField;
        private ProgressionNodeSearchProvider _searchProvider;
        private int _selectedNodeIndex = -1;
        private List<ProgressionValidationIssue> _issues = new();

        [MenuItem("LoogaSoft/Advancement/Progression Graph", priority = 20)]
        public static void Open()
        {
            Open(Selection.activeObject as ProgressionGraphDefinition);
        }

        public static void Open(ProgressionGraphDefinition graph)
        {
            ProgressionGraphEditorWindow window = GetWindow<ProgressionGraphEditorWindow>();
            window.titleContent = new GUIContent("Progression Graph");
            window.minSize = new Vector2(840f, 520f);
            window.Show();
            window.SetGraph(graph);
        }

        private void OnEnable()
        {
            BuildInterface();
            if (_graph != null)
                SetGraph(_graph);
        }

        private void BuildInterface()
        {
            rootVisualElement.Clear();

            Toolbar toolbar = new();
            _graphField = new ObjectField("Graph")
            {
                objectType = typeof(ProgressionGraphDefinition),
                allowSceneObjects = false,
                value = _graph
            };
            _graphField.style.width = 310f;
            _graphField.RegisterValueChangedCallback(change =>
                SetGraph(change.newValue as ProgressionGraphDefinition));
            toolbar.Add(_graphField);
            toolbar.Add(CreateToolbarButton("Add Node", AddNodeAtViewCenter));
            toolbar.Add(CreateToolbarButton("Arrange Top Down", ArrangeTopDown));
            toolbar.Add(CreateToolbarButton("Frame All", () => _canvas?.FrameAll()));
            toolbar.Add(CreateToolbarButton("Validate", ValidateGraph));
            rootVisualElement.Add(toolbar);

            TwoPaneSplitView split = new(1, InspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            _canvas = new ProgressionGraphCanvas(this);
            _inspector = new IMGUIContainer(DrawInspector);
            _inspector.style.minWidth = 280f;
            _inspector.style.paddingLeft = 8f;
            _inspector.style.paddingRight = 8f;
            _inspector.style.paddingTop = 6f;
            split.Add(_canvas);
            split.Add(_inspector);
            rootVisualElement.Add(split);

            _searchProvider = CreateInstance<ProgressionNodeSearchProvider>();
            _searchProvider.hideFlags = HideFlags.HideAndDontSave;
            _searchProvider.Initialize(this);
        }

        private static ToolbarButton CreateToolbarButton(string label, Action action)
        {
            return new ToolbarButton(action) { text = label };
        }

        private void DrawInspector()
        {
            if (_graph == null || _serializedGraph == null)
            {
                EditorGUILayout.HelpBox("Assign a progression graph to begin.", MessageType.Info);
                return;
            }

            if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _graph.Nodes.Count)
                DrawNodeInspector();
            else
                DrawGraphInspector();

            DrawValidationIssues();
        }

        private void DrawGraphInspector()
        {
            _serializedGraph.Update();
            EditorGUILayout.LabelField(_graph.name, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedGraph.FindProperty("_layoutMode"));
            EditorGUILayout.PropertyField(_serializedGraph.FindProperty("_tierSpacing"));
            EditorGUILayout.PropertyField(_serializedGraph.FindProperty("_nodeSpacing"));
            EditorGUILayout.PropertyField(_serializedGraph.FindProperty("_branchSpacing"));
            EditorGUILayout.Space(5f);
            EditorGUILayout.PropertyField(_serializedGraph.FindProperty("_branches"), true);

            if (_serializedGraph.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_graph);
                RebuildCanvas();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Drag from a node's bottom port to another node's top port. " +
                "Drop a connection on empty canvas to create and connect a new node.",
                MessageType.Info);
        }

        private void DrawNodeInspector()
        {
            _serializedGraph.Update();
            SerializedProperty nodes = _serializedGraph.FindProperty("_nodes");
            if (_selectedNodeIndex >= nodes.arraySize)
            {
                SelectNode(-1);
                return;
            }

            SerializedProperty node = nodes.GetArrayElementAtIndex(_selectedNodeIndex);
            EditorGUILayout.LabelField("Node", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_displayName"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_description"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_icon"));
            SerializedProperty prerequisites = node.FindPropertyRelative("_prerequisites");
            SerializedProperty entries = prerequisites.FindPropertyRelative("_entries");
            if (entries.arraySize == 0)
                DrawBranchPopup(node.FindPropertyRelative("_branchId"));
            else
                DrawInheritedBranches(_graph.Nodes[_selectedNodeIndex]);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(node.FindPropertyRelative("_tier"));
                EditorGUILayout.PropertyField(node.FindPropertyRelative("_maxRank"));
            }

            EditorGUILayout.PropertyField(node.FindPropertyRelative("_pointCostPerRank"));
            DrawPrerequisiteRule(prerequisites);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_requirements"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_costs"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("_effects"), true);

            if (_serializedGraph.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_graph);
                _canvas.RefreshNode(_selectedNodeIndex);
                ValidateGraph();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Delete Node"))
                DeleteNode(_selectedNodeIndex);
        }

        private void DrawBranchPopup(SerializedProperty branchId)
        {
            SerializedProperty branches = _serializedGraph.FindProperty("_branches");
            string[] labels = new string[branches.arraySize + 1];
            string[] ids = new string[branches.arraySize + 1];
            labels[0] = "Select Origin";
            ids[0] = string.Empty;
            int selected = 0;

            for (int index = 0; index < branches.arraySize; index++)
            {
                SerializedProperty branch = branches.GetArrayElementAtIndex(index);
                labels[index + 1] = branch.FindPropertyRelative("_displayName").stringValue;
                ids[index + 1] = branch.FindPropertyRelative("_stableId").stringValue;
                if (string.Equals(ids[index + 1], branchId.stringValue, StringComparison.OrdinalIgnoreCase))
                    selected = index + 1;
            }

            branchId.stringValue = ids[EditorGUILayout.Popup("Origin Branch", selected, labels)];
        }

        private void DrawInheritedBranches(ProgressionNodeDefinition node)
        {
            IReadOnlyList<ProgressionBranchDefinition> branches = _graph.GetNodeBranches(node);
            string label = branches.Count switch
            {
                0 => "Unresolved",
                1 => branches[0].DisplayName,
                _ => string.Join(" + ", branches.Select(branch => branch.DisplayName))
            };

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Inherited Branches", label);
        }

        private void DrawPrerequisiteRule(SerializedProperty prerequisites)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Incoming Connections", EditorStyles.boldLabel);
            SerializedProperty mode = prerequisites.FindPropertyRelative("_mode");
            EditorGUILayout.PropertyField(mode, new GUIContent("Requirement"));
            SerializedProperty entries = prerequisites.FindPropertyRelative("_entries");
            if ((ProgressionPrerequisiteMode)mode.enumValueIndex == ProgressionPrerequisiteMode.AtLeast)
            {
                EditorGUILayout.PropertyField(
                    prerequisites.FindPropertyRelative("_requiredCount"),
                    new GUIContent("Required Connections"));
            }

            if (entries.arraySize == 0)
            {
                EditorGUILayout.HelpBox("This node has no prerequisites.", MessageType.None);
                return;
            }

            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                string nodeId = entry.FindPropertyRelative("_nodeId").stringValue;
                string label = _graph.TryGetNode(nodeId, out ProgressionNodeDefinition source)
                    ? source.DisplayName
                    : nodeId;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label);
                    EditorGUILayout.PropertyField(
                        entry.FindPropertyRelative("_requiredRank"),
                        new GUIContent("Rank"),
                        GUILayout.Width(104f));
                }
            }
        }

        private void DrawValidationIssues()
        {
            if (_issues.Count == 0)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            for (int index = 0; index < _issues.Count; index++)
            {
                ProgressionValidationIssue issue = _issues[index];
                MessageType type = issue.Severity switch
                {
                    ProgressionValidationSeverity.Error => MessageType.Error,
                    ProgressionValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        internal void OpenNodeSearch(Vector2 canvasPosition, string sourceNodeId)
        {
            if (_graph == null)
                return;

            Vector2 graphPosition = _canvas.CanvasToGraphPosition(canvasPosition);
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? canvasPosition);
            _searchProvider.Prepare(graphPosition, sourceNodeId);
            SearchWindow.Open(new SearchWindowContext(screenPosition), _searchProvider);
        }

        internal void CreateNode(Vector2 graphPosition, string branchId, string sourceNodeId)
        {
            if (_graph == null || _serializedGraph == null)
                return;

            EnsureManualLayout();
            Undo.RecordObject(_graph, "Create Progression Node");
            _serializedGraph.Update();
            SerializedProperty nodes = _serializedGraph.FindProperty("_nodes");
            int index = nodes.arraySize;
            nodes.InsertArrayElementAtIndex(index);
            SerializedProperty node = nodes.GetArrayElementAtIndex(index);
            int tier = !string.IsNullOrEmpty(sourceNodeId) &&
                       _graph.TryGetNode(sourceNodeId, out ProgressionNodeDefinition source)
                ? source.Tier + 1
                : 1;
            ResetNode(node, index, graphPosition, branchId, sourceNodeId, tier);
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            RebuildCanvas();
            SelectNode(index);
            ValidateGraph();
        }

        internal void AddPrerequisite(string sourceNodeId, string targetNodeId)
        {
            if (string.IsNullOrEmpty(sourceNodeId) || string.IsNullOrEmpty(targetNodeId) ||
                string.Equals(sourceNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
                return;

            int targetIndex = FindNodeIndex(targetNodeId);
            if (targetIndex < 0 || HasConnection(sourceNodeId, targetNodeId))
                return;

            Undo.RecordObject(_graph, "Connect Progression Nodes");
            _serializedGraph.Update();
            SerializedProperty target = _serializedGraph.FindProperty("_nodes").GetArrayElementAtIndex(targetIndex);
            SerializedProperty entries = target.FindPropertyRelative("_prerequisites")
                .FindPropertyRelative("_entries");
            int entryIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(entryIndex);
            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
            entry.FindPropertyRelative("_nodeId").stringValue = sourceNodeId;
            entry.FindPropertyRelative("_requiredRank").intValue = 1;
            target.FindPropertyRelative("_branchId").stringValue = string.Empty;
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            ValidateGraph();
            _canvas.RefreshAllNodes();
            _inspector.MarkDirtyRepaint();
        }

        internal void RemovePrerequisite(string sourceNodeId, string targetNodeId)
        {
            int targetIndex = FindNodeIndex(targetNodeId);
            if (targetIndex < 0)
                return;

            Undo.RecordObject(_graph, "Disconnect Progression Nodes");
            _serializedGraph.Update();
            SerializedProperty target = _serializedGraph.FindProperty("_nodes").GetArrayElementAtIndex(targetIndex);
            SerializedProperty entries = target.FindPropertyRelative("_prerequisites")
                .FindPropertyRelative("_entries");
            string fallbackBranchId = string.Empty;
            if (_graph.TryGetNode(sourceNodeId, out ProgressionNodeDefinition source))
                fallbackBranchId = _graph.GetSingleNodeBranch(source)?.StableId ?? string.Empty;
            for (int index = entries.arraySize - 1; index >= 0; index--)
            {
                if (string.Equals(
                        entries.GetArrayElementAtIndex(index).FindPropertyRelative("_nodeId").stringValue,
                        sourceNodeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    entries.DeleteArrayElementAtIndex(index);
                }
            }
            if (entries.arraySize == 0)
                target.FindPropertyRelative("_branchId").stringValue = fallbackBranchId;
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            ValidateGraph();
            _canvas.RefreshAllNodes();
            _inspector.MarkDirtyRepaint();
        }

        internal bool HasConnection(string sourceNodeId, string targetNodeId)
        {
            if (_graph == null || !_graph.TryGetNode(targetNodeId, out ProgressionNodeDefinition target))
                return false;

            IReadOnlyList<ProgressionPrerequisiteEntryDefinition> entries = target.Prerequisites.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index]?.NodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal void SaveNodePosition(int nodeIndex, Vector2 graphPosition)
        {
            if (_graph == null || nodeIndex < 0 || nodeIndex >= _graph.Nodes.Count)
                return;

            EnsureManualLayout();
            _serializedGraph.Update();
            SerializedProperty node = _serializedGraph.FindProperty("_nodes").GetArrayElementAtIndex(nodeIndex);
            node.FindPropertyRelative("_graphPosition").vector2Value = graphPosition;
            _serializedGraph.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_graph);
        }

        internal void SelectNode(int nodeIndex)
        {
            _selectedNodeIndex = nodeIndex;
            _inspector?.MarkDirtyRepaint();
        }

        internal IReadOnlyList<ProgressionBranchDefinition> GetBranches()
        {
            return _graph?.Branches;
        }

        internal void DeleteNode(int nodeIndex)
        {
            if (_graph == null || nodeIndex < 0 || nodeIndex >= _graph.Nodes.Count)
                return;

            string nodeId = _graph.Nodes[nodeIndex].StableId;
            Undo.RecordObject(_graph, "Delete Progression Node");
            _serializedGraph.Update();
            SerializedProperty nodes = _serializedGraph.FindProperty("_nodes");
            for (int targetIndex = 0; targetIndex < nodes.arraySize; targetIndex++)
            {
                SerializedProperty entries = nodes.GetArrayElementAtIndex(targetIndex)
                    .FindPropertyRelative("_prerequisites")
                    .FindPropertyRelative("_entries");
                for (int entryIndex = entries.arraySize - 1; entryIndex >= 0; entryIndex--)
                {
                    if (string.Equals(
                            entries.GetArrayElementAtIndex(entryIndex).FindPropertyRelative("_nodeId").stringValue,
                            nodeId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        entries.DeleteArrayElementAtIndex(entryIndex);
                    }
                }
            }
            nodes.DeleteArrayElementAtIndex(nodeIndex);
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            _selectedNodeIndex = -1;
            RebuildCanvas();
            ValidateGraph();
        }

        private void AddNodeAtViewCenter()
        {
            if (_graph == null)
                return;

            OpenNodeSearch(_canvas.layout.center, string.Empty);
        }

        private void ArrangeTopDown()
        {
            if (_graph == null || _serializedGraph == null)
                return;

            Undo.RecordObject(_graph, "Arrange Progression Graph");
            _serializedGraph.Update();
            _serializedGraph.FindProperty("_layoutMode").enumValueIndex =
                (int)ProgressionGraphLayoutMode.Automatic;
            _serializedGraph.ApplyModifiedProperties();
            SnapshotResolvedPositions();
            RebuildCanvas();
            _canvas.FrameAll();
        }

        private void EnsureManualLayout()
        {
            if (_graph.LayoutMode == ProgressionGraphLayoutMode.Manual)
                return;

            SnapshotResolvedPositions();
        }

        private void SnapshotResolvedPositions()
        {
            Vector2[] positions = new Vector2[_graph.Nodes.Count];
            for (int index = 0; index < positions.Length; index++)
                positions[index] = _graph.GetNodePosition(_graph.Nodes[index]);

            _serializedGraph.Update();
            SerializedProperty nodes = _serializedGraph.FindProperty("_nodes");
            for (int index = 0; index < positions.Length; index++)
            {
                nodes.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_graphPosition").vector2Value = positions[index];
            }
            _serializedGraph.FindProperty("_layoutMode").enumValueIndex =
                (int)ProgressionGraphLayoutMode.Manual;
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }

        private void SetGraph(ProgressionGraphDefinition graph)
        {
            _graph = graph;
            _graphField?.SetValueWithoutNotify(graph);
            _serializedGraph = graph != null ? new SerializedObject(graph) : null;
            _selectedNodeIndex = -1;
            ValidateGraph();
            RebuildCanvas();
            _inspector?.MarkDirtyRepaint();
        }

        private void RebuildCanvas()
        {
            _canvas?.Populate(_graph);
        }

        private void ValidateGraph()
        {
            _issues = ProgressionGraphValidator.Validate(_graph);
            _inspector?.MarkDirtyRepaint();
        }

        private int FindNodeIndex(string nodeId)
        {
            if (_graph == null)
                return -1;

            for (int index = 0; index < _graph.Nodes.Count; index++)
            {
                if (string.Equals(_graph.Nodes[index]?.StableId, nodeId, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        private static void ResetNode(
            SerializedProperty node,
            int index,
            Vector2 graphPosition,
            string branchId,
            string sourceNodeId,
            int tier)
        {
            node.FindPropertyRelative("_stableId").stringValue = $"node-{Guid.NewGuid():N}";
            node.FindPropertyRelative("_displayName").stringValue = $"New Node {index + 1}";
            node.FindPropertyRelative("_description").stringValue = string.Empty;
            node.FindPropertyRelative("_icon").objectReferenceValue = null;
            node.FindPropertyRelative("_branchId").stringValue = branchId ?? string.Empty;
            node.FindPropertyRelative("_tier").intValue = Mathf.Max(1, tier);
            node.FindPropertyRelative("_graphPosition").vector2Value = graphPosition;
            node.FindPropertyRelative("_maxRank").intValue = 1;
            node.FindPropertyRelative("_pointCostPerRank").intValue = 0;

            SerializedProperty prerequisites = node.FindPropertyRelative("_prerequisites");
            prerequisites.FindPropertyRelative("_mode").enumValueIndex = (int)ProgressionPrerequisiteMode.All;
            prerequisites.FindPropertyRelative("_requiredCount").intValue = 1;
            SerializedProperty entries = prerequisites.FindPropertyRelative("_entries");
            entries.ClearArray();
            if (!string.IsNullOrEmpty(sourceNodeId))
            {
                entries.InsertArrayElementAtIndex(0);
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("_nodeId").stringValue = sourceNodeId;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("_requiredRank").intValue = 1;
            }

            node.FindPropertyRelative("_prerequisiteNodeIds").ClearArray();
            node.FindPropertyRelative("_requirements").ClearArray();
            node.FindPropertyRelative("_costs").ClearArray();
            node.FindPropertyRelative("_effects").ClearArray();
        }

        private sealed class ProgressionGraphCanvas : GraphView
        {
            private readonly ProgressionGraphEditorWindow _owner;
            private readonly ProgressionEdgeConnectorListener _edgeListener;
            private readonly Dictionary<string, ProgressionNodeElement> _nodes =
                new(StringComparer.OrdinalIgnoreCase);
            private bool _rebuilding;

            public ProgressionGraphCanvas(ProgressionGraphEditorWindow owner)
            {
                _owner = owner;
                _edgeListener = new ProgressionEdgeConnectorListener(this, owner);
                style.flexGrow = 1f;
                Insert(0, new GridBackground());
                SetupZoom(0.15f, 2.5f);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                graphViewChanged = HandleGraphViewChanged;
                RegisterCallback<PointerMoveEvent>(HandlePointerMove, TrickleDown.TrickleDown);

            }

            public void Populate(ProgressionGraphDefinition graph)
            {
                _rebuilding = true;
                DeleteElements(graphElements.ToList());
                _nodes.Clear();

                if (graph != null)
                {
                    for (int index = 0; index < graph.Nodes.Count; index++)
                    {
                        ProgressionNodeDefinition definition = graph.Nodes[index];
                        if (definition == null)
                            continue;

                        IReadOnlyList<ProgressionBranchDefinition> branches = graph.GetNodeBranches(definition);
                        ProgressionNodeElement node = new(
                            index,
                            definition,
                            branches,
                            _edgeListener,
                            _owner.SelectNode);
                        node.SetPosition(new Rect(graph.GetNodePosition(definition), new Vector2(NodeWidth, NodeHeight)));
                        node.style.height = StyleKeyword.Auto;
                        AddElement(node);
                        _nodes[definition.StableId] = node;
                    }

                    for (int targetIndex = 0; targetIndex < graph.Nodes.Count; targetIndex++)
                    {
                        ProgressionNodeDefinition target = graph.Nodes[targetIndex];
                        if (target == null || !_nodes.TryGetValue(target.StableId, out ProgressionNodeElement targetNode))
                            continue;

                        IReadOnlyList<ProgressionPrerequisiteEntryDefinition> entries = target.Prerequisites.Entries;
                        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                        {
                            ProgressionPrerequisiteEntryDefinition entry = entries[entryIndex];
                            if (entry == null || !_nodes.TryGetValue(entry.NodeId, out ProgressionNodeElement sourceNode))
                                continue;

                            Edge edge = sourceNode.OutputPort.ConnectTo(targetNode.InputPort);
                            edge.userData = new ProgressionEdgeData(sourceNode.NodeId, targetNode.NodeId);
                            AddElement(edge);
                        }
                    }
                }

                _rebuilding = false;
            }

            public void RefreshNode(int nodeIndex)
            {
                foreach (ProgressionNodeElement node in _nodes.Values)
                {
                    if (node.NodeIndex != nodeIndex)
                        continue;

                    ProgressionNodeDefinition definition = _owner._graph.Nodes[nodeIndex];
                    node.Refresh(definition, _owner._graph.GetNodeBranches(definition));
                    return;
                }
            }

            public void RefreshAllNodes()
            {
                if (_owner._graph == null)
                    return;

                foreach (ProgressionNodeElement node in _nodes.Values)
                {
                    ProgressionNodeDefinition definition = _owner._graph.Nodes[node.NodeIndex];
                    node.Refresh(definition, _owner._graph.GetNodeBranches(definition));
                }
            }

            public Vector2 CanvasToGraphPosition(Vector2 canvasPosition)
            {
                return this.ChangeCoordinatesTo(contentViewContainer, canvasPosition);
            }

            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                List<Port> compatible = new();
                ProgressionNodeElement startNode = startPort.node as ProgressionNodeElement;
                ports.ForEach(port =>
                {
                    if (port == startPort || port.direction == startPort.direction || port.node == startPort.node)
                        return;

                    ProgressionNodeElement otherNode = port.node as ProgressionNodeElement;
                    if (startNode == null || otherNode == null)
                        return;

                    string sourceId = startPort.direction == Direction.Output ? startNode.NodeId : otherNode.NodeId;
                    string targetId = startPort.direction == Direction.Output ? otherNode.NodeId : startNode.NodeId;
                    if (!_owner.HasConnection(sourceId, targetId))
                        compatible.Add(port);
                });
                return compatible;
            }

            private GraphViewChange HandleGraphViewChanged(GraphViewChange change)
            {
                if (_rebuilding)
                    return change;

                if (change.movedElements != null)
                {
                    for (int index = 0; index < change.movedElements.Count; index++)
                    {
                        if (change.movedElements[index] is ProgressionNodeElement node)
                            _owner.SaveNodePosition(node.NodeIndex, node.GetPosition().position);
                    }
                }

                if (change.elementsToRemove != null)
                {
                    List<int> nodeIndices = new();
                    for (int index = 0; index < change.elementsToRemove.Count; index++)
                    {
                        GraphElement element = change.elementsToRemove[index];
                        if (element is Edge edge && edge.userData is ProgressionEdgeData edgeData)
                            _owner.RemovePrerequisite(edgeData.SourceNodeId, edgeData.TargetNodeId);
                        else if (element is ProgressionNodeElement node)
                            nodeIndices.Add(node.NodeIndex);
                    }

                    nodeIndices.Sort((left, right) => right.CompareTo(left));
                    for (int index = 0; index < nodeIndices.Count; index++)
                        _owner.DeleteNode(nodeIndices[index]);
                }

                return change;
            }

            private void HandlePointerMove(PointerMoveEvent evt)
            {
                foreach (ProgressionNodeElement node in _nodes.Values)
                    node.RefreshPortHover(evt.position);
            }
        }

        private sealed class ProgressionNodeElement : Node
        {
            private readonly Action<int> _selectionChanged;
            private readonly Label _details;

            public ProgressionNodeElement(
                int nodeIndex,
                ProgressionNodeDefinition definition,
                IReadOnlyList<ProgressionBranchDefinition> branches,
                IEdgeConnectorListener edgeListener,
                Action<int> selectionChanged)
            {
                NodeIndex = nodeIndex;
                NodeId = definition.StableId;
                _selectionChanged = selectionChanged;
                style.width = NodeWidth;
                style.minHeight = 0f;
                style.overflow = Overflow.Visible;
                style.borderLeftWidth = 0f;
                style.borderRightWidth = 0f;
                style.borderTopWidth = 0f;
                style.borderBottomWidth = 0f;
                mainContainer.style.overflow = Overflow.Visible;
                VisualElement nodeBody = this.Q<VisualElement>("node-border");
                nodeBody.style.overflow = Overflow.Visible;

                bool acceptsPrerequisites = definition.Tier > 1 || definition.Prerequisites.Entries.Count > 0;
                if (acceptsPrerequisites)
                {
                    InputPort = CreatePort(Direction.Input, edgeListener);
                    InputPort.portName = string.Empty;
                    nodeBody.Add(InputPort);
                    PositionPort(InputPort, true);
                }

                OutputPort = CreatePort(Direction.Output, edgeListener);
                OutputPort.portName = string.Empty;
                nodeBody.Add(OutputPort);
                PositionPort(OutputPort, false);

                _details = new Label();
                _details.style.whiteSpace = WhiteSpace.Normal;
                extensionContainer.Add(_details);
                Refresh(definition, branches);
                RefreshExpandedState();
                RefreshPorts();
            }

            public int NodeIndex { get; }
            public string NodeId { get; }
            public Port InputPort { get; }
            public Port OutputPort { get; }

            public override void OnSelected()
            {
                base.OnSelected();
                _selectionChanged?.Invoke(NodeIndex);
            }

            public void RefreshPortHover(Vector2 pointerPosition)
            {
                RefreshPortHover(InputPort, pointerPosition);
                RefreshPortHover(OutputPort, pointerPosition);
            }

            public void Refresh(
                ProgressionNodeDefinition definition,
                IReadOnlyList<ProgressionBranchDefinition> branches)
            {
                title = definition.DisplayName;
                string branchName = branches.Count switch
                {
                    0 => "Unresolved",
                    1 => branches[0].DisplayName,
                    _ => string.Join(" + ", branches.Select(branch => branch.DisplayName))
                };
                string rule = definition.Prerequisites.Entries.Count > 1
                    ? $" | {definition.Prerequisites.Mode}"
                    : string.Empty;
                _details.text = $"{branchName}\nTier {definition.Tier}  |  Rank {definition.MaxRank}{rule}";
                Color color = BlendBranchColors(branches);
                titleContainer.style.borderLeftWidth = 0f;
                titleContainer.style.borderBottomWidth = 3f;
                titleContainer.style.borderBottomColor = color;
            }

            private static Color BlendBranchColors(IReadOnlyList<ProgressionBranchDefinition> branches)
            {
                if (branches.Count == 0)
                    return new Color(0.45f, 0.48f, 0.52f, 1f);

                Color color = Color.clear;
                for (int index = 0; index < branches.Count; index++)
                    color += branches[index].Color;
                return color / branches.Count;
            }

            private static Port CreatePort(Direction direction, IEdgeConnectorListener listener)
            {
                Port port = Port.Create<Edge>(Orientation.Vertical, direction, Port.Capacity.Multi, typeof(bool));
                port.AddManipulator(new EdgeConnector<Edge>(listener));
                port.tooltip = direction == Direction.Input ? "Prerequisite input" : "Dependent output";

                Label connectorText = port.Q<Label>("type");
                connectorText.style.display = DisplayStyle.None;

                VisualElement connector = port.Q<VisualElement>("connector");
                connector.style.position = Position.Absolute;
                connector.style.left = 0f;
                connector.style.top = 0f;
                connector.style.width = PortSize;
                connector.style.height = PortSize;
                connector.style.marginLeft = 0f;
                connector.style.marginRight = 0f;
                connector.style.marginTop = 0f;
                connector.style.marginBottom = 0f;
                connector.style.backgroundColor = Color.clear;
                connector.style.borderLeftWidth = 0f;
                connector.style.borderRightWidth = 0f;
                connector.style.borderTopWidth = 0f;
                connector.style.borderBottomWidth = 0f;

                VisualElement cap = port.Q<VisualElement>("cap");
                cap.style.position = Position.Absolute;
                cap.style.left = 4f;
                cap.style.top = 4f;
                cap.style.width = 8f;
                cap.style.height = 8f;
                cap.style.marginLeft = 0f;
                cap.style.marginRight = 0f;
                cap.style.marginTop = 0f;
                cap.style.marginBottom = 0f;
                cap.style.backgroundColor = new Color(0.50f, 0.52f, 0.55f, 1f);
                cap.style.borderLeftWidth = 1f;
                cap.style.borderRightWidth = 1f;
                cap.style.borderTopWidth = 1f;
                cap.style.borderBottomWidth = 1f;
                cap.style.borderLeftColor = new Color(0.78f, 0.80f, 0.82f, 1f);
                cap.style.borderRightColor = new Color(0.78f, 0.80f, 0.82f, 1f);
                cap.style.borderTopColor = new Color(0.78f, 0.80f, 0.82f, 1f);
                cap.style.borderBottomColor = new Color(0.78f, 0.80f, 0.82f, 1f);
                cap.style.borderTopLeftRadius = 4f;
                cap.style.borderTopRightRadius = 4f;
                cap.style.borderBottomLeftRadius = 4f;
                cap.style.borderBottomRightRadius = 4f;

                port.RegisterCallback<PointerEnterEvent>(_ => SetPortHovered(cap, true));
                port.RegisterCallback<PointerLeaveEvent>(_ => SetPortHovered(cap, false));
                return port;
            }

            private static void SetPortHovered(VisualElement cap, bool hovered)
            {
                float size = hovered ? 10f : 8f;
                float offset = (PortSize - size) * 0.5f;
                Color fill = hovered
                    ? new Color(0.72f, 0.74f, 0.78f, 1f)
                    : new Color(0.50f, 0.52f, 0.55f, 1f);
                Color border = hovered
                    ? new Color(0.95f, 0.96f, 0.98f, 1f)
                    : new Color(0.78f, 0.80f, 0.82f, 1f);

                cap.style.left = offset;
                cap.style.top = offset;
                cap.style.width = size;
                cap.style.height = size;
                cap.style.backgroundColor = fill;
                cap.style.borderLeftColor = border;
                cap.style.borderRightColor = border;
                cap.style.borderTopColor = border;
                cap.style.borderBottomColor = border;
                cap.style.borderTopLeftRadius = size * 0.5f;
                cap.style.borderTopRightRadius = size * 0.5f;
                cap.style.borderBottomLeftRadius = size * 0.5f;
                cap.style.borderBottomRightRadius = size * 0.5f;
            }

            private static void RefreshPortHover(Port port, Vector2 pointerPosition)
            {
                if (port == null)
                    return;

                VisualElement cap = port.Q<VisualElement>("cap");
                SetPortHovered(cap, port.worldBound.Contains(pointerPosition));
            }

            private static void PositionPort(Port port, bool input)
            {
                port.style.position = Position.Absolute;
                port.style.left = (NodeWidth - PortSize) * 0.5f;
                port.style.width = PortSize;
                port.style.height = PortSize;
                port.style.marginLeft = 0f;
                port.style.marginRight = 0f;
                port.style.marginTop = 0f;
                port.style.marginBottom = 0f;
                port.BringToFront();

                if (input)
                    port.style.top = -PortSize * 0.5f;
                else
                    port.style.bottom = -PortSize * 0.5f;
            }
        }

        private sealed class ProgressionEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly ProgressionGraphCanvas _canvas;
            private readonly ProgressionGraphEditorWindow _owner;

            public ProgressionEdgeConnectorListener(
                ProgressionGraphCanvas canvas,
                ProgressionGraphEditorWindow owner)
            {
                _canvas = canvas;
                _owner = owner;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                if (edge.output?.node is not ProgressionNodeElement source)
                    return;

                _owner.OpenNodeSearch(position, source.NodeId);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.output?.node is not ProgressionNodeElement source ||
                    edge.input?.node is not ProgressionNodeElement target)
                    return;

                edge.output.Connect(edge);
                edge.input.Connect(edge);
                edge.userData = new ProgressionEdgeData(source.NodeId, target.NodeId);
                graphView.AddElement(edge);
                _owner.AddPrerequisite(source.NodeId, target.NodeId);
            }
        }

        private sealed class ProgressionNodeSearchProvider : ScriptableObject, ISearchWindowProvider
        {
            private ProgressionGraphEditorWindow _owner;
            private Vector2 _graphPosition;
            private string _sourceNodeId = string.Empty;

            public void Initialize(ProgressionGraphEditorWindow owner)
            {
                _owner = owner;
            }

            public void Prepare(Vector2 graphPosition, string sourceNodeId)
            {
                _graphPosition = graphPosition;
                _sourceNodeId = sourceNodeId ?? string.Empty;
            }

            public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
            {
                List<SearchTreeEntry> entries = new()
                {
                    new SearchTreeGroupEntry(new GUIContent("Create Progression Node"), 0)
                };

                if (!string.IsNullOrEmpty(_sourceNodeId))
                {
                    entries.Add(CreateEntry("Create Node", string.Empty));
                    return entries;
                }

                IReadOnlyList<ProgressionBranchDefinition> branches = _owner.GetBranches();
                if (branches == null)
                    return entries;

                for (int index = 0; index < branches.Count; index++)
                {
                    ProgressionBranchDefinition branch = branches[index];
                    if (branch != null)
                        entries.Add(CreateEntry($"Create {branch.DisplayName} Root", branch.StableId));
                }
                return entries;
            }

            public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
            {
                if (entry.userData is not string branchId)
                    return false;

                _owner.CreateNode(_graphPosition, branchId, _sourceNodeId);
                return true;
            }

            private static SearchTreeEntry CreateEntry(string label, string branchId)
            {
                return new SearchTreeEntry(new GUIContent(label))
                {
                    level = 1,
                    userData = branchId
                };
            }
        }

        private readonly struct ProgressionEdgeData
        {
            public ProgressionEdgeData(string sourceNodeId, string targetNodeId)
            {
                SourceNodeId = sourceNodeId;
                TargetNodeId = targetNodeId;
            }

            public string SourceNodeId { get; }
            public string TargetNodeId { get; }
        }
    }
}
