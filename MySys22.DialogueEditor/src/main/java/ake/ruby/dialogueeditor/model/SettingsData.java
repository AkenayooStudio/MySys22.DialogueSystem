package ake.ruby.dialogueeditor.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class SettingsData {

    @JsonProperty("language")
    public String language;
}
