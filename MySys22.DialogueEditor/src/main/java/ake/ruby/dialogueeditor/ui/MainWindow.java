package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.RecentProject;
import ake.ruby.dialogueeditor.project.RecentProjectsStore;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import javafx.geometry.Pos;
import javafx.geometry.Rectangle2D;
import javafx.scene.Cursor;
import javafx.scene.Node;
import javafx.scene.Scene;
import javafx.scene.control.Alert;
import javafx.scene.control.Button;
import javafx.scene.control.ButtonType;
import javafx.scene.control.ContextMenu;
import javafx.scene.control.Label;
import javafx.scene.control.MenuItem;
import javafx.scene.control.ScrollPane;
import javafx.scene.control.SeparatorMenuItem;
import javafx.scene.control.TextInputDialog;
import javafx.scene.control.Tooltip;
import javafx.scene.effect.ColorAdjust;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.input.Clipboard;
import javafx.scene.input.ClipboardContent;
import javafx.scene.input.MouseButton;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.VBox;
import javafx.scene.paint.Color;
import javafx.scene.shape.Line;
import javafx.scene.shape.Rectangle;
import javafx.scene.text.TextAlignment;
import javafx.stage.Screen;
import javafx.stage.Stage;
import javafx.stage.StageStyle;
import org.kordamp.ikonli.javafx.FontIcon;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

public class MainWindow {

    private static final double WINDOW_W = 1200;
    private static final double WINDOW_H = 720;
    private static final double LOGO_BRIGHTNESS = 0.8;
    private static final String APP_TITLE_FALLBACK = "MySys22 Dialogue Editor";

    private static final int ICON_SIZE_TITLEBAR = 18;
    private static final int ICON_SIZE_FOOTER = 20;
    private static final int WINDOW_BTN_GLYPH = 4;

    private final Stage stage;
    private StackPane outerStack;
    private BorderPane rootContainer;
    private VBox projectsList;

    private double dragOffsetX;
    private double dragOffsetY;
    private boolean isMaximized = false;
    private double preMaxX, preMaxY, preMaxW, preMaxH;

    public MainWindow(Stage stage) {
        this.stage = stage;
    }

    public void show() {
        stage.initStyle(StageStyle.TRANSPARENT);
        stage.setResizable(false);
        applyStageTitle();

        rootContainer = new BorderPane();
        rootContainer.getStyleClass().add("root-container");

        rootContainer.setTop(buildTitleBar());
        rootContainer.setCenter(buildHomeView());
        rootContainer.setBottom(buildFooter());

        outerStack = new StackPane(rootContainer);
        outerStack.getStyleClass().add("outer-stack");

        Scene scene = new Scene(outerStack, WINDOW_W, WINDOW_H);
        scene.setFill(Color.TRANSPARENT);
        scene.getStylesheets().add(
                MainWindow.class.getResource("/css/main.css").toExternalForm()
        );

        stage.setScene(scene);
        stage.centerOnScreen();
        stage.show();
        stage.toFront();
        stage.requestFocus();

        refreshRecentProjects();
    }

    private void applyStageTitle() {
        String title = I18n.t("app.title");
        if (title == null || title.startsWith("!") || title.endsWith("!")) {
            title = APP_TITLE_FALLBACK;
        }
        stage.setTitle(title);
    }

    private void rebuildUI() {
        rootContainer.setTop(buildTitleBar());
        rootContainer.setCenter(buildHomeView());
        rootContainer.setBottom(buildFooter());
        refreshRecentProjects();
    }

    private HBox buildTitleBar() {
        HBox bar = new HBox();
        bar.getStyleClass().add("title-bar");

        HBox logoBox = new HBox();
        logoBox.getStyleClass().add("logo-box");
        logoBox.setAlignment(Pos.CENTER_LEFT);
        logoBox.getChildren().add(buildLogo());

        Region sep1 = new Region();
        sep1.getStyleClass().add("title-separator");
        logoBox.getChildren().add(sep1);

        HBox actions = new HBox();
        actions.getStyleClass().add("titlebar-actions");
        actions.setAlignment(Pos.CENTER_LEFT);

        Button newBtn = titlebarButton("home.new_project", "antf-folder-add", this::onNewProject);
        Button openBtn = titlebarButton("home.open_project", "antf-folder-open", this::onOpenProject);

        Region midSep = new Region();
        midSep.getStyleClass().add("title-separator-inline");

        actions.getChildren().addAll(newBtn, midSep, openBtn);

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        HBox controls = new HBox();
        controls.getStyleClass().add("window-controls");
        controls.setAlignment(Pos.CENTER_RIGHT);
        controls.getChildren().addAll(
                createMaximizeButton(),
                createMinimizeButton(),
                createCloseButton()
        );

        bar.getChildren().addAll(logoBox, actions, spacer, controls);
        enableWindowDrag(bar);
        return bar;
    }

    private Button titlebarButton(String i18nKey, String iconCode, Runnable action) {
        Button btn = new Button(I18n.t(i18nKey));
        btn.getStyleClass().add("titlebar-btn");
        btn.setCursor(Cursor.HAND);

        FontIcon icon = new FontIcon(iconCode);
        icon.setIconSize(ICON_SIZE_TITLEBAR);
        icon.getStyleClass().add("titlebar-btn-icon");
        btn.setGraphic(icon);

        btn.setOnAction(e -> action.run());
        return btn;
    }

    private Node buildLogo() {
        try (InputStream is = MainWindow.class.getResourceAsStream("/images/logo.png")) {
            if (is != null) {
                ImageView view = new ImageView(new Image(is));
                view.getStyleClass().add("logo-image");
                view.setFitWidth(22);
                view.setFitHeight(22);
                view.setPreserveRatio(true);
                view.setSmooth(true);

                ColorAdjust adjust = new ColorAdjust();
                adjust.setBrightness(LOGO_BRIGHTNESS);
                view.setEffect(adjust);
                return view;
            }
        } catch (Exception ignored) {
        }
        Label fallback = new Label("AKE");
        fallback.getStyleClass().add("logo-fallback");
        return fallback;
    }

    private StackPane createCloseButton() {
        StackPane btn = windowBtn("btn-close", I18n.t("window.close"));
        int g = WINDOW_BTN_GLYPH;
        Line l1 = new Line(-g, -g, g, g);
        Line l2 = new Line(-g, g, g, -g);
        l1.getStyleClass().add("btn-glyph-dark");
        l2.getStyleClass().add("btn-glyph-dark");
        btn.getChildren().addAll(l1, l2);
        btn.setOnMouseClicked(e -> {
            if (e.getButton() == MouseButton.PRIMARY) {
                stage.close();
                javafx.application.Platform.exit();
            }
        });
        return btn;
    }

    private StackPane createMinimizeButton() {
        StackPane btn = windowBtn("btn-minimize", I18n.t("window.minimize"));
        Line dash = new Line(-WINDOW_BTN_GLYPH, 0, WINDOW_BTN_GLYPH, 0);
        dash.getStyleClass().add("btn-glyph-dark");
        btn.getChildren().add(dash);
        btn.setOnMouseClicked(e -> {
            if (e.getButton() == MouseButton.PRIMARY) stage.setIconified(true);
        });
        return btn;
    }

    private StackPane createMaximizeButton() {
        StackPane btn = windowBtn("btn-maximize", I18n.t("window.maximize"));
        Rectangle rect = new Rectangle(WINDOW_BTN_GLYPH * 2, WINDOW_BTN_GLYPH * 2);
        rect.setFill(Color.TRANSPARENT);
        rect.setStroke(Color.BLACK);
        rect.setStrokeWidth(1.6);
        btn.getChildren().add(rect);
        btn.setOnMouseClicked(e -> {
            if (e.getButton() == MouseButton.PRIMARY) toggleMaximize();
        });
        return btn;
    }

    private StackPane windowBtn(String styleClass, String tooltip) {
        StackPane btn = new StackPane();
        btn.getStyleClass().addAll("window-btn", styleClass);
        btn.setCursor(Cursor.HAND);
        Tooltip.install(btn, new Tooltip(tooltip));
        return btn;
    }

    private void toggleMaximize() {
        if (!isMaximized) {
            preMaxX = stage.getX();
            preMaxY = stage.getY();
            preMaxW = stage.getWidth();
            preMaxH = stage.getHeight();

            Rectangle2D vb = Screen.getPrimary().getVisualBounds();
            stage.setX(vb.getMinX());
            stage.setY(vb.getMinY());
            stage.setWidth(vb.getWidth());
            stage.setHeight(vb.getHeight());

            rootContainer.getStyleClass().add("maximized");
            isMaximized = true;
        } else {
            stage.setX(preMaxX);
            stage.setY(preMaxY);
            stage.setWidth(preMaxW);
            stage.setHeight(preMaxH);

            rootContainer.getStyleClass().remove("maximized");
            isMaximized = false;
        }
    }

    private void enableWindowDrag(HBox titleBar) {
        titleBar.setOnMousePressed(e -> {
            if (isMaximized || isInteractive(e.getTarget())) return;
            dragOffsetX = e.getScreenX() - stage.getX();
            dragOffsetY = e.getScreenY() - stage.getY();
        });
        titleBar.setOnMouseDragged(e -> {
            if (isMaximized || isInteractive(e.getTarget())) return;
            stage.setX(e.getScreenX() - dragOffsetX);
            stage.setY(e.getScreenY() - dragOffsetY);
        });
        titleBar.setOnMouseClicked(e -> {
            if (isInteractive(e.getTarget())) return;
            if (e.getButton() == MouseButton.PRIMARY && e.getClickCount() == 2) {
                toggleMaximize();
            }
        });
    }

    private boolean isInteractive(Object target) {
        if (!(target instanceof Node node)) return false;
        Node current = node;
        while (current != null) {
            if (current instanceof Button) return true;
            if (current.getStyleClass().contains("window-btn")) return true;
            current = current.getParent();
        }
        return false;
    }

    private VBox buildHomeView() {
        VBox home = new VBox();
        home.getStyleClass().add("home-view");
        VBox.setVgrow(home, Priority.ALWAYS);

        Label recentTitle = new Label(I18n.t("home.recent_title"));
        recentTitle.getStyleClass().add("section-title");

        projectsList = new VBox();
        projectsList.getStyleClass().add("projects-list");
        projectsList.setFillWidth(true);
        VBox.setVgrow(projectsList, Priority.ALWAYS);

        ScrollPane scroll = new ScrollPane(projectsList);
        scroll.getStyleClass().add("projects-scroll");
        scroll.setFitToWidth(true);
        scroll.setFitToHeight(true);
        scroll.setHbarPolicy(ScrollPane.ScrollBarPolicy.NEVER);
        VBox.setVgrow(scroll, Priority.ALWAYS);

        home.getChildren().addAll(recentTitle, scroll);
        return home;
    }

    private void refreshRecentProjects() {
        if (projectsList == null) return;
        projectsList.getChildren().clear();

        List<RecentProject> recents = RecentProjectsStore.load();
        if (recents.isEmpty()) {
            projectsList.setAlignment(Pos.CENTER);
            projectsList.getChildren().add(buildEmptyState());
        } else {
            projectsList.setAlignment(Pos.TOP_LEFT);
            for (RecentProject p : recents) {
                projectsList.getChildren().add(buildProjectCard(p));
            }
        }
    }

    private VBox buildEmptyState() {
        VBox empty = new VBox();
        empty.getStyleClass().add("empty-state");
        empty.setAlignment(Pos.CENTER);
        empty.setFillWidth(true);

        Label title = new Label(I18n.t("home.empty_title"));
        title.getStyleClass().add("empty-title");
        title.setTextAlignment(TextAlignment.CENTER);
        title.setAlignment(Pos.CENTER);
        title.setMaxWidth(Double.MAX_VALUE);

        Label desc = new Label(I18n.t("home.empty_desc"));
        desc.getStyleClass().add("empty-desc");
        desc.setWrapText(true);
        desc.setMaxWidth(420);
        desc.setTextAlignment(TextAlignment.CENTER);
        desc.setAlignment(Pos.CENTER);

        empty.getChildren().addAll(title, desc);
        return empty;
    }

    private HBox buildProjectCard(RecentProject p) {
        HBox card = new HBox();
        card.getStyleClass().add("project-card");
        card.setAlignment(Pos.CENTER_LEFT);
        card.setCursor(Cursor.HAND);

        StackPane icon = new StackPane();
        icon.getStyleClass().add("project-icon");
        FontIcon iconGlyph = new FontIcon(
                p.type == RecentProject.Type.SFTP ? "oct-cloud" : "oct-file-directory-16");
        iconGlyph.setIconSize(22);
        iconGlyph.getStyleClass().add("project-icon-glyph");
        icon.getChildren().add(iconGlyph);

        VBox info = new VBox();
        info.getStyleClass().add("project-info");
        HBox.setHgrow(info, Priority.ALWAYS);

        Label name = new Label(p.name != null ? p.name : p.projectId);
        name.getStyleClass().add("project-name");

        Label metaLabel = new Label(buildMetaLine(p));
        metaLabel.getStyleClass().add("project-meta");

        info.getChildren().addAll(name, metaLabel);

        HBox badges = new HBox();
        badges.getStyleClass().add("project-badges");
        badges.setAlignment(Pos.CENTER_RIGHT);

        badges.getChildren().add(badge(
                p.type == RecentProject.Type.SFTP
                        ? I18n.t("home.badge.sftp")
                        : I18n.t("home.badge.local"),
                "badge-type"));

        if (p.isGit) {
            badges.getChildren().add(badge(I18n.t("home.badge.git"), "badge-git"));
        }

        card.getChildren().addAll(icon, info, badges);
        card.setOnMouseClicked(e -> {
            if (e.getButton() == MouseButton.PRIMARY) onOpenRecent(p);
        });
        card.setOnContextMenuRequested(e -> {
            ContextMenu menu = buildProjectContextMenu(p);
            menu.show(card, e.getScreenX(), e.getScreenY());
        });
        return card;
    }

    private Label badge(String text, String extraClass) {
        Label l = new Label(text);
        l.getStyleClass().addAll("badge", extraClass);
        return l;
    }

    private String buildMetaLine(RecentProject p) {
        StringBuilder sb = new StringBuilder();

        if (p.dialogueVersion != null && !p.dialogueVersion.isBlank()) {
            sb.append(I18n.t("home.card.version")).append(" ").append(p.dialogueVersion);
        }
        if (p.chapter != null && !p.chapter.isBlank()) {
            if (sb.length() > 0) sb.append("  \u00b7  ");
            sb.append(I18n.t("home.card.chapter")).append(" ").append(p.chapter);
        }

        String when = RelativeTime.format(p.lastOpenedAt);
        String where = p.type == RecentProject.Type.SFTP
                ? p.sftpUser + "@" + p.sftpHost + ":" + p.sftpRemoteRoot
                : p.localPath;

        if (sb.length() > 0) sb.append("\n");
        sb.append(when).append("  \u00b7  ").append(where);
        return sb.toString();
    }

    private HBox buildFooter() {
        HBox footer = new HBox();
        footer.getStyleClass().add("footer-bar");
        footer.setAlignment(Pos.CENTER_LEFT);

        Label version = new Label(ProjectSession.EDITOR_VERSION);
        version.getStyleClass().add("footer-version");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button infoBtn = new Button();
        infoBtn.getStyleClass().add("footer-icon-btn");
        infoBtn.setCursor(Cursor.HAND);
        FontIcon infoIcon = new FontIcon("antf-info-circle");
        infoIcon.setIconSize(ICON_SIZE_FOOTER);
        infoIcon.getStyleClass().add("footer-icon-glyph");
        infoBtn.setGraphic(infoIcon);
        Tooltip.install(infoBtn, new Tooltip(I18n.t("settings.title")));
        infoBtn.setOnAction(e -> openInfoPanel());

        footer.getChildren().addAll(version, spacer, infoBtn);
        return footer;
    }

    private void openInfoPanel() {
        InfoPanel panel = new InfoPanel(this::closeInfoPanel, this::onLanguageChanged);
        outerStack.getChildren().add(panel);
    }

    private void closeInfoPanel() {
        outerStack.getChildren().removeIf(n -> n instanceof InfoPanel);
    }

    private void onLanguageChanged(String langCode) {
        I18n.setLocale(java.util.Locale.forLanguageTag(langCode.toLowerCase()));
        applyStageTitle();
        closeInfoPanel();
        rebuildUI();
    }

    private void onNewProject() {
        NewProjectPanel panel = new NewProjectPanel(
                this::closeNewProjectPanel,
                this::onProjectCreated
        );
        outerStack.getChildren().add(panel);
    }

    private void closeNewProjectPanel() {
        outerStack.getChildren().removeIf(n -> n instanceof NewProjectPanel);
    }

    private void onProjectCreated(ProjectSession session) {
        closeNewProjectPanel();
        registerRecent(session);
        openEditorFor(session, null);
    }

    private void registerRecent(ProjectSession session) {
        RecentProject rp = new RecentProject();
        rp.projectId = session.manifest().projectId;
        rp.name = session.manifest().name;
        rp.type = RecentProject.Type.LOCAL;
        rp.dialogueVersion = session.manifest().dialogueVersion;
        rp.chapter = session.manifest().chapter;
        rp.editorVersion = ProjectSession.EDITOR_VERSION;

        if (session.storage() instanceof LocalStorage local) {
            rp.localPath = local.root().toString();
            try {
                rp.isGit = Files.exists(local.root().resolve(".git"));
            } catch (Exception ignored) {
            }
        }

        RecentProjectsStore.touch(rp);
    }

    private void onOpenProject() {
        OpenProjectPanel panel = new OpenProjectPanel(
                this::closeOpenProjectPanel,
                this::onImportedProject
        );
        outerStack.getChildren().add(panel);
    }

    private void closeOpenProjectPanel() {
        outerStack.getChildren().removeIf(n -> n instanceof OpenProjectPanel);
    }

    private void onImportedProject(ProjectSession session) {
        closeOpenProjectPanel();
        registerRecent(session);
        openEditorFor(session, null);
    }

    private void onOpenRecent(RecentProject p) {
        if (p.localPath == null || p.localPath.isBlank()) {
            System.out.println("[MainWindow] Recent entry has no local path");
            return;
        }

        try {
            LocalStorage storage = new LocalStorage(Path.of(p.localPath));
            if (!ProjectSession.isProject(storage)) {
                System.out.println("[MainWindow] Not a valid project: " + p.localPath);
                return;
            }
            ProjectSession session = ProjectSession.open(storage);
            openEditorFor(session, p);
        } catch (Exception e) {
            System.err.println("[MainWindow] Failed to open project: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private void openEditorFor(ProjectSession session, RecentProject recent) {
        if (recent != null) {
            RecentProjectsStore.touch(recent);
        }

        stage.hide();

        LoadingScreen loading = new LoadingScreen(1500, () -> {
            Stage editorStage = new Stage();
            EditorWindow editor = new EditorWindow(editorStage, session, () -> {
                editorStage.close();
                stage.show();
                refreshRecentProjects();
            });
            editor.show();
        });
        loading.show();
    }

    private ContextMenu buildProjectContextMenu(RecentProject p) {
        ContextMenu menu = new ContextMenu();

        MenuItem openItem = new MenuItem(I18n.t("context.open"));
        openItem.setOnAction(e -> onOpenRecent(p));

        MenuItem renameItem = new MenuItem(I18n.t("context.edit_name"));
        renameItem.setOnAction(e -> onEditName(p));

        MenuItem copyPathItem = new MenuItem(I18n.t("context.copy_path"));
        copyPathItem.setOnAction(e -> onCopyPath(p));

        MenuItem removeItem = new MenuItem(I18n.t("context.remove_from_list"));
        removeItem.setOnAction(e -> onRemoveFromList(p));

        MenuItem deleteItem = new MenuItem(I18n.t("context.local_delete"));
        deleteItem.setOnAction(e -> onLocalDelete(p));
        deleteItem.getStyleClass().add("menu-item-danger");
        deleteItem.setDisable(p.type != RecentProject.Type.LOCAL
                || p.localPath == null || p.localPath.isBlank());

        menu.getItems().addAll(
                openItem,
                renameItem,
                copyPathItem,
                new SeparatorMenuItem(),
                removeItem,
                deleteItem
        );
        menu.getStyleClass().add("project-context-menu");
        return menu;
    }

    private void onEditName(RecentProject p) {
        TextInputDialog dialog = new TextInputDialog(p.name != null ? p.name : "");
        dialog.setTitle(I18n.t("context.edit_name"));
        dialog.setHeaderText(null);
        dialog.setContentText(I18n.t("newproject.project_name"));
        dialog.initOwner(stage);

        dialog.getDialogPane().getStylesheets().add(
                MainWindow.class.getResource("/css/main.css").toExternalForm()
        );
        dialog.getDialogPane().getStyleClass().add("custom-dialog");

        dialog.showAndWait().ifPresent(newName -> {
            if (newName == null || newName.isBlank()) return;

            p.name = newName.trim();
            RecentProjectsStore.touch(p);

            if (p.type == RecentProject.Type.LOCAL
                    && p.localPath != null && !p.localPath.isBlank()) {
                try {
                    LocalStorage storage = new LocalStorage(Path.of(p.localPath));
                    if (ProjectSession.isProject(storage)) {
                        ProjectSession session = ProjectSession.open(storage);
                        session.manifest().name = p.name;
                        session.writeManifest();
                    }
                } catch (Exception ex) {
                    System.err.println("[MainWindow] Manifest rename failed: " + ex.getMessage());
                }
            }

            refreshRecentProjects();
        });
    }

    private void onCopyPath(RecentProject p) {
        String path = p.type == RecentProject.Type.SFTP
                ? p.sftpUser + "@" + p.sftpHost + ":" + p.sftpRemoteRoot
                : p.localPath;
        if (path == null) return;

        ClipboardContent content = new ClipboardContent();
        content.putString(path);
        Clipboard.getSystemClipboard().setContent(content);
    }

    private void onRemoveFromList(RecentProject p) {
        RecentProjectsStore.remove(p);
        refreshRecentProjects();
    }

    private void onLocalDelete(RecentProject p) {
        if (p.type != RecentProject.Type.LOCAL
                || p.localPath == null || p.localPath.isBlank()) {
            return;
        }

        Alert confirm = new Alert(Alert.AlertType.CONFIRMATION);
        confirm.setTitle(I18n.t("context.local_delete"));
        confirm.setHeaderText(I18n.t("context.delete_warning_title"));
        confirm.setContentText(I18n.t("context.delete_warning_body", p.localPath));
        confirm.initOwner(stage);

        confirm.getDialogPane().getStylesheets().add(
                MainWindow.class.getResource("/css/main.css").toExternalForm()
        );
        confirm.getDialogPane().getStyleClass().add("custom-dialog");

        var result = confirm.showAndWait();
        if (result.isEmpty() || result.get() != ButtonType.OK) return;

        try {
            deleteRecursive(Path.of(p.localPath));
            RecentProjectsStore.remove(p);
            refreshRecentProjects();
        } catch (Exception ex) {
            System.err.println("[MainWindow] Delete failed: " + ex.getMessage());
        }
    }

    private void deleteRecursive(Path path) throws IOException {
        if (!Files.exists(path)) return;
        if (Files.isDirectory(path)) {
            try (var s = Files.list(path)) {
                for (Path child : s.toList()) {
                    deleteRecursive(child);
                }
            }
        }
        Files.deleteIfExists(path);
    }
}
