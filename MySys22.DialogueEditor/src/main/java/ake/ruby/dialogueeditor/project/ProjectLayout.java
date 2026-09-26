package ake.ruby.dialogueeditor.project;

import java.nio.file.Path;

public final class ProjectLayout {

    public static final Path MANIFEST = Path.of(".project.yaml");
    public static final Path REGISTRY_LOG = Path.of(".registry.log");
    public static final Path LINES_DIR = Path.of("lines");
    public static final Path CHARACTERS_DIR = Path.of("Characters");
    public static final Path CHARACTER_LIST = CHARACTERS_DIR.resolve("characters.list.yaml");

    public static final Path GRAPHS_DIR = Path.of("graphs");

    public static final Path VARIABLES_DIR = Path.of("variables");
    public static final Path VARIABLES_FILE = VARIABLES_DIR.resolve("variables.yaml");

    public static final Path ENGINE_DIR = Path.of(".engine");

    public static final Path DIGEST = ENGINE_DIR.resolve("graphdigest.json");

    private ProjectLayout() {}

    public static Path linesFor(String language, String characterId) {
        return LINES_DIR.resolve(language).resolve(characterId + ".yaml");
    }
}
