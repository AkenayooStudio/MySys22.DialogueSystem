package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import javafx.geometry.Pos;
import javafx.geometry.Rectangle2D;
import javafx.scene.Cursor;
import javafx.scene.Node;
import javafx.scene.control.Button;
import javafx.scene.control.ContextMenu;
import javafx.scene.control.Label;
import javafx.scene.control.MenuItem;
import javafx.scene.control.SeparatorMenuItem;
import javafx.scene.control.Tooltip;
import javafx.scene.effect.ColorAdjust;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.input.MouseButton;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.paint.Color;
import javafx.scene.shape.Line;
import javafx.scene.shape.Rectangle;
import javafx.stage.Screen;
import javafx.stage.Stage;

import java.io.InputStream;

public class EditorTitleBar extends HBox {

    public interface MaximizeCallback {
        void toggle();
    }

    public interface InjectCallback {
        void inject();
    }

    private static final double LOGO_BRIGHTNESS = 0.8;
    private static final int WINDOW_BTN_GLYPH = 4;

    private final Stage stage;
    private final Runnable onSettingsRequested;
    private final MaximizeCallback onMaximizeToggle;
    private final InjectCallback onInject;

    private double dragOffsetX;
    private double dragOffsetY;
    private boolean allowDrag = false;

    public EditorTitleBar(Stage stage,
                          Runnable onSettingsRequested,
                          MaximizeCallback onMaximizeToggle,
                          InjectCallback onInject) {
        this.stage = stage;
        this.onSettingsRequested = onSettingsRequested;
        this.onMaximizeToggle = onMaximizeToggle;
        this.onInject = onInject;

        getStyleClass().add("title-bar");

        HBox logoBox = new HBox();
        logoBox.getStyleClass().add("logo-box");
        logoBox.setAlignment(Pos.CENTER_LEFT);
        logoBox.getChildren().add(buildLogo());

        Region sep = new Region();
        sep.getStyleClass().add("title-separator");
        logoBox.getChildren().add(sep);

        HBox actions = new HBox();
        actions.getStyleClass().add("titlebar-actions");
        actions.setAlignment(Pos.CENTER_LEFT);
        actions.getChildren().addAll(
                buildFileMenu(),
                buildSettingsButton(),
                buildWindowMenu()
        );

        Region midSep = new Region();
        midSep.getStyleClass().add("title-separator-inline");
        actions.getChildren().add(midSep);

        actions.getChildren().add(buildInjectButton());

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

        getChildren().addAll(logoBox, actions, spacer, controls);
        enableDrag();
    }

    private Node buildLogo() {
        try (InputStream is = EditorTitleBar.class.getResourceAsStream("/images/logo.png")) {
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

    private Button buildInjectButton() {
        Button btn = new Button(I18n.t("editor.inject"));
        btn.getStyleClass().add("titlebar-btn");
        btn.getStyleClass().add("titlebar-btn-accent");
        btn.setCursor(Cursor.HAND);

        org.kordamp.ikonli.javafx.FontIcon icon =
                new org.kordamp.ikonli.javafx.FontIcon("bi-box-arrow-up");
        icon.setIconSize(13);
        icon.getStyleClass().add("titlebar-btn-icon");
        btn.setGraphic(icon);

        btn.setOnAction(e -> {
            if (onInject != null) onInject.inject();
        });
        return btn;
    }

    private Button buildFileMenu() {
        Button btn = titlebarButton("editor.file");
        ContextMenu menu = new ContextMenu();

        MenuItem save = new MenuItem(I18n.t("editor.file.save"));
        save.setDisable(true);

        MenuItem saveAll = new MenuItem(I18n.t("editor.file.save_all"));
        saveAll.setDisable(true);

        MenuItem exit = new MenuItem(I18n.t("editor.file.exit"));
        exit.setOnAction(e -> {
            stage.close();
            javafx.application.Platform.exit();
        });

        menu.getItems().addAll(save, saveAll, new SeparatorMenuItem(), exit);
        menu.getStyleClass().add("project-context-menu");

        btn.setOnAction(e -> menu.show(btn, javafx.geometry.Side.BOTTOM, 0, 0));
        return btn;
    }

    private Button buildSettingsButton() {
        Button btn = titlebarButton("editor.settings");
        btn.setOnAction(e -> {
            if (onSettingsRequested != null) onSettingsRequested.run();
        });
        return btn;
    }

    private Button buildWindowMenu() {
        Button btn = titlebarButton("editor.window");
        ContextMenu menu = new ContextMenu();

        MenuItem resetLayout = new MenuItem(I18n.t("editor.window.reset_layout"));
        resetLayout.setDisable(true);

        menu.getItems().add(resetLayout);
        menu.getStyleClass().add("project-context-menu");

        btn.setOnAction(e -> menu.show(btn, javafx.geometry.Side.BOTTOM, 0, 0));
        return btn;
    }

    private Button titlebarButton(String i18nKey) {
        Button btn = new Button(I18n.t(i18nKey));
        btn.getStyleClass().add("titlebar-btn");
        btn.setCursor(Cursor.HAND);
        return btn;
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
            if (e.getButton() == MouseButton.PRIMARY && onMaximizeToggle != null) {
                onMaximizeToggle.toggle();
            }
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

    private void enableDrag() {
        setOnMousePressed(e -> {
            if (isInteractive(e.getTarget())) return;
            dragOffsetX = e.getScreenX() - stage.getX();
            dragOffsetY = e.getScreenY() - stage.getY();
        });
        setOnMouseDragged(e -> {
            if (isInteractive(e.getTarget())) return;
            stage.setX(e.getScreenX() - dragOffsetX);
            stage.setY(e.getScreenY() - dragOffsetY);
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
}
