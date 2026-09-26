package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.debug.Diagnostic;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.LineEntry;
import ake.ruby.dialogueeditor.model.VariableListData;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;
import java.util.stream.Stream;

public final class UnityDeployService {

    private static final ObjectMapper JSON = new ObjectMapper();

    private UnityDeployService() {
    }

    public record Result(int languages,
                         int filesCopied,
                         int totalLines,
                         Path targetDir,
                         List<String> warnings,
                         List<String> errors) {

        public boolean success() {
            return errors.isEmpty();
        }
    }

    public static Result deploy(ProjectSession session) throws IOException {
        return deploy(session, null);
    }

    public static Result deploy(ProjectSession session, String unityPathOverride) throws IOException {
        if (!(session.storage() instanceof LocalStorage local)) {
            throw new IOException("Deploy currently supports local projects only.");
        }

        String configured = unityPathOverride != null && !unityPathOverride.isBlank()
                ? unityPathOverride
                : session.manifest().unityProjectPath;
        if (configured == null || configured.isBlank()) {
            throw new IOException("Unity project path not configured. Set it in the project configuration.");
        }

        Path unityRoot = Path.of(configured.trim());
        if (!Files.isDirectory(unityRoot)) {
            throw new IOException("Unity project path does not exist: " + unityRoot);
        }
        if (!Files.isDirectory(unityRoot.resolve("Assets"))) {
            throw new IOException("Not a Unity project (no Assets folder): " + unityRoot);
        }

        List<Diagnostic> diagnostics = ProjectValidator.validateForDeploy(session);
        List<String> warnings = new ArrayList<>();
        List<String> errors = new ArrayList<>();
        for (Diagnostic d : diagnostics) {
            String line = "[" + d.code + "] " + d.message;
            if (d.severity == Diagnostic.Severity.ERROR) errors.add(line);
            else if (d.severity == Diagnostic.Severity.WARNING) warnings.add(line);
        }
        if (!errors.isEmpty()) {
            throw new IOException("Deploy aborted: " + errors.size()
                    + " validation error(s).\n" + String.join("\n", errors));
        }

        Path engineRoot = unityRoot.resolve(DialogueContract.STREAMING_ROOT);
        migrateLegacyRoot(unityRoot, warnings);
        Path dialogueRoot = engineRoot.resolve(DialogueContract.DIALOGUE_DIR);
        Path charactersRoot = engineRoot.resolve(DialogueContract.CHARACTERS_DIR);
        Path graphsRoot = engineRoot.resolve(DialogueContract.GRAPHS_DIR);
        Files.createDirectories(dialogueRoot);
        Files.createDirectories(charactersRoot);

        Path projectRoot = local.root();
        int filesCopied = 0;
        int totalLines = 0;
        int deployedLanguages = 0;
        int variablesCopied = 0;
        List<String> effectiveLanguages = ProjectValidator.deployLanguages(session);

        for (String lang : effectiveLanguages) {
            Path srcDir = projectRoot.resolve(ProjectLayout.LINES_DIR).resolve(lang);
            Path destDir = dialogueRoot.resolve(lang);

            if (!Files.isDirectory(srcDir)) {
                continue;
            }

            Set<String> written = new HashSet<>();
            List<String> collisions = new ArrayList<>();

            for (Path src : yamlFilesIn(srcDir)) {
                LineDatabaseData data;
                try {
                    data = YamlIO.readLines(Files.readAllBytes(src));
                } catch (Exception ex) {
                    warnings.add("Skipped " + src.getFileName() + ": " + ex.getMessage());
                    continue;
                }

                String stem = src.getFileName().toString().replaceAll("(?i)\\.ya?ml$", "");
                String cid = data.character == null ? "" : data.character.trim();
                if (cid.isEmpty()) {
                    cid = ProjectValidator.resolveCidByName(session, stem);
                    if (cid == null) cid = stem;
                }

                LineDatabaseData normalized = new LineDatabaseData();
                normalized.character = cid;
                normalized.language = lang;
                normalized.lines = new ArrayList<>();
                List<LineEntry> source = data.lines == null ? List.of() : data.lines;
                for (LineEntry line : source) {
                    if (line == null) continue;
                    LineEntry copy = new LineEntry();
                    copy.did = line.did;
                    copy.text = line.text == null ? "" : line.text;
                    normalized.lines.add(copy);
                }
                normalized.lines.sort(Comparator.comparingInt(l -> l.did));

                if (!written.add(cid)) {
                    collisions.add(cid);
                }

                Files.createDirectories(destDir);
                Files.write(destDir.resolve(cid + ".yaml"), YamlIO.writeLines(normalized));
                filesCopied++;
                totalLines += normalized.lines.size();
            }

            if (!collisions.isEmpty()) {
                warnings.add("Language " + lang + ": multiple files resolved to the same CID "
                        + collisions + "; the last one wins.");
            }

            pruneStaleYaml(destDir, written);
            writeIndexFile(destDir, new TreeSet<>(written));
            deployedLanguages++;
        }

        pruneStaleLanguageFolders(dialogueRoot, effectiveLanguages);

        CharacterListData characters;
        try {
            characters = session.readCharacterList();
        } catch (Exception ex) {
            characters = new CharacterListData();
            warnings.add("Character list could not be read (" + ex.getMessage()
                    + "); an empty registry was written.");
        }
        Files.createDirectories(charactersRoot);
        Files.write(engineRoot.resolve(DialogueContract.CHARACTERS_FILE),
                YamlIO.writeCharacterList(characters));

        Path srcVariables = projectRoot.resolve(ProjectLayout.VARIABLES_FILE);
        Path destVariablesDir = engineRoot.resolve(DialogueContract.VARIABLES_DIR);
        Path destVariablesFile = destVariablesDir.resolve(DialogueContract.VARIABLES_FILE.getFileName());
        Files.createDirectories(destVariablesDir);
        if (Files.isRegularFile(srcVariables)) {
            byte[] raw = Files.readAllBytes(srcVariables);
            try {
                VariableListData variables = YamlIO.readVariables(raw);
                Files.write(destVariablesFile, YamlIO.writeVariables(variables));
                variablesCopied = variables.variables == null ? 0 : variables.variables.size();
            } catch (Exception ex) {
                warnings.add("Cannot parse " + srcVariables.getFileName() + ": " + ex.getMessage());
                Files.copy(srcVariables, destVariablesFile, StandardCopyOption.REPLACE_EXISTING);
            }
        } else {
            Files.write(destVariablesFile, YamlIO.writeVariables(new VariableListData()));
        }

        Path srcGraphs = projectRoot.resolve(ProjectLayout.GRAPHS_DIR);
        int graphsCopied = 0;
        if (Files.isDirectory(srcGraphs)) {
            Files.createDirectories(graphsRoot);
            Set<String> graphNames = new HashSet<>();
            for (Path src : yamlFilesIn(srcGraphs)) {
                String name = src.getFileName().toString();
                Files.copy(src, graphsRoot.resolve(name), StandardCopyOption.REPLACE_EXISTING);
                graphNames.add(name.replaceAll("(?i)\\.ya?ml$", ""));
                graphsCopied++;
            }
            pruneStaleYaml(graphsRoot, graphNames);
        }

        if (Files.isDirectory(graphsRoot)) {
            List<String> indexed = new ArrayList<>();
            for (Path graph : yamlFilesIn(graphsRoot)) indexed.add(graph.getFileName().toString());
            writeIndexFile(graphsRoot, indexed);
        }

        String defaultLanguage = DialogueContract.normalizeLanguage(
                session.manifest().defaultLanguage);
        if (defaultLanguage == null || !effectiveLanguages.contains(defaultLanguage)) {
            defaultLanguage = effectiveLanguages.isEmpty()
                    ? DialogueContract.DEFAULT_LANGUAGE
                    : effectiveLanguages.get(0);
        }

        Map<String, Object> manifest = new LinkedHashMap<>();
        manifest.put("formatVersion", 1);
        manifest.put("projectId", session.manifest().projectId);
        manifest.put("name", session.manifest().name);
        manifest.put("dialogueVersion", session.manifest().dialogueVersion);
        manifest.put("chapter", session.manifest().chapter);
        manifest.put("defaultLanguage", defaultLanguage);
        manifest.put("languages", effectiveLanguages);
        manifest.put("graphs", graphsCopied);
        manifest.put("variables", variablesCopied);
        manifest.put("generatedBy", "MySys22 Dialogue Editor " + ProjectSession.EDITOR_VERSION);
        manifest.put("generatedAt", Instant.now().toString());
        Files.write(engineRoot.resolve(DialogueContract.ENGINE_MANIFEST),
                JSON.writerWithDefaultPrettyPrinter().writeValueAsBytes(manifest));

        List<String> declared = ProjectValidator.declaredLanguages(session);
        if (!declared.equals(effectiveLanguages)) {
            session.manifest().languages = new ArrayList<>(effectiveLanguages);
            if (defaultLanguage != null) session.manifest().defaultLanguage = defaultLanguage;
            try {
                session.writeManifest();
            } catch (IOException ex) {
                warnings.add("Could not update .project.yaml: " + ex.getMessage());
            }
        }

        try {
            session.log().append("unity-deploy",
                    "copied " + filesCopied + " line file(s), " + totalLines + " line(s), "
                            + deployedLanguages + " language(s) -> " + engineRoot);
        } catch (Exception ignored) {

        }

        return new Result(deployedLanguages, filesCopied, totalLines, engineRoot, warnings, errors);
    }

    private static void migrateLegacyRoot(Path unityRoot, List<String> warnings) {
        Path legacy = unityRoot.resolve(DialogueContract.LEGACY_STREAMING_ROOT);
        if (!Files.isDirectory(legacy)) return;
        Path target = unityRoot.resolve(DialogueContract.STREAMING_ROOT);
        if (Files.isDirectory(target.resolve(DialogueContract.GRAPHS_DIR))) return;

        List<Path> entries = new ArrayList<>();
        entries.add(DialogueContract.GRAPHS_DIR);
        entries.add(DialogueContract.DIALOGUE_DIR);
        entries.add(DialogueContract.CHARACTERS_DIR);
        entries.add(DialogueContract.VARIABLES_DIR);
        entries.add(DialogueContract.ENGINE_MANIFEST);
        entries.add(DialogueContract.ENGINE_DIGEST);

        for (Path entry : entries) {
            Path from = legacy.resolve(entry);
            Path to = target.resolve(entry);
            if (!Files.exists(from) || Files.exists(to)) continue;
            try {
                Files.createDirectories(to.getParent() == null ? target : to.getParent());
                Files.move(from, to);
                Path metaFrom = legacy.resolve(entry.getFileName() + ".meta");
                if (Files.isRegularFile(metaFrom) && !Files.exists(target.resolve(entry.getFileName() + ".meta"))) {
                    Files.move(metaFrom, target.resolve(entry.getFileName() + ".meta"));
                }
            } catch (IOException ex) {
                warnings.add("Could not move " + from.getFileName() + " out of "
                        + DialogueContract.LEGACY_ENGINE_FOLDER + ": " + ex.getMessage());
            }
        }

        try (Stream<Path> s = Files.list(legacy)) {
            if (s.findAny().isEmpty()) {
                Files.deleteIfExists(legacy);
                Files.deleteIfExists(unityRoot.resolve(
                        Path.of("Assets", "StreamingAssets",
                                DialogueContract.LEGACY_ENGINE_FOLDER + ".meta")));
            }
        } catch (IOException ignored) {

        }
    }

    public static List<Diagnostic> validate(ProjectSession session) {
        return ProjectValidator.validateForDeploy(session);
    }

    private static List<Path> yamlFilesIn(Path dir) throws IOException {
        try (Stream<Path> s = Files.list(dir)) {
            return s.filter(Files::isRegularFile)
                    .filter(p -> {
                        String n = p.getFileName().toString().toLowerCase();
                        return n.endsWith(".yaml") || n.endsWith(".yml");
                    })
                    .sorted()
                    .toList();
        }
    }

    private static void writeIndexFile(Path dir, Collection<String> names) throws IOException {
        Files.createDirectories(dir);
        Path index = dir.resolve(DialogueContract.INDEX_FILE);
        if (names.isEmpty()) {
            Files.writeString(index, "# No files deployed." + System.lineSeparator(),
                    StandardCharsets.UTF_8);
            return;
        }
        List<String> sorted = new ArrayList<>(names);
        Collections.sort(sorted);
        Files.write(index, sorted, StandardCharsets.UTF_8);
    }

    private static void pruneStaleYaml(Path dir, Set<String> keep) throws IOException {
        if (!Files.isDirectory(dir)) return;
        try (Stream<Path> s = Files.list(dir)) {
            for (Path p : s.filter(Files::isRegularFile).toList()) {
                String name = p.getFileName().toString();
                String lower = name.toLowerCase();
                if (!lower.endsWith(".yaml") && !lower.endsWith(".yml")) continue;
                String stem = name.replaceAll("(?i)\\.ya?ml$", "");
                if (keep.contains(stem) || keep.contains(name)) continue;
                Files.deleteIfExists(p);
            }
        }
    }

    private static void pruneStaleLanguageFolders(Path dialogueRoot, List<String> languages)
            throws IOException {
        if (!Files.isDirectory(dialogueRoot)) return;
        Set<String> keep = new HashSet<>(languages);
        try (Stream<Path> s = Files.list(dialogueRoot)) {
            for (Path dir : s.filter(Files::isDirectory).toList()) {
                String name = dir.getFileName().toString();
                if (keep.contains(name)) continue;
                try (Stream<Path> inner = Files.list(dir)) {
                    for (Path f : inner.toList()) Files.deleteIfExists(f);
                }
                Files.deleteIfExists(dir);
                Files.deleteIfExists(dialogueRoot.resolve(name + ".meta"));
            }
        }
    }

    public static Map<?, ?> readEngineManifest(Path unityRoot) throws IOException {
        Path p = unityRoot.resolve(DialogueContract.STREAMING_ROOT)
                .resolve(DialogueContract.ENGINE_MANIFEST);
        if (!Files.exists(p)) return null;
        return JSON.readValue(Files.readString(p, StandardCharsets.UTF_8), Map.class);
    }
}
