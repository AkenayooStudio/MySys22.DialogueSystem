package ake.ruby.dialogueeditor.io;

import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.ProjectManifest;
import ake.ruby.dialogueeditor.model.SettingsData;
import ake.ruby.dialogueeditor.model.VariableListData;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.dataformat.yaml.YAMLFactory;
import com.fasterxml.jackson.dataformat.yaml.YAMLGenerator;

import java.io.IOException;

public final class YamlIO {

    private static final ObjectMapper MAPPER = new ObjectMapper(
            new YAMLFactory()
                    .disable(YAMLGenerator.Feature.WRITE_DOC_START_MARKER)
                    .enable(YAMLGenerator.Feature.MINIMIZE_QUOTES)
                    .enable(YAMLGenerator.Feature.LITERAL_BLOCK_STYLE)
    );

    static {
        MAPPER.setSerializationInclusion(JsonInclude.Include.NON_NULL);
        MAPPER.configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false);
    }

    private YamlIO() {}

    public static ProjectManifest readManifest(byte[] yaml) throws IOException {
        return MAPPER.readValue(yaml, ProjectManifest.class);
    }

    public static byte[] writeManifest(ProjectManifest data) throws IOException {
        return MAPPER.writeValueAsBytes(data);
    }

    public static CharacterListData readCharacterList(byte[] yaml) throws IOException {
        return MAPPER.readValue(yaml, CharacterListData.class);
    }

    public static byte[] writeCharacterList(CharacterListData data) throws IOException {
        return MAPPER.writeValueAsBytes(data);
    }

    public static LineDatabaseData readLines(byte[] yaml) throws IOException {
        return MAPPER.readValue(yaml, LineDatabaseData.class);
    }

    public static byte[] writeLines(LineDatabaseData data) throws IOException {
        return MAPPER.writeValueAsBytes(data);
    }

    public static VariableListData readVariables(byte[] yaml) throws IOException {
        return MAPPER.readValue(yaml, VariableListData.class);
    }

    public static byte[] writeVariables(VariableListData data) throws IOException {
        return MAPPER.writeValueAsBytes(data);
    }

    public static SettingsData readSettings(byte[] yaml) throws IOException {
        return MAPPER.readValue(yaml, SettingsData.class);
    }

    public static byte[] writeSettings(SettingsData data) throws IOException {
        return MAPPER.writeValueAsBytes(data);
    }
}
