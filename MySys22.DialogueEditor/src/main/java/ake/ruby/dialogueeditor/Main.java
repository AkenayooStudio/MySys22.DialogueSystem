package ake.ruby.dialogueeditor;

import ake.ruby.dialogueeditor.cli.DialogueEditorCli;
import ake.ruby.dialogueeditor.project.ProjectShortcut;

import java.nio.file.Path;

public final class Main {

    private Main() {
    }

    public static void main(String[] args) {
        if (args == null || args.length == 0) {
            EditorApplication.launchEditor(null);
            return;
        }

        String first = args[0] == null ? "" : args[0].trim();

        if (isCliCommand(first)) {
            System.exit(DialogueEditorCli.run(args));
        }

        if ("open".equalsIgnoreCase(first)) {
            EditorApplication.launchEditor(option(args, "--project"));
            return;
        }

        if (ProjectShortcut.isShortcut(first)) {
            try {
                Path target = ProjectShortcut.readTarget(Path.of(first));
                EditorApplication.launchEditor(target.toString());
            } catch (Exception e) {
                System.err.println("Cannot open shortcut '" + first + "': " + e.getMessage());
                EditorApplication.launchEditor(null);
            }
            return;
        }

        System.err.println("Unrecognised argument '" + first + "': opening the editor.");
        EditorApplication.launchEditor(null);
    }

    private static boolean isCliCommand(String value) {
        return "validate".equals(value) || "coverage".equals(value) || "deploy".equals(value);
    }

    private static String option(String[] args, String name) {
        for (int i = 0; i < args.length - 1; i++) {
            if (name.equals(args[i])) return args[i + 1];
        }
        return null;
    }
}
