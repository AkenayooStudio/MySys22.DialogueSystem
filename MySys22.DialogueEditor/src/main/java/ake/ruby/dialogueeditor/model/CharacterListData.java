package ake.ruby.dialogueeditor.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class CharacterListData {

    @JsonProperty("characters")
    public List<CharacterListEntry> characters = new ArrayList<>();
}
