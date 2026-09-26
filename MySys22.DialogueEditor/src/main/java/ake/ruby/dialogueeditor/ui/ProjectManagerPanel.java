package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.storage.LocalStorage;

import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.Node;
import javafx.scene.control.Alert;
import javafx.scene.control.ButtonType;
import javafx.scene.control.ContextMenu;
import javafx.scene.control.Label;
import javafx.scene.control.MenuItem;
import javafx.scene.control.SeparatorMenuItem;
import javafx.scene.control.TextInputDialog;
import javafx.scene.control.TreeCell;
import javafx.scene.control.TreeItem;
import javafx.scene.control.TreeView;
import javafx.scene.input.ClipboardContent;
import javafx.scene.input.Dragboard;
import javafx.scene.input.MouseEvent;
import javafx.scene.input.TransferMode;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;
import org.kordamp.ikonli.javafx.FontIcon;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.function.Consumer;
import java.util.stream.Stream;

public class ProjectManagerPanel extends VBox {

    private static final double DEFAULT_WIDTH = 300;
    private static final double MIN_WIDTH = 220;
    private static final double GRIP_SIZE = 6;

    private static final String PROJECT_YAML = ".project.yaml";
    private static final String REGISTRY_LOG = ".registry.log";

    private final ProjectSession session;
    private final Path rootPath;

    private final TreeView<Path> tree;
    private TreeItem<Path> draggedItem;
    private Consumer<Path> onFileOpen;

    private double resizeStartW, resizeStartX;

    public ProjectManagerPanel(ProjectSession session) {
        this.session = session;
        this.rootPath = (session.storage() instanceof LocalStorage local)
                ? local.root()
                : null;

        getStyleClass().add("pm-panel");
        setPrefWidth(DEFAULT_WIDTH);
        setMinWidth(MIN_WIDTH);

        HBox header = new HBox();
        header.getStyleClass().add("pm-header");
        header.setAlignment(Pos.CENTER_LEFT);
        Label title = new Label(I18n.t("editor.project_manager"));
        title.getStyleClass().add("pm-title");
        header.getChildren().add(title);

        tree = new TreeView<>();
        tree.getStyleClass().add("pm-tree");
        tree.setShowRoot(true);
        tree.setCellFactory(tv -> new FileTreeCell());
        installEmptyClickDeselect();
        VBox.setVgrow(tree, Priority.ALWAYS);

        Region grip = new Region();
        grip.getStyleClass().add("pm-grip");
        grip.setPrefWidth(GRIP_SIZE);
        grip.setMaxWidth(GRIP_SIZE);
        grip.setCursor(Cursor.H_RESIZE);
        grip.setMaxHeight(Double.MAX_VALUE);

        HBox body = new HBox();
        body.setAlignment(Pos.TOP_LEFT);
        VBox.setVgrow(body, Priority.ALWAYS);
        HBox.setHgrow(tree, Priority.ALWAYS);
        body.getChildren().addAll(tree, grip);

        getChildren().addAll(header, body);

        grip.setOnMousePressed(e -> {
            resizeStartW = getWidth();
            resizeStartX = e.getSceneX();
        });
        grip.setOnMouseDragged(e -> {
            double dx = e.getSceneX() - resizeStartX;
            double newW = Math.max(MIN_WIDTH, resizeStartW + dx);
            setPrefWidth(newW);
        });

        rebuildTree();
    }

    public void setOnFileOpen(Consumer<Path> handler) {
        this.onFileOpen = handler;
    }

    public void rebuildTree() {
        if (rootPath == null || !Files.exists(rootPath)) return;

        TreeItem<Path> rootItem = new TreeItem<>(rootPath);
        rootItem.setExpanded(true);

        try (Stream<Path> stream = Files.walk(rootPath)) {
            stream.filter(p -> !p.equals(rootPath))
                    .filter(this::isVisible)
                    .sorted(Comparator.comparing(Path::toString))
                    .forEach(p -> addNode(rootItem, p));
        } catch (IOException e) {
            e.printStackTrace();
        }

        tree.setRoot(rootItem);
    }

    private boolean isVisible(Path p) {
        Path rel = rootPath.relativize(p);
        for (Path seg : rel) {
            String s = seg.toString();
            if (s.equals(".git") || s.equals("target") || s.equals(".idea")) return false;
        }
        return true;
    }

    private void addNode(TreeItem<Path> rootItem, Path file) {
        Path rel = rootPath.relativize(file);
        TreeItem<Path> parent = rootItem;
        for (int i = 0; i < rel.getNameCount() - 1; i++) {
            parent = findOrCreate(parent, rootPath.resolve(rel.subpath(0, i + 1)));
        }
        TreeItem<Path> leaf = new TreeItem<>(file);
        if (Files.isDirectory(file)) leaf.setExpanded(true);
        parent.getChildren().add(leaf);
    }

    private TreeItem<Path> findOrCreate(TreeItem<Path> parent, Path path) {
        for (TreeItem<Path> child : parent.getChildren()) {
            if (child.getValue().equals(path)) return child;
        }
        TreeItem<Path> item = new TreeItem<>(path);
        item.setExpanded(true);
        parent.getChildren().add(item);
        return item;
    }

    private ContextMenu buildContextMenu(TreeItem<Path> item) {
        ContextMenu menu = new ContextMenu();
        boolean isLocal = rootPath != null;
        boolean isRoot = isLocal && item.getValue().equals(rootPath);
        boolean isDir = isLocal && Files.isDirectory(item.getValue());
        Path targetDir = isDir ? item.getValue() : item.getValue().getParent();

        MenuItem newFile = new MenuItem(I18n.t("pm.new_file"));
        newFile.setDisable(!isLocal);
        newFile.setOnAction(e -> doCreateFile(targetDir));

        MenuItem newFolder = new MenuItem(I18n.t("pm.new_folder"));
        newFolder.setDisable(!isLocal);
        newFolder.setOnAction(e -> doCreateFolder(targetDir));

        MenuItem delete = new MenuItem(I18n.t("pm.delete"));
        delete.getStyleClass().add("menu-item-danger");
        boolean canDelete = isLocal && !isRoot && !isProtected(item.getValue());
        delete.setDisable(!canDelete);
        delete.setOnAction(e -> doDelete(item));

        menu.getItems().addAll(newFile, newFolder, new SeparatorMenuItem(), delete);
        menu.getStyleClass().add("project-context-menu");
        return menu;
    }

    private boolean isProtected(Path p) {
        String name = p.getFileName().toString();
        return name.equals(PROJECT_YAML) || name.equals(REGISTRY_LOG);
    }

    private void doCreateFile(Path parent) {
        String name = promptForName(I18n.t("pm.new_file_prompt"), "untitled");
        if (name == null || name.isBlank()) return;

        name = name.trim();
        if (!name.toLowerCase().endsWith(".yaml") && !name.toLowerCase().endsWith(".yml")) {
            name = name + ".yaml";
        }

        Path target = parent.resolve(name);
        if (Files.exists(target)) {
            showError(I18n.t("pm.error_exists"));
            return;
        }

        try {
            Files.createDirectories(parent);
            Files.writeString(target, "");
            rebuildTree();
            selectPath(target);
        } catch (IOException e) {
            showError(e.getMessage());
        }
    }

    private void doCreateFolder(Path parent) {
        String name = promptForName(I18n.t("pm.new_folder_prompt"), "newfolder");
        if (name == null || name.isBlank()) return;

        Path target = parent.resolve(name);
        if (Files.exists(target)) {
            showError(I18n.t("pm.error_exists"));
            return;
        }

        try {
            Files.createDirectories(target);
            rebuildTree();
            selectPath(target);
        } catch (IOException e) {
            showError(e.getMessage());
        }
    }

    private void doDelete(TreeItem<Path> item) {
        Path p = item.getValue();

        Alert confirm = new Alert(Alert.AlertType.CONFIRMATION);
        confirm.setTitle(I18n.t("pm.delete"));
        confirm.setHeaderText(I18n.t("pm.delete_title"));
        confirm.setContentText(I18n.t("pm.delete_body", p.getFileName().toString()));
        confirm.getDialogPane().getStylesheets().add(
                ProjectManagerPanel.class.getResource("/css/main.css").toExternalForm());
        confirm.getDialogPane().getStyleClass().add("custom-dialog");

        var result = confirm.showAndWait();
        if (result.isEmpty() || result.get() != ButtonType.OK) return;

        try {
            deleteRecursive(p);
            rebuildTree();
        } catch (IOException e) {
            showError(e.getMessage());
        }
    }

    private void deleteRecursive(Path p) throws IOException {
        if (!Files.exists(p)) return;
        if (Files.isDirectory(p)) {
            try (Stream<Path> s = Files.list(p)) {
                for (Path c : s.toList()) deleteRecursive(c);
            }
        }
        Files.deleteIfExists(p);
    }

    private String promptForName(String title, String defaultValue) {
        TextInputDialog dialog = new TextInputDialog(defaultValue);
        dialog.setTitle(title);
        dialog.setHeaderText(null);
        dialog.setContentText(I18n.t("pm.name"));
        dialog.getDialogPane().getStylesheets().add(
                ProjectManagerPanel.class.getResource("/css/main.css").toExternalForm());
        dialog.getDialogPane().getStyleClass().add("custom-dialog");
        return dialog.showAndWait().orElse(null);
    }

    private void showError(String msg) {
        Alert a = new Alert(Alert.AlertType.ERROR);
        a.setTitle(I18n.t("pm.error"));
        a.setHeaderText(null);
        a.setContentText(msg != null ? msg : "");
        a.getDialogPane().getStylesheets().add(
                ProjectManagerPanel.class.getResource("/css/main.css").toExternalForm());
        a.getDialogPane().getStyleClass().add("custom-dialog");
        a.showAndWait();
    }

    private void selectPath(Path p) {
        walkAndSelect(tree.getRoot(), p);
    }

    private boolean walkAndSelect(TreeItem<Path> node, Path target) {
        if (node.getValue().equals(target)) {
            tree.getSelectionModel().select(node);
            int idx = tree.getRow(node);
            if (idx >= 0) tree.scrollTo(idx);
            return true;
        }
        for (TreeItem<Path> child : node.getChildren()) {
            if (walkAndSelect(child, target)) return true;
        }
        return false;
    }

    private void installEmptyClickDeselect() {
        tree.addEventFilter(MouseEvent.MOUSE_PRESSED, e -> {
            Node n = e.getPickResult().getIntersectedNode();
            Node cell = n;
            while (cell != null && !(cell instanceof TreeCell)) {
                cell = cell.getParent();
            }
            if (cell == null) {
                tree.getSelectionModel().clearSelection();
                e.consume();
                return;
            }
            TreeCell<?> tc = (TreeCell<?>) cell;
            if (tc.isEmpty()) {
                tree.getSelectionModel().clearSelection();
                e.consume();
            }
        });
    }

    private class FileTreeCell extends TreeCell<Path> {
        private final FontIcon icon = new FontIcon();

        FileTreeCell() {
            setOnMouseClicked(e -> {
                if (e.getClickCount() == 2 && getTreeItem() != null) {
                    Path p = getTreeItem().getValue();
                    if (Files.isRegularFile(p) && onFileOpen != null) {
                        onFileOpen.accept(p);
                    }
                }
            });

            setOnContextMenuRequested(e -> {
                TreeItem<Path> item = getTreeItem();
                if (item == null) return;
                tree.getSelectionModel().select(item);
                ContextMenu menu = buildContextMenu(item);
                menu.show(this, e.getScreenX(), e.getScreenY());
                e.consume();
            });

            setOnDragDetected(e -> {
                TreeItem<Path> item = getTreeItem();
                if (item == null || rootPath == null) return;
                if (isProtected(item.getValue())) return;
                if (item.getValue().equals(rootPath)) return;

                draggedItem = item;
                Dragboard db = startDragAndDrop(TransferMode.MOVE);
                ClipboardContent content = new ClipboardContent();
                content.putString(item.getValue().toString());
                db.setContent(content);
                e.consume();
            });

            setOnDragOver(e -> {
                if (draggedItem == null) return;
                TreeItem<Path> target = getTreeItem();
                if (target == null) return;
                if (!Files.isDirectory(target.getValue())) return;
                if (isInvalidDrop(target)) return;
                e.acceptTransferModes(TransferMode.MOVE);
                e.consume();
            });

            setOnDragEntered(e -> {
                TreeItem<Path> target = getTreeItem();
                if (target == null || draggedItem == null) return;
                if (isInvalidDrop(target)) return;
                getStyleClass().add("drop-target");
            });

            setOnDragExited(e -> getStyleClass().remove("drop-target"));

            setOnDragDropped(e -> {
                TreeItem<Path> target = getTreeItem();
                if (target == null || draggedItem == null) return;
                if (isInvalidDrop(target)) return;
                try {
                    Path source = draggedItem.getValue();
                    Path targetDir = target.getValue();
                    Path dest = targetDir.resolve(source.getFileName());
                    if (Files.exists(dest)) {
                        showError(I18n.t("pm.error_exists"));
                        e.setDropCompleted(false);
                        e.consume();
                        return;
                    }
                    Files.move(source, dest);
                    e.setDropCompleted(true);
                    rebuildTree();
                } catch (IOException ex) {
                    showError(ex.getMessage());
                    e.setDropCompleted(false);
                }
                draggedItem = null;
                e.consume();
            });

            setOnDragDone(e -> {
                getStyleClass().remove("drop-target");
                draggedItem = null;
            });
        }

        private boolean isInvalidDrop(TreeItem<Path> target) {
            if (draggedItem == null) return true;
            Path src = draggedItem.getValue();
            Path dst = target.getValue();

            if (src.equals(dst)) return true;
            if (Files.isDirectory(src) && dst.startsWith(src)) return true;
            if (isProtected(src)) return true;
            if (src.equals(rootPath)) return true;

            Path parent = src.getParent();
            if (parent != null && parent.equals(dst)) return true;
            return false;
        }

        @Override
        protected void updateItem(Path item, boolean empty) {
            super.updateItem(item, empty);
            if (empty || item == null) {
                setText(null);
                setGraphic(null);
                return;
            }
            setText(item.getFileName() != null
                    ? item.getFileName().toString()
                    : item.toString());
            icon.setIconSize(14);
            icon.setIconLiteral(iconCodeFor(item));
            icon.getStyleClass().clear();
            icon.getStyleClass().add("pm-tree-icon");
            setGraphic(icon);
        }

        private String iconCodeFor(Path p) {
            if (Files.isDirectory(p)) return "bxs-folder";
            String name = p.getFileName().toString().toLowerCase();
            if (name.equals(PROJECT_YAML)) return "antf-project";
            if (name.equals(REGISTRY_LOG)) return "bi-file-text";
            if (name.endsWith(".yaml") || name.endsWith(".yml")) return "bi-file-code";
            return "bi-file-earmark-text";
        }
    }
}
