package ake.ruby.dialogueeditor.cli;

import ake.ruby.dialogueeditor.debug.Diagnostic;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.UnityDeployService;
import ake.ruby.dialogueeditor.storage.LocalStorage;

import java.nio.file.Path;
import java.util.List;

public final class DialogueEditorCli {

    private DialogueEditorCli() {
    }

    public static int run(String[] args) {
        if (args.length == 0) return usage(null);

        String command = args[0];
        String project = option(args, "--project");
        String unity = option(args, "--unity");

        if (project == null) return usage("missing --project");

        Path projectRoot = Path.of(project).toAbsolutePath().normalize();
        if (!java.nio.file.Files.isDirectory(projectRoot)) {
            System.err.println("Project folder not found: " + projectRoot);
            return 2;
        }

        try (LocalStorage storage = new LocalStorage(projectRoot)) {
            if (!ProjectSession.isProject(storage)) {
                System.err.println("Not a MySys22 dialogue project (missing .project.yaml): " + projectRoot);
                return 2;
            }
            ProjectSession session = ProjectSession.open(storage);

            return switch (command) {
                case "validate" -> validate(session);
                case "coverage" -> coverage(session);
                case "deploy" -> deploy(session, unity);
                default -> usage("unknown command: " + command);
            };
        } catch (Exception e) {
            System.err.println("ERROR: " + e.getMessage());
            return 1;
        }
    }

    private static int validate(ProjectSession session) throws Exception {
        List<Diagnostic> diagnostics = UnityDeployService.validate(session);

        int errors = 0;
        int warnings = 0;
        for (Diagnostic d : diagnostics) {
            if (d.severity == Diagnostic.Severity.ERROR) errors++;
            if (d.severity == Diagnostic.Severity.WARNING) warnings++;
            System.out.printf("[%-7s] %-18s %s%n", d.severity, d.code, d.message);
        }

        System.out.printf("%nProject '%s': %d error(s), %d warning(s)%n",
                session.manifest().name, errors, warnings);
        return errors == 0 ? 0 : 1;
    }

    private static int coverage(ProjectSession session) throws Exception {
        ake.ruby.dialogueeditor.project.DigestService.LoadResult digest =
                ake.ruby.dialogueeditor.project.DigestService.load(session);

        System.out.println("Digest: " + (digest.found() ? digest.path() + " (" + digest.origin() + ")" : "not found"));

        List<Diagnostic> diagnostics =
                ake.ruby.dialogueeditor.project.DigestService.coverage(session, digest.data());

        int missing = 0;
        for (Diagnostic d : diagnostics) {
            System.out.printf("[%-7s] %-22s %s%n", d.severity, d.code, d.message);
            if ("digest.missing_line".equals(d.code)) missing++;
        }

        if (!digest.found()) {
            System.out.println("\nExport the digest in Unity (MySys22 > Dialogue > Export Graph Digest) " +
                    "and import it here, or set unityProjectPath in the project configuration.");
        }

        return missing == 0 ? 0 : 1;
    }

    private static int deploy(ProjectSession session, String unity) throws Exception {
        UnityDeployService.Result result = UnityDeployService.deploy(session, unity);

        System.out.println("Deployed to: " + result.targetDir());
        System.out.println("Languages:   " + result.languages());
        System.out.println("Line files:  " + result.filesCopied());
        System.out.println("Lines:       " + result.totalLines());
        for (String warning : result.warnings()) {
            System.out.println("WARN  " + warning);
        }
        for (String error : result.errors()) {
            System.out.println("ERROR " + error);
        }
        System.out.println(result.success() ? "OK" : "FAILED");
        return result.success() ? 0 : 1;
    }

    private static String option(String[] args, String name) {
        for (int i = 0; i < args.length - 1; i++) {
            if (args[i].equals(name)) return args[i + 1];
        }
        return null;
    }

    private static int usage(String problem) {
        if (problem != null) System.err.println("ERROR: " + problem);
        System.err.println("""
                MySys22 Dialogue Editor - headless tool

                  validate --project <dir>
                  coverage --project <dir>
                  deploy   --project <dir> --unity <unity-project-root>
                """);
        return 2;
    }
}
