package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectImportService;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import ake.ruby.dialogueeditor.storage.SftpStorage;
import javafx.geometry.Pos;
import javafx.scene.Node;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.PasswordField;
import javafx.scene.control.RadioButton;
import javafx.scene.control.TextField;
import javafx.scene.control.ToggleGroup;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.VBox;
import javafx.stage.DirectoryChooser;
import org.kordamp.ikonli.javafx.FontIcon;

import java.io.File;
import java.nio.file.Path;
import java.util.function.Consumer;

public class OpenProjectPanel extends StackPane {

    private static final String TAB_LOCAL = "local";
    private static final String TAB_SFTP = "sftp";
    private static final String TAB_GIT = "git";

    private final Runnable onClose;
    private final Consumer<ProjectSession> onProjectOpened;

    private final VBox panel = new VBox();
    private final VBox content = new VBox(12);
    private final HBox tabBar = new HBox();

    private String activeTab = TAB_LOCAL;

    public OpenProjectPanel(Runnable onClose, Consumer<ProjectSession> onProjectOpened) {
        this.onClose = onClose;
        this.onProjectOpened = onProjectOpened;

        getStyleClass().add("modal-overlay");
        setAlignment(Pos.CENTER);
        setOnMouseClicked(e -> {
            if (e.getTarget() == this) onClose.run();
        });

        panel.getStyleClass().add("modal-panel");
        panel.setMaxWidth(560);
        panel.setMaxHeight(Region.USE_PREF_SIZE);

        panel.getChildren().addAll(buildHeader(), buildTabBar(), content);
        getChildren().add(panel);

        renderTab();
    }

    private HBox buildHeader() {
        HBox row = new HBox();
        row.setAlignment(Pos.CENTER_LEFT);

        Label title = new Label(I18n.t("openproject.title"));
        title.getStyleClass().add("modal-title");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("modal-close-btn");
        closeBtn.setOnAction(e -> onClose.run());

        row.getChildren().addAll(title, spacer, closeBtn);
        return row;
    }

    private HBox buildTabBar() {
        tabBar.getStyleClass().add("tab-bar");
        tabBar.setAlignment(Pos.CENTER_LEFT);
        tabBar.setSpacing(4);

        tabBar.getChildren().addAll(
                buildTabButton(TAB_LOCAL, "oct-file-directory-16", "openproject.tab.local"),
                buildTabButton(TAB_SFTP, "bi-server", "openproject.tab.sftp"),
                buildTabButton(TAB_GIT, "bxl-git", "openproject.tab.git")
        );
        return tabBar;
    }

    private Button buildTabButton(String tabId, String iconCode, String labelKey) {
        Button btn = new Button(I18n.t(labelKey));
        btn.getStyleClass().add("tab-btn");
        if (tabId.equals(activeTab)) btn.getStyleClass().add("tab-btn-active");

        FontIcon icon = new FontIcon(iconCode);
        icon.setIconSize(14);
        icon.getStyleClass().add("tab-btn-icon");
        btn.setGraphic(icon);

        btn.setOnAction(e -> {
            activeTab = tabId;
            for (Node n : tabBar.getChildren()) {
                n.getStyleClass().remove("tab-btn-active");
            }
            btn.getStyleClass().add("tab-btn-active");
            renderTab();
        });
        return btn;
    }

    private void renderTab() {
        content.getChildren().clear();
        content.getStyleClass().add("tab-content");

        switch (activeTab) {
            case TAB_LOCAL -> renderLocal();
            case TAB_SFTP -> renderSftp();
            case TAB_GIT -> renderGit();
        }
    }

    private void renderLocal() {
        Label pathLabel = new Label(I18n.t("openproject.local.folder"));
        pathLabel.getStyleClass().add("settings-label");

        TextField pathField = new TextField();
        pathField.getStyleClass().add("settings-text-field");
        pathField.setPromptText("/path/to/project");
        HBox.setHgrow(pathField, Priority.ALWAYS);

        Button browseBtn = new Button();
        browseBtn.getStyleClass().add("browse-btn");
        FontIcon folderIcon = new FontIcon("oct-file-directory-16");
        folderIcon.setIconSize(16);
        browseBtn.setGraphic(folderIcon);
        browseBtn.setOnAction(e -> {
            DirectoryChooser chooser = new DirectoryChooser();
            File home = new File(System.getProperty("user.home"));
            if (home.exists()) chooser.setInitialDirectory(home);
            File sel = chooser.showDialog(getScene().getWindow());
            if (sel != null) pathField.setText(sel.getAbsolutePath());
        });

        HBox pathRow = new HBox(8, pathField, browseBtn);
        pathRow.setAlignment(Pos.CENTER_LEFT);

        Button importBtn = new Button(I18n.t("openproject.import"));
        importBtn.getStyleClass().add("modal-btn-primary");
        importBtn.setOnAction(e -> doLocalImport(pathField.getText()));

        HBox actions = new HBox(importBtn);
        actions.setAlignment(Pos.CENTER_RIGHT);

        content.getChildren().addAll(pathLabel, pathRow, actions);
    }

    private void doLocalImport(String pathStr) {
        if (pathStr == null || pathStr.isBlank()) return;
        try {
            Path path = Path.of(pathStr);
            LocalStorage storage = new LocalStorage(path);

            if (!ProjectSession.isProject(storage)) {
                System.err.println("[OpenProject] Not a project (missing .project.yaml)");
                return;
            }

            ProjectSession session = ProjectSession.open(storage);
            onProjectOpened.accept(session);
        } catch (Exception e) {
            System.err.println("[OpenProject] Import failed: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private boolean sftpStep2 = false;

    private void renderSftp() {
        if (!sftpStep2) renderSftpStep1();
        else renderSftpStep2();
    }

    private TextField sftpHost;
    private TextField sftpPort;
    private TextField sftpUser;
    private TextField sftpKeyPath;
    private PasswordField sftpPassword;
    private RadioButton sftpAuthKey;
    private RadioButton sftpAuthPassword;
    private TextField sftpLocalPath;

    private void renderSftpStep1() {
        sftpHost = new TextField();
        sftpHost.getStyleClass().add("settings-text-field");

        sftpPort = new TextField("22");
        sftpPort.getStyleClass().add("settings-text-field");

        sftpUser = new TextField();
        sftpUser.getStyleClass().add("settings-text-field");

        ToggleGroup authGroup = new ToggleGroup();
        sftpAuthKey = new RadioButton(I18n.t("openproject.sftp.auth_key"));
        sftpAuthKey.setToggleGroup(authGroup);
        sftpAuthKey.getStyleClass().add("settings-radio");
        sftpAuthKey.setSelected(true);

        sftpAuthPassword = new RadioButton(I18n.t("openproject.sftp.auth_password"));
        sftpAuthPassword.setToggleGroup(authGroup);
        sftpAuthPassword.getStyleClass().add("settings-radio");

        sftpKeyPath = new TextField();
        sftpKeyPath.getStyleClass().add("settings-text-field");
        sftpKeyPath.setPromptText("~/.ssh/id_rsa");
        HBox.setHgrow(sftpKeyPath, Priority.ALWAYS);

        Button keyBrowse = new Button();
        keyBrowse.getStyleClass().add("browse-btn");
        FontIcon f = new FontIcon("oct-file-directory-16");
        f.setIconSize(16);
        keyBrowse.setGraphic(f);
        keyBrowse.setOnAction(e -> {
            javafx.stage.FileChooser chooser = new javafx.stage.FileChooser();
            File home = new File(System.getProperty("user.home"), ".ssh");
            if (home.exists()) chooser.setInitialDirectory(home);
            File sel = chooser.showOpenDialog(getScene().getWindow());
            if (sel != null) sftpKeyPath.setText(sel.getAbsolutePath());
        });

        HBox keyRow = new HBox(8, sftpKeyPath, keyBrowse);
        keyRow.setAlignment(Pos.CENTER_LEFT);

        sftpPassword = new PasswordField();
        sftpPassword.getStyleClass().add("settings-text-field");

        Runnable updateAuthVisibility = () -> {
            boolean keyMode = sftpAuthKey.isSelected();
            keyRow.setVisible(keyMode);
            keyRow.setManaged(keyMode);
            sftpPassword.setVisible(!keyMode);
            sftpPassword.setManaged(!keyMode);
        };
        sftpAuthKey.selectedProperty().addListener((o, a, b) -> updateAuthVisibility.run());
        sftpAuthPassword.selectedProperty().addListener((o, a, b) -> updateAuthVisibility.run());
        updateAuthVisibility.run();

        Button next = new Button(I18n.t("openproject.next"));
        next.getStyleClass().add("modal-btn-primary");
        next.setOnAction(e -> {
            sftpStep2 = true;
            renderTab();
        });

        HBox actions = new HBox(next);
        actions.setAlignment(Pos.CENTER_RIGHT);

        content.getChildren().addAll(
                formRow("openproject.sftp.host", sftpHost),
                formRow("openproject.sftp.port", sftpPort),
                formRow("openproject.sftp.user", sftpUser),
                sftpAuthKey,
                sftpAuthPassword,
                keyRow,
                sftpPassword,
                actions
        );
    }

    private void renderSftpStep2() {
        sftpLocalPath = new TextField();
        sftpLocalPath.getStyleClass().add("settings-text-field");
        sftpLocalPath.setPromptText(defaultSftpLocalPath());
        HBox.setHgrow(sftpLocalPath, Priority.ALWAYS);

        Button browse = new Button();
        browse.getStyleClass().add("browse-btn");
        FontIcon f = new FontIcon("oct-file-directory-16");
        f.setIconSize(16);
        browse.setGraphic(f);
        browse.setOnAction(e -> {
            DirectoryChooser chooser = new DirectoryChooser();
            File home = new File(System.getProperty("user.home"));
            if (home.exists()) chooser.setInitialDirectory(home);
            File sel = chooser.showDialog(getScene().getWindow());
            if (sel != null) sftpLocalPath.setText(sel.getAbsolutePath());
        });

        HBox pathRow = new HBox(8, sftpLocalPath, browse);
        pathRow.setAlignment(Pos.CENTER_LEFT);

        Button back = new Button(I18n.t("openproject.back"));
        back.getStyleClass().add("modal-btn-secondary");
        back.setOnAction(e -> {
            sftpStep2 = false;
            renderTab();
        });

        Button save = new Button(I18n.t("openproject.save"));
        save.getStyleClass().add("modal-btn-primary");
        save.setOnAction(e -> doSftpImport());

        HBox actions = new HBox(8, back, save);
        actions.setAlignment(Pos.CENTER_RIGHT);

        content.getChildren().addAll(
                new Label(I18n.t("openproject.local.folder")) {{
                    getStyleClass().add("settings-label");
                }},
                pathRow,
                actions
        );
    }

    private void doSftpImport() {
        try {
            String host = sftpHost.getText();
            int port = Integer.parseInt(sftpPort.getText());
            String user = sftpUser.getText();
            String remoteRoot = "/";

            SftpStorage.Config cfg = new SftpStorage.Config(
                    host, port, user,
                    sftpAuthKey.isSelected() ? Path.of(sftpKeyPath.getText()) : null,
                    sftpAuthPassword.isSelected() ? sftpPassword.getText() : null,
                    remoteRoot
            );

            String localPathStr = sftpLocalPath.getText();
            if (localPathStr == null || localPathStr.isBlank()) {
                localPathStr = defaultSftpLocalPath();
            }
            Path localPath = Path.of(localPathStr);

            ProjectImportService.downloadSftpProject(cfg, localPath);

            LocalStorage localStorage = new LocalStorage(localPath);
            if (!ProjectSession.isProject(localStorage)) {
                System.err.println("[OpenProject] Downloaded folder is not a project");
                return;
            }
            ProjectSession session = ProjectSession.open(localStorage);
            onProjectOpened.accept(session);
        } catch (Exception e) {
            System.err.println("[OpenProject] SFTP import failed: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private String defaultSftpLocalPath() {
        String home = System.getProperty("user.home");
        return home + "/Documents/Akenayō/MySys22/DialogueEditor/sftp_import";
    }

    private boolean gitStep2 = false;

    private TextField gitUrl;
    private TextField gitBranch;
    private TextField gitLocalPath;

    private void renderGit() {
        if (!gitStep2) renderGitStep1();
        else renderGitStep2();
    }

    private void renderGitStep1() {
        gitUrl = new TextField();
        gitUrl.getStyleClass().add("settings-text-field");
        gitUrl.setPromptText("https://github.com/user/repo.git");

        gitBranch = new TextField("main");
        gitBranch.getStyleClass().add("settings-text-field");

        Button next = new Button(I18n.t("openproject.next"));
        next.getStyleClass().add("modal-btn-primary");
        next.setOnAction(e -> {
            gitStep2 = true;
            renderTab();
        });

        HBox actions = new HBox(next);
        actions.setAlignment(Pos.CENTER_RIGHT);

        content.getChildren().addAll(
                formRow("openproject.git.url", gitUrl),
                formRow("openproject.git.branch", gitBranch),
                actions
        );
    }

    private void renderGitStep2() {
        gitLocalPath = new TextField();
        gitLocalPath.getStyleClass().add("settings-text-field");
        gitLocalPath.setPromptText(defaultGitLocalPath());
        HBox.setHgrow(gitLocalPath, Priority.ALWAYS);

        Button browse = new Button();
        browse.getStyleClass().add("browse-btn");
        FontIcon f = new FontIcon("oct-file-directory-16");
        f.setIconSize(16);
        browse.setGraphic(f);
        browse.setOnAction(e -> {
            DirectoryChooser chooser = new DirectoryChooser();
            File home = new File(System.getProperty("user.home"));
            if (home.exists()) chooser.setInitialDirectory(home);
            File sel = chooser.showDialog(getScene().getWindow());
            if (sel != null) gitLocalPath.setText(sel.getAbsolutePath());
        });

        HBox pathRow = new HBox(8, gitLocalPath, browse);
        pathRow.setAlignment(Pos.CENTER_LEFT);

        Button back = new Button(I18n.t("openproject.back"));
        back.getStyleClass().add("modal-btn-secondary");
        back.setOnAction(e -> {
            gitStep2 = false;
            renderTab();
        });

        Button save = new Button(I18n.t("openproject.save"));
        save.getStyleClass().add("modal-btn-primary");
        save.setOnAction(e -> doGitImport());

        HBox actions = new HBox(8, back, save);
        actions.setAlignment(Pos.CENTER_RIGHT);

        content.getChildren().addAll(
                new Label(I18n.t("openproject.local.folder")) {{
                    getStyleClass().add("settings-label");
                }},
                pathRow,
                actions
        );
    }

    private void doGitImport() {
        try {
            String url = gitUrl.getText();
            String branch = gitBranch.getText();
            String localPathStr = gitLocalPath.getText();
            if (localPathStr == null || localPathStr.isBlank()) {
                localPathStr = defaultGitLocalPath();
            }
            Path localPath = Path.of(localPathStr);

            ProjectImportService.cloneGitProject(url, branch, localPath);

            LocalStorage storage = new LocalStorage(localPath);
            if (!ProjectSession.isProject(storage)) {
                System.err.println("[OpenProject] Cloned folder is not a project");
                return;
            }
            ProjectSession session = ProjectSession.open(storage);
            onProjectOpened.accept(session);
        } catch (Exception e) {
            System.err.println("[OpenProject] Git import failed: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private String defaultGitLocalPath() {
        String home = System.getProperty("user.home");
        return home + "/Documents/Akenayō/MySys22/DialogueEditor/git_clone";
    }

    private HBox formRow(String labelKey, Node field) {
        Label label = new Label(I18n.t(labelKey));
        label.getStyleClass().add("settings-label");
        label.setMinWidth(120);
        HBox.setHgrow(field, Priority.ALWAYS);
        HBox row = new HBox(12, label, field);
        row.setAlignment(Pos.CENTER_LEFT);
        row.getStyleClass().add("settings-row");
        return row;
    }
}
