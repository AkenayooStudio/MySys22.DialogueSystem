package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.ProjectManifest;
import ake.ruby.dialogueeditor.project.ProjectLayout;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.ProjectShortcut;

import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.CheckBox;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.ScrollPane;
import javafx.scene.control.TextField;
import javafx.scene.layout.GridPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public class ProjectConfigTab extends EditorTab {

    private static final List<String> LANGUAGES = List.of("EN", "IT", "ES", "RO", "JA");

    private final ProjectSession session;

    public ProjectConfigTab(ProjectSession session, Path filePath) {
        super(filePath, "antf-project", ".project.yaml");

        this.session = session;
        ProjectManifest m = session.manifest();

        TextField nameField = new TextField(m.name != null ? m.name : "");
        nameField.getStyleClass().add("editor-field");

        TextField projectIdField = new TextField(m.projectId != null ? m.projectId : "");
        projectIdField.getStyleClass().add("editor-field");
        projectIdField.setEditable(false);

        TextField versionField = new TextField(m.dialogueVersion != null ? m.dialogueVersion : "");
        versionField.getStyleClass().add("editor-field");

        TextField chapterField = new TextField(m.chapter != null ? m.chapter : "");
        chapterField.getStyleClass().add("editor-field");

        ComboBox<String> defaultLangCombo = new ComboBox<>();
        defaultLangCombo.getItems().addAll(LANGUAGES);
        defaultLangCombo.setValue(m.defaultLanguage != null ? m.defaultLanguage : "EN");
        defaultLangCombo.getStyleClass().add("editor-combo");
        defaultLangCombo.setMaxWidth(Double.MAX_VALUE);
        defaultLangCombo.setEditable(true);
        defaultLangCombo.setConverter(new javafx.util.StringConverter<>() {
            @Override
            public String toString(String value) {
                return value == null ? "" : value;
            }

            @Override
            public String fromString(String text) {
                return text == null ? null : text.trim().toUpperCase(java.util.Locale.ROOT);
            }
        });

        TextField languagesField = new TextField(m.languages != null
                ? String.join(", ", m.languages) : "EN");
        languagesField.getStyleClass().add("editor-field");

        CheckBox sftpCheck = new CheckBox();
        sftpCheck.setSelected(m.sftpEnabled);
        sftpCheck.getStyleClass().add("editor-check");

        CheckBox gitCheck = new CheckBox();
        gitCheck.setSelected(m.gitEnabled);
        gitCheck.getStyleClass().add("editor-check");

        GridPane grid = new GridPane();
        grid.getStyleClass().add("editor-form");
        grid.setHgap(14);
        grid.setVgap(10);

        int row = 0;
        addRow(grid, row++, "editor.config.name", nameField);
        addRow(grid, row++, "editor.config.project_id", projectIdField);
        addRow(grid, row++, "editor.config.dialogue_version", versionField);
        addRow(grid, row++, "editor.config.chapter", chapterField);
        addRow(grid, row++, "editor.config.default_language", defaultLangCombo);
        addRow(grid, row++, "editor.config.languages", languagesField);
        addRow(grid, row++, "editor.config.sftp_enabled", sftpCheck);
        addRow(grid, row++, "editor.config.git_enabled", gitCheck);

        TextField unityField = new TextField(
                m.unityProjectPath != null ? m.unityProjectPath : "");
        unityField.getStyleClass().add("editor-field");
        addRow(grid, row++, "editor.config.unity_path", unityField);

        Label sftpTitle = new Label("SFTP");
        sftpTitle.getStyleClass().add("editor-section-title");

        TextField sftpHostField = new TextField(
                m.sftpHost != null ? m.sftpHost : "");
        sftpHostField.getStyleClass().add("editor-field");

        TextField sftpPortField = new TextField(String.valueOf(m.sftpPort));
        sftpPortField.getStyleClass().add("editor-field");

        TextField sftpUserField = new TextField(
                m.sftpUser != null ? m.sftpUser : "");
        sftpUserField.getStyleClass().add("editor-field");

        ComboBox<String> sftpAuthCombo = new ComboBox<>();
        sftpAuthCombo.getItems().addAll("key", "password");
        sftpAuthCombo.setValue(m.sftpAuthType != null ? m.sftpAuthType : "key");
        sftpAuthCombo.getStyleClass().add("editor-combo");
        sftpAuthCombo.setMaxWidth(Double.MAX_VALUE);

        TextField sftpKeyField = new TextField(
                m.sftpKeyPath != null ? m.sftpKeyPath : "");
        sftpKeyField.getStyleClass().add("editor-field");

        TextField sftpRemoteField = new TextField(
                m.sftpRemoteRoot != null ? m.sftpRemoteRoot : "/");
        sftpRemoteField.getStyleClass().add("editor-field");

        TextField sftpLocalField = new TextField(
                m.sftpLocalPath != null ? m.sftpLocalPath : "");
        sftpLocalField.getStyleClass().add("editor-field");

        GridPane sftpGrid = new GridPane();
        sftpGrid.getStyleClass().add("editor-form");
        sftpGrid.setHgap(14);
        sftpGrid.setVgap(10);
        int srow = 0;
        addRow(sftpGrid, srow++, "editor.config.sftp_host", sftpHostField);
        addRow(sftpGrid, srow++, "editor.config.sftp_port", sftpPortField);
        addRow(sftpGrid, srow++, "editor.config.sftp_user", sftpUserField);
        addRow(sftpGrid, srow++, "editor.config.sftp_auth", sftpAuthCombo);
        addRow(sftpGrid, srow++, "editor.config.sftp_key", sftpKeyField);
        addRow(sftpGrid, srow++, "editor.config.sftp_remote_root", sftpRemoteField);
        addRow(sftpGrid, srow++, "editor.config.sftp_local_path", sftpLocalField);

        Label infoTitle = new Label(I18n.t("editor.config.info"));
        infoTitle.getStyleClass().add("editor-section-title");

        GridPane infoGrid = new GridPane();
        infoGrid.getStyleClass().add("editor-form");
        infoGrid.setHgap(14);
        infoGrid.setVgap(6);
        int irow = 0;
        addInfoRow(infoGrid, irow++, "editor.config.created",
                m.createdAt != null ? m.createdAt : "-");
        addInfoRow(infoGrid, irow++, "editor.config.last_modified",
                m.lastModifiedAt != null ? m.lastModifiedAt : "-");
        addInfoRow(infoGrid, irow++, "editor.config.editor_version",
                m.editorVersion != null ? m.editorVersion : "-");

        Button saveBtn = new Button(I18n.t("editor.config.save"));
        saveBtn.getStyleClass().add("modal-btn-primary");
        saveBtn.setOnAction(e -> {
            m.name = nameField.getText();
            m.dialogueVersion = versionField.getText();
            m.chapter = chapterField.getText();
            String customDefault = defaultLangCombo.getEditor().getText();
            m.defaultLanguage = (customDefault == null || customDefault.isBlank())
                    ? defaultLangCombo.getValue()
                    : customDefault.trim().toUpperCase(java.util.Locale.ROOT);
            m.languages = parseLanguages(languagesField.getText(), m.defaultLanguage);
            m.sftpEnabled = sftpCheck.isSelected();
            m.gitEnabled = gitCheck.isSelected();
            String up = unityField.getText();
            m.unityProjectPath = (up != null && !up.isBlank()) ? up.trim() : null;

            m.sftpHost = blankToNull(sftpHostField.getText());
            try { m.sftpPort = Integer.parseInt(sftpPortField.getText().trim()); }
            catch (Exception ignored) { m.sftpPort = 22; }
            m.sftpUser = blankToNull(sftpUserField.getText());
            m.sftpAuthType = sftpAuthCombo.getValue();
            m.sftpKeyPath = blankToNull(sftpKeyField.getText());
            m.sftpRemoteRoot = blankToNull(sftpRemoteField.getText());
            m.sftpLocalPath = blankToNull(sftpLocalField.getText());

            try {
                session.writeManifest();
            } catch (Exception ex) {
                ex.printStackTrace();
            }
        });

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button shortcutBtn = new Button(I18n.t("editor.config.shortcut"));
        shortcutBtn.getStyleClass().add("modal-btn-secondary");
        shortcutBtn.setTooltip(new javafx.scene.control.Tooltip(I18n.t("editor.config.shortcut.tooltip")));
        shortcutBtn.setOnAction(e -> createShortcut());

        HBox actions = new HBox(8, spacer, shortcutBtn, saveBtn);
        actions.setAlignment(Pos.CENTER_RIGHT);

        VBox content = new VBox(grid, sftpTitle, sftpGrid, infoTitle, infoGrid, actions);
        content.getStyleClass().add("editor-tab-content");
        content.setSpacing(18);

        ScrollPane scroll = new ScrollPane(content);
        scroll.setFitToWidth(true);
        scroll.getStyleClass().add("editor-scroll");
        setContent(scroll);
    }

    private void createShortcut() {
        if (!(session.storage() instanceof ake.ruby.dialogueeditor.storage.LocalStorage local)) {
            showError(I18n.t("editor.config.shortcut.failed", "local project required"));
            return;
        }

        javafx.stage.FileChooser chooser = new javafx.stage.FileChooser();
        chooser.setTitle(I18n.t("editor.config.shortcut.title"));
        chooser.setInitialDirectory(local.root().toFile());
        chooser.setInitialFileName(ProjectShortcut.defaultFileName(session));
        chooser.getExtensionFilters().add(new javafx.stage.FileChooser.ExtensionFilter(
                "MySys22 Dialogue project", "*" + ProjectShortcut.EXTENSION));

        java.io.File selected = chooser.showSaveDialog(ownerWindow());
        if (selected == null) return;

        try {
            java.nio.file.Path written = ProjectShortcut.create(local.root(), selected.toPath());
            showInfo(I18n.t("editor.config.shortcut.done", written.toString()));
        } catch (Exception ex) {
            showError(I18n.t("editor.config.shortcut.failed", ex.getMessage()));
        }
    }

    private javafx.stage.Window ownerWindow() {
        javafx.scene.Node content = getContent();
        if (content == null || content.getScene() == null) return null;
        return content.getScene().getWindow();
    }

    private void showInfo(String message) {
        javafx.scene.control.Alert alert =
                new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.INFORMATION);
        alert.setHeaderText(null);
        alert.setContentText(message);
        alert.getDialogPane().getStyleClass().add("custom-dialog");
        alert.showAndWait();
    }

    private void showError(String message) {
        javafx.scene.control.Alert alert =
                new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.ERROR);
        alert.setHeaderText(null);
        alert.setContentText(message);
        alert.getDialogPane().getStyleClass().add("custom-dialog");
        alert.showAndWait();
    }

    private void addRow(GridPane grid, int row, String labelKey, javafx.scene.Node field) {
        Label label = new Label(I18n.t(labelKey));
        label.getStyleClass().add("editor-field-label");
        label.setMinWidth(160);
        grid.add(label, 0, row);
        GridPane.setHgrow(field, Priority.ALWAYS);
        grid.add(field, 1, row);
    }

    private void addInfoRow(GridPane grid, int row, String labelKey, String value) {
        Label label = new Label(I18n.t(labelKey));
        label.getStyleClass().add("editor-field-label");
        label.setMinWidth(160);
        Label val = new Label(value);
        val.getStyleClass().add("editor-info-value");
        grid.add(label, 0, row);
        grid.add(val, 1, row);
    }

    private static String blankToNull(String s) {
        return (s == null || s.isBlank()) ? null : s.trim();
    }

    private static String normalizeLanguage(String raw) {
        if (raw == null) return null;
        String value = raw.trim().toUpperCase(java.util.Locale.ROOT);
        return value.isEmpty() ? null : value;
    }

    private List<String> parseLanguages(String raw, String fallback) {
        List<String> out = new ArrayList<>();
        if (raw != null) {
            for (String part : raw.split(",")) {
                String t = part.trim().toUpperCase();
                if (!t.isEmpty() && !out.contains(t)) out.add(t);
            }
        }
        if (out.isEmpty() && fallback != null) out.add(fallback);
        return out;
    }
}
