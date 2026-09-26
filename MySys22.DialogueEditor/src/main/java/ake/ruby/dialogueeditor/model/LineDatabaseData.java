package ake.ruby.dialogueeditor.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class LineDatabaseData {

    @JsonProperty("character")
    public String character;

    @JsonProperty("language")
    public String language;

    @JsonProperty("lines")
    public List<LineEntry> lines = new ArrayList<>();
}
