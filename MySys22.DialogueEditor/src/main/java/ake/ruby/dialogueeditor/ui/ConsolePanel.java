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
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.concurrent.atomic.AtomicBoolean;

public class ConsolePanel extends VBox {

    private static final double DEFAULT_HEIGHT = 220;
    private static final double MIN_HEIGHT = 80;
    private static final int MAX_PROMPT_PATH = 60;

    private final TextArea output;
    private final TextField input;
    private final Label promptLabel;

    private Process process;
    private BufferedWriter processStdin;
    private Thread readerThread;
    private final AtomicBoolean alive = new AtomicBoolean(true);

    private Path currentDir;
    private final Path homeDir;
    private final String userAtHost;

    private final Runnable onClose;

    private double resizeStartH, resizeStartY;

    public ConsolePanel(Path workingDir, Runnable onClose) {
        this.onClose = onClose;
        this.currentDir = workingDir;
        this.homeDir = Path.of(System.getProperty("user.home")).toAbsolutePath().normalize();
        this.userAtHost = buildUserAtHost();

        getStyleClass().add("bottom-panel");
        setPrefHeight(DEFAULT_HEIGHT);
        setMinHeight(MIN_HEIGHT);

        HBox header = new HBox();
        header.getStyleClass().add("bottom-header");
        header.setAlignment(Pos.CENTER_LEFT);

        Label grip = new Label("\u22EE\u22EE");
        grip.getStyleClass().add("bottom-grip");

        Label title = new Label(I18n.t("editor.console"));
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
        promptLabel.getStyleClass().add("console-prompt");
        promptLabel.setText(promptText());

        input = new TextField();
        input.getStyleClass().add("console-input");
        HBox.setHgrow(input, Priority.ALWAYS);
        input.setOnKeyPressed(e -> {
            if (e.getCode() == KeyCode.ENTER) {
                submitCommand();
            }
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

    private String promptText() {
        if (currentDir == null) return userAtHost + " $";
        return userAtHost + ":" + displayPath(currentDir) + "$";
    }

    private String displayPath(Path dir) {
        if (dir == null) return "?";

        Path abs = dir.toAbsolutePath().normalize();
        String result;

        if (abs.startsWith(homeDir)) {
            Path rel = homeDir.relativize(abs);
            if (rel.getNameCount() == 0) {
                result = "~";
            } else {
                result = "~/" + rel.toString().replace('\\', '/');
            }
        } else {
            result = abs.toString().replace('\\', '/');
        }

        if (result.length() > MAX_PROMPT_PATH) {

            String[] parts = result.split("/");
            if (parts.length >= 2) {
                result = parts[0] + "/.../" + parts[parts.length - 1];
            } else {
                result = "..." + result.substring(result.length() - MAX_PROMPT_PATH + 3);
            }
        }
        return result;
    }

    private void startProcess() {
        if (currentDir == null || !currentDir.toFile().exists()) {
            appendLine("[console] Project directory not available.");
            return;
        }

        String os = System.getProperty("os.name").toLowerCase();
        String[] cmd = os.contains("win")
                ? new String[]{"powershell.exe", "-NoLogo", "-NoProfile"}
                : new String[]{"/bin/bash", "--norc"};

        try {
            ProcessBuilder pb = new ProcessBuilder(cmd);
            pb.directory(currentDir.toFile());
            pb.redirectErrorStream(true);
            process = pb.start();
            processStdin = new BufferedWriter(new OutputStreamWriter(
                    process.getOutputStream(), StandardCharsets.UTF_8));

            readerThread = new Thread(() -> {
                try (BufferedReader r = new BufferedReader(new InputStreamReader(
                        process.getInputStream(), StandardCharsets.UTF_8))) {
                    String line;
                    while (alive.get() && (line = r.readLine()) != null) {
                        String l = line;
                        Platform.runLater(() -> appendLine(l));
                    }
                } catch (IOException ignored) {
                }
            }, "console-reader");
            readerThread.setDaemon(true);
            readerThread.start();

            appendLine("[console] " + I18n.t("editor.console.ready") + " " + currentDir);
        } catch (IOException e) {
            appendLine("[console] Failed to start shell: " + e.getMessage());
        }
    }

    private void stopProcess() {
        alive.set(false);
        try {
            if (processStdin != null) {
                processStdin.write("exit\n");
                processStdin.flush();
            }
        } catch (IOException ignored) {
        }
        if (process != null) {
            process.destroy();
        }
    }

    private void submitCommand() {
        String cmd = input.getText();
        if (cmd == null || cmd.isBlank()) {
            input.clear();
            return;
        }
        input.clear();

        String trimmed = cmd.trim();

        appendLine(promptText() + " " + cmd);

        if (trimmed.equals("clear") || trimmed.equals("cls")) {
            output.clear();
            return;
        }

        if (trimmed.equals("cd") || trimmed.startsWith("cd ")) {
            handleCd(trimmed);

            sendToProcess(cmd);
            return;
        }

        sendToProcess(cmd);
    }

    private void handleCd(String cmd) {
        String arg = cmd.length() > 3 ? cmd.substring(3).trim() : "";

        Path target;
        if (arg.isEmpty() || arg.equals("~")) {
            target = homeDir;
        } else if (arg.startsWith("~/")) {
            target = homeDir.resolve(arg.substring(2));
        } else if (arg.startsWith("/")) {
            target = Path.of(arg);
        } else {
            target = currentDir.resolve(arg);
        }

        target = target.toAbsolutePath().normalize();

        if (!Files.exists(target) || !Files.isDirectory(target)) {
            appendLine("cd: no such directory: " + arg);
            return;
        }

        currentDir = target;
        updatePrompt();
    }

    private void sendToProcess(String cmd) {
        try {
            if (processStdin != null) {
                processStdin.write(cmd + "\n");
                processStdin.flush();
            }
        } catch (IOException e) {
            appendLine("[console] Write failed: " + e.getMessage());
        }
    }

    private void updatePrompt() {
        promptLabel.setText(promptText());
    }

    private void appendLine(String line) {
        output.appendText(line + "\n");
    }

    public void dispose() {
        stopProcess();
    }
}
