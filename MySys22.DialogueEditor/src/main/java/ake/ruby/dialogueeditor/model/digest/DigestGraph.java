package ake.ruby.dialogueeditor.model.digest;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

@JsonIgnoreProperties(ignoreUnknown = true)
public class DigestGraph {

    @JsonProperty("graphId")
    public String graphId;

    @JsonProperty("file")
    public String file;

    @JsonProperty("startNode")
    public String startNode;

    @JsonProperty("nodes")
    public List<DigestNode> nodes = new ArrayList<>();

    @JsonProperty("usage")
    public List<DigestUsage> usage = new ArrayList<>();
}
