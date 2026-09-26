package ake.ruby.dialogueeditor.project;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

public final class ProjectShortcut {

    public static final String EXTENSION = ".mysys22";
    private static final String KEY = "project=";

    private ProjectShortcut() {
    }

    public static Path create(Path projectRoot, Path targetFile) throws IOException {
        Path root = projectRoot.toAbsolutePath().normalize();
        Path target = targetFile.toAbsolutePath().normalize();
        if (target.getParent() != null) Files.createDirectories(target.getParent());

        String content = "# MySys22 Dialogue Editor project shortcut\n"
                + KEY + root + "\n";
        Files.writeString(target, content, StandardCharsets.UTF_8);
        return target;
    }

    public static String defaultFileName(ProjectSession session) {
        String id = session.manifest().projectId;
        if (id == null || id.isBlank()) id = "project";
        return id + EXTENSION;
    }

    public static Path readTarget(Path shortcutFile) throws IOException {
        if (!Files.isRegularFile(shortcutFile)) {
            throw new IOException("Shortcut not found: " + shortcutFile);
        }

        for (String raw : Files.readAllLines(shortcutFile, StandardCharsets.UTF_8)) {
            String line = raw.trim();
            if (line.isEmpty() || line.startsWith("#")) continue;
            if (line.startsWith(KEY)) line = line.substring(KEY.length()).trim();
            if (line.isEmpty()) continue;

            Path path = Path.of(line);
            if (!path.isAbsolute()) {
                Path parent = shortcutFile.toAbsolutePath().normalize().getParent();
                path = parent == null ? path : parent.resolve(path);
            }
            return path.normalize();
        }

        throw new IOException("Shortcut has no 'project=' entry: " + shortcutFile);
    }

    public static boolean isShortcut(String argument) {
        return argument != null && argument.toLowerCase().endsWith(EXTENSION);
    }
}
