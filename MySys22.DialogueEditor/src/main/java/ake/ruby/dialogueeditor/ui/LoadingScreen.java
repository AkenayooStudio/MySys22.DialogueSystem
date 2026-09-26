package ake.ruby.dialogueeditor.ui;

import javafx.animation.Animation;
import javafx.animation.Interpolator;
import javafx.animation.PauseTransition;
import javafx.animation.TranslateTransition;
import javafx.scene.Node;
import javafx.scene.Scene;
import javafx.scene.control.Label;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.StackPane;
import javafx.scene.paint.Color;
import javafx.stage.Stage;
import javafx.stage.StageStyle;
import javafx.util.Duration;

import java.io.InputStream;

public class LoadingScreen {

    private static final double WINDOW_W = 1200;
    private static final double WINDOW_H = 720;
    private static final double LOGO_SIZE = 120;

    private final Stage stage = new Stage();
    private final long durationMs;
    private final Runnable onComplete;

    public LoadingScreen(long durationMs, Runnable onComplete) {
        this.durationMs = durationMs;
        this.onComplete = onComplete;
    }

    public void show() {
        stage.initStyle(StageStyle.UNDECORATED);
        stage.setResizable(false);

        StackPane root = new StackPane();
        root.setStyle("-fx-background-color: #000000;");

        Node logo = buildLogo();
        if (logo != null) {
            root.getChildren().add(logo);
            animateBounce(logo);
        }

        Scene scene = new Scene(root, WINDOW_W, WINDOW_H);
        scene.setFill(Color.BLACK);
        stage.setScene(scene);
        stage.setAlwaysOnTop(true);
        stage.centerOnScreen();
        stage.show();
        stage.toFront();
        stage.requestFocus();

        PauseTransition pause = new PauseTransition(Duration.millis(durationMs));
        pause.setOnFinished(e -> {
            if (onComplete != null) onComplete.run();
            stage.close();
        });
        pause.play();
    }

    private Node buildLogo() {
        try (InputStream is = LoadingScreen.class.getResourceAsStream("/images/logo.png")) {
            if (is != null) {
                ImageView view = new ImageView(new Image(is));
                view.setFitWidth(LOGO_SIZE);
                view.setFitHeight(LOGO_SIZE);
                view.setPreserveRatio(true);
                view.setSmooth(true);
                return view;
            }
        } catch (Exception ignored) {
        }
        Label fallback = new Label("MySys22");
        fallback.setStyle(
                "-fx-text-fill: #ffffff; -fx-font-size: 32px; -fx-font-weight: bold;");
        return fallback;
    }

    private void animateBounce(Node logo) {
        TranslateTransition bounce = new TranslateTransition(Duration.millis(700), logo);
        bounce.setByY(-40);
        bounce.setAutoReverse(true);
        bounce.setCycleCount(Animation.INDEFINITE);
        bounce.setInterpolator(Interpolator.EASE_BOTH);
        bounce.play();
    }
}
