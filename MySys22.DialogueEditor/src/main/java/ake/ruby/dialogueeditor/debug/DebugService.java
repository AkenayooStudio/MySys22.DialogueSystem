package ake.ruby.dialogueeditor.debug;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.CharacterListData;
import ake.ruby.dialogueeditor.model.CharacterListEntry;
import ake.ruby.dialogueeditor.model.LineDatabaseData;
import ake.ruby.dialogueeditor.project.ProjectLayout;
import ake.ruby.dialogueeditor.project.ProjectSession;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

public final class DebugService {

    private DebugService() {}

    public static List<Diagnostic> run(ProjectSession session) {
        List<Diagnostic> out = new ArrayList<>();

        CharacterListData chars;
        try {
            chars = session.readCharacterList();
        } catch (Exception e) {
            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "char.read",
                    "Cannot read characters.list.yaml: " + e.getMessage()));
            return out;
        }

        if (chars.characters.isEmpty()) {
            out.add(new Diagnostic(Diagnostic.Severity.INFO, "char.empty",
                    I18n.t("debug.no_characters")));
        }

        Set<Integer> seen = new HashSet<>();
        for (CharacterListEntry c : chars.characters) {
            if (c.id == null) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "char.null_id",
                        I18n.t("debug.char_null_id", c.name != null ? c.name : "?")));
                continue;
            }
            if (!seen.add(c.id)) {
                out.add(new Diagnostic(Diagnostic.Severity.WARNING, "char.dup_id",
                        I18n.t("debug.char_dup_id", String.valueOf(c.id))));
            }
        }

        List<String> languages = session.manifest().languages != null
                ? session.manifest().languages
                : List.of();

        Map<String, Set<String>> cidsByLang = new HashMap<>();

        for (String lang : languages) {
            Set<String> present = new HashSet<>();
            try {
                Path dir = ProjectLayout.LINES_DIR.resolve(lang);
                if (session.storage().exists(dir)) {
                    for (Path p : session.storage().list(dir)) {
                        if (!p.getFileName().toString().endsWith(".yaml")) continue;
                        try {
                            byte[] raw = session.storage().read(p);
                            LineDatabaseData db = ake.ruby.dialogueeditor.io.YamlIO.readLines(raw);
                            if (db != null && db.character != null && !db.character.isBlank()) {
                                present.add(db.character.trim());
                            }
                        } catch (Exception ex) {
                            out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.parse",
                                    "Failed to parse " + p + ": " + ex.getMessage()));
                        }
                    }
                }
            } catch (Exception ex) {
                out.add(new Diagnostic(Diagnostic.Severity.ERROR, "lines.list",
                        "Failed to list lines/" + lang + ": " + ex.getMessage()));
            }
            cidsByLang.put(lang, present);
        }

        for (String lang : languages) {
            Set<String> present = cidsByLang.getOrDefault(lang, Set.of());
            for (CharacterListEntry c : chars.characters) {
                if (c.id == null) continue;
                String cidStr = String.valueOf(c.id);
                if (!present.contains(cidStr)) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "lines.missing",
                            I18n.t("debug.missing_lines",
                                    c.name != null ? c.name : cidStr,
                                    cidStr, lang)));
                }
            }
        }

        Set<String> knownCids = new HashSet<>();
        for (CharacterListEntry c : chars.characters) {
            if (c.id != null) knownCids.add(String.valueOf(c.id));
        }
        for (String lang : languages) {
            for (String cid : cidsByLang.getOrDefault(lang, Set.of())) {
                if (!knownCids.contains(cid)) {
                    out.add(new Diagnostic(Diagnostic.Severity.WARNING, "char.orphan",
                            I18n.t("debug.orphan_lines", cid, lang)));
                }
            }
        }

        Set<String> already = new HashSet<>();
        for (Diagnostic d : out) already.add(d.code);
        for (Diagnostic d : ake.ruby.dialogueeditor.project.ProjectValidator.validateForDeploy(session)) {
            if (already.contains(d.code)) continue;
            out.add(d);
        }

        ake.ruby.dialogueeditor.project.DigestService.LoadResult digest =
                ake.ruby.dialogueeditor.project.DigestService.load(session);
        out.addAll(ake.ruby.dialogueeditor.project.DigestService.coverage(session, digest.data()));

        out.addAll(ake.ruby.dialogueeditor.project.ProjectValidator.validateConditionVariables(
                session, digest.data(), new java.util.ArrayList<>()));

        if (out.isEmpty()) {
            out.add(new Diagnostic(Diagnostic.Severity.INFO, "ok",
                    I18n.t("debug.no_issues")));
        }

        return out;
    }
}
