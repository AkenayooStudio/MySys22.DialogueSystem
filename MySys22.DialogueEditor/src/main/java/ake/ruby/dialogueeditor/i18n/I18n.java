package ake.ruby.dialogueeditor.i18n;

import java.text.MessageFormat;
import java.util.Locale;
import java.util.MissingResourceException;
import java.util.ResourceBundle;

public final class I18n {

    private static final String BUNDLE = "i18n.messages";
    private static final Locale DEFAULT = Locale.ENGLISH;

    private static ResourceBundle bundle;
    private static Locale currentLocale;

    private I18n() {}

    public static void setLocale(Locale locale) {
        if (locale == null) locale = DEFAULT;
        currentLocale = locale;
        try {
            bundle = ResourceBundle.getBundle(BUNDLE, locale);
        } catch (MissingResourceException e) {
            bundle = ResourceBundle.getBundle(BUNDLE, Locale.ENGLISH);
            currentLocale = Locale.ENGLISH;
        }
    }

    public static Locale getLocale() {
        if (currentLocale == null) setLocale(Locale.getDefault());
        return currentLocale;
    }

    public static String t(String key) {
        if (bundle == null) setLocale(Locale.getDefault());
        try {
            return bundle.getString(key);
        } catch (MissingResourceException e) {
            return "!" + key + "!";
        }
    }

    public static String t(String key, Object... args) {
        return MessageFormat.format(t(key), args);
    }
}
