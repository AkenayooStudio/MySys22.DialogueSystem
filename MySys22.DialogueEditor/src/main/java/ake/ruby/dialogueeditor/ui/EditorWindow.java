package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.editor.EditorTabPane;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.SessionStore;
import ake.ruby.dialogueeditor.storage.LocalStorage;

import javafx.application.Platform;
import javafx.geometry.Rectangle2D;
import ake.ruby.dialogueeditor.editor.EditorTab;
import javafx.scene.Scene;
import javafx.scene.control.Tab;
import javafx.scene.input.KeyCode;
import javafx.scene.input.KeyCodeCombination;
import javafx.scene.input.KeyCombination;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.StackPane;
import javafx.scene.paint.Color;
import javafx.stage.Screen;
import javafx.stage.Stage;
import javafx.stage.StageStyle;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public class EditorWindow {

    public interface CloseCallback {
        void onClose();
    }

    private static final double WINDOWED_W = 1200;
    private static final double WINDOWED_H = 720;
    private static final String PROJECT_YAML = ".project.yaml";

    private static final String PANEL_CONSOLE = "console";
    private static final String PANEL_DEBUG = "debug";

    private final Stage stage;
    private final ProjectSession session;
    private final CloseCallback onClose;

    private StackPane outerStack;
    private BorderPane rootContainer;
    private BorderPane innerPane;
    private EditorTabPane tabPane;
    private LocalStorage localStorage;
    private boolean isFullscreen = true;

    private String activePanel = null;
    private ConsolePanel consolePanel;
    private DebugPanel debugPanel;
    private SftpPanel sftpPanel;
    private GitPanel gitPanel;

    public EditorWindow(Stage stage, ProjectSession session, CloseCallback onClose) {
        this.stage = stage;
        this.session = session;
        this.onClose = onClose;
    }

    public void show() {
        stage.initStyle(StageStyle.TRANSPARENT);
        stage.setTitle("MySys22 Dialogue Editor - " + session.manifest().name);
        stage.setResizable(true);

        if (session.storage() instanceof LocalStorage l) {
            this.localStorage = l;
        }

        rootContainer = new BorderPane();
        rootContainer.getStyleClass().add("root-container");
        rootContainer.getStyleClass().add("editor-window");
        rootContainer.getStyleClass().add("maximized");

        rootContainer.setTop(new EditorTitleBar(stage, this::onSettingsRequested, this::toggleFullscreen, this::onInjectRequested));

        ProjectManagerPanel projectManager = new ProjectManagerPanel(session);
        tabPane = new EditorTabPane(session);
        projectManager.setOnFileOpen(tabPane::openFile);

        RightSidebar sidebar = new RightSidebar(session, this::togglePanel);

        innerPane = new BorderPane();
        innerPane.setLeft(projectManager);
        innerPane.setCenter(tabPane);
        innerPane.setRight(sidebar);

        rootContainer.setCenter(innerPane);

        outerStack = new StackPane(rootContainer);
        outerStack.getStyleClass().add("outer-stack");

        Scene scene = new Scene(outerStack, WINDOWED_W, WINDOWED_H);
        scene.setFill(Color.TRANSPARENT);
        scene.getStylesheets().add(
                EditorWindow.class.getResource("/css/main.css").toExternalForm()
        );
        installEditShortcuts(scene);
        stage.setScene(scene);

        stage.setOnCloseRequest(e -> {
            saveSession();
            disposePanels();
            if (onClose != null) onClose.onClose();
        });

        applyFullscreen();
        stage.show();
        stage.toFront();

        Platform.runLater(this::restoreSession);
    }

    private void installEditShortcuts(Scene scene) {
        scene.getAccelerators().put(
                new KeyCodeCombination(KeyCode.Z, KeyCombination.SHORTCUT_DOWN),
                () -> runOnActiveTab(EditorTab::undo));

        scene.getAccelerators().put(
                new KeyCodeCombination(KeyCode.Y, KeyCombination.SHORTCUT_DOWN),
                () -> runOnActiveTab(EditorTab::redo));

        scene.getAccelerators().put(
                new KeyCodeCombination(KeyCode.Z, KeyCombination.SHORTCUT_DOWN, KeyCombination.SHIFT_DOWN),
                () -> runOnActiveTab(EditorTab::redo));
    }

    private void runOnActiveTab(java.util.function.Predicate<EditorTab> action) {
        if (tabPane == null) return;
        Tab selected = tabPane.getSelectionModel().getSelectedItem();
        if (selected instanceof EditorTab tab) action.test(tab);
    }

    private void togglePanel(String panelId) {
        if (panelId.equals(activePanel)) {
            hideBottomPanel();
            return;
        }

        if (PANEL_CONSOLE.equals(panelId)) {
            if (consolePanel == null) {
                Path wd = localStorage != null ? localStorage.root() : null;
                consolePanel = new ConsolePanel(wd, this::hideBottomPanel);
            }
            showBottomPanel(consolePanel);
            activePanel = PANEL_CONSOLE;
        } else if (PANEL_DEBUG.equals(panelId)) {
            if (debugPanel == null) {
                debugPanel = new DebugPanel(session, this::hideBottomPanel);
            } else {
                debugPanel.refresh();
            }
            showBottomPanel(debugPanel);
            activePanel = PANEL_DEBUG;
        } else if ("sftp".equals(panelId)) {
            if (sftpPanel == null) {
                sftpPanel = new SftpPanel(session, this::hideBottomPanel);
            }
            showBottomPanel(sftpPanel);
            activePanel = "sftp";
        } else if ("git".equals(panelId)) {
            if (gitPanel == null) {
                Path wd = localStorage != null ? localStorage.root() : null;
                gitPanel = new GitPanel(wd, this::hideBottomPanel);
            }
            showBottomPanel(gitPanel);
            activePanel = "git";
        }
    }

    private void showBottomPanel(javafx.scene.Node panel) {
        innerPane.setBottom(panel);
    }

    private void hideBottomPanel() {
        innerPane.setBottom(null);
        activePanel = null;
    }

    private void disposePanels() {
        if (consolePanel != null) consolePanel.dispose();
        if (gitPanel != null) gitPanel.dispose();
    }

    private void restoreSession() {
        if (localStorage == null) {
            Path yaml = null;
            if (session.storage() instanceof LocalStorage l) {
                yaml = l.root().resolve(PROJECT_YAML);
            }
            if (yaml != null && Files.exists(yaml)) tabPane.openFile(yaml);
            return;
        }

        Path root = localStorage.root();
        List<String> saved = SessionStore.load(session.manifest().projectId);

        boolean openedProjectYaml = false;
        for (String rel : saved) {
            if (rel == null || rel.isBlank()) continue;
            Path abs = root.resolve(rel).normalize();
            if (!abs.startsWith(root)) continue;
            if (!Files.exists(abs)) continue;
            if (Files.isDirectory(abs)) continue;

            tabPane.openFile(abs);
            if (rel.equals(PROJECT_YAML)) openedProjectYaml = true;
        }

        if (!openedProjectYaml) {
            Path yaml = root.resolve(PROJECT_YAML);
            if (Files.exists(yaml)) {
                tabPane.openFile(yaml);
                tabPane.getSelectionModel().select(0);
            }
        }
    }

    private void saveSession() {
        if (tabPane == null || localStorage == null) return;

        Path root = localStorage.root();
        List<String> rels = new ArrayList<>();

        for (Path abs : tabPane.getOpenFilePaths()) {
            if (abs == null) continue;
            if (!abs.startsWith(root)) continue;
            rels.add(root.relativize(abs).toString().replace('\\', '/'));
        }

        SessionStore.save(session.manifest().projectId, rels);
    }

    private void applyFullscreen() {
        Rectangle2D vb = Screen.getPrimary().getVisualBounds();
        stage.setX(vb.getMinX());
        stage.setY(vb.getMinY());
        stage.setWidth(vb.getWidth());
        stage.setHeight(vb.getHeight());
        isFullscreen = true;

        if (!rootContainer.getStyleClass().contains("maximized")) {
            rootContainer.getStyleClass().add("maximized");
        }
    }

    private void toggleFullscreen() {
        if (isFullscreen) {
            rootContainer.getStyleClass().remove("maximized");
            isFullscreen = false;

            stage.setWidth(WINDOWED_W);
            stage.setHeight(WINDOWED_H);

            Platform.runLater(() -> {
                Rectangle2D vb = Screen.getPrimary().getVisualBounds();
                double cx = vb.getMinX() + (vb.getWidth() - stage.getWidth()) / 2;
                double cy = vb.getMinY() + (vb.getHeight() - stage.getHeight()) / 2;
                stage.setX(cx);
                stage.setY(cy);
            });
        } else {
            applyFullscreen();
        }
    }

    private void onInjectRequested() {
        try {
            var result = ake.ruby.dialogueeditor.project.UnityDeployService.deploy(session);

            StringBuilder msg = new StringBuilder();
            msg.append("Languages:   ").append(result.languages()).append("\n");
            msg.append("Files copied: ").append(result.filesCopied()).append("\n");
            msg.append("Total lines: ").append(result.totalLines()).append("\n");
            msg.append("Target: ").append(result.targetDir());

            if (!result.warnings().isEmpty()) {
                msg.append("\n\nWarnings:\n");
                int max = Math.min(result.warnings().size(), 10);
                for (int i = 0; i < max; i++) {
                    msg.append("• ").append(result.warnings().get(i)).append("\n");
                }
                if (result.warnings().size() > max) {
                    msg.append("... and ").append(result.warnings().size() - max).append(" more.");
                }
            }

            javafx.scene.control.Alert info =
                    new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.INFORMATION);
            info.setTitle("Inject to Unity");
            info.setHeaderText("Deploy complete");
            info.setContentText(msg.toString());
            info.initOwner(stage);
            info.getDialogPane().getStylesheets().add(
                    EditorWindow.class.getResource("/css/main.css").toExternalForm());
            info.getDialogPane().getStyleClass().add("custom-dialog");
            info.showAndWait();

        } catch (Exception e) {
            javafx.scene.control.Alert err =
                    new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.ERROR);
            err.setTitle("Inject to Unity");
            err.setHeaderText("Deploy failed");
            err.setContentText(e.getMessage());
            err.initOwner(stage);
            err.getDialogPane().getStylesheets().add(
                    EditorWindow.class.getResource("/css/main.css").toExternalForm());
            err.getDialogPane().getStyleClass().add("custom-dialog");
            err.showAndWait();
        }
    }

    private void onSettingsRequested() {
        InfoPanel panel = new InfoPanel(
                () -> outerStack.getChildren().removeIf(n -> n instanceof InfoPanel),
                langCode -> System.out.println("[EditorWindow] Language would change to " + langCode)
        );
        outerStack.getChildren().add(panel);
    }
}
