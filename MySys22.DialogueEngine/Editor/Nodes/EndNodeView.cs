using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{
    public class EndNodeView : Node
    {
        public string NodeId { get; private set; }
        private Port _inputPort;

        public EndNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("end-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(int));
            _inputPort.portName = "In";
            inputContainer.Add(_inputPort);

            RefreshExpandedState();
        }

        public NodeData GetData()
        {
            return new NodeData
            {
                Id = NodeId,
                Type = "end",
                Position = new PositionData
                {
                    X = GetPosition().x,
                    Y = GetPosition().y
                }
            };
        }
    }
}
