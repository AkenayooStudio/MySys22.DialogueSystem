using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public class ConditionNodeView : Node
    {
        public string NodeId { get; private set; }

        public const string TruePort = "true";
        public const string FalsePort = "false";

        private Port _inputPort;
        private Port _truePort;
        private Port _falsePort;

        public ConditionNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("condition-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(int));
            _inputPort.portName = "Function";
            inputContainer.Add(_inputPort);

            var hint = new Label("Routes on the function result");
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.7f, 0.7f, 0.7f);
            hint.style.marginTop = 4;
            extensionContainer.Add(hint);

            _truePort = MakeOutputPort("True", TruePort);
            _falsePort = MakeOutputPort("False", FalsePort);

            RefreshExpandedState();
        }

        private Port MakeOutputPort(string label, string guid)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(int));
            port.portName = label;
            port.userData = guid;
            port.tooltip = $"Node played when the function closes with {label}.";
            port.style.marginTop = 2;
            outputContainer.Add(port);
            return port;
        }

        public void SetData(NodeData data)
        {

        }

        public NodeData GetData()
        {
            return new NodeData
            {
                Id = NodeId,
                Type = "condition",
                Position = new PositionData
                {
                    X = GetPosition().x,
                    Y = GetPosition().y
                }
            };
        }

        public Port GetInputPort() => _inputPort;
        public Port GetTruePort() => _truePort;
        public Port GetFalsePort() => _falsePort;
    }
}
