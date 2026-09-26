package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.LineEntry;
import ake.ruby.dialogueeditor.project.DigestService;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.ProjectValidator;
import ake.ruby.dialogueeditor.model.digest.GraphDigestData;

import javafx.beans.property.SimpleStringProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.Alert;
import javafx.scene.control.ButtonType;
import javafx.scene.control.Label;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.TextField;
import javafx.scene.control.cell.TextFieldTableCell;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;

import java.nio.file.Path;
import java.util.Comparator;
import java.util.List;

public class LinesEditorTab extends EditorTab {

    private final ProjectSession session;
    private final String language;
    private String cid;
    private final ObservableList<LineEntry> items;
    private Button[] historyButtons = new Button[0];
    private final GraphDigestData digest;
    private final java.util.Set<Integer> referencedDids;

    public LinesEditorTab(ProjectSession session, String language, String fallbackCid, Path filePath) {
        super(filePath, "bi-file-code", buildTabTitle(fallbackCid, language));

        this.session = session;
        this.language = language;

        LineDatabaseData data = safeRead(fallbackCid);

        this.cid = resolveCid(data, fallbackCid);

        setText(buildTabTitle(this.cid, language));

        items = FXCollections.observableArrayList(data.lines);

        this.digest = DigestService.load(session).data();
        this.referencedDids = DigestService.usedDids(digest, this.cid);

        Label langLabel = new Label(I18n.t("editor.lines.language") + ":");
        langLabel.getStyleClass().add("editor-field-label");

        Label langValue = new Label(language);
        langValue.getStyleClass().add("editor-header-value");

        Label cidLabel = new Label(I18n.t("editor.lines.cid") + ":");
        cidLabel.getStyleClass().add("editor-field-label");

        TextField cidField = new TextField(this.cid);
        cidField.getStyleClass().add("editor-field");
        cidField.setPrefWidth(180);
        cidField.focusedProperty().addListener((o, was, isFocused) -> {
            if (!isFocused) applyCidChange(cidField);
        });
        cidField.setOnAction(e -> applyCidChange(cidField));

        HBox header = new HBox(8, langLabel, langValue, new Label("  "), cidLabel, cidField);
        header.setAlignment(Pos.CENTER_LEFT);
        header.getStyleClass().add("editor-tab-header");

        if (!referencedDids.isEmpty()) {
            Label usedLabel = new Label(I18n.t("editor.lines.used_dids",
                    referencedDids.stream().map(String::valueOf).collect(java.util.stream.Collectors.joining(", "))));
            usedLabel.getStyleClass().add("editor-field-label");
            usedLabel.setStyle("-fx-text-fill: #d9a441;");
            header.getChildren().add(usedLabel);
        }

        TableView<LineEntry> table = new TableView<>(items);
        table.setEditable(true);
        table.getStyleClass().add("editor-table");
        table.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN);

        TableColumn<LineEntry, String> lidCol = new TableColumn<>(I18n.t("editor.lines.lid"));
        lidCol.setCellValueFactory(c -> new SimpleStringProperty(String.valueOf(c.getValue().did)));
        lidCol.setEditable(false);
        lidCol.setPrefWidth(80);
        lidCol.setMinWidth(80);
        lidCol.setMaxWidth(80);

        TableColumn<LineEntry, String> textCol = new TableColumn<>(I18n.t("editor.lines.text"));
        textCol.setCellValueFactory(c -> new SimpleStringProperty(
                c.getValue().text != null ? c.getValue().text : ""));
        textCol.setCellFactory(col -> new TextFieldTableCell<>(new javafx.util.converter.DefaultStringConverter()) {
            @Override
            public void startEdit() {

                beginEdit(I18n.t("editor.undo.edit_text"));
                super.startEdit();
            }
        });
        textCol.setOnEditCommit(e -> {
            e.getRowValue().text = e.getNewValue();
            save();
        });
        textCol.setEditable(true);
        textCol.setMinWidth(200);

        table.getColumns().add(lidCol);
        table.getColumns().add(textCol);

        Button addBtn = new Button("+");
        addBtn.getStyleClass().add("editor-icon-btn");
        addBtn.setOnAction(e -> {
            beginEdit(I18n.t("editor.undo.add_line"));
            LineEntry entry = new LineEntry();
            entry.did = nextLid();
            entry.text = "";
            items.add(entry);
            items.sort(Comparator.comparingInt(l -> l.did));
            save();
        });

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

        Button insertBtn = new Button(I18n.t("editor.lines.insert"));
        insertBtn.getStyleClass().add("editor-icon-btn");
        insertBtn.setTooltip(new javafx.scene.control.Tooltip(I18n.t("editor.lines.insert.tooltip")));
        insertBtn.setOnAction(e -> insertBetween(table.getSelectionModel().getSelectedIndex()));

        Button removeBtn = new Button("\u2212");
        removeBtn.getStyleClass().add("editor-icon-btn");
        removeBtn.setOnAction(e -> {
            int idx = table.getSelectionModel().getSelectedIndex();
            if (idx < 0) return;

            LineEntry entry = items.get(idx);
            if (isReferenced(entry.did)) {
                String node = DigestService.usageNode(digest, cid, entry.did);
                Alert blocked = new Alert(Alert.AlertType.WARNING,
                        I18n.t("editor.lines.used_blocked", node == null ? "?" : node), ButtonType.OK);
                blocked.setHeaderText(null);
                blocked.showAndWait();
                return;
            }

            beginEdit(I18n.t("editor.undo.remove_line"));
            items.remove(idx);
            save();
        });

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        HBox toolbar = new HBox(spacer, undoBtn, redoBtn, insertBtn, addBtn, removeBtn);
        toolbar.setAlignment(Pos.CENTER_RIGHT);
        toolbar.setSpacing(6);
        toolbar.getStyleClass().add("editor-toolbar");

        BorderPane content = new BorderPane();
        content.getStyleClass().add("editor-tab-content");
        content.setTop(header);
        content.setCenter(table);
        content.setBottom(toolbar);

        setContent(content);
    }

    private static String buildTabTitle(String cid, String language) {
        String safe = (cid == null || cid.isBlank()) ? "?" : cid;
        return safe + " [" + language + "]";
    }

    private String resolveCid(LineDatabaseData data, String fallbackCid) {
        if (data.character != null && !data.character.isBlank()) {
            return data.character.trim();
        }
        String fromRegistry = ProjectValidator.resolveCidByName(session, fallbackCid);
        return fromRegistry != null ? fromRegistry : fallbackCid;
    }

    private void applyCidChange(TextField cidField) {
        String newCid = cidField.getText() == null ? "" : cidField.getText().trim();
        if (newCid.equals(cid)) return;

        if (!referencedDids.isEmpty()) {
            String node = DigestService.usageNode(digest, cid, referencedDids.iterator().next());
            Alert warn = new Alert(Alert.AlertType.CONFIRMATION,
                    I18n.t("editor.lines.used", referencedDids.iterator().next(),
                            node == null ? "?" : node),
                    ButtonType.OK, ButtonType.CANCEL);
            warn.setHeaderText(null);
            if (warn.showAndWait().orElse(ButtonType.CANCEL) != ButtonType.OK) {
                cidField.setText(cid);
                return;
            }
        }

        beginEdit(I18n.t("editor.undo.change_cid"));
        cid = newCid;
        setText(buildTabTitle(cid, language));
        save();
    }

    private boolean isReferenced(int did) {
        return referencedDids.contains(did);
    }

    private void insertBetween(int selectedIndex) {
        int insertAt = selectedIndex < 0 ? items.size() : selectedIndex;

        int previousDid = 0;
        if (insertAt > 0) previousDid = items.get(insertAt - 1).did;

        int nextDid = Integer.MAX_VALUE;
        if (insertAt < items.size()) nextDid = items.get(insertAt).did;

        if (nextDid - previousDid <= 1) {
            Alert alert = new Alert(Alert.AlertType.INFORMATION,
                    I18n.t("editor.lines.no_gap"), ButtonType.OK);
            alert.setHeaderText(null);
            alert.showAndWait();
            return;
        }

        int newDid = previousDid + 1;
        while (containsDid(newDid) && newDid < nextDid) newDid++;
        if (newDid >= nextDid) {
            newDid = previousDid + (nextDid - previousDid) / 2;
        }

        beginEdit(I18n.t("editor.undo.insert_line"));
        LineEntry entry = new LineEntry();
        entry.did = newDid;
        entry.text = "";
        items.add(insertAt, entry);
        items.sort(Comparator.comparingInt(l -> l.did));
        save();
    }

    private void refreshHistoryButtons() {
        if (historyButtons.length < 2) return;
        historyButtons[0].setDisable(!canUndo());
        historyButtons[1].setDisable(!canRedo());
    }

    private boolean containsDid(int did) {
        for (LineEntry entry : items) {
            if (entry.did == did) return true;
        }
        return false;
    }

    private int nextLid() {
        return items.stream().mapToInt(l -> l.did).max().orElse(0) + 1;
    }

    private LineDatabaseData safeRead(String fallbackCid) {
        try {
            return session.readLines(language, fallbackCid);
        } catch (Exception e) {
            LineDatabaseData d = new LineDatabaseData();
            d.character = fallbackCid;
            d.language = language;
            return d;
        }
    }

    private void save() {
        try {
            session.writeLinesAt(getFilePath(), currentData());
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private LineDatabaseData currentData() {
        LineDatabaseData data = new LineDatabaseData();
        data.character = cid;
        data.language = language;
        data.lines = List.copyOf(items);
        return data;
    }

    @Override
    protected byte[] captureState() {
        try {
            return YamlIO.writeLines(currentData());
        } catch (Exception e) {
            return null;
        }
    }

    @Override
    protected void applyState(byte[] state) {
        try {
            LineDatabaseData restored = YamlIO.readLines(state);
            items.setAll(restored.lines == null ? List.of() : restored.lines);
            items.sort(Comparator.comparingInt(l -> l.did));
            save();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}
