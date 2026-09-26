package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonIgnoreProperties(ignoreUnknown = true)
public class GraphDigestData {

    @JsonProperty("formatVersion")
    public int formatVersion;

    @JsonProperty("generatedBy")
    public String generatedBy;

    @JsonProperty("generatedAt")
    public String generatedAt;

    @JsonProperty("previewLanguage")
    public String previewLanguage;

    @JsonProperty("languages")
    public List<String> languages = new ArrayList<>();

    @JsonProperty("characters")
    public List<DigestCharacter> characters = new ArrayList<>();

    @JsonProperty("graphs")
    public List<DigestGraph> graphs = new ArrayList<>();
}
