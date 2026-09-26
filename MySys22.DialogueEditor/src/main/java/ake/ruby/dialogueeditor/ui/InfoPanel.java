package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;
import ake.ruby.dialogueeditor.model.SettingsData;
import ake.ruby.dialogueeditor.project.ProjectSession;
import ake.ruby.dialogueeditor.project.SettingsStore;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.VBox;

import java.util.List;
import java.util.Locale;
import java.util.function.Consumer;

public class InfoPanel extends StackPane {

    private static final List<String> LANGUAGES = List.of("EN", "IT", "ES", "RO", "JA");

    private final Consumer<String> onLanguageChanged;

    public InfoPanel(Runnable onClose, Consumer<String> onLanguageChanged) {
        this.onLanguageChanged = onLanguageChanged;

        getStyleClass().add("modal-overlay");
        setAlignment(Pos.CENTER);

        setOnMouseClicked(event -> {
            if (event.getTarget() == this) {
                onClose.run();
            }
        });

        VBox panel = new VBox();
        panel.getStyleClass().add("modal-panel");
        panel.setAlignment(Pos.TOP_LEFT);
        panel.setMaxWidth(420);
        panel.setMaxHeight(Region.USE_PREF_SIZE);

        HBox titleRow = new HBox();
        titleRow.setAlignment(Pos.CENTER_LEFT);
        Label title = new Label(I18n.t("settings.title"));
        title.getStyleClass().add("modal-title");

        Region spacer = new Region();
        HBox.setHgrow(spacer, Priority.ALWAYS);

        Button closeBtn = new Button("\u2715");
        closeBtn.getStyleClass().add("modal-close-btn");
        closeBtn.setOnAction(e -> onClose.run());

        titleRow.getChildren().addAll(title, spacer, closeBtn);

        Label langLabel = new Label(I18n.t("settings.language"));
        langLabel.getStyleClass().add("settings-label");

        ComboBox<String> langSelect = new ComboBox<>();
        langSelect.getItems().addAll(LANGUAGES);
        langSelect.getStyleClass().add("settings-combo");

        SettingsData current = SettingsStore.load();
        String currentLang = current.language;
        if (currentLang == null || !LANGUAGES.contains(currentLang)) {
            currentLang = resolveSystemLanguage();
        }
        langSelect.setValue(currentLang);

        langSelect.valueProperty().addListener((obs, oldV, newV) -> {
            if (newV == null || newV.equals(oldV)) return;
            SettingsData s = new SettingsData();
            s.language = newV;
            SettingsStore.save(s);
            if (this.onLanguageChanged != null) {
                this.onLanguageChanged.accept(newV);
            }
        });

        HBox langRow = new HBox(langLabel, langSelect);
        langRow.getStyleClass().add("settings-row");
        langRow.setAlignment(Pos.CENTER_LEFT);

        Label infoTitle = new Label(I18n.t("settings.info"));
        infoTitle.getStyleClass().add("settings-section-title");

        Label versionLabel = new Label(
                I18n.t("settings.info.version") + ": " + ProjectSession.EDITOR_VERSION);
        versionLabel.getStyleClass().add("settings-info-value");

        Label fileLabel = new Label(
                I18n.t("settings.info.settings_file") + ": " + SettingsStore.path());
        fileLabel.getStyleClass().add("settings-info-value");
        fileLabel.setWrapText(true);

        panel.getChildren().addAll(
                titleRow,
                langRow,
                infoTitle,
                versionLabel,
                fileLabel
        );

        getChildren().add(panel);
    }

    private String resolveSystemLanguage() {
        String lang = Locale.getDefault().getLanguage().toUpperCase();
        return LANGUAGES.contains(lang) ? lang : "EN";
    }
}
