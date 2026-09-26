package ake.ruby.dialogueeditor.storage;

import net.schmizz.sshj.SSHClient;
import net.schmizz.sshj.sftp.OpenMode;
import net.schmizz.sshj.sftp.RemoteFile;
import net.schmizz.sshj.sftp.RemoteResourceInfo;
import net.schmizz.sshj.sftp.SFTPClient;
import net.schmizz.sshj.transport.verification.PromiscuousVerifier;
import net.schmizz.sshj.userauth.keyprovider.KeyProvider;

import java.io.IOException;
import java.nio.file.Path;
import java.util.EnumSet;
import java.util.List;
import java.util.stream.Collectors;

public class SftpStorage implements StorageBackend {

    public record Config(
            String host,
            int port,
            String user,
            Path privateKeyPath,
            String password,
            String remoteRoot
    ) {}

    private final Config config;
    private final SSHClient ssh;
    private final SFTPClient sftp;

    public SftpStorage(Config cfg) throws IOException {
        this.config = cfg;
        this.ssh = new SSHClient();
        this.ssh.addHostKeyVerifier(new PromiscuousVerifier());
        this.ssh.connect(cfg.host(), cfg.port());

        if (cfg.privateKeyPath() != null) {
            KeyProvider kp = ssh.loadKeys(cfg.privateKeyPath().toString());
            ssh.authPublickey(cfg.user(), kp);
        } else {
            ssh.authPassword(cfg.user(), cfg.password());
        }

        this.sftp = ssh.newSFTPClient();
    }

    @Override
    public String describe() {
        return "sftp:" + config.user() + "@" + config.host() + ":" + config.remoteRoot();
    }

    private String remote(Path relative) {
        String base = config.remoteRoot().replaceAll("/+$", "");
        return base + "/" + relative.toString().replace('\\', '/');
    }

    @Override
    public boolean exists(Path relative) {
        try {
            return sftp.statExistence(remote(relative)) != null;
        } catch (IOException e) {
            return false;
        }
    }

    @Override
    public byte[] read(Path relative) throws IOException {
        try (RemoteFile handle = sftp.open(remote(relative))) {
            long size = handle.length();
            if (size == 0) return new byte[0];

            byte[] data = new byte[(int) size];
            int totalRead = 0;
            while (totalRead < size) {
                int n = handle.read(totalRead, data, totalRead, (int) (size - totalRead));
                if (n < 0) break;
                totalRead += n;
            }
            return data;
        }
    }

    @Override
    public void write(Path relative, byte[] data) throws IOException {
        String target = remote(relative);
        mkdirsRemoteParent(target);
        try (RemoteFile handle = sftp.open(target, EnumSet.of(
                OpenMode.WRITE, OpenMode.CREAT, OpenMode.TRUNC))) {
            handle.write(0, data, 0, data.length);
        }
    }

    @Override
    public void append(Path relative, byte[] data) throws IOException {
        String target = remote(relative);
        mkdirsRemoteParent(target);
        try (RemoteFile handle = sftp.open(target, EnumSet.of(
                OpenMode.WRITE, OpenMode.CREAT, OpenMode.APPEND))) {
            long size = handle.length();
            handle.write(size, data, 0, data.length);
        }
    }

    @Override
    public void delete(Path relative) throws IOException {
        sftp.rm(remote(relative));
    }

    @Override
    public List<Path> list(Path folder) throws IOException {
        String dir = remote(folder);
        return sftp.ls(dir).stream()
                .filter(RemoteResourceInfo::isRegularFile)
                .map(e -> folder.resolve(e.getName()))
                .collect(Collectors.toList());
    }

    @Override
    public void mkdirs(Path relative) throws IOException {
        sftp.mkdirs(remote(relative));
    }

    private void mkdirsRemoteParent(String remotePath) throws IOException {
        int idx = remotePath.lastIndexOf('/');
        if (idx > 0) {
            try {
                sftp.mkdirs(remotePath.substring(0, idx));
            } catch (IOException ignored) {
            }
        }
    }

    @Override
    public void close() throws Exception {
        try { sftp.close(); } catch (Exception ignored) {}
        try { ssh.disconnect(); } catch (Exception ignored) {}
    }
}
