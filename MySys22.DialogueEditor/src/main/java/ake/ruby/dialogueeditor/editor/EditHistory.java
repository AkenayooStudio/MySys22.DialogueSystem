package ake.ruby.dialogueeditor.editor;

import java.util.ArrayDeque;
import java.util.Deque;

public final class EditHistory {

    public record Step(byte[] snapshot, String label) {
    }

    private final int limit;
    private final Deque<Step> undoStack = new ArrayDeque<>();
    private final Deque<Step> redoStack = new ArrayDeque<>();

    public EditHistory(int limit) {
        this.limit = Math.max(1, limit);
    }

    public void push(byte[] snapshot, String label) {
        if (snapshot == null) return;
        undoStack.push(new Step(snapshot, label == null ? "" : label));
        redoStack.clear();
        while (undoStack.size() > limit) undoStack.removeLast();
    }

    public Step undo(byte[] currentState) {
        if (undoStack.isEmpty()) return null;
        Step step = undoStack.pop();
        if (currentState != null) redoStack.push(new Step(currentState, step.label()));
        return step;
    }

    public Step redo(byte[] currentState) {
        if (redoStack.isEmpty()) return null;
        Step step = redoStack.pop();
        if (currentState != null) undoStack.push(new Step(currentState, step.label()));
        return step;
    }

    public boolean canUndo() {
        return !undoStack.isEmpty();
    }

    public boolean canRedo() {
        return !redoStack.isEmpty();
    }

    public String undoLabel() {
        Step step = undoStack.peek();
        return step == null ? null : step.label();
    }

    public String redoLabel() {
        Step step = redoStack.peek();
        return step == null ? null : step.label();
    }

    public int undoDepth() {
        return undoStack.size();
    }

    public int redoDepth() {
        return redoStack.size();
    }

    public void clear() {
        undoStack.clear();
        redoStack.clear();
    }
}
