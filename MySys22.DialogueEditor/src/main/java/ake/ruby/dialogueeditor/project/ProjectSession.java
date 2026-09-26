package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.ProjectManifest;
import ake.ruby.dialogueeditor.model.VariableListData;
import ake.ruby.dialogueeditor.storage.StorageBackend;

import java.io.IOException;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

public class ProjectSession {

    public static final String EDITOR_VERSION = "0.1.0-SNAPSHOT";

    private final StorageBackend storage;
    private final ProjectManifest manifest;
    private final RegistryLog log;

    private ProjectSession(StorageBackend storage, ProjectManifest manifest) {
        this.storage = storage;
        this.manifest = manifest;
        this.log = new RegistryLog(storage, ProjectLayout.REGISTRY_LOG);
    }

    public static ProjectSession createFromManifest(StorageBackend storage,
                                                     ProjectManifest manifest) throws IOException {
        if (manifest == null) throw new IllegalArgumentException("manifest is null");
        if (manifest.name == null || manifest.name.isBlank()) {
            throw new IllegalArgumentException("Project name is required");
        }

        if (manifest.projectId == null || manifest.projectId.isBlank()) {
            manifest.projectId = slugify(manifest.name);
        }
        if (manifest.defaultLanguage == null || manifest.defaultLanguage.isBlank()) {
            manifest.defaultLanguage = "EN";
        }
        if (manifest.languages == null || manifest.languages.isEmpty()) {
            manifest.languages = new ArrayList<>(List.of(manifest.defaultLanguage));
        }

        manifest.createdAt = Instant.now().toString();
        manifest.lastModifiedAt = manifest.createdAt;
        manifest.editorVersion = EDITOR_VERSION;

        ProjectSession session = new ProjectSession(storage, manifest);
        session.materializeStructure();
        session.writeManifest();
        session.log.ensureHeader();
        session.log.append(ProjectLayout.MANIFEST.toString(),
                "project created, languages=" + String.join(",", manifest.languages));
        return session;
    }

    public static ProjectSession open(StorageBackend storage) throws IOException {
        if (!storage.exists(ProjectLayout.MANIFEST)) {
            throw new IOException("Not a project: missing " + ProjectLayout.MANIFEST);
        }
        byte[] raw = storage.read(ProjectLayout.MANIFEST);
        ProjectManifest m = YamlIO.readManifest(raw);
        ProjectSession session = new ProjectSession(storage, m);
        session.log.ensureHeader();
        return session;
    }

    public static boolean isProject(StorageBackend storage) throws IOException {
        return storage.exists(ProjectLayout.MANIFEST);
    }

    private void materializeStructure() throws IOException {
        storage.mkdirs(ProjectLayout.LINES_DIR);
        storage.mkdirs(ProjectLayout.CHARACTERS_DIR);
        storage.mkdirs(ProjectLayout.VARIABLES_DIR);
        for (String lang : manifest.languages) {
            storage.mkdirs(ProjectLayout.LINES_DIR.resolve(lang));
        }
        if (!storage.exists(ProjectLayout.CHARACTER_LIST)) {
            storage.write(ProjectLayout.CHARACTER_LIST,
                    YamlIO.writeCharacterList(new CharacterListData()));
        }
    }

    public void writeManifest() throws IOException {
        manifest.lastModifiedAt = Instant.now().toString();
        manifest.editorVersion = EDITOR_VERSION;
        storage.write(ProjectLayout.MANIFEST, YamlIO.writeManifest(manifest));
    }

    public ProjectManifest manifest() { return manifest; }
    public StorageBackend storage() { return storage; }
    public RegistryLog log() { return log; }

    public CharacterListData readCharacterList() throws IOException {
        if (!storage.exists(ProjectLayout.CHARACTER_LIST)) return new CharacterListData();
        return YamlIO.readCharacterList(storage.read(ProjectLayout.CHARACTER_LIST));
    }

    public void writeCharacterList(CharacterListData data) throws IOException {
        storage.write(ProjectLayout.CHARACTER_LIST, YamlIO.writeCharacterList(data));
        log.append(ProjectLayout.CHARACTER_LIST.toString(),
                "character list updated, count=" + data.characters.size());
        writeManifest();
    }

    public VariableListData readVariables() throws IOException {
        if (!storage.exists(ProjectLayout.VARIABLES_FILE)) return new VariableListData();
        return YamlIO.readVariables(storage.read(ProjectLayout.VARIABLES_FILE));
    }

    public void writeVariables(VariableListData data) throws IOException {
        storage.write(ProjectLayout.VARIABLES_FILE, YamlIO.writeVariables(data));
        log.append(ProjectLayout.VARIABLES_FILE.toString(),
                "variable list updated, count=" + data.variables.size());
        writeManifest();
    }

    public LineDatabaseData readLines(String language, String characterId) throws IOException {
        var path = ProjectLayout.linesFor(language, characterId);
        if (!storage.exists(path)) {
            LineDatabaseData empty = new LineDatabaseData();
            empty.character = characterId;
            empty.language = language;
            return empty;
        }
        return YamlIO.readLines(storage.read(path));
    }

    public void writeLines(String language, String characterId, LineDatabaseData data)
            throws IOException {
        var path = ProjectLayout.linesFor(language, characterId);
        data.character = characterId;
        data.language = language;
        storage.write(path, YamlIO.writeLines(data));
        log.append(path.toString(), "user edit, did count=" + data.lines.size());
        writeManifest();
    }

    public void writeLinesAt(java.nio.file.Path filePath, LineDatabaseData data)
            throws IOException {
        if (storage instanceof ake.ruby.dialogueeditor.storage.LocalStorage local) {
            java.nio.file.Path root = local.root();
            java.nio.file.Path rel = root.relativize(filePath);
            storage.write(rel, YamlIO.writeLines(data));
            log.append(rel.toString(), "user edit, did count=" + data.lines.size());
        } else {
            throw new IOException("writeLinesAt currently supports LocalStorage only");
        }
        writeManifest();
    }

    private static String slugify(String s) {
        return s.toLowerCase()
                .replaceAll("[^a-z0-9]+", "_")
                .replaceAll("^_+|_+$", "");
    }
}
