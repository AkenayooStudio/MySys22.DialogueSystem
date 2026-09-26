package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.storage.SftpStorage;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.function.BiConsumer;

public final class SyncService {

    private SyncService() {}

    public static int downloadRecursive(SftpStorage storage,
                                        Path remoteDir,
                                        Path localTarget,
                                        BiConsumer<Path, Boolean> progress) throws IOException {
        Files.createDirectories(localTarget);
        return downloadInto(storage, remoteDir, localTarget, progress, 0);
    }

    private static int downloadInto(SftpStorage storage,
                                    Path remoteDir,
                                    Path localTarget,
                                    BiConsumer<Path, Boolean> progress,
                                    int depth) throws IOException {
        if (depth > 32) throw new IOException("Max depth exceeded at " + remoteDir);

        List<Path> entries = storage.list(remoteDir);
        int count = 0;

        for (Path entry : entries) {
            String name = entry.getFileName().toString();
            if (name.equals(".") || name.equals("..")) continue;

            Path localFile = localTarget.resolve(name);

            try {
                byte[] data = storage.read(entry);
                Files.createDirectories(localFile.getParent());
                Files.write(localFile, data);
                if (progress != null) progress.accept(entry, false);
                count++;
            } catch (IOException notAFile) {

                try {
                    Files.createDirectories(localFile);
                    if (progress != null) progress.accept(entry, true);
                    count += downloadInto(storage, entry, localFile, progress, depth + 1);
                } catch (IOException nested) {

                }
            }
        }
        return count;
    }
}
