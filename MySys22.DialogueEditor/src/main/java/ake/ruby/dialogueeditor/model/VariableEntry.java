package ake.ruby.dialogueeditor.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class VariableEntry {

    @JsonProperty("name")
    public String name;

    @JsonProperty("type")
    public String type = "bool";

    @JsonProperty("value")
    public String value;

    @JsonProperty("description")
    public String description;
}
