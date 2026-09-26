package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonIgnoreProperties(ignoreUnknown = true)
public class DigestUsage {

    @JsonProperty("speaker")
    public String speaker;

    @JsonProperty("did")
    public int did;

    @JsonProperty("nodeId")
    public String nodeId;

    @JsonProperty("text")
    public String text;

    @JsonProperty("missing")
    public boolean missing;
}
