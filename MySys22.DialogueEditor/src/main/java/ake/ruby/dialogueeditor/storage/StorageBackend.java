package ake.ruby.dialogueeditor.storage;

import java.io.IOException;
import java.nio.file.Path;
import java.util.List;

public interface StorageBackend extends AutoCloseable {

    String describe();

    boolean exists(Path relative) throws IOException;

    byte[] read(Path relative) throws IOException;

    void write(Path relative, byte[] data) throws IOException;

    void append(Path relative, byte[] data) throws IOException;

    void delete(Path relative) throws IOException;

    List<Path> list(Path folder) throws IOException;

    void mkdirs(Path relative) throws IOException;

    @Override
    default void close() throws Exception {}
}
