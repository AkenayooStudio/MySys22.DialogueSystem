package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.debug.DebugService;
import ake.ruby.dialogueeditor.debug.Diagnostic;
import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectSession;

import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.TableCell;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;

import java.util.List;

public class DebugPanel extends VBox {

    private static final double DEFAULT_HEIGHT = 220;
    private static final double MIN_HEIGHT = 80;

    private final ProjectSession session;
    private final Runnable onClose;
    private final ObservableList<Diagnostic> items = FXCollections.observableArrayList();

    private double resizeStartH, resizeStartY;

    public DebugPanel(ProjectSession session, Runnable onClose) {
        this.session = session;
        this.onClose = onClose;

        getStyleClass().add("bottom-panel");
        setPrefHeight(DEFAULT_HEIGHT);
        setMinHeight(MIN_HEIGHT);

        HBox header = new HBox();
        header.getStyleClass().add("bottom-header");
        header.setAlignment(Pos.CENTER_LEFT);

        Label grip = new Label("\u22EE\u22EE");
        grip.getStyleClass().add("bottom-grip");

        Label title = new Label(I18n.t("editor.debug"));
        title.getStyleClass().add("bottom-title");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button importBtn = new Button(I18n.t("debug.digest.import"));
        importBtn.getStyleClass().add("bottom-icon-btn");
        importBtn.setOnAction(e -> importDigest());

        Button refreshBtn = new Button("\u21BB");
        refreshBtn.getStyleClass().add("bottom-icon-btn");
        refreshBtn.setOnAction(e -> refresh());

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("bottom-close-btn");
        closeBtn.setOnAction(e -> {
            if (onClose != null) onClose.run();
        });

        header.getChildren().addAll(grip, title, spacer, importBtn, refreshBtn, closeBtn);

        TableView<Diagnostic> table = new TableView<>(items);
        table.getStyleClass().add("editor-table");
        table.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN);
        VBox.setVgrow(table, Priority.ALWAYS);

        TableColumn<Diagnostic, Diagnostic.Severity> sevCol =
                new TableColumn<>(I18n.t("editor.debug.severity"));
        sevCol.setCellValueFactory(c ->
                new SimpleObjectProperty<>(c.getValue().severity));
        sevCol.setCellFactory(col -> new SeverityCell());
        sevCol.setPrefWidth(110);
        sevCol.setMinWidth(90);
        sevCol.setMaxWidth(140);

        TableColumn<Diagnostic, String> msgCol =
                new TableColumn<>(I18n.t("editor.debug.message"));
        msgCol.setCellValueFactory(c -> new SimpleStringProperty(c.getValue().message));
        msgCol.setMinWidth(300);

        table.getColumns().add(sevCol);
        table.getColumns().add(msgCol);

        getChildren().addAll(header, table);

        grip.setCursor(Cursor.N_RESIZE);
        header.setOnMousePressed(e -> {
            resizeStartH = getHeight();
            resizeStartY = e.getSceneY();
        });
        header.setOnMouseDragged(e -> {
            double dy = e.getSceneY() - resizeStartY;
            double newH = Math.max(MIN_HEIGHT, resizeStartH - dy);
            setPrefHeight(newH);
        });

        refresh();
    }

    public void refresh() {
        items.clear();
        List<Diagnostic> diags = DebugService.run(session);
        items.addAll(diags);
    }

    private void importDigest() {
        javafx.stage.FileChooser chooser = new javafx.stage.FileChooser();
        chooser.setTitle(I18n.t("debug.digest.import.title"));
        chooser.getExtensionFilters().add(
                new javafx.stage.FileChooser.ExtensionFilter("engine.graphdigest.json", "*.json"));

        java.io.File home = new java.io.File(System.getProperty("user.home"), "Documents");
        if (home.isDirectory()) chooser.setInitialDirectory(home);

        java.io.File selected = chooser.showOpenDialog(getScene() == null ? null : getScene().getWindow());
        if (selected == null) return;

        try {
            java.nio.file.Path target = ake.ruby.dialogueeditor.project.DigestService.importInto(
                    session, selected.toPath());
            refresh();
            info(I18n.t("debug.digest.import.done", target.toString()));
        } catch (Exception ex) {
            error(I18n.t("debug.digest.import.failed", ex.getMessage()));
        }
    }

    private void info(String message) {
        javafx.scene.control.Alert alert =
                new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.INFORMATION);
        alert.setTitle(I18n.t("debug.digest.import.title"));
        alert.setHeaderText(null);
        alert.setContentText(message);
        alert.getDialogPane().getStyleClass().add("custom-dialog");
        alert.showAndWait();
    }

    private void error(String message) {
        javafx.scene.control.Alert alert =
                new javafx.scene.control.Alert(javafx.scene.control.Alert.AlertType.ERROR);
        alert.setTitle(I18n.t("debug.digest.import.title"));
        alert.setHeaderText(null);
        alert.setContentText(message);
        alert.getDialogPane().getStyleClass().add("custom-dialog");
        alert.showAndWait();
    }

    private static class SeverityCell extends TableCell<Diagnostic, Diagnostic.Severity> {
        @Override
        protected void updateItem(Diagnostic.Severity item, boolean empty) {
            super.updateItem(item, empty);
            getStyleClass().removeAll("sev-info", "sev-warning", "sev-error");

            if (empty || item == null) {
                setText(null);
                return;
            }

            switch (item) {
                case INFO -> {
                    setText(I18n.t("editor.debug.severity.info"));
                    getStyleClass().add("sev-info");
                }
                case WARNING -> {
                    setText(I18n.t("editor.debug.severity.warning"));
                    getStyleClass().add("sev-warning");
                }
                case ERROR -> {
                    setText(I18n.t("editor.debug.severity.error"));
                    getStyleClass().add("sev-error");
                }
            }
        }
    }
}
