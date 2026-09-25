using System.Globalization;
using MacroGrid.Core.Preferences;

namespace MacroGrid.Host;

/// <summary>
/// Texts of the native shell (tray menu, native dialogs, start-up messages). The editor's own texts live in
/// the editor's i18n files; this is the small counterpart for the parts that are drawn by Windows itself.
/// The language is the one in the preferences ("tr" or "en"); before the preferences exist it follows the
/// Windows display language, the same rule as <see cref="AppPreferences.Language"/>.
/// </summary>
internal static class HostText
{
    private static readonly Dictionary<string, (string En, string Tr)> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tray.openEditor"] = ("Open editor", "Düzenleyiciyi aç"),
        ["tray.openTestPage"] = ("Open test page", "Test sayfasını aç"),
        ["tray.openDataFolder"] = ("Open data folder", "Veri klasörünü aç"),
        ["tray.exit"] = ("Exit", "Çıkış"),
        ["tray.running"] = ("Macro Grid is running", "Macro Grid çalışıyor"),
        ["tray.connectFrom"] = ("Connect from your phone: {0}", "Telefondan bağlan: {0}"),
        ["tray.noClients"] = ("No connected devices", "Bağlı cihaz yok"),
        ["tray.clients"] = ("Connected: {0}", "Bağlı: {0}"),
        ["tray.checkForUpdates"] = ("Check for updates", "Güncellemeleri denetle"),
        ["tray.updateAvailable"] = ("Macro Grid {0} is available", "Macro Grid {0} sürümü hazır"),
        ["tray.updateAvailableHint"] = ("Click to see what is new.", "Yenilikleri görmek için tıklayın."),
        ["tray.upToDate"] = ("You are up to date.", "Uygulamanız güncel."),
        ["tray.updateCheckFailed"] = ("Could not check for updates. Try again later.", "Güncellemeler denetlenemedi. Daha sonra tekrar deneyin."),
        ["tray.updated"] = ("Macro Grid was updated to {0}", "Macro Grid {0} sürümüne güncellendi"),
        ["agreement.title"] = ("Macro Grid user agreement", "Macro Grid kullanıcı sözleşmesi"),
        ["agreement.intro"] = ("Read the user agreement below. Macro Grid can only be used if you accept it.", "Aşağıdaki kullanıcı sözleşmesini okuyun. Macro Grid yalnızca kabul ederseniz kullanılabilir."),
        ["agreement.accept"] = ("Accept", "Kabul ediyorum"),
        ["agreement.decline"] = ("Decline and exit", "Kabul etmiyorum, çık"),
        ["dialog.pickApp"] = ("Choose an app", "Uygulama seç"),
        ["dialog.appFilter"] = ("Apps (*.exe)|*.exe|All files (*.*)|*.*", "Uygulamalar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*"),
        ["app.alreadyRunning"] = ("Macro Grid is already running (look in the notification area).", "Macro Grid zaten çalışıyor (sistem tepsisine bakın)."),
        ["server.startFailed"] = ("The server could not be started on port {0}:\n{1}", "Sunucu {0} portunda başlatılamadı:\n{1}"),
        ["webview.missing"] = ("The WebView2 runtime could not be started. Microsoft Edge WebView2 Runtime must be installed:\n", "WebView2 çalışma zamanı başlatılamadı. Microsoft Edge WebView2 Runtime kurulu olmalı:\n"),
        ["menu.back"] = ("Back", "Geri"),
        ["menu.forward"] = ("Forward", "İleri"),
        ["menu.reload"] = ("Reload", "Yeniden yükle"),
        ["menu.undo"] = ("Undo", "Geri al"),
        ["menu.redo"] = ("Redo", "Yinele"),
        ["menu.cut"] = ("Cut", "Kes"),
        ["menu.copy"] = ("Copy", "Kopyala"),
        ["menu.paste"] = ("Paste", "Yapıştır"),
        ["menu.pasteAsPlainText"] = ("Paste as plain text", "Düz metin olarak yapıştır"),
        ["menu.delete"] = ("Delete", "Sil"),
        ["menu.selectAll"] = ("Select all", "Tümünü seç"),
        ["webview.writtenTo"] = ("(This text was also written to {0}.)", "(Bu metin {0} dosyasına da yazıldı.)"),
    };

    private static Func<string> _language = () => AppPreferencesLanguage();

    /// <summary>Follows the language of the stored preferences from now on.</summary>
    public static void Bind(PreferencesStore preferences) => _language = () => preferences.Get().Language;

    public static string Language => _language();

    public static bool Has(string key) => Texts.ContainsKey(key);

    public static string Get(string key, params object[] args)
    {
        var (en, tr) = Texts[key];
        var text = string.Equals(_language(), "tr", StringComparison.OrdinalIgnoreCase) ? tr : en;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    private static string AppPreferencesLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
}
