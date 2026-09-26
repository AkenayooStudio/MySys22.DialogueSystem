package ake.ruby.dialogueeditor.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class ProjectManifest {

    @JsonProperty("formatVersion")
    public int formatVersion = 1;

    @JsonProperty("projectId")
    public String projectId;

    @JsonProperty("name")
    public String name;

    @JsonProperty("dialogueVersion")
    public String dialogueVersion;

    @JsonProperty("chapter")
    public String chapter;

    @JsonProperty("createdAt")
    public String createdAt;

    @JsonProperty("lastModifiedAt")
    public String lastModifiedAt;

    @JsonProperty("editorVersion")
    public String editorVersion;

    @JsonProperty("defaultLanguage")
    public String defaultLanguage = "EN";

    @JsonProperty("languages")
    public List<String> languages = new ArrayList<>();

    @JsonProperty("sftpEnabled")
    public boolean sftpEnabled;

    @JsonProperty("gitEnabled")
    public boolean gitEnabled;

    @JsonProperty("sftpHost")
    public String sftpHost;

    @JsonProperty("sftpPort")
    public int sftpPort = 22;

    @JsonProperty("sftpUser")
    public String sftpUser;

    @JsonProperty("sftpAuthType")
    public String sftpAuthType = "key";

    @JsonProperty("sftpKeyPath")
    public String sftpKeyPath;

    @JsonProperty("sftpRemoteRoot")
    public String sftpRemoteRoot = "/";

    @JsonProperty("sftpLocalPath")
    public String sftpLocalPath;

    @JsonProperty("unityProjectPath")
    public String unityProjectPath;
}
