package ake.ruby.dialogueeditor.editor;

import javafx.scene.control.Tab;
import org.kordamp.ikonli.javafx.FontIcon;

import java.nio.file.Path;

public abstract class EditorTab extends Tab {

    private static final int HISTORY_LIMIT = 200;

    private final Path filePath;
    protected final EditHistory history = new EditHistory(HISTORY_LIMIT);

    private Runnable historyListener;

    protected EditorTab(Path filePath, String iconLiteral, String displayName) {
        this.filePath = filePath;

        FontIcon icon = new FontIcon(iconLiteral);
        icon.setIconSize(13);
        icon.getStyleClass().add("tab-file-icon");
        setGraphic(icon);
        setText(displayName);
        setClosable(true);
    }

    public Path getFilePath() {
        return filePath;
    }

    public void setHistoryListener(Runnable listener) {
        this.historyListener = listener;
    }

    public boolean isUndoable() {
        return true;
    }

    protected void beginEdit(String label) {
        if (!isUndoable()) return;
        byte[] snapshot = captureState();
        if (snapshot == null) return;
        history.push(snapshot, label);
        notifyHistoryChanged();
    }

    public boolean canUndo() {
        return isUndoable() && history.canUndo();
    }

    public boolean canRedo() {
        return isUndoable() && history.canRedo();
    }

    public String undoLabel() {
        return history.undoLabel();
    }

    public String redoLabel() {
        return history.redoLabel();
    }

    public boolean undo() {
        if (!canUndo()) return false;
        EditHistory.Step step = history.undo(captureState());
        if (step == null) return false;
        applyState(step.snapshot());
        notifyHistoryChanged();
        return true;
    }

    public boolean redo() {
        if (!canRedo()) return false;
        EditHistory.Step step = history.redo(captureState());
        if (step == null) return false;
        applyState(step.snapshot());
        notifyHistoryChanged();
        return true;
    }

    public void clearHistory() {
        history.clear();
        notifyHistoryChanged();
    }

    private void notifyHistoryChanged() {
        if (historyListener != null) historyListener.run();
    }

    protected byte[] captureState() {
        return null;
    }

    protected void applyState(byte[] state) {
        throw new UnsupportedOperationException(getClass().getSimpleName() + " is not undoable");
    }
}
