using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{
    public class DialogueGraphWindow : EditorWindow
    {
        private const string LAST_GRAPH_PATH_KEY = "MySys22_Dialogue_LastGraphPath";

        private DialogueGraphView _graphView;
        private Label _graphPathLabel;
        private VisualElement _root;

        private double _nextReloadTime = 0;
        private const double RELOAD_INTERVAL = 1.0;

        [MenuItem("MySys22/Dialogue/Dialogue Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueGraphWindow>();
            window.titleContent = new GUIContent("Dialogue Graph Editor");
            window.minSize = new Vector2(800, 600);
        }

        void OnEnable()
        {
            DialoguePaths.EnsureLayout();
            _root = rootVisualElement;
            _root.style.backgroundColor = new Color(0.137f, 0.137f, 0.137f);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/MySys22.DialogueEngine/Editor/DialogueGraphWindow.uss");
            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            CreateMenuBar();
            CreateGraphView();

            UpdatePathLabel();

            string lastPath = EditorPrefs.GetString(LAST_GRAPH_PATH_KEY, "");
            if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath))
                lastPath = DialogueProjectLoader.FindFirstGraphFile();

            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
            {
                _graphView?.LoadGraph(lastPath);
                EditorPrefs.SetString(LAST_GRAPH_PATH_KEY, lastPath);
            }
        }

        void OnDisable()
        {
            if (_graphView != null)
            {
                _graphView.OnGraphChanged -= UpdatePathLabel;
                _graphView = null;
            }
        }

        void Update()
        {
            if (!EditorApplication.isPlaying) return;

            if (EditorApplication.timeSinceStartup < _nextReloadTime) return;
            _nextReloadTime = EditorApplication.timeSinceStartup + RELOAD_INTERVAL;

            _graphView?.ReloadLines();
            _graphView?.RefreshSpeakers();
        }

        private void CreateMenuBar()
        {
            var menuBar = new VisualElement();
            menuBar.style.flexDirection = FlexDirection.Row;
            menuBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            menuBar.style.paddingLeft = 8;
            menuBar.style.paddingRight = 8;
            menuBar.style.paddingTop = 4;
            menuBar.style.paddingBottom = 4;
            menuBar.style.borderBottomColor = new Color(0.08f, 0.08f, 0.08f);
            menuBar.style.borderBottomWidth = 1;
            menuBar.style.minHeight = 24;

            var fileButton = new Button(() => ShowFileMenu()) { text = "File" };
            fileButton.style.backgroundColor = Color.clear;
            fileButton.style.color = Color.white;
            fileButton.style.borderLeftWidth = 0;
            fileButton.style.borderRightWidth = 0;
            fileButton.style.borderTopWidth = 0;
            fileButton.style.borderBottomWidth = 0;
            fileButton.style.marginRight = 12;
            fileButton.style.fontSize = 12;
            menuBar.Add(fileButton);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            menuBar.Add(spacer);

            _graphPathLabel = new Label("No graph loaded");
            _graphPathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _graphPathLabel.style.fontSize = 11;
            _graphPathLabel.style.alignSelf = Align.Center;
            _graphPathLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            menuBar.Add(_graphPathLabel);

            _root.Add(menuBar);
        }

        private void ShowFileMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("New"), false, () => NewGraph());
            menu.AddItem(new GUIContent("Load"), false, () => LoadGraph());
            menu.AddItem(new GUIContent("Save"), false, () => SaveGraph());
            menu.AddItem(new GUIContent("Save As..."), false, () => SaveGraphAs());
            menu.ShowAsContext();
        }

        private void CreateGraphView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.backgroundColor = new Color(0.137f, 0.137f, 0.137f);
            _root.Add(container);

            _graphView = new DialogueGraphView();
            _graphView.StretchToParentSize();
            container.Add(_graphView);
            _graphView.OnGraphChanged += UpdatePathLabel;
        }

        private void UpdatePathLabel()
        {
            if (_graphPathLabel == null) return;
            if (_graphView != null && _graphView.HasGraphPath())
            {
                string path = _graphView.GetGraphPath();
                _graphPathLabel.text = Path.GetFileName(path);
                _graphPathLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
            }
            else
            {
                _graphPathLabel.text = "No graph loaded";
                _graphPathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }

        private void NewGraph()
        {
            _graphView?.NewGraph();
            EditorPrefs.SetString(LAST_GRAPH_PATH_KEY, "");
        }

        private void LoadGraph()
        {
            DialoguePaths.EnsureLayout();
            string path = EditorUtility.OpenFilePanel(
                "Load Dialogue Graph",
                DialoguePaths.Graphs,
                "yaml"
            );
            if (!string.IsNullOrEmpty(path))
            {
                _graphView?.LoadGraph(path);
                EditorPrefs.SetString(LAST_GRAPH_PATH_KEY, path);
            }
        }

        private void SaveGraph()
        {
            if (_graphView == null) return;
            if (!_graphView.HasGraphPath()) { SaveGraphAs(); return; }
            _graphView.SaveGraph();
            string path = _graphView.GetGraphPath();
            if (!string.IsNullOrEmpty(path))
                EditorPrefs.SetString(LAST_GRAPH_PATH_KEY, path);
        }

        private void SaveGraphAs()
        {
            DialoguePaths.EnsureLayout();
            string path = EditorUtility.SaveFilePanel(
                "Save Dialogue Graph",
                DialoguePaths.Graphs,
                "new_graph",
                "yaml"
            );
            if (!string.IsNullOrEmpty(path))
            {
                _graphView?.SaveGraph(path);
                EditorPrefs.SetString(LAST_GRAPH_PATH_KEY, path);
            }
        }
    }
}
