package ake.ruby.dialogueeditor.debug;

public class Diagnostic {

    public enum Severity { INFO, WARNING, ERROR }

    public final Severity severity;
    public final String code;
    public final String message;

    public Diagnostic(Severity severity, String code, String message) {
        this.severity = severity;
        this.code = code;
        this.message = message;
    }
}
