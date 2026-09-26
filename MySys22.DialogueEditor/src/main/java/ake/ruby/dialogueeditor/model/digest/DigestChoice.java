package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonIgnoreProperties(ignoreUnknown = true)
public class DigestChoice {

    @JsonProperty("text")
    public String text;

    @JsonProperty("textDid")
    public int textDid;

    @JsonProperty("speaker")
    public String speaker;

    @JsonProperty("next")
    public String next;

    @JsonProperty("portGuid")
    public String portGuid;

    @JsonProperty("condition")
    public String condition;
}
