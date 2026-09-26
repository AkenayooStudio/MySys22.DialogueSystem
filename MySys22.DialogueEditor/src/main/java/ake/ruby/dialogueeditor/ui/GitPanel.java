package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;

import javafx.application.Platform;
import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.TextArea;
import javafx.scene.control.TextField;
import javafx.scene.input.KeyCode;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.concurrent.atomic.AtomicBoolean;

public class GitPanel extends VBox {

    private static final double DEFAULT_HEIGHT = 220;
    private static final double MIN_HEIGHT = 80;

    private final TextArea output;
    private final TextField input;
    private final Label promptLabel;

    private Process process;
    private BufferedWriter processStdin;
    private final AtomicBoolean alive = new AtomicBoolean(true);

    private final Path workingDir;
    private final Runnable onClose;
    private final String userAtHost;

    private double resizeStartH, resizeStartY;

    public GitPanel(Path workingDir, Runnable onClose) {
        this.workingDir = workingDir;
        this.onClose = onClose;
        this.userAtHost = buildUserAtHost();

        getStyleClass().add("bottom-panel");
        setPrefHeight(DEFAULT_HEIGHT);
        setMinHeight(MIN_HEIGHT);

        HBox header = new HBox();
        header.getStyleClass().add("bottom-header");
        header.setAlignment(Pos.CENTER_LEFT);

        Label grip = new Label("\u22EE\u22EE");
        grip.getStyleClass().add("bottom-grip");

        Label title = new Label(I18n.t("editor.git") + "  (git-only)");
        title.getStyleClass().add("bottom-title");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("bottom-close-btn");
        closeBtn.setOnAction(e -> {
            stopProcess();
            if (onClose != null) onClose.run();
        });

        header.getChildren().addAll(grip, title, spacer, closeBtn);

        output = new TextArea();
        output.setEditable(false);
        output.setWrapText(false);
        output.getStyleClass().add("console-output");
        VBox.setVgrow(output, Priority.ALWAYS);

        promptLabel = new Label();
        promptLabel.getStyleClass().add("git-prompt");
        promptLabel.setText("[git] " + shortPath());

        input = new TextField();
        input.getStyleClass().add("console-input");
        HBox.setHgrow(input, Priority.ALWAYS);
        input.setOnKeyPressed(e -> {
            if (e.getCode() == KeyCode.ENTER) submit();
        });

        HBox inputRow = new HBox(6, promptLabel, input);
        inputRow.setAlignment(Pos.CENTER_LEFT);
        inputRow.getStyleClass().add("console-input-row");

        getChildren().addAll(header, output, inputRow);

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

        startProcess();
    }

    private String buildUserAtHost() {
        String user = System.getProperty("user.name", "user");
        String host;
        try {
            host = InetAddress.getLocalHost().getHostName();
            if (host == null || host.isBlank()) host = "localhost";
        } catch (Exception e) {
            host = "localhost";
        }
        return user + "@" + host;
    }

    private String shortPath() {
        if (workingDir == null) return "?";
        return workingDir.getFileName() != null
                ? workingDir.getFileName().toString()
                : workingDir.toString();
    }

    private void startProcess() {
        if (workingDir == null || !workingDir.toFile().exists()) {
            appendLine("[git] Project directory not available.");
            return;
        }

        String os = System.getProperty("os.name").toLowerCase();
        String[] cmd = os.contains("win")
                ? new String[]{"powershell.exe", "-NoLogo", "-NoProfile"}
                : new String[]{"/bin/bash", "--norc"};

        try {
            ProcessBuilder pb = new ProcessBuilder(cmd);
            pb.directory(workingDir.toFile());
            pb.redirectErrorStream(true);
            process = pb.start();
            processStdin = new BufferedWriter(new OutputStreamWriter(
                    process.getOutputStream(), StandardCharsets.UTF_8));

            new Thread(() -> {
                try (BufferedReader r = new BufferedReader(new InputStreamReader(
                        process.getInputStream(), StandardCharsets.UTF_8))) {
                    String line;
                    while (alive.get() && (line = r.readLine()) != null) {
                        String l = line;
                        Platform.runLater(() -> appendLine(l));
                    }
                } catch (IOException ignored) {}
            }, "git-reader").start();

            appendLine("[git] Shell ready in " + workingDir);
            appendLine("[git] Only 'git ...' commands are allowed.");
        } catch (IOException e) {
            appendLine("[git] Failed to start shell: " + e.getMessage());
        }
    }

    private void stopProcess() {
        alive.set(false);
        try {
            if (processStdin != null) {
                processStdin.write("exit\n");
                processStdin.flush();
            }
        } catch (IOException ignored) {}
        if (process != null) process.destroy();
    }

    private void submit() {
        String cmd = input.getText();
        if (cmd == null || cmd.isBlank()) {
            input.clear();
            return;
        }
        input.clear();

        String trimmed = cmd.trim();

        if (!isGitCommand(trimmed)) {
            appendLine("[git] BLOCKED: only 'git ...' commands are allowed. Got: " + trimmed);
            return;
        }

        appendLine("[git] " + shortPath() + "$ " + cmd);

        try {
            if (processStdin != null) {
                processStdin.write(cmd + "\n");
                processStdin.flush();
            }
        } catch (IOException e) {
            appendLine("[git] Write failed: " + e.getMessage());
        }
    }

    private boolean isGitCommand(String cmd) {
        if (cmd.equals("git")) return true;
        if (cmd.startsWith("git ")) return true;

        if (cmd.equals("clear") || cmd.equals("cls")) return true;
        if (cmd.equals("pwd")) return true;
        if (cmd.equals("exit")) return true;
        return false;
    }

    private void appendLine(String line) {
        output.appendText(line + "\n");
    }

    public void dispose() {
        stopProcess();
    }
}
