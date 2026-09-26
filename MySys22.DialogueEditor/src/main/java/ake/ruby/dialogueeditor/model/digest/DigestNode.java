package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonIgnoreProperties(ignoreUnknown = true)
public class DigestNode {

    @JsonProperty("id")
    public String id;

    @JsonProperty("type")
    public String type;

    @JsonProperty("speaker")
    public String speaker;

    @JsonProperty("startDid")
    public int startDid;

    @JsonProperty("endDid")
    public int endDid;

    @JsonProperty("next")
    public String next;

    @JsonProperty("action")
    public String action;

    @JsonProperty("wait")
    public boolean wait;

    @JsonProperty("onComplete")
    public String onComplete;

    @JsonProperty("onParallel")
    public String onParallel;

    @JsonProperty("graph")
    public String graph;

    @JsonProperty("returns")
    public boolean returns;

    @JsonProperty("condition")
    public String condition;

    @JsonProperty("elseBranch")
    public String elseBranch;

    @JsonProperty("conditionVariables")
    public List<String> conditionVariables = new ArrayList<>();

    @JsonProperty("choices")
    public List<DigestChoice> choices = new ArrayList<>();
}
