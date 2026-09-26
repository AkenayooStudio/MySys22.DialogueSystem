package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.SettingsData;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

public final class SettingsStore {

    private static final Path STORE_PATH = Path.of(
            System.getProperty("user.home"),
            ".mysys22-dialogue-editor",
            "settings.yaml"
    );

    private SettingsStore() {}

    public static SettingsData load() {
        try {
            if (!Files.exists(STORE_PATH)) return new SettingsData();
            byte[] raw = Files.readAllBytes(STORE_PATH);
            SettingsData data = YamlIO.readSettings(raw);
            return data != null ? data : new SettingsData();
        } catch (IOException e) {
            System.err.println("[SettingsStore] Failed to load: " + e.getMessage());
            return new SettingsData();
        }
    }

    public static void save(SettingsData data) {
        try {
            Files.createDirectories(STORE_PATH.getParent());
            Files.write(STORE_PATH, YamlIO.writeSettings(data));
        } catch (IOException e) {
            System.err.println("[SettingsStore] Failed to save: " + e.getMessage());
        }
    }

    public static Path path() {
        return STORE_PATH;
    }
}
