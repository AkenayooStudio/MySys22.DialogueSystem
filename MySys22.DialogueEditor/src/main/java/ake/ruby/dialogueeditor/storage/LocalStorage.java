package ake.ruby.dialogueeditor.storage;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.List;
import java.util.stream.Collectors;

public class LocalStorage implements StorageBackend {

    private final Path root;

    public LocalStorage(Path root) {
        this.root = root.toAbsolutePath().normalize();
    }

    public Path root() {
        return root;
    }

    @Override
    public String describe() {
        return "local:" + root;
    }

    private Path resolve(Path relative) {
        Path p = root.resolve(relative).normalize();
        if (!p.startsWith(root)) {
            throw new IllegalArgumentException("Path escapes root: " + relative);
        }
        return p;
    }

    @Override
    public boolean exists(Path relative) {
        return Files.exists(resolve(relative));
    }

    @Override
    public byte[] read(Path relative) throws IOException {
        return Files.readAllBytes(resolve(relative));
    }

    @Override
    public void write(Path relative, byte[] data) throws IOException {
        Path target = resolve(relative);
        Path parent = target.getParent();
        if (parent != null) Files.createDirectories(parent);
        Files.write(target, data,
                StandardOpenOption.CREATE,
                StandardOpenOption.WRITE,
                StandardOpenOption.TRUNCATE_EXISTING);
    }

    @Override
    public void append(Path relative, byte[] data) throws IOException {
        Path target = resolve(relative);
        Path parent = target.getParent();
        if (parent != null) Files.createDirectories(parent);
        Files.write(target, data,
                StandardOpenOption.CREATE,
                StandardOpenOption.WRITE,
                StandardOpenOption.APPEND);
    }

    @Override
    public void delete(Path relative) throws IOException {
        Files.deleteIfExists(resolve(relative));
    }

    @Override
    public List<Path> list(Path folder) throws IOException {
        Path dir = resolve(folder);
        if (!Files.isDirectory(dir)) return List.of();
        try (var s = Files.list(dir)) {
            return s.map(root::relativize).collect(Collectors.toList());
        }
    }

    @Override
    public void mkdirs(Path relative) throws IOException {
        Files.createDirectories(resolve(relative));
    }
}
