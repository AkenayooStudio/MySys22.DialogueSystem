package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.storage.SftpStorage;
import org.eclipse.jgit.api.Git;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.stream.Collectors;
import java.util.stream.Stream;

public final class ProjectImportService {

    private ProjectImportService() {}

    public static void downloadSftpProject(SftpStorage.Config cfg, Path localTarget) throws IOException {
        Files.createDirectories(localTarget);

        try (SftpStorage storage = new SftpStorage(cfg)) {
            copyRecursive(storage, Path.of(""), localTarget);
        } catch (IOException e) {
            throw e;
        } catch (Exception e) {
            throw new IOException("SFTP download failed: " + e.getMessage(), e);
        }
    }

    private static void copyRecursive(SftpStorage storage, Path relative, Path localRoot) throws IOException {
        List<Path> entries;
        try {
            entries = storage.list(relative);
        } catch (IOException e) {
            return;
        }

        for (Path entry : entries) {
            Path localFile = localRoot.resolve(entry.getFileName().toString());
            try {
                byte[] data = storage.read(entry);
                Files.createDirectories(localFile.getParent());
                Files.write(localFile, data);
            } catch (IOException ignored) {

                Path subLocal = localRoot.resolve(entry.getFileName().toString());
                Files.createDirectories(subLocal);
                copyRecursive(storage, entry, subLocal);
            }
        }
    }

    public static void cloneGitProject(String repoUrl, String branch, Path localTarget)
            throws IOException {
        try {
            Files.createDirectories(localTarget.getParent());
            var cmd = Git.cloneRepository()
                    .setURI(repoUrl)
                    .setDirectory(localTarget.toFile());
            if (branch != null && !branch.isBlank()) {
                cmd.setBranch(branch);
            }
            try (Git git = cmd.call()) {

            }
        } catch (Exception e) {
            throw new IOException("Git clone failed: " + e.getMessage(), e);
        }
    }
}
