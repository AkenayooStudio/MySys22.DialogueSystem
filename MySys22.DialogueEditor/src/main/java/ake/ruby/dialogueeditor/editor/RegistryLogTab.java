package ake.ruby.dialogueeditor.editor;

import ake.ruby.dialogueeditor.project.ProjectLayout;
import ake.ruby.dialogueeditor.project.ProjectSession;

import javafx.scene.control.TextArea;
import javafx.scene.layout.Priority;
import javafx.scene.layout.VBox;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;

public class RegistryLogTab extends EditorTab {

    public RegistryLogTab(ProjectSession session, Path filePath) {
        super(filePath, "whhgmz-rawaccesslogs", ".registry.log");

        TextArea area = new TextArea();
        area.setEditable(false);
        area.setWrapText(false);
        area.getStyleClass().add("editor-textarea");
        VBox.setVgrow(area, Priority.ALWAYS);

        try {
            if (session.storage().exists(ProjectLayout.REGISTRY_LOG)) {
                byte[] data = session.storage().read(ProjectLayout.REGISTRY_LOG);
                area.setText(new String(data, StandardCharsets.UTF_8));
            }
        } catch (IOException e) {
            area.setText("Error: " + e.getMessage());
        }

        VBox content = new VBox(area);
        content.getStyleClass().add("editor-tab-content");
        setContent(content);
    }
}
