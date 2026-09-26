package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.storage.LocalStorage;

import javafx.scene.control.Tab;
import javafx.scene.control.TabPane;

import java.nio.file.Path;

public class EditorTabPane extends TabPane {

    private static final String PROJECT_YAML = ".project.yaml";
    private static final String REGISTRY_LOG = ".registry.log";
    private static final String CHAR_LIST = "characters.list.yaml";
    private static final String VAR_LIST = "variables.yaml";

    private final ProjectSession session;

    public EditorTabPane(ProjectSession session) {
        this.session = session;

        getStyleClass().add("editor-tab-pane");
        setTabClosingPolicy(TabClosingPolicy.ALL_TABS);
    }

    public void openFile(Path filePath) {
        if (filePath == null) return;

        for (Tab t : getTabs()) {
            if (t instanceof EditorTab et && et.getFilePath() != null
                    && et.getFilePath().equals(filePath)) {
                getSelectionModel().select(t);
                return;
            }
        }

        EditorTab tab = createTab(filePath);
        if (tab == null) return;

        getTabs().add(tab);
        getSelectionModel().select(tab);
    }

    private EditorTab createTab(Path filePath) {
        String name = filePath.getFileName().toString();

        if (name.equals(PROJECT_YAML)) {
            return new ProjectConfigTab(session, filePath);
        }
        if (name.equals(REGISTRY_LOG)) {
            return new RegistryLogTab(session, filePath);
        }
        if (name.equals(CHAR_LIST)) {
            return new CharacterListTab(session, filePath);
        }
        if (name.equals(VAR_LIST)) {
            return new VariableListTab(session, filePath);
        }
        if (name.endsWith(".yaml") || name.endsWith(".yml")) {
            return createLinesTab(filePath);
        }
        return null;
    }

    private EditorTab createLinesTab(Path filePath) {
        if (!(session.storage() instanceof LocalStorage local)) return null;

        Path root = local.root();
        if (!filePath.startsWith(root)) return null;

        Path rel = root.relativize(filePath);
        if (rel.getNameCount() < 3) return null;

        if (!rel.getName(0).toString().equals("lines")) return null;

        String lang = rel.getName(1).toString();
        String cid = rel.getName(2).toString()
                .replaceAll("\\.ya?ml$", "");

        return new LinesEditorTab(session, lang, cid, filePath);
    }

    public java.util.List<java.nio.file.Path> getOpenFilePaths() {
        java.util.List<java.nio.file.Path> out = new java.util.ArrayList<>();
        for (Tab t : getTabs()) {
            if (t instanceof EditorTab et && et.getFilePath() != null) {
                out.add(et.getFilePath());
            }
        }
        return out;
    }
}
