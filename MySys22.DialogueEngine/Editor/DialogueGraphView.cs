using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using MySys22.DialogueEngine.Core;
using System.IO;

namespace MySys22.DialogueEngine.Editor
{
    public class DialogueGraphView : GraphView
    {
        private string _currentGraphPath;
        private GraphData _currentGraphData;
        public event System.Action OnGraphChanged;

        private List<(string sourceId, string portGuid, string targetId)> _connections = new List<(string, string, string)>();
        private string _startNodeId;

        public DialogueGraphView()
        {
            SetupZoom(0.5f, 2f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            grid.style.backgroundColor = new Color(0.137f, 0.137f, 0.137f);
            grid.StretchToParentSize();
            Insert(0, grid);

            graphViewChanged += OnGraphViewChanged;
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenu);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null)
                foreach (var edge in change.edgesToCreate)
                    RegisterConnection(edge);

            if (change.elementsToRemove != null)
                foreach (var element in change.elementsToRemove)
                    if (element is Edge edge)
                        UnregisterConnection(edge);

            return change;
        }

        private void RegisterConnection(Edge edge)
        {
            if (edge == null) return;
            var sourceNode = edge.output.node as Node;
            var targetNode = edge.input.node as Node;
            if (sourceNode == null || targetNode == null) return;

            string sourceId = GetNodeId(sourceNode);
            string targetId = GetNodeId(targetNode);
            string portGuid = edge.output.userData as string ?? "default";

            _connections.RemoveAll(c => c.sourceId == sourceId && c.portGuid == portGuid);
            _connections.Add((sourceId, portGuid, targetId));
        }

        private void UnregisterConnection(Edge edge)
        {
            if (edge == null) return;
            var sourceNode = edge.output.node as Node;
            if (sourceNode == null) return;
            string sourceId = GetNodeId(sourceNode);
            string portGuid = edge.output.userData as string ?? "default";
            _connections.RemoveAll(c => c.sourceId == sourceId && c.portGuid == portGuid);
        }

        private string GetNodeId(Node node)
        {
            if (node is DialogueNodeView d) return d.NodeId;
            if (node is ChoiceNodeView c) return c.NodeId;
            if (node is FunctionNodeView f) return f.NodeId;
            if (node is JumpNodeView j) return j.NodeId;
            if (node is ConditionNodeView cond) return cond.NodeId;
            if (node is EndNodeView e) return e.NodeId;
            return null;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (port == startPort || port.direction == startPort.direction) continue;

                if (port.node is ConditionNodeView && startPort.node is not FunctionNodeView) continue;

                compatiblePorts.Add(port);
            }
            return compatiblePorts;
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add Dialogue Node", (e) => CreateDialogueNode(evt.mousePosition));
            evt.menu.AppendAction("Add Choice Node", (e) => CreateChoiceNode(evt.mousePosition));
            evt.menu.AppendAction("Add Function Node", (e) => CreateFunctionNode(evt.mousePosition));
            evt.menu.AppendAction("Add Condition Node", (e) => CreateConditionNode(evt.mousePosition));
            evt.menu.AppendAction("Add Jump Node", (e) => CreateJumpNode(evt.mousePosition));
            evt.menu.AppendAction("Add End Node", (e) => CreateEndNode(evt.mousePosition));
            evt.menu.AppendSeparator();

            Node targetNode = GetNodeAtMouse(evt.mousePosition);
            evt.menu.AppendAction("Set as Start Node", (e) => SetStartNode(targetNode), (e) => CanSetStartNode(targetNode));
            evt.menu.AppendAction("Clear Start Node", (e) => ClearStartNode(), (e) => string.IsNullOrEmpty(_startNodeId) ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Paste Nodes", (e) => PasteNodes());
        }

        private Node GetNodeAtMouse(Vector2 mousePosition)
        {
            var selected = selection.OfType<Node>().ToList();
            return selected.Count == 1 ? selected[0] : selected.FirstOrDefault();
        }

        private void SetStartNode(Node node)
        {
            if (node == null || node is EndNodeView) return;

            _startNodeId = GetNodeId(node);

            foreach (var n in nodes)
            {
                n.style.borderBottomWidth = 0;
                n.style.borderTopWidth = 0;
                n.style.borderLeftWidth = 0;
                n.style.borderRightWidth = 0;
                n.style.borderBottomColor = new StyleColor(Color.gray);
                n.style.borderTopColor = new StyleColor(Color.gray);
                n.style.borderLeftColor = new StyleColor(Color.gray);
                n.style.borderRightColor = new StyleColor(Color.gray);
            }

            if (!string.IsNullOrEmpty(_startNodeId))
            {
                var startNode = nodes.FirstOrDefault(n => GetNodeId(n) == _startNodeId);
                if (startNode != null)
                {
                    startNode.style.borderBottomColor = new StyleColor(Color.green);
                    startNode.style.borderBottomWidth = 4;
                    startNode.style.borderTopColor = new StyleColor(Color.green);
                    startNode.style.borderTopWidth = 4;
                    startNode.style.borderLeftColor = new StyleColor(Color.green);
                    startNode.style.borderLeftWidth = 4;
                    startNode.style.borderRightColor = new StyleColor(Color.green);
                    startNode.style.borderRightWidth = 4;
                }
            }

            OnGraphChanged?.Invoke();
        }

        private void ClearStartNode()
        {
            _startNodeId = null;
            foreach (var n in nodes)
            {
                n.style.borderBottomWidth = 0;
                n.style.borderTopWidth = 0;
                n.style.borderLeftWidth = 0;
                n.style.borderRightWidth = 0;
            }
            OnGraphChanged?.Invoke();
        }

        private DropdownMenuAction.Status CanSetStartNode(Node node)
        {
            if (node == null || node is EndNodeView) return DropdownMenuAction.Status.Disabled;
            return DropdownMenuAction.Status.Normal;
        }

        private void CreateDialogueNode(Vector2? position = null)
        {
            var node = new DialogueNodeView("Dialogue", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(200, 150)));
            AddElement(node);
        }

        private void CreateChoiceNode(Vector2? position = null)
        {
            var node = new ChoiceNodeView("Choice", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(200, 150)));
            AddElement(node);
        }

        private void CreateFunctionNode(Vector2? position = null)
        {
            var node = new FunctionNodeView("Function", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(200, 150)));
            AddElement(node);
        }

        private void CreateConditionNode(Vector2? position = null)
        {
            var node = new ConditionNodeView("Condition", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(240, 150)));
            AddElement(node);
        }

        private void CreateJumpNode(Vector2? position = null)
        {
            var node = new JumpNodeView("Jump", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(220, 150)));
            AddElement(node);
        }

        private void CreateEndNode(Vector2? position = null)
        {
            var node = new EndNodeView("End", GetNewNodeId());
            if (position.HasValue) node.SetPosition(new Rect(position.Value, new Vector2(120, 80)));
            AddElement(node);
        }

        private void PasteNodes() { }

        private string GetNewNodeId() => System.Guid.NewGuid().ToString("N").Substring(0, 8);

        public void NewGraph()
        {
            ClearGraph();
            _connections.Clear();
            _startNodeId = null;
            _currentGraphPath = null;
            _currentGraphData = null;
            OnGraphChanged?.Invoke();
        }

        public void LoadGraph(string path)
        {
            try
            {
                var data = GraphYamlParser.LoadFromFile(path);
                if (data == null)
                {
                    DialogueLogger.LogError("318", "Load graph error", $"Empty graph: {path}");
                    return;
                }

                if (string.IsNullOrWhiteSpace(data.GraphId))
                {
                    data.GraphId = DeriveGraphId(path);
                    DialogueLogger.Log($"Graph had no id; using '{data.GraphId}' derived from the file name.");
                }

                _currentGraphData = data;
                _currentGraphPath = path;
                PopulateGraph(data);
                OnGraphChanged?.Invoke();
                DialogueLogger.Log($"Graph loaded: {data.GraphId}, nodes: {data.Nodes.Count}");
            }
            catch (System.Exception ex)
            {
                DialogueLogger.LogError("318", "Load graph error", ex.Message);
                EditorUtility.DisplayDialog("Error", $"Cannot load graph: {ex.Message}", "OK");
            }
        }

        public void SaveGraph(string path = null)
        {
            if (!string.IsNullOrEmpty(path)) _currentGraphPath = path;
            if (string.IsNullOrEmpty(_currentGraphPath))
                _currentGraphPath = DialoguePaths.GraphFile("new_graph");

            try
            {
                var data = ExtractGraphData();
                GraphYamlParser.SaveToFile(data, _currentGraphPath);
                _currentGraphData = data;
                OnGraphChanged?.Invoke();
                DialogueLogger.Log($"Graph saved: {_currentGraphPath}");

                ManifestGenerator.WriteGraphManifest();
                DialogueDigestTools.ExportQuietly();
            }
            catch (System.Exception ex)
            {
                DialogueLogger.LogError("318", "Save error", ex.Message);
                EditorUtility.DisplayDialog("Error", $"Cannot save graph: {ex.Message}", "OK");
            }
        }

        private static string DeriveGraphId(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string name = System.IO.Path.GetFileName(path);
            name = System.Text.RegularExpressions.Regex.Replace(name, "(?i)\\.graph\\.ya?ml$", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, "(?i)\\.ya?ml$", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, "[^A-Za-z0-9]+", "_").Trim('_');
            return string.IsNullOrEmpty(name) ? null : name;
        }

        public bool HasGraphPath() => !string.IsNullOrEmpty(_currentGraphPath);
        public string GetGraphPath() => _currentGraphPath;

        public void ReloadLines()
        {
            DialogueLogger.Log("Reloading lines...");
            RefreshSpeakers();
        }

        public void RefreshSpeakers()
        {
            foreach (var node in nodes)
                if (node is DialogueNodeView dNode)
                    dNode.RefreshSpeakers();
            DialogueLogger.Log("Speakers refreshed.");
        }

        public void CompileGraph() => DialogueLogger.Log("Compile graph (not implemented)");

        private void ClearGraph()
        {
            foreach (var edge in edges.ToList()) RemoveElement(edge);
            foreach (var node in nodes.ToList()) RemoveElement(node);
            _connections.Clear();
            _startNodeId = null;
        }

        private void PopulateGraph(GraphData data)
        {
            ClearGraph();
            _connections.Clear();

            foreach (var nodeData in data.Nodes)
            {
                Node nodeView = null;
                switch (nodeData.Type)
                {
                    case "dialogue": var d = new DialogueNodeView("Dialogue", nodeData.Id); d.SetData(nodeData); nodeView = d; break;
                    case "choice":   var c = new ChoiceNodeView("Choice", nodeData.Id); c.SetData(nodeData); nodeView = c; break;
                    case "function": var f = new FunctionNodeView("Function", nodeData.Id); f.SetData(nodeData); nodeView = f; break;
                    case "condition": var condNode = new ConditionNodeView("Condition", nodeData.Id); condNode.SetData(nodeData); nodeView = condNode; break;
                    case "jump":     var j = new JumpNodeView("Jump", nodeData.Id); j.SetData(nodeData); nodeView = j; break;
                    case "end":      nodeView = new EndNodeView("End", nodeData.Id); break;
                }

                if (nodeView != null)
                {
                    if (nodeData.Position != null)
                        nodeView.SetPosition(new Rect(nodeData.Position.X, nodeData.Position.Y, 200, 150));
                    AddElement(nodeView);
                }
            }

            foreach (var nodeData in data.Nodes)
            {
                if (nodeData.Type == "function" && nodeData.Conditions != null &&
                    nodeData.Conditions.Count > 0)
                {
                    var functionNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == nodeData.Id) as FunctionNodeView;
                    if (functionNode != null)
                    {
                        foreach (string conditionId in nodeData.Conditions)
                        {
                            var conditionNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == conditionId)
                                as ConditionNodeView;
                            if (conditionNode == null) continue;

                            Port outPort = functionNode.GetOutputPort();
                            Port inPort = conditionNode.GetInputPort();
                            var edge = new Edge { output = outPort, input = inPort };
                            outPort.Connect(edge);
                            inPort.Connect(edge);
                            AddElement(edge);
                        }
                    }
                }

                if (nodeData.Type == "dialogue" || nodeData.Type == "function")
                {
                    if (!string.IsNullOrEmpty(nodeData.Next))
                    {
                        var sourceNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == nodeData.Id);
                        var targetNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == nodeData.Next);
                        if (sourceNode != null && targetNode != null)
                        {
                            Port outputPort = null;
                            if (sourceNode is DialogueNodeView d) outputPort = d.GetOutputPort();
                            else if (sourceNode is FunctionNodeView f) outputPort = f.GetOutputPort();

                            if (outputPort != null)
                            {
                                var targetPort = GetInputPort(targetNode as Node);
                                if (targetPort != null)
                                {
                                    var edge = new Edge { output = outputPort, input = targetPort };
                                    outputPort.Connect(edge);
                                    targetPort.Connect(edge);
                                    AddElement(edge);
                                    RegisterConnection(edge);
                                }
                            }
                        }
                    }
                }
                else if (nodeData.Type == "condition")
                {
                    var sourceNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == nodeData.Id);
                    if (sourceNode is ConditionNodeView conditionNode)
                    {
                        ConnectConditionBranch(conditionNode.GetTruePort(), nodeData.OnTrue);
                        ConnectConditionBranch(conditionNode.GetFalsePort(), nodeData.OnFalse);
                    }
                }
                else if (nodeData.Type == "choice")
                {
                    var sourceNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == nodeData.Id);
                    if (sourceNode is ChoiceNodeView cNode)
                    {
                        foreach (var choice in nodeData.Choices)
                        {
                            if (!string.IsNullOrEmpty(choice.Next))
                            {
                                var targetNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == choice.Next);
                                if (targetNode != null)
                                {
                                    var entry = cNode.GetChoiceEntries().Find(e => e.Guid == choice.PortGuid);
                                    if (entry != null)
                                    {
                                        var targetPort = GetInputPort(targetNode as Node);
                                        if (targetPort != null)
                                        {
                                            var edge = new Edge { output = entry.OutputPort, input = targetPort };
                                            entry.OutputPort.Connect(edge);
                                            targetPort.Connect(edge);
                                            AddElement(edge);
                                            RegisterConnection(edge);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var startNodeData = data.Nodes.FirstOrDefault(n => n.IsStart);
            if (startNodeData != null)
            {
                var nodeView = nodes.FirstOrDefault(n => GetNodeId(n) == startNodeData.Id);
                if (nodeView != null) SetStartNode(nodeView);
            }
            else
            {
                string fallbackId = FindStartNode();
                if (!string.IsNullOrEmpty(fallbackId))
                {
                    var nodeView = nodes.FirstOrDefault(n => GetNodeId(n) == fallbackId);
                    if (nodeView != null) SetStartNode(nodeView);
                }
            }

            _currentGraphData = data;
            OnGraphChanged?.Invoke();
        }

        private void ConnectConditionBranch(Port outputPort, string targetId)
        {
            if (outputPort == null || string.IsNullOrEmpty(targetId)) return;

            var targetNode = nodes.FirstOrDefault(n => GetNodeId(n as Node) == targetId);
            if (targetNode == null) return;

            var targetPort = GetInputPort(targetNode as Node);
            if (targetPort == null) return;

            var edge = new Edge { output = outputPort, input = targetPort };
            outputPort.Connect(edge);
            targetPort.Connect(edge);
            AddElement(edge);
            RegisterConnection(edge);
        }

        private Port GetInputPort(Node node)
        {
            if (node == null) return null;
            foreach (var child in node.inputContainer.Children())
                if (child is Port p && p.direction == Direction.Input)
                    return p;
            return null;
        }

        private GraphData ExtractGraphData()
        {

            string graphId = _currentGraphData?.GraphId;
            if (string.IsNullOrWhiteSpace(graphId)) graphId = DeriveGraphId(_currentGraphPath);
            if (string.IsNullOrWhiteSpace(graphId)) graphId = "graph_" + System.DateTime.Now.Ticks;

            var data = new GraphData
            {
                GraphId = graphId,
                Nodes = new List<NodeData>(),
                StartNode = null
            };

            foreach (var node in nodes)
            {
                if (node is DialogueNodeView dNode)
                {
                    var nodeData = dNode.GetData();
                    nodeData.Next = GetNextFromConnections(dNode.NodeId, "default");
                    data.Nodes.Add(nodeData);
                }
                else if (node is ChoiceNodeView cNode)
                {
                    var nodeData = cNode.GetData();
                    foreach (var entry in cNode.GetChoiceEntries())
                    {
                        string target = GetNextFromConnections(cNode.NodeId, entry.Guid);
                        var choice = nodeData.Choices.Find(c => c.PortGuid == entry.Guid);
                        if (choice != null) choice.Next = target;
                    }
                    data.Nodes.Add(nodeData);
                }
                else if (node is FunctionNodeView fNode)
                {
                    var nodeData = fNode.GetData();

                    var conditions = new List<string>();
                    string fallback = null;

                    foreach (var connection in _connections.FindAll(c => c.sourceId == fNode.NodeId))
                    {
                        if (string.IsNullOrEmpty(connection.targetId)) continue;
                        var target = nodes.FirstOrDefault(n => GetNodeId(n as Node) == connection.targetId);
                        if (target is ConditionNodeView) conditions.Add(connection.targetId);
                        else fallback = connection.targetId;
                    }

                    nodeData.Conditions = conditions;
                    nodeData.Next = fallback;
                    data.Nodes.Add(nodeData);
                }
                else if (node is ConditionNodeView condNodeView)
                {
                    var conditionData = condNodeView.GetData();
                    conditionData.OnTrue = GetNextFromConnections(condNodeView.NodeId, ConditionNodeView.TruePort);
                    conditionData.OnFalse = GetNextFromConnections(condNodeView.NodeId, ConditionNodeView.FalsePort);
                    data.Nodes.Add(conditionData);
                }
                else if (node is JumpNodeView jNode)
                {

                    data.Nodes.Add(jNode.GetData());
                }
                else if (node is EndNodeView eNode)
                {
                    data.Nodes.Add(eNode.GetData());
                }
            }

            string startId = FindStartNode();
            data.StartNode = startId;
            if (!string.IsNullOrEmpty(startId))
            {
                var startNode = data.Nodes.Find(n => n.Id == startId);
                if (startNode != null) startNode.IsStart = true;
            }

            return data;
        }

        private string GetNextFromConnections(string sourceId, string portGuid)
        {
            var conn = _connections.Find(c => c.sourceId == sourceId && c.portGuid == portGuid);
            return conn.targetId;
        }

        private string FindStartNode()
        {
            if (!string.IsNullOrEmpty(_startNodeId) && nodes.Any(n => GetNodeId(n) == _startNodeId))
                return _startNodeId;

            var nodesWithInput = new HashSet<string>();
            foreach (var conn in _connections) nodesWithInput.Add(conn.targetId);

            foreach (var node in nodes)
            {
                if (node is EndNodeView) continue;
                string id = GetNodeId(node);
                if (!string.IsNullOrEmpty(id) && !nodesWithInput.Contains(id))
                    return id;
            }

            foreach (var node in nodes)
            {
                if (!(node is EndNodeView))
                {
                    string id = GetNodeId(node);
                    if (!string.IsNullOrEmpty(id)) return id;
                }
            }
            return null;
        }
    }
}
