package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.debug.Diagnostic;
import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.LineEntry;
import ake.ruby.dialogueeditor.model.digest.DigestGraph;
import ake.ruby.dialogueeditor.model.digest.DigestUsage;
import ake.ruby.dialogueeditor.model.digest.GraphDigestData;
import ake.ruby.dialogueeditor.storage.LocalStorage;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.time.Instant;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

public final class DigestService {

    private static final ObjectMapper JSON = new ObjectMapper()
            .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false);

    private static final long STALE_SECONDS = 60;

    private DigestService() {
    }

    public record LoadResult(Path path, String origin, GraphDigestData data) {
        public boolean found() {
            return data != null;
        }
    }

    public record UsageKey(String cid, int did) {
    }

    public static Optional<Path> unityPath(ProjectSession session) {
        String unity = session.manifest().unityProjectPath;
        if (unity == null || unity.isBlank()) return Optional.empty();
        Path path = Path.of(unity.trim())
                .resolve(DialogueContract.STREAMING_ROOT)
                .resolve(DialogueContract.ENGINE_DIGEST);
        return Files.isRegularFile(path) ? Optional.of(path) : Optional.empty();
    }

    public static LoadResult load(ProjectSession session) {
        if (session.storage() instanceof LocalStorage local) {
            Path localDigest = local.root().resolve(ProjectLayout.DIGEST);
            if (Files.isRegularFile(localDigest)) {
                GraphDigestData data = readFile(localDigest);
                if (data != null) return new LoadResult(localDigest, "project", data);
            }
        }

        Optional<Path> unity = unityPath(session);
        if (unity.isPresent()) {
            GraphDigestData data = readFile(unity.get());
            if (data != null) return new LoadResult(unity.get(), "unity", data);
        }

        return new LoadResult(null, "none", null);
    }

    public static Path importInto(ProjectSession session, Path source) throws IOException {
        if (!Files.isRegularFile(source)) {
            throw new IOException("Digest not found: " + source);
        }
        GraphDigestData data = readFile(source);
        if (data == null) {
            throw new IOException("Not a valid graph digest: " + source);
        }
        if (session.storage() instanceof LocalStorage local) {
            Path target = local.root().resolve(ProjectLayout.DIGEST);
            Files.createDirectories(target.getParent());
            Files.copy(source, target, StandardCopyOption.REPLACE_EXISTING);
            try {
                session.log().append(ProjectLayout.DIGEST.toString(),
                        "graph digest imported (" + data.graphs.size() + " graph(s))");
            } catch (Exception ignored) {

            }
            return target;
        }
        throw new IOException("Importing a digest requires a local project.");
    }

    public static GraphDigestData readFile(Path path) {
        try {
            return JSON.readValue(Files.readString(path), GraphDigestData.class);
        } catch (Exception e) {
            return null;
        }
    }

    public static Set<UsageKey> usedPairs(GraphDigestData digest) {
        Set<UsageKey> out = new LinkedHashSet<>();
        if (digest == null || digest.graphs == null) return out;

        for (DigestGraph graph : digest.graphs) {
            if (graph.usage == null) continue;
            for (DigestUsage usage : graph.usage) {
                if (usage == null || usage.speaker == null) continue;
                out.add(new UsageKey(usage.speaker, usage.did));
            }
        }
        return out;
    }

    public static Set<Integer> usedDids(GraphDigestData digest, String cid) {
        Set<Integer> out = new LinkedHashSet<>();
        if (digest == null || cid == null) return out;

        for (DigestGraph graph : digest.graphs) {
            if (graph.usage == null) continue;
            for (DigestUsage usage : graph.usage) {
                if (usage != null && cid.equals(usage.speaker)) out.add(usage.did);
            }
        }
        return out;
    }

    public static String usageNode(GraphDigestData digest, String cid, int did) {
        if (digest == null || cid == null) return null;
        for (DigestGraph graph : digest.graphs) {
            if (graph.usage == null) continue;
            for (DigestUsage usage : graph.usage) {
                if (usage != null && cid.equals(usage.speaker) && usage.did == did) {
                    return (graph.file == null ? "?" : graph.file) + "#" + usage.nodeId;
                }
            }
        }
        return null;
    }

    public static boolean isUsed(GraphDigestData digest, String cid, int did) {
        return usedDids(digest, cid).contains(did);
    }

    public static int countUsed(GraphDigestData digest) {
        return usedPairs(digest).size();
    }

    public static int countMissing(GraphDigestData digest) {
        int missing = 0;
        if (digest == null) return 0;
        for (DigestGraph graph : digest.graphs) {
            if (graph.usage == null) continue;
            for (DigestUsage usage : graph.usage) {
                if (usage != null && usage.missing) missing++;
            }
        }
        return missing;
    }

    public static List<Diagnostic> coverage(ProjectSession session, GraphDigestData digest) {
        List<Diagnostic> out = new ArrayList<>();

        if (digest == null) {
            out.add(new Diagnostic(Diagnostic.Severity.INFO, "digest.none",
                    I18n.t("debug.digest.none")));
            return out;
        }

        if (digest.formatVersion > 1) {
            out.add(new Diagnostic(Diagnostic.Severity.WARNING, "digest.version",
                    I18n.t("debug.digest.version", digest.formatVersion)));
        }

        out.add(new Diagnostic(Diagnostic.Severity.INFO, "digest.source",
                I18n.t("debug.digest.summary", digest.graphs.size(), countUsed(digest),
                        digest.previewLanguage == null ? "-" : digest.previewLanguage)));

        if (isStale(session, digest)) {
            out.add(new Diagnostic(Diagnostic.Severity.WARNING, "digest.stale",
                    I18n.t("debug.digest.stale", digest.generatedAt == null ? "-" : digest.generatedAt)));
        }

        Set<UsageKey> used = usedPairs(digest);

        Set<String> knownCids = new HashSet<>();
        try {
            session.readCharacterList().characters.forEach(c -> {
                if (c.id != null) knownCids.add(String.valueOf(c.id));
            });
        } catch (Exception ignored) {

        }
        Set<String> reportedCids = new HashSet<>();
        if (!knownCids.isEmpty()) {
            for (UsageKey key : used) {
                if (knownCids.contains(key.cid())) continue;
                if (!reportedCids.add(key.cid())) continue;
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "digest.unknown_character",
                        I18n.t("debug.digest.unknown_character", key.cid())));
            }
        }

        for (String language : ProjectValidator.deployLanguages(session)) {
            Map<String, Set<Integer>> present = readLanguage(session, language);

            int missing = 0;
            for (UsageKey key : used) {
                Set<Integer> dids = present.getOrDefault(key.cid(), Set.of());
                if (dids.contains(key.did())) continue;
                missing++;
                String node = usageNode(digest, key.cid(), key.did());
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "digest.missing_line",
                        I18n.t("debug.digest.missing_line", language, key.cid(), key.did(),
                                node == null ? "-" : node)));
            }

            int orphan = 0;
            for (Map.Entry<String, Set<Integer>> entry : present.entrySet()) {
                for (Integer did : entry.getValue()) {
                    if (used.contains(new UsageKey(entry.getKey(), did))) continue;
                    orphan++;
                    out.add(new Diagnostic(Diagnostic.Severity.INFO, "digest.orphan_line",
                            I18n.t("debug.digest.orphan_line", language, entry.getKey(), did)));
                }
            }

            out.add(new Diagnostic(Diagnostic.Severity.INFO, "digest.coverage",
                    I18n.t("debug.digest.coverage", language, used.size() - missing, used.size(),
                            missing, orphan)));
        }

        return out;
    }

    public static Map<String, Set<Integer>> readLanguage(ProjectSession session, String language) {
        Map<String, Set<Integer>> out = new LinkedHashMap<>();
        Path dir = ProjectLayout.LINES_DIR.resolve(language);

        try {
            if (!session.storage().exists(dir)) return out;
            for (Path file : session.storage().list(dir)) {
                String name = file.getFileName().toString().toLowerCase();
                if (!name.endsWith(".yaml") && !name.endsWith(".yml")) continue;

                LineDatabaseData data;
                try {
                    data = YamlIO.readLines(session.storage().read(file));
                } catch (Exception e) {
                    continue;
                }

                String cid = data.character == null ? "" : data.character.trim();
                if (cid.isEmpty()) cid = file.getFileName().toString().replaceAll("(?i)\\.ya?ml$", "");

                Set<Integer> dids = out.computeIfAbsent(cid, k -> new LinkedHashSet<>());
                if (data.lines != null) {
                    for (LineEntry line : data.lines) {
                        if (line != null) dids.add(line.did);
                    }
                }
            }
        } catch (Exception ignored) {

        }
        return out;
    }

    private static boolean isStale(ProjectSession session, GraphDigestData digest) {
        if (digest.generatedAt == null) return false;
        String modified = session.manifest().lastModifiedAt;
        if (modified == null) return false;

        try {
            Instant generated = Instant.parse(digest.generatedAt);
            Instant last = Instant.parse(modified);
            return last.isAfter(generated.plusSeconds(STALE_SECONDS));
        } catch (Exception e) {
            return false;
        }
    }

    public static Map<String, Integer> summarize(GraphDigestData digest) {
        Map<String, Integer> out = new HashMap<>();
        out.put("graphs", digest == null ? 0 : digest.graphs.size());
        out.put("used", countUsed(digest));
        return out;
    }
}
