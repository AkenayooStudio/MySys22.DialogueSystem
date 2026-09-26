package ake.ruby.dialogueeditor.project;

import java.nio.file.Path;
import java.util.List;
import java.util.Locale;

public final class DialogueContract {

    public static final String ENGINE_NAME = "MySys22.DialogueEngine";

    public static final String LEGACY_ENGINE_FOLDER = ENGINE_NAME;

    public static final Path STREAMING_ROOT =
            Path.of("Assets", "StreamingAssets");

    public static final Path LEGACY_STREAMING_ROOT =
            Path.of("Assets", "StreamingAssets", LEGACY_ENGINE_FOLDER);

    public static final Path GRAPHS_DIR = Path.of("Graphs");

    public static final Path DIALOGUE_DIR = Path.of("Dialogue");

    public static final Path CHARACTERS_DIR = Path.of("Characters");

    public static final Path VARIABLES_DIR = Path.of("Variables");

    public static final Path VARIABLES_FILE = VARIABLES_DIR.resolve("variables.yaml");

    public static final Path CHARACTERS_FILE = CHARACTERS_DIR.resolve("characters.list.yaml");

    public static final Path ENGINE_MANIFEST = Path.of("engine.manifest.json");

    public static final Path ENGINE_DIGEST = Path.of("engine.graphdigest.json");

    public static final String INDEX_FILE = "manifest.txt";

    public static final List<String> LANGUAGES = List.of("EN", "IT", "ES", "RO", "JA");

    public static final String DEFAULT_LANGUAGE = "EN";

    private DialogueContract() {
    }

    public static String normalizeLanguage(String raw) {
        if (raw == null) return null;
        String v = raw.trim().toUpperCase(Locale.ROOT);
        return v.isEmpty() ? null : v;
    }

    public static boolean isSupportedLanguage(String raw) {
        String v = normalizeLanguage(raw);
        return v != null && LANGUAGES.contains(v);
    }

    public static Path lineFile(String language, String cid) {
        return DIALOGUE_DIR.resolve(normalizeLanguage(language)).resolve(cid + ".yaml");
    }
}
