package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.SyncService;
import ake.ruby.dialogueeditor.storage.SftpStorage;

import javafx.application.Platform;
import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.PasswordField;
import javafx.scene.control.TextArea;
import javafx.scene.control.TreeItem;
import javafx.scene.control.TreeView;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;
import org.kordamp.ikonli.javafx.FontIcon;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.List;

public class SftpPanel extends VBox {

    private static final double DEFAULT_HEIGHT = 280;
    private static final double MIN_HEIGHT = 120;

    private final ProjectSession session;
    private final Runnable onClose;

    private final TextArea log = new TextArea();
    private final TreeView<Path> remoteTree = new TreeView<>();
    private final PasswordField passwordField = new PasswordField();
    private final Label statusLabel = new Label();

    private SftpStorage sftp;
    private String activePassword;
    private double resizeStartH, resizeStartY;

    public SftpPanel(ProjectSession session, Runnable onClose) {
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

        Label title = new Label(I18n.t("editor.sftp"));
        title.getStyleClass().add("bottom-title");

        statusLabel.getStyleClass().add("bottom-status");
        statusLabel.setText("");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button reloadBtn = new Button();
        reloadBtn.getStyleClass().add("bottom-icon-btn");
        FontIcon reloadIcon = new FontIcon("bi-arrow-clockwise");
        reloadIcon.setIconSize(13);
        reloadBtn.setGraphic(reloadIcon);
        reloadBtn.setOnAction(e -> reloadSession());

        Button downloadBtn = new Button(I18n.t("editor.sftp.download"));
        downloadBtn.getStyleClass().add("bottom-text-btn");
        downloadBtn.setOnAction(e -> doDownload());

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("bottom-close-btn");
        closeBtn.setOnAction(e -> {
            disconnect();
            if (onClose != null) onClose.run();
        });

        header.getChildren().addAll(grip, title, statusLabel, spacer,
                reloadBtn, downloadBtn, closeBtn);

        HBox authRow = new HBox(8);
        authRow.setAlignment(Pos.CENTER_LEFT);
        authRow.getStyleClass().add("sftp-auth-row");

        Label pwLabel = new Label(I18n.t("editor.sftp.password") + ":");
        pwLabel.getStyleClass().add("editor-field-label");

        passwordField.getStyleClass().add("editor-field");
        passwordField.setPrefWidth(200);
        HBox.setHgrow(passwordField, Priority.ALWAYS);

        Button connectBtn = new Button(I18n.t("editor.sftp.connect"));
        connectBtn.getStyleClass().add("modal-btn-primary");
        connectBtn.setOnAction(e -> connect());

        authRow.getChildren().addAll(pwLabel, passwordField, connectBtn);

        remoteTree.getStyleClass().add("editor-tree");
        remoteTree.setShowRoot(true);
        HBox.setHgrow(remoteTree, Priority.ALWAYS);
        remoteTree.setPrefWidth(400);

        log.setEditable(false);
        log.setWrapText(false);
        log.getStyleClass().add("console-output");
        HBox.setHgrow(log, Priority.ALWAYS);

        HBox body = new HBox(remoteTree, log);
        body.setAlignment(Pos.TOP_LEFT);
        VBox.setVgrow(body, Priority.ALWAYS);
        body.setSpacing(8);
        body.getStyleClass().add("sftp-body");

        getChildren().addAll(header, authRow, body);

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

        var m = session.manifest();
        if (m.sftpHost == null || m.sftpHost.isBlank()
                || m.sftpUser == null || m.sftpUser.isBlank()) {
            authRow.setVisible(false);
            authRow.setManaged(false);
            appendLog("[sftp] " + I18n.t("editor.sftp.not_configured"));
            return;
        }

        if ("password".equals(m.sftpAuthType)) {
            authRow.setVisible(true);
            authRow.setManaged(true);
            appendLog("[sftp] " + I18n.t("editor.sftp.enter_password"));
        } else {
            authRow.setVisible(false);
            authRow.setManaged(false);
            connect();
        }
    }

    private void connect() {
        var m = session.manifest();

        String password = null;
        if ("password".equals(m.sftpAuthType)) {
            password = passwordField.getText();
            if (password == null || password.isEmpty()) {
                appendLog("[sftp] Password required.");
                return;
            }
        }
        activePassword = password;

        final String pw = password;
        setStatus(I18n.t("editor.sftp.connecting"));

        new Thread(() -> {
            try {
                Path keyPath = null;
                if ("key".equals(m.sftpAuthType) && m.sftpKeyPath != null
                        && !m.sftpKeyPath.isBlank()) {
                    keyPath = Path.of(m.sftpKeyPath);
                }

                SftpStorage.Config cfg = new SftpStorage.Config(
                        m.sftpHost, m.sftpPort, m.sftpUser,
                        keyPath, pw,
                        m.sftpRemoteRoot != null ? m.sftpRemoteRoot : "/"
                );
                SftpStorage storage = new SftpStorage(cfg);

                Platform.runLater(() -> {
                    this.sftp = storage;
                    appendLog("[sftp] " + I18n.t("editor.sftp.connected")
                            + " " + m.sftpUser + "@" + m.sftpHost);
                    setStatus(I18n.t("editor.sftp.connected"));
                    loadRemoteTree();
                });
            } catch (Exception e) {
                Platform.runLater(() -> {
                    appendLog("[sftp] Error: " + e.getMessage());
                    setStatus(I18n.t("editor.sftp.failed"));
                });
            }
        }, "sftp-connect").start();
    }

    private void disconnect() {
        if (sftp != null) {
            try { sftp.close(); } catch (Exception ignored) {}
            sftp = null;
        }
    }

    private void reloadSession() {
        disconnect();
        remoteTree.setRoot(null);
        connect();
    }

    private void loadRemoteTree() {
        if (sftp == null) return;

        new Thread(() -> {
            try {
                String root = session.manifest().sftpRemoteRoot;
                if (root == null || root.isBlank()) root = "/";

                TreeItem<Path> rootItem = new TreeItem<>(Path.of(root));
                rootItem.setExpanded(true);

                loadChildren(rootItem, Path.of(root), 0);

                Platform.runLater(() -> remoteTree.setRoot(rootItem));
            } catch (Exception e) {
                Platform.runLater(() -> appendLog("[sftp] Tree load error: " + e.getMessage()));
            }
        }, "sftp-tree").start();
    }

    private void loadChildren(TreeItem<Path> parent, Path dir, int depth) {
        if (depth > 8) return;
        try {
            List<Path> entries = sftp.list(dir);
            entries.sort(Comparator.comparing(p -> p.getFileName().toString()));

            for (Path entry : entries) {
                String name = entry.getFileName().toString();
                if (name.equals(".") || name.equals("..")) continue;

                TreeItem<Path> item = new TreeItem<>(entry);

                try {
                    List<Path> sub = sftp.list(entry);
                    if (sub != null) {
                        loadChildren(item, entry, depth + 1);
                        item.setExpanded(false);
                    }
                } catch (IOException notDir) {

                }
                parent.getChildren().add(item);
            }
        } catch (IOException e) {

        }
    }

    private void doDownload() {
        if (sftp == null) {
            appendLog("[sftp] Not connected.");
            return;
        }

        TreeItem<Path> selected = remoteTree.getSelectionModel().getSelectedItem();
        if (selected == null) {
            appendLog("[sftp] " + I18n.t("editor.sftp.select_folder"));
            return;
        }

        Path remoteDir = selected.getValue();
        String localPathStr = session.manifest().sftpLocalPath;
        if (localPathStr == null || localPathStr.isBlank()) {
            localPathStr = defaultLocalPath();
        }
        Path localTarget = Path.of(localPathStr);

        appendLog("[sftp] Downloading " + remoteDir + " -> " + localTarget);

        new Thread(() -> {
            try {
                int count = SyncService.downloadRecursive(
                        sftp, remoteDir, localTarget,
                        (p, isDir) -> Platform.runLater(() -> {
                            if (!isDir) appendLog("  + " + p.getFileName());
                        })
                );
                Platform.runLater(() -> appendLog("[sftp] Done. Files: " + count));
            } catch (Exception e) {
                Platform.runLater(() -> appendLog("[sftp] Download error: " + e.getMessage()));
            }
        }, "sftp-download").start();
    }

    private String defaultLocalPath() {
        String home = System.getProperty("user.home");
        String id = session.manifest().projectId;
        if (id == null || id.isBlank()) id = "sftp_project";
        return home + "/Documents/Akenayō/MySys22/DialogueEditor/" + id;
    }

    private void appendLog(String msg) {
        log.appendText(msg + "\n");
    }

    private void setStatus(String s) {
        statusLabel.setText("(" + s + ")");
    }
}
