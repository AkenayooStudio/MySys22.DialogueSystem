package ake.ruby.dialogueeditor.ui;

import ake.ruby.dialogueeditor.i18n.I18n;

import java.time.Duration;
import java.time.Instant;

public final class RelativeTime {

    private RelativeTime() {}

    public static String format(String isoInstant) {
        if (isoInstant == null || isoInstant.isBlank()) {
            return I18n.t("home.card.never_opened");
        }
        try {
            Instant then = Instant.parse(isoInstant);
            Instant now = Instant.now();
            Duration d = Duration.between(then, now);

            long seconds = d.getSeconds();
            if (seconds < 60) return I18n.t("time.just_now");

            long minutes = seconds / 60;
            if (minutes < 60) return I18n.t("time.minutes_ago", minutes);

            long hours = minutes / 60;
            if (hours < 24) return I18n.t("time.hours_ago", hours);

            long days = hours / 24;
            if (days == 1) return I18n.t("time.yesterday");
            if (days < 30) return I18n.t("time.days_ago", days);

            long months = days / 30;
            if (months < 12) return I18n.t("time.months_ago", months);

            long years = days / 365;
            return I18n.t("time.years_ago", years);
        } catch (Exception e) {
            return isoInstant;
        }
    }
}
