package ake.ruby.dialogueeditor.project;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public final class SessionStore {

    private static final Path BASE_DIR = Path.of(
            System.getProperty("user.home"),
            ".mysys22-dialogue-editor",
            "sessions"
    );

    private static final ObjectMapper MAPPER = new ObjectMapper()
            .enable(SerializationFeature.INDENT_OUTPUT);

    private SessionStore() {}

    private static Path fileFor(String projectId) {
        String safe = (projectId == null || projectId.isBlank())
                ? "unknown"
                : projectId.replaceAll("[^a-zA-Z0-9_\\-]", "_");
        return BASE_DIR.resolve(safe + ".json");
    }

    public static List<String> load(String projectId) {
        Path file = fileFor(projectId);
        if (!Files.exists(file)) return new ArrayList<>();

        try {
            List<String> list = MAPPER.readValue(
                    file.toFile(),
                    new TypeReference<List<String>>() {}
            );
            return list != null ? list : new ArrayList<>();
        } catch (IOException e) {
            System.err.println("[SessionStore] Failed to load: " + e.getMessage());
            return new ArrayList<>();
        }
    }

    public static void save(String projectId, List<String> relativePaths) {
        Path file = fileFor(projectId);
        try {
            Files.createDirectories(file.getParent());
            MAPPER.writeValue(file.toFile(), relativePaths);
        } catch (IOException e) {
            System.err.println("[SessionStore] Failed to save: " + e.getMessage());
        }
    }

    public static Path pathFor(String projectId) {
        return fileFor(projectId);
    }
}
