package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.VariableEntry;
import ake.ruby.dialogueeditor.model.VariableListData;
import ake.ruby.dialogueeditor.project.ProjectSession;

import javafx.beans.property.SimpleStringProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.cell.TextFieldTableCell;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;

import java.nio.file.Path;
import java.util.List;

public class VariableListTab extends EditorTab {

    private final ProjectSession session;
    private final ObservableList<VariableEntry> items;
    private Button[] historyButtons = new Button[0];

    public VariableListTab(ProjectSession session, Path filePath) {
        super(filePath, "antf-apartment", "variables");

        this.session = session;

        VariableListData data = safeRead();
        items = FXCollections.observableArrayList(data.variables);

        TableView<VariableEntry> table = new TableView<>(items);
        table.setEditable(true);
        table.getStyleClass().add("editor-table");
        table.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN);

        TableColumn<VariableEntry, String> nameCol =
                new TableColumn<>(I18n.t("editor.vars.name"));
        nameCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().name != null ? c.getValue().name : ""));
        nameCol.setCellFactory(col -> editingCell("editor.undo.edit_variable"));
        nameCol.setOnEditCommit(e -> {
            e.getRowValue().name = e.getNewValue();
            save();
        });
        nameCol.setMinWidth(160);

        TableColumn<VariableEntry, String> typeCol =
                new TableColumn<>(I18n.t("editor.vars.type"));
        typeCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().type != null ? c.getValue().type : "bool"));
        typeCol.setCellFactory(col -> editingCell("editor.undo.edit_variable"));
        typeCol.setOnEditCommit(e -> {
            e.getRowValue().type = e.getNewValue();
            save();
        });
        typeCol.setPrefWidth(90);
        typeCol.setMinWidth(70);

        TableColumn<VariableEntry, String> valueCol =
                new TableColumn<>(I18n.t("editor.vars.default"));
        valueCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().value != null ? c.getValue().value : ""));
        valueCol.setCellFactory(col -> editingCell("editor.undo.edit_variable"));
        valueCol.setOnEditCommit(e -> {
            e.getRowValue().value = e.getNewValue();
            save();
        });
        valueCol.setPrefWidth(110);
        valueCol.setMinWidth(80);

        TableColumn<VariableEntry, String> descCol =
                new TableColumn<>(I18n.t("editor.vars.description"));
        descCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().description != null ? c.getValue().description : ""));
        descCol.setCellFactory(col -> editingCell("editor.undo.edit_variable"));
        descCol.setOnEditCommit(e -> {
            e.getRowValue().description = e.getNewValue();
            save();
        });
        descCol.setMinWidth(200);

        table.getColumns().add(nameCol);
        table.getColumns().add(typeCol);
        table.getColumns().add(valueCol);
        table.getColumns().add(descCol);

        Button undoBtn = new Button("\u21B6");
        undoBtn.getStyleClass().add("editor-icon-btn");
        undoBtn.setTooltip(new javafx.scene.control.Tooltip(I18n.t("editor.undo.tooltip")));
        undoBtn.setOnAction(e -> { if (undo()) refreshHistoryButtons(); });

        Button redoBtn = new Button("\u21B7");
        redoBtn.getStyleClass().add("editor-icon-btn");
        redoBtn.setTooltip(new javafx.scene.control.Tooltip(I18n.t("editor.redo.tooltip")));
        redoBtn.setOnAction(e -> { if (redo()) refreshHistoryButtons(); });

        historyButtons = new Button[] { undoBtn, redoBtn };
        setHistoryListener(this::refreshHistoryButtons);
        refreshHistoryButtons();

        Button addBtn = new Button("+");
        addBtn.getStyleClass().add("editor-icon-btn");
        addBtn.setOnAction(e -> {
            beginEdit(I18n.t("editor.undo.add_variable"));
            VariableEntry entry = new VariableEntry();
            entry.name = "new_variable";
            entry.type = "bool";
            entry.value = "false";
            entry.description = "";
            items.add(entry);
            save();
        });

        Button removeBtn = new Button("\u2212");
        removeBtn.getStyleClass().add("editor-icon-btn");
        removeBtn.setOnAction(e -> {
            int idx = table.getSelectionModel().getSelectedIndex();
            if (idx < 0) return;
            beginEdit(I18n.t("editor.undo.remove_variable"));
            items.remove(idx);
            save();
        });

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        HBox toolbar = new HBox(spacer, undoBtn, redoBtn, addBtn, removeBtn);
        toolbar.setAlignment(Pos.CENTER_RIGHT);
        toolbar.setSpacing(6);
        toolbar.getStyleClass().add("editor-toolbar");

        BorderPane content = new BorderPane();
        content.getStyleClass().add("editor-tab-content");
        content.setCenter(table);
        content.setBottom(toolbar);

        setContent(content);
    }

    private TextFieldTableCell<VariableEntry, String> editingCell(String labelKey) {
        return new TextFieldTableCell<>(new javafx.util.converter.DefaultStringConverter()) {
            @Override
            public void startEdit() {
                beginEdit(I18n.t(labelKey));
                super.startEdit();
            }
        };
    }

    private VariableListData safeRead() {
        try {
            return session.readVariables();
        } catch (Exception e) {
            return new VariableListData();
        }
    }

    private void save() {
        try {
            session.writeVariables(currentData());
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private VariableListData currentData() {
        VariableListData data = new VariableListData();
        data.variables = List.copyOf(items);
        return data;
    }

    private void refreshHistoryButtons() {
        if (historyButtons.length < 2) return;
        historyButtons[0].setDisable(!canUndo());
        historyButtons[1].setDisable(!canRedo());
    }

    @Override
    protected byte[] captureState() {
        try {
            return YamlIO.writeVariables(currentData());
        } catch (Exception e) {
            return null;
        }
    }

    @Override
    protected void applyState(byte[] state) {
        try {
            VariableListData restored = YamlIO.readVariables(state);
            items.setAll(restored.variables == null ? List.of() : restored.variables);
            save();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}
