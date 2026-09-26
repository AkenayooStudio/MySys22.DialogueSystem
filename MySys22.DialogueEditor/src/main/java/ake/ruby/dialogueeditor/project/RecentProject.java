package ake.ruby.dialogueeditor.project;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class RecentProject {

    public enum Type { LOCAL, SFTP }

    @JsonProperty("projectId")
    public String projectId;

    @JsonProperty("name")
    public String name;

    @JsonProperty("type")
    public Type type = Type.LOCAL;

    @JsonProperty("localPath")
    public String localPath;

    @JsonProperty("isGit")
    public boolean isGit;

    @JsonProperty("dialogueVersion")
    public String dialogueVersion;

    @JsonProperty("chapter")
    public String chapter;

    @JsonProperty("sftpHost")
    public String sftpHost;

    @JsonProperty("sftpPort")
    public int sftpPort = 22;

    @JsonProperty("sftpUser")
    public String sftpUser;

    @JsonProperty("sftpRemoteRoot")
    public String sftpRemoteRoot;

    @JsonProperty("lastOpenedAt")
    public String lastOpenedAt;

    @JsonProperty("editorVersion")
    public String editorVersion;

    public String stableKey() {
        if (type == Type.SFTP) {
            return "sftp:" + sftpUser + "@" + sftpHost + ":" + sftpPort + sftpRemoteRoot;
        }
        return "local:" + localPath;
    }
}
