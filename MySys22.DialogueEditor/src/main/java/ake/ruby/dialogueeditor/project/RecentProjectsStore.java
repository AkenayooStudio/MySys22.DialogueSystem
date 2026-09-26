package ake.ruby.dialogueeditor.project;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

public final class RecentProjectsStore {

    private static final int MAX_ENTRIES = 20;
    private static final Path STORE_PATH = resolveStorePath();

    private static final ObjectMapper MAPPER = new ObjectMapper()
            .enable(SerializationFeature.INDENT_OUTPUT);

    private RecentProjectsStore() {}

    private static Path resolveStorePath() {
        String home = System.getProperty("user.home");
        return Path.of(home, ".mysys22-dialogue-editor", "recent-projects.json");
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private static class Wrapper {
        public List<RecentProject> projects = new ArrayList<>();
    }

    public static List<RecentProject> load() {
        try {
            if (!Files.exists(STORE_PATH)) return new ArrayList<>();
            Wrapper w = MAPPER.readValue(STORE_PATH.toFile(), Wrapper.class);
            if (w == null || w.projects == null) return new ArrayList<>();

            List<RecentProject> out = new ArrayList<>(w.projects);
            out.sort(Comparator.comparing(
                    (RecentProject p) -> p.lastOpenedAt != null ? p.lastOpenedAt : "",
                    Comparator.reverseOrder()));
            return out;
        } catch (IOException e) {
            System.err.println("[RecentProjectsStore] Failed to load: " + e.getMessage());
            return new ArrayList<>();
        }
    }

    public static synchronized void touch(RecentProject entry) {
        if (entry == null) return;
        entry.lastOpenedAt = Instant.now().toString();
        if (entry.editorVersion == null) {
            entry.editorVersion = ProjectSession.EDITOR_VERSION;
        }

        List<RecentProject> list = load();
        String key = entry.stableKey();
        list.removeIf(p -> key.equals(p.stableKey()));
        list.add(0, entry);

        if (list.size() > MAX_ENTRIES) {
            list = new ArrayList<>(list.subList(0, MAX_ENTRIES));
        }

        Wrapper w = new Wrapper();
        w.projects = list;

        try {
            Files.createDirectories(STORE_PATH.getParent());
            MAPPER.writeValue(STORE_PATH.toFile(), w);
        } catch (IOException e) {
            System.err.println("[RecentProjectsStore] Failed to save: " + e.getMessage());
        }
    }

    public static synchronized void remove(RecentProject entry) {
        if (entry == null) return;
        List<RecentProject> list = load();
        String key = entry.stableKey();
        list.removeIf(p -> key.equals(p.stableKey()));

        Wrapper w = new Wrapper();
        w.projects = list;

        try {
            Files.createDirectories(STORE_PATH.getParent());
            MAPPER.writeValue(STORE_PATH.toFile(), w);
        } catch (IOException e) {
            System.err.println("[RecentProjectsStore] Failed to remove: " + e.getMessage());
        }
    }
}
