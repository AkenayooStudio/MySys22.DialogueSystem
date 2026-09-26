package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.ProjectManifest;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import javafx.geometry.Pos;
import javafx.scene.Node;
import javafx.scene.control.Button;
import javafx.scene.control.CheckBox;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.TextField;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.VBox;
import javafx.stage.DirectoryChooser;
import org.kordamp.ikonli.javafx.FontIcon;

import java.io.File;
import java.nio.file.Path;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class NewProjectPanel extends StackPane {

    public interface Callback {
        void onProjectCreated(ProjectSession session);
    }

    private static final List<String> LANGUAGES = List.of("EN", "IT", "ES", "RO", "JA");

    private final Runnable onClose;
    private final Callback onProjectCreated;
    private final VBox panel = new VBox();

    private int step = 1;

    private TextField nameField;
    private ComboBox<String> languageCombo;
    private TextField versionField;
    private TextField chapterField;

    private TextField pathField;
    private TextField unityPathField;
    private CheckBox sftpCheck;
    private CheckBox gitCheck;

    public NewProjectPanel(Runnable onClose, Callback onProjectCreated) {
        this.onClose = onClose;
        this.onProjectCreated = onProjectCreated;

        getStyleClass().add("modal-overlay");
        setAlignment(Pos.CENTER);

        setOnMouseClicked(e -> {
            if (e.getTarget() == this) onClose.run();
        });

        panel.getStyleClass().add("modal-panel");
        panel.setMaxWidth(500);
        panel.setMaxHeight(Region.USE_PREF_SIZE);

        showStep1();
        getChildren().add(panel);
    }

    private void showStep1() {
        step = 1;
        panel.getChildren().clear();

        panel.getChildren().add(buildHeader());

        nameField = new TextField();
        nameField.getStyleClass().add("settings-text-field");
        nameField.setPromptText("My Project");

        languageCombo = new ComboBox<>();
        languageCombo.getItems().addAll(LANGUAGES);
        languageCombo.setValue(resolveDefaultLanguage());
        languageCombo.getStyleClass().add("settings-combo");
        languageCombo.setMaxWidth(Double.MAX_VALUE);
        languageCombo.setEditable(true);
        languageCombo.setConverter(new javafx.util.StringConverter<>() {
            @Override
            public String toString(String value) {
                return value == null ? "" : value;
            }

            @Override
            public String fromString(String text) {
                return normalizeLanguage(text);
            }
        });

        versionField = new TextField("1.0");
        versionField.getStyleClass().add("settings-text-field");

        chapterField = new TextField("1");
        chapterField.getStyleClass().add("settings-text-field");

        panel.getChildren().addAll(
                formRow("newproject.project_name", nameField),
                formRow("newproject.language", languageCombo),
                formRow("newproject.dialogue_version", versionField),
                formRow("newproject.chapter", chapterField),
                buildButtonRow()
        );
    }

    private void showStep2() {
        step = 2;
        panel.getChildren().clear();

        panel.getChildren().add(buildHeader());

        Label pathLabel = new Label(I18n.t("newproject.path"));
        pathLabel.getStyleClass().add("settings-label");

        pathField = new TextField(defaultProjectPath());
        pathField.getStyleClass().add("settings-text-field");
        HBox.setHgrow(pathField, Priority.ALWAYS);

        Button browseBtn = new Button();
        browseBtn.getStyleClass().add("browse-btn");
        FontIcon folderIcon = new FontIcon("bi-folder2-open");
        folderIcon.setIconSize(16);
        browseBtn.setGraphic(folderIcon);
        browseBtn.setOnAction(e -> browseFolder());

        HBox pathRow = new HBox(8);
        pathRow.setAlignment(Pos.CENTER_LEFT);
        pathRow.getChildren().addAll(pathField, browseBtn);

        Label unityLabel = new Label(I18n.t("newproject.unity_path"));
        unityLabel.getStyleClass().add("settings-label");

        unityPathField = new TextField();
        unityPathField.getStyleClass().add("settings-text-field");
        unityPathField.setPromptText("(optional) /path/to/UnityProject");
        HBox.setHgrow(unityPathField, Priority.ALWAYS);

        Button browseUnityBtn = new Button();
        browseUnityBtn.getStyleClass().add("browse-btn");
        FontIcon unityFolderIcon = new FontIcon("bi-folder2-open");
        unityFolderIcon.setIconSize(16);
        browseUnityBtn.setGraphic(unityFolderIcon);
        browseUnityBtn.setOnAction(e -> {
            DirectoryChooser chooser = new DirectoryChooser();
            chooser.setTitle(I18n.t("newproject.browse"));
            File home = new File(System.getProperty("user.home"));
            if (home.exists()) chooser.setInitialDirectory(home);
            File sel = chooser.showDialog(getScene().getWindow());
            if (sel != null) unityPathField.setText(sel.getAbsolutePath());
        });

        HBox unityRow = new HBox(8);
        unityRow.setAlignment(Pos.CENTER_LEFT);
        unityRow.getChildren().addAll(unityPathField, browseUnityBtn);

        sftpCheck = new CheckBox(I18n.t("newproject.sftp_enable"));
        sftpCheck.getStyleClass().add("settings-check");

        gitCheck = new CheckBox(I18n.t("newproject.git_enable"));
        gitCheck.getStyleClass().add("settings-check");

        VBox checks = new VBox(6, sftpCheck, gitCheck);
        checks.getStyleClass().add("settings-checks-box");

        panel.getChildren().addAll(
                pathLabel,
                pathRow,
                unityLabel,
                unityRow,
                checks,
                buildButtonRow()
        );
    }

    private HBox buildHeader() {
        HBox titleRow = new HBox();
        titleRow.setAlignment(Pos.CENTER_LEFT);

        Label title = new Label(I18n.t("newproject.title"));
        title.getStyleClass().add("modal-title");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("modal-close-btn");
        closeBtn.setOnAction(e -> onClose.run());

        titleRow.getChildren().addAll(title, spacer, closeBtn);
        return titleRow;
    }

    private HBox formRow(String labelKey, Node field) {
        Label label = new Label(I18n.t(labelKey));
        label.getStyleClass().add("settings-label");
        label.setMinWidth(140);

        HBox.setHgrow(field, Priority.ALWAYS);

        HBox row = new HBox(12, label, field);
        row.setAlignment(Pos.CENTER_LEFT);
        row.getStyleClass().add("settings-row");
        return row;
    }

    private HBox buildButtonRow() {
        HBox row = new HBox(8);
        row.setAlignment(Pos.CENTER_RIGHT);

        if (step == 2) {
            Button back = new Button(I18n.t("newproject.back"));
            back.getStyleClass().add("modal-btn-secondary");
            back.setOnAction(e -> showStep1());
            row.getChildren().add(back);
        }

        boolean last = step == 2;
        Button primary = new Button(last
                ? I18n.t("newproject.create")
                : I18n.t("newproject.next"));
        primary.getStyleClass().add("modal-btn-primary");
        primary.setOnAction(e -> {
            if (last) onCreate();
            else showStep2();
        });
        row.getChildren().add(primary);
        return row;
    }

    private void browseFolder() {
        DirectoryChooser chooser = new DirectoryChooser();
        chooser.setTitle(I18n.t("newproject.browse"));
        File home = new File(System.getProperty("user.home"), "Documents");
        if (home.exists() && home.isDirectory()) {
            chooser.setInitialDirectory(home);
        }
        File selected = chooser.showDialog(getScene().getWindow());
        if (selected != null) {
            pathField.setText(selected.getAbsolutePath());
        }
    }

    private void onCreate() {
        String name = nameField.getText();
        if (name == null || name.isBlank()) return;
        String path = pathField.getText();
        if (path == null || path.isBlank()) return;

        ProjectManifest manifest = new ProjectManifest();
        manifest.projectId = slugify(name);
        manifest.name = name;
        manifest.dialogueVersion = versionField.getText();
        manifest.chapter = chapterField.getText();
        manifest.defaultLanguage = normalizeLanguage(languageCombo.getEditor().getText());
        manifest.languages = new ArrayList<>(List.of(manifest.defaultLanguage));
        manifest.sftpEnabled = sftpCheck.isSelected();
        manifest.gitEnabled = gitCheck.isSelected();
        String unityPath = unityPathField.getText();
        manifest.unityProjectPath = (unityPath != null && !unityPath.isBlank())
                ? unityPath.trim() : null;
        manifest.createdAt = Instant.now().toString();

        try {
            LocalStorage storage = new LocalStorage(Path.of(path));
            ProjectSession session = ProjectSession.createFromManifest(storage, manifest);
            onProjectCreated.onProjectCreated(session);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private String defaultProjectPath() {
        String home = System.getProperty("user.home");
        String os = System.getProperty("os.name").toLowerCase();
        String sep = os.contains("win") ? "\\" : "/";
        String name = nameField != null && nameField.getText() != null
                && !nameField.getText().isBlank()
                ? slugify(nameField.getText())
                : "MyProject";
        return home + sep + "Documents" + sep + "Akenayō" + sep
                + "MySys22" + sep + "DialogueEditor" + sep + name;
    }

    private static String slugify(String s) {
        return s.toLowerCase()
                .replaceAll("[^a-z0-9]+", "_")
                .replaceAll("^_+|_+$", "");
    }

    private static String normalizeLanguage(String raw) {
        if (raw == null) return null;
        String value = raw.trim().toUpperCase(java.util.Locale.ROOT);
        return value.isEmpty() ? null : value;
    }

    private String resolveDefaultLanguage() {
        String lang = Locale.getDefault().getLanguage().toUpperCase();
        return LANGUAGES.contains(lang) ? lang : "EN";
    }
}
