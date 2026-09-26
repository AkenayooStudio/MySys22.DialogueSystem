package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonIgnoreProperties(ignoreUnknown = true)
public class DigestCharacter {

    @JsonProperty("cid")
    public String cid;

    @JsonProperty("name")
    public String name;

    @JsonProperty("language")
    public String language;
}
