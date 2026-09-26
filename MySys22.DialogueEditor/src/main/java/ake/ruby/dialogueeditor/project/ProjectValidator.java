package ake.ruby.dialogueeditor.project;

import ake.ruby.dialogueeditor.debug.Diagnostic;
import ake.ruby.dialogueeditor.io.YamlIO;
import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.CharacterListEntry;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.model.LineEntry;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;

public final class ProjectValidator {

    private ProjectValidator() {
    }

    public static List<Diagnostic> validateForDeploy(ProjectSession session) {
        List<Diagnostic> out = new ArrayList<>();

        CharacterListData chars = null;
        try {
            chars = session.readCharacterList();
        } catch (Exception e) {
            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "char.read",
                    "Cannot read " + ProjectLayout.CHARACTER_LIST + ": " + e.getMessage()));
        }

        if (chars != null) {
            Set<Integer> seen = new HashSet<>();
            for (CharacterListEntry c : chars.characters) {
                if (c.id == null) {
                    out.add(new Diagnostic(Diagnostic.Severity.ERROR, "char.null_id",
                            "Character '" + safe(c.name) + "' has no CID. Every character needs a numeric id."));
                    continue;
                }
                if (c.id < 0) {
                    out.add(new Diagnostic(Diagnostic.Severity.ERROR, "char.neg_id",
                            "Character '" + safe(c.name) + "' has a negative CID (" + c.id + ")."));
                }
                if (!seen.add(c.id)) {
                    out.add(new Diagnostic(Diagnostic.Severity.ERROR, "char.dup_id",
                            "Duplicate CID " + c.id + " in the character list."));
                }
                if (c.name == null || c.name.isBlank()) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "char.null_name",
                            "CID " + c.id + " has no display name; the engine will show the raw CID."));
                }
            }
        }

        List<String> languages = declaredLanguages(session);
        if (languages.isEmpty()) {
            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lang.empty",
                    "The project declares no languages. Add at least one in the project configuration."));
        }

        List<String> discovered = discoveredLanguages(session);
        if (!discovered.isEmpty() && languages.isEmpty()) {
            out.add(new Diagnostic(Diagnostic.Severity.INFO, "lang.discovered",
                    "No declared languages, but line folders were found: " + discovered
                            + ". They will be deployed."));
        }
        for (String lang : discovered) {
            if (!languages.contains(lang)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lang.undeclared",
                        "Line folder '" + lang + "' is not declared in the project languages "
                                + languages + ". It WILL be deployed; add it to the project configuration "
                                + "to silence this warning."));
            }
        }

        List<String> effective = deployLanguages(session);
        for (String lang : effective) {
            if (!DialogueContract.isSupportedLanguage(lang)) {
                out.add(new Diagnostic(Diagnostic.Severity.INFO, "lang.custom",
                        "Language '" + lang + "' is not one of the official codes "
                                + DialogueContract.LANGUAGES + ". The engine loads any folder, "
                                + "make sure the game exposes it in its language menu."));
            }
            validateLanguage(session, lang, out);
        }

        validateVariables(session, out);
        validateGraphs(session, out);
        return out;
    }

    private static void validateLanguage(ProjectSession session, String lang, List<Diagnostic> out) {
        Path dir = ProjectLayout.LINES_DIR.resolve(lang);
        List<Path> files;
        try {
            if (!session.storage().exists(dir)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.folder",
                        "No line database folder for '" + lang + "' (expected " + dir + ")."));
                return;
            }
            files = session.storage().list(dir);
        } catch (Exception e) {
            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.list",
                    "Cannot list " + dir + ": " + e.getMessage()));
            return;
        }

        Set<String> cids = new HashSet<>();
        boolean any = false;

        for (Path rel : files) {
            String fileName = rel.getFileName().toString();
            if (!isYaml(fileName)) continue;
            any = true;

            LineDatabaseData data;
            try {
                data = YamlIO.readLines(session.storage().read(rel));
            } catch (Exception e) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.parse",
                        "Cannot parse " + rel + ": " + e.getMessage()));
                continue;
            }

            String stem = stripExtension(fileName);
            String cid = data.character == null ? "" : data.character.trim();

            if (cid.isEmpty()) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.no_cid",
                        rel + " has no 'character' field. Set it to the character CID before deploying."));
                continue;
            }
            if (!cid.equals(stem)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.name_mismatch",
                        rel + " declares character '" + cid + "' but the file is named '" + stem
                                + ".yaml'. On deploy it will be written as " + cid + ".yaml."));
            }
            if (!cids.add(cid)) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.dup_cid",
                        "CID '" + cid + "' appears in more than one file in " + dir + "."));
            }

            Set<Integer> dids = new TreeSet<>();
            List<Integer> duplicates = new ArrayList<>();
            List<LineEntry> lines = data.lines == null ? List.of() : data.lines;
            for (LineEntry line : lines) {
                if (line == null) continue;
                if (line.did <= 0) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.bad_did",
                            cid + " [" + lang + "] has a non positive DID (" + line.did + ")."));
                }
                if (!dids.add(line.did)) duplicates.add(line.did);
                if (line.text == null || line.text.isBlank()) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.empty_text",
                            cid + " [" + lang + "] DID " + line.did + " has empty text."));
                }
            }
            if (!duplicates.isEmpty()) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.dup_did",
                        cid + " [" + lang + "] has duplicate DIDs: " + duplicates));
            }
            if (lines.isEmpty()) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.empty",
                        cid + " [" + lang + "] contains no lines."));
            }
        }

        if (!any) {
            out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.none",
                    "No .yaml line database found in " + dir + "."));
        }
    }

    private static void validateVariables(ProjectSession session, List<Diagnostic> out) {
        ake.ruby.dialogueeditor.model.VariableListData data;
        try {
            data = session.readVariables();
        } catch (Exception e) {
            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "vars.parse",
                    "Cannot read " + ProjectLayout.VARIABLES_FILE + ": " + e.getMessage()));
            return;
        }

        Set<String> seen = new HashSet<>();
        for (ake.ruby.dialogueeditor.model.VariableEntry entry : data.variables) {
            if (entry == null) continue;
            String name = entry.name == null ? "" : entry.name.trim();

            if (name.isEmpty()) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "vars.no_name",
                        "A variable declaration has no name."));
                continue;
            }
            if (!name.matches("[A-Za-z_][A-Za-z0-9_.]*")) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "vars.bad_name",
                        "Variable '" + name + "' has an invalid name (letters, digits, _ and . only)."));
            }
            if (!seen.add(name)) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "vars.dup",
                        "Variable '" + name + "' is declared more than once."));
            }

            String type = entry.type == null ? "bool" : entry.type.trim().toLowerCase();
            if (!Set.of("bool", "int", "float", "string", "boolean", "integer", "number", "text")
                    .contains(type)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "vars.type",
                        "Variable '" + name + "' has unknown type '" + entry.type
                                + "'; the engine will treat it as bool."));
            }
            if (!defaultParses(entry.value, type)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "vars.default",
                        "Variable '" + name + "' default '" + entry.value + "' does not look like a "
                                + type + " value."));
            }
        }

        if (data.variables.isEmpty()) {
            out.add(new Diagnostic(Diagnostic.Severity.INFO, "vars.none",
                    "No dialogue variables declared (" + ProjectLayout.VARIABLES_FILE + ")."));
        }
    }

    private static boolean defaultParses(String value, String type) {
        if (value == null || value.isBlank()) return true;
        String text = value.trim();
        try {
            switch (type) {
                case "int": case "integer": Integer.parseInt(text); return true;
                case "float": case "number": Double.parseDouble(text); return true;
                case "bool": case "boolean":
                    return "true".equalsIgnoreCase(text) || "false".equalsIgnoreCase(text)
                            || "1".equals(text) || "0".equals(text);
                default: return true;
            }
        } catch (NumberFormatException e) {
            return false;
        }
    }

    public static List<Diagnostic> validateConditionVariables(ProjectSession session,
            ake.ruby.dialogueeditor.model.digest.GraphDigestData digest, List<Diagnostic> out) {
        if (digest == null || digest.graphs == null) return out;

        Set<String> declared = new HashSet<>();
        try {
            for (ake.ruby.dialogueeditor.model.VariableEntry entry : session.readVariables().variables) {
                if (entry != null && entry.name != null) declared.add(entry.name.trim());
            }
        } catch (Exception e) {
            return out;
        }

        Set<String> reported = new HashSet<>();
        for (ake.ruby.dialogueeditor.model.digest.DigestGraph graph : digest.graphs) {
            if (graph.nodes == null) continue;
            for (ake.ruby.dialogueeditor.model.digest.DigestNode node : graph.nodes) {
                if (node == null || node.conditionVariables == null) continue;
                for (String name : node.conditionVariables) {
                    if (name == null || name.isBlank()) continue;
                    if (name.startsWith("quest.")) continue;
                    if (declared.contains(name)) continue;
                    if (!reported.add(name)) continue;
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "cond.unknown_variable",
                            "Condition in " + graph.file + "#" + node.id + " uses variable '" + name
                                    + "', which is not declared in " + ProjectLayout.VARIABLES_FILE + "."));
                }
            }
        }
        return out;
    }

    private static void validateGraphs(ProjectSession session, List<Diagnostic> out) {
        Path dir = ProjectLayout.GRAPHS_DIR;
        try {
            if (!session.storage().exists(dir)) {
                out.add(new Diagnostic(Diagnostic.Severity.INFO, "graphs.none",
                        "No '" + dir + "' folder in the project: Unity authored graphs will be left untouched."));
                return;
            }
            int count = 0;
            for (Path rel : session.storage().list(dir)) {
                if (!isYaml(rel.getFileName().toString())) continue;
                count++;
                String text = new String(session.storage().read(rel));
                if (!text.contains("nodes:")) {
                    out.add(new Diagnostic(Diagnostic.Severity.ERROR, "graphs.invalid",
                            rel + " does not look like an engine graph (missing 'nodes:')."));
                }
                if (!text.contains("startNode:")) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "graphs.no_start",
                            rel + " has no 'startNode:' entry; the engine falls back to the first node."));
                }
            }
            if (count == 0) {
                out.add(new Diagnostic(Diagnostic.Severity.INFO, "graphs.empty",
                        "The '" + dir + "' folder exists but contains no graph."));
            }
        } catch (Exception e) {
            out.add(new Diagnostic(Diagnostic.Severity.WARNING, "graphs.error",
                    "Cannot inspect '" + dir + "': " + e.getMessage()));
        }
    }

    public static List<String> declaredLanguages(ProjectSession session) {
        Set<String> out = new LinkedHashSet<>();
        List<String> declared = session.manifest().languages;
        if (declared != null) {
            for (String raw : declared) {
                String lang = DialogueContract.normalizeLanguage(raw);
                if (lang != null) out.add(lang);
            }
        }
        String def = DialogueContract.normalizeLanguage(session.manifest().defaultLanguage);
        if (def != null) out.add(def);
        return new ArrayList<>(out);
    }

    public static List<String> discoveredLanguages(ProjectSession session) {
        List<String> out = new ArrayList<>();
        try {
            if (!session.storage().exists(ProjectLayout.LINES_DIR)) return out;
            for (Path p : session.storage().list(ProjectLayout.LINES_DIR)) {
                Path name = p.getFileName();
                if (name == null) continue;
                String lang = DialogueContract.normalizeLanguage(name.toString());
                if (lang != null && !out.contains(lang)) out.add(lang);
            }
        } catch (Exception ignored) {

        }
        out.sort(String::compareTo);
        return out;
    }

    public static List<String> deployLanguages(ProjectSession session) {
        Set<String> out = new LinkedHashSet<>(declaredLanguages(session));
        out.addAll(discoveredLanguages(session));
        return new ArrayList<>(out);
    }

    public static String resolveCidByName(ProjectSession session, String name) {
        if (name == null || name.isBlank()) return null;
        try {
            CharacterListData chars = session.readCharacterList();
            for (CharacterListEntry c : chars.characters) {
                if (c.id != null && c.name != null && c.name.equalsIgnoreCase(name.trim())) {
                    return String.valueOf(c.id);
                }
            }
        } catch (Exception ignored) {

        }
        return null;
    }

    private static boolean isYaml(String fileName) {
        String n = fileName.toLowerCase();
        return n.endsWith(".yaml") || n.endsWith(".yml");
    }

    private static String stripExtension(String fileName) {
        return fileName.replaceAll("(?i)\\.ya?ml$", "");
    }

    private static String safe(String s) {
        return s == null ? "?" : s;
    }
}
