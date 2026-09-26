package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.CharacterListEntry;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.project.ProjectSession;

import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.TableCell;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.TextField;
import javafx.scene.control.cell.TextFieldTableCell;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;

import java.nio.file.Path;
import java.util.List;

public class CharacterListTab extends EditorTab {

    private final ProjectSession session;
    private final ObservableList<CharacterListEntry> items;

    public CharacterListTab(ProjectSession session, Path filePath) {
        super(filePath, "bi-people", "characters.list");

        this.session = session;

        CharacterListData data = safeRead(session);
        items = FXCollections.observableArrayList(data.characters);

        TableView<CharacterListEntry> table = new TableView<>(items);
        table.setEditable(true);
        table.getStyleClass().add("editor-table");
        table.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN);

        TableColumn<CharacterListEntry, Integer> cidCol =
                new TableColumn<>(I18n.t("editor.char.cid"));
        cidCol.setCellValueFactory(c ->
                new SimpleObjectProperty<>(c.getValue().id));
        cidCol.setCellFactory(col -> new IntegerCell());
        cidCol.setOnEditCommit(e -> {
            e.getRowValue().id = e.getNewValue();
            save();
        });
        cidCol.setPrefWidth(100);
        cidCol.setMinWidth(80);

        TableColumn<CharacterListEntry, String> nameCol =
                new TableColumn<>(I18n.t("editor.char.name"));
        nameCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().name != null ? c.getValue().name : ""));
        nameCol.setCellFactory(col -> new TextFieldTableCell<>(new javafx.util.converter.DefaultStringConverter()) {
            @Override
            public void startEdit() {
                beginEdit(I18n.t("editor.undo.edit_character"));
                super.startEdit();
            }
        });
        nameCol.setOnEditCommit(e -> {
            e.getRowValue().name = e.getNewValue();
            save();
        });
        nameCol.setMinWidth(150);

        TableColumn<CharacterListEntry, String> langCol =
                new TableColumn<>(I18n.t("editor.char.language"));
        langCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().language != null ? c.getValue().language : ""));
        langCol.setCellFactory(col -> new TextFieldTableCell<>(new javafx.util.converter.DefaultStringConverter()) {
            @Override
            public void startEdit() {
                beginEdit(I18n.t("editor.undo.edit_character"));
                super.startEdit();
            }
        });
        langCol.setOnEditCommit(e -> {
            e.getRowValue().language = e.getNewValue();
            save();
        });
        langCol.setPrefWidth(120);
        langCol.setMinWidth(80);

        table.getColumns().add(cidCol);
        table.getColumns().add(nameCol);
        table.getColumns().add(langCol);

        Button addBtn = new Button("+");
        addBtn.getStyleClass().add("editor-icon-btn");
        addBtn.setOnAction(e -> {
            beginEdit(I18n.t("editor.undo.add_character"));
            CharacterListEntry entry = new CharacterListEntry();
            entry.id = nextCid();
            entry.name = "";
            entry.language = session.manifest().defaultLanguage;
            items.add(entry);
            save();
        });

        Button removeBtn = new Button("\u2212");
        removeBtn.getStyleClass().add("editor-icon-btn");
        removeBtn.setOnAction(e -> {
            int idx = table.getSelectionModel().getSelectedIndex();
            if (idx >= 0) {
                beginEdit(I18n.t("editor.undo.remove_character"));
                items.remove(idx);
                save();
            }
        });

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

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

    private Button[] historyButtons = new Button[0];

    private void refreshHistoryButtons() {
        if (historyButtons.length < 2) return;
        historyButtons[0].setDisable(!canUndo());
        historyButtons[1].setDisable(!canRedo());
    }

    private int nextCid() {
        return items.stream()
                .filter(e -> e.id != null)
                .mapToInt(e -> e.id)
                .max()
                .orElse(-1) + 1;
    }

    private CharacterListData safeRead(ProjectSession session) {
        try {
            return session.readCharacterList();
        } catch (Exception e) {
            return new CharacterListData();
        }
    }

    private void save() {
        try {
            session.writeCharacterList(currentData());
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private CharacterListData currentData() {
        CharacterListData data = new CharacterListData();
        data.characters = List.copyOf(items);
        return data;
    }

    @Override
    protected byte[] captureState() {
        try {
            return YamlIO.writeCharacterList(currentData());
        } catch (Exception e) {
            return null;
        }
    }

    @Override
    protected void applyState(byte[] state) {
        try {
            CharacterListData restored = YamlIO.readCharacterList(state);
            items.setAll(restored.characters == null ? List.of() : restored.characters);
            save();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private static class IntegerCell extends TableCell<CharacterListEntry, Integer> {
        private TextField textField;

        @Override
        protected void updateItem(Integer item, boolean empty) {
            super.updateItem(item, empty);
            if (empty) {
                setText(null);
                setGraphic(null);
                return;
            }
            setText(item != null ? String.valueOf(item) : "0");
        }

        @Override
        public void startEdit() {
            if (isEmpty()) return;
            super.startEdit();

            if (textField == null) {
                textField = new TextField();
                textField.getStyleClass().add("editor-cell-editor");
                textField.setOnAction(e -> commitEdit(parse(textField.getText())));
                textField.focusedProperty().addListener((o, old, focused) -> {
                    if (!focused && isEditing()) {
                        commitEdit(parse(textField.getText()));
                    }
                });
                textField.setTextFormatter(new javafx.scene.control.TextFormatter<>(change -> {
                    String newText = change.getControlNewText();
                    if (newText.isEmpty() || newText.matches("\\d+")) {
                        return change;
                    }
                    return null;
                }));
            }

            textField.setText(getItem() != null ? String.valueOf(getItem()) : "0");
            setText(null);
            setGraphic(textField);
            textField.selectAll();
            textField.requestFocus();
        }

        @Override
        public void cancelEdit() {
            super.cancelEdit();
            setText(getItem() != null ? String.valueOf(getItem()) : "0");
            setGraphic(null);
        }

        private Integer parse(String s) {
            try {
                return Integer.parseInt(s.trim());
            } catch (Exception e) {
                return 0;
            }
        }
    }
}
