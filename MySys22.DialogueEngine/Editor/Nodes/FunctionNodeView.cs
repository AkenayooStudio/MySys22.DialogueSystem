using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public class FunctionNodeView : Node
    {
        public string NodeId { get; private set; }

        private TextField _actionField;
        private Toggle _waitToggle;
        private VisualElement _paramsContainer;
        private Port _inputPort;
        private Port _outputPort;

        private readonly List<ParamRow> _paramRows = new List<ParamRow>();

        private sealed class ParamRow
        {
            public VisualElement Container;
            public TextField Key;
            public TextField Value;
        }

        public FunctionNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("function-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(int));
            _inputPort.portName = "In";
            inputContainer.Add(_inputPort);

            var mainContainer = new VisualElement();
            mainContainer.style.marginTop = 8;

            _actionField = new TextField("Action") { value = "LogDebug" };
            mainContainer.Add(_actionField);

            _waitToggle = new Toggle("Wait for completion") { value = false };
            _waitToggle.tooltip =
                "true: the dialogue resumes only when the action finishes (missions, cinematics, loads).\n" +
                "false: fire and forget, the graph continues immediately.";
            _waitToggle.style.marginTop = 4;
            mainContainer.Add(_waitToggle);

            var paramsHeader = new Label("Parameters");
            paramsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            paramsHeader.style.marginTop = 6;
            mainContainer.Add(paramsHeader);

            _paramsContainer = new VisualElement();
            mainContainer.Add(_paramsContainer);

            var addParam = new Button(() => AddParamRow("", "")) { text = "+ param" };
            addParam.style.marginTop = 4;
            mainContainer.Add(addParam);

            mainContainer.Add(new VisualElement { style = { height = 8 } });

            _outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(int));
            _outputPort.portName = "Out";
            outputContainer.Add(_outputPort);

            extensionContainer.Add(mainContainer);
            RefreshExpandedState();
        }

        private void AddParamRow(string key, string value)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 2;

            var keyField = new TextField { value = key ?? "" };
            keyField.style.width = 90;
            keyField.style.marginRight = 4;
            keyField.tooltip = "Parameter name, for example missionId";

            var valueField = new TextField { value = value ?? "" };
            valueField.style.flexGrow = 1;
            valueField.tooltip = "Parameter value";

            var remove = new Button(() =>
            {
                _paramsContainer.Remove(container);
                _paramRows.RemoveAll(r => r.Container == container);
                RefreshExpandedState();
            })
            { text = "✕" };
            remove.style.width = 20;

            container.Add(keyField);
            container.Add(valueField);
            container.Add(remove);
            _paramsContainer.Add(container);

            _paramRows.Add(new ParamRow { Container = container, Key = keyField, Value = valueField });
            RefreshExpandedState();
        }

        private void ClearParamRows()
        {
            _paramsContainer.Clear();
            _paramRows.Clear();
        }

        public Dictionary<string, string> GetParameters()
        {
            var result = new Dictionary<string, string>();
            foreach (ParamRow row in _paramRows)
            {
                string key = row.Key.value == null ? "" : row.Key.value.Trim();
                if (string.IsNullOrEmpty(key)) continue;
                result[key] = row.Value.value ?? "";
            }
            return result;
        }

        public void SetData(NodeData data)
        {
            _actionField.value = data.Action ?? "";
            _waitToggle.value = data.Wait;

            ClearParamRows();
            if (data.Params != null)
            {
                foreach (var pair in data.Params)
                    AddParamRow(pair.Key, pair.Value);
            }
        }

        public NodeData GetData()
        {
            return new NodeData
            {
                Id = NodeId,
                Type = "function",
                Action = string.IsNullOrEmpty(_actionField.value) ? null : _actionField.value.Trim(),
                Wait = _waitToggle.value,
                Params = GetParameters(),
                Position = new PositionData
                {
                    X = GetPosition().x,
                    Y = GetPosition().y
                }
            };
        }

        public Port GetOutputPort() => _outputPort;
    }
}
