package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.storage.StorageBackend;

import java.io.IOException;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;

public class RegistryLog {

    private static final DateTimeFormatter TIMESTAMP =
            DateTimeFormatter.ofPattern("MM;dd;yyyy : HH;mm;ss");

    private static final String HEADER =
            "# MySys22 Dialogue Editor registry. Read-only. Do not edit manually.\n";

    private final StorageBackend storage;
    private final Path path;
    private final String machineName;

    public RegistryLog(StorageBackend storage, Path path) {
        this.storage = storage;
        this.path = path;
        this.machineName = resolveMachineName();
    }

    public void ensureHeader() throws IOException {
        if (!storage.exists(path)) {
            storage.write(path, HEADER.getBytes(StandardCharsets.UTF_8));
        }
    }

    public void append(String file, String action) throws IOException {
        String timestamp = LocalDateTime.now().format(TIMESTAMP);
        String line = String.format("%s | [%s] | %s #@> %s%n",
                machineName, timestamp, file, action);
        storage.append(path, line.getBytes(StandardCharsets.UTF_8));
    }

    private static String resolveMachineName() {
        try {
            String host = InetAddress.getLocalHost().getHostName();
            if (host != null && !host.isBlank()) return host.toUpperCase();
        } catch (Exception ignored) {
        }
        String env = System.getenv("COMPUTERNAME");
        if (env == null || env.isBlank()) env = System.getenv("HOSTNAME");
        if (env == null || env.isBlank()) env = "UNKNOWN";
        return env.toUpperCase();
    }
}
