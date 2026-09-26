package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectSession;
import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.control.Button;
import javafx.scene.control.Tooltip;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;
import org.kordamp.ikonli.javafx.FontIcon;

public class RightSidebar extends VBox {

    private static final int ICON_SIZE = 20;

    public interface PanelToggle {
        void toggle(String panelId);
    }

    private final PanelToggle onToggle;

    public RightSidebar(ProjectSession session, PanelToggle onToggle) {
        this.onToggle = onToggle;

        getStyleClass().add("right-sidebar");
        setAlignment(Pos.BOTTOM_CENTER);
        setSpacing(6);

        Region topSpacer = new Region();
        VBox.setVgrow(topSpacer, Priority.ALWAYS);
        getChildren().add(topSpacer);

        var manifest = session.manifest();

        if (manifest.gitEnabled) {
            getChildren().add(sidebarButton("bxl-git", "editor.git",
                    () -> trigger("git")));
        }
        if (manifest.sftpEnabled) {
            getChildren().add(sidebarButton("antf-database", "editor.sftp",
                    () -> trigger("sftp")));
        }

        getChildren().add(sidebarButton("bi-bug", "editor.debug",
                () -> trigger("debug")));

        getChildren().add(sidebarButton("bi-terminal-fill", "editor.console",
                () -> trigger("console")));

        Region bottomSpacer = new Region();
        bottomSpacer.setPrefHeight(6);
        getChildren().add(bottomSpacer);
    }

    private void trigger(String panelId) {
        if (onToggle != null) onToggle.toggle(panelId);
    }

    private Button sidebarButton(String iconCode, String tooltipKey, Runnable action) {
        Button btn = new Button();
        btn.getStyleClass().add("sidebar-btn");
        btn.setCursor(Cursor.HAND);

        FontIcon icon = new FontIcon(iconCode);
        icon.setIconSize(ICON_SIZE);
        icon.getStyleClass().add("sidebar-icon");
        btn.setGraphic(icon);

        Tooltip.install(btn, new Tooltip(I18n.t(tooltipKey)));
        btn.setOnAction(e -> action.run());
        return btn;
    }
}
