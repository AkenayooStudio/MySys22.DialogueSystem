using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public class JumpNodeView : Node
    {
        public string NodeId { get; private set; }

        private const string SameGraph = "(same graph)";

        private PopupField<string> _graphDropdown;
        private TextField _customGraphField;
        private TextField _returnField;
        private Toggle _returnToggle;
        private Port _inputPort;

        private readonly List<string> _graphOptions = new List<string>();

        public JumpNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("jump-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(int));
            _inputPort.portName = "In";
            inputContainer.Add(_inputPort);

            var container = new VisualElement();
            container.style.marginTop = 8;

            LoadGraphOptions();
            _graphDropdown = new PopupField<string>("Target graph", _graphOptions, 0);
            _graphDropdown.tooltip =
                "Graph to continue in. '(same graph)' jumps inside this graph.\n" +
                "Add the file in StreamingAssets/" + DialoguePaths.GraphsFolderName + ".";
            _graphDropdown.style.marginBottom = 4;
            container.Add(_graphDropdown);

            _customGraphField = new TextField("or .yaml") { value = "" };
            _customGraphField.tooltip =
                "Type a graph file name when it does not exist yet (for example chapter_03.graph.yaml).\n" +
                "It overrides the dropdown above.";
            _customGraphField.style.marginBottom = 4;
            container.Add(_customGraphField);

            _returnField = new TextField("Return to") { value = "" };
            _returnField.tooltip =
                "Node of THIS graph resumed when every dialogue of the target graph is finished.";
            _returnField.style.marginBottom = 4;
            _returnField.style.display = DisplayStyle.None;
            container.Add(_returnField);

            _returnToggle = new Toggle("Return here") { value = false };
            _returnToggle.tooltip =
                "true: when every dialogue of the target graph is finished, the dialogue resumes at " +
                "'Return to' in this graph.\nfalse: the target graph replaces this one and the " +
                "dialogue ends with it.";
            _returnToggle.RegisterValueChangedCallback(evt =>
                _returnField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
            container.Add(_returnToggle);

            extensionContainer.Add(container);
            RefreshExpandedState();
        }

        private void LoadGraphOptions()
        {
            _graphOptions.Clear();
            _graphOptions.Add(SameGraph);

            foreach (string file in DialogueProjectLoader.ListGraphFiles())
            {
                string name = Path.GetFileName(file);
                if (!_graphOptions.Contains(name)) _graphOptions.Add(name);
            }
        }

        public void SetData(NodeData data)
        {
            string graph = string.IsNullOrWhiteSpace(data.Graph) ? SameGraph : data.Graph.Trim();
            if (!_graphOptions.Contains(graph))
            {
                _graphOptions.Add(graph);
                _graphDropdown.choices = new List<string>(_graphOptions);
            }
            _graphDropdown.value = graph;

            _customGraphField.value = _graphOptions.Contains(graph) && graph != SameGraph ? "" : data.Graph ?? "";

            _returnField.value = data.Next ?? "";
            _returnToggle.value = data.Return;
            _returnField.style.display = data.Return ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public NodeData GetData()
        {
            string custom = _customGraphField.value == null ? "" : _customGraphField.value.Trim();
            string graph = string.IsNullOrEmpty(custom) ? _graphDropdown?.value : custom;
            string returnTo = _returnField.value == null ? "" : _returnField.value.Trim();

            return new NodeData
            {
                Id = NodeId,
                Type = "jump",
                Graph = graph == SameGraph || string.IsNullOrEmpty(graph) ? null : graph,
                Next = string.IsNullOrEmpty(returnTo) ? null : returnTo,
                Return = _returnToggle.value,
                Position = new PositionData
                {
                    X = GetPosition().x,
                    Y = GetPosition().y
                }
            };
        }
    }
}
