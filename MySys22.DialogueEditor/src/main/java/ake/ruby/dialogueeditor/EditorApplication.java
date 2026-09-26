package ake.ruby.dialogueeditor;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.SettingsData;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.SettingsStore;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import ake.ruby.dialogueeditor.ui.EditorWindow;
import ake.ruby.dialogueeditor.ui.LoadingScreen;
import ake.ruby.dialogueeditor.ui.MainWindow;
import javafx.application.Application;
import javafx.application.Platform;
import javafx.scene.control.Alert;
import javafx.stage.Stage;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.Locale;

public class EditorApplication extends Application {

    private static final List<String> SUPPORTED_LANGUAGES =
            List.of("EN", "IT", "ES", "RO", "JA");

    private static String pendingProject;
    private static Stage editorStage;

    public static void launchEditor() {
        launchEditor(null);
    }

    public static void launchEditor(String projectPath) {
        pendingProject = projectPath == null || projectPath.isBlank() ? null : projectPath.trim();
        Application.launch(EditorApplication.class);
    }

    @Override
    public void start(Stage primaryStage) {
        Platform.setImplicitExit(false);

        Locale locale = resolveLocale();
        I18n.setLocale(locale);

        LoadingScreen loading = new LoadingScreen(2000, () -> {
            if (pendingProject != null && openProject(pendingProject)) return;
            if (pendingProject != null) showProjectError(pendingProject);
            new MainWindow(primaryStage).show();
        });
        loading.show();
    }

    private boolean openProject(String projectPath) {
        try {
            Path root = Path.of(projectPath).toAbsolutePath().normalize();
            if (!Files.isDirectory(root)) {
                System.err.println("Not a folder: " + root);
                return false;
            }

            LocalStorage storage = new LocalStorage(root);
            if (!ProjectSession.isProject(storage)) {
                System.err.println("Not a MySys22 dialogue project (missing .project.yaml): " + root);
                return false;
            }

            ProjectSession session = ProjectSession.open(storage);

            editorStage = new Stage();
            EditorWindow window = new EditorWindow(editorStage, session, () -> {
                editorStage.close();
                Platform.exit();
            });
            window.show();
            return true;
        } catch (Exception e) {
            System.err.println("Cannot open project '" + projectPath + "': " + e.getMessage());
            return false;
        }
    }

    private void showProjectError(String projectPath) {
        Alert alert = new Alert(Alert.AlertType.ERROR);
        alert.setTitle(I18n.t("app.title"));
        alert.setHeaderText(I18n.t("app.error.not_project"));
        alert.setContentText(projectPath);
        alert.getDialogPane().getStyleClass().add("custom-dialog");
        alert.showAndWait();
    }

    private Locale resolveLocale() {
        SettingsData settings = SettingsStore.load();
        if (settings.language != null && SUPPORTED_LANGUAGES.contains(settings.language)) {
            return Locale.forLanguageTag(settings.language.toLowerCase());
        }

        Locale system = Locale.getDefault();
        String tag = system.getLanguage().toUpperCase();
        if (SUPPORTED_LANGUAGES.contains(tag)) {
            return system;
        }

        return Locale.ENGLISH;
    }
}
