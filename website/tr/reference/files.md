# Dosyalar ve portlar

## Ağ

| Ne | Nerede |
|---|---|
| Port | Bilgisayarınızda TCP **9820**. Kurulum programı bunu yalnızca özel ağlar için açar. |
| Şifreli port | Bilgisayarınızda TCP **9821** (`wss://`, `https://`). Kurulum programı bunu yalnızca özel ağlar için açar. Bkz. [Güvenlik](/tr/guide/security). |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |

Bilgisayarınızın adresi sistem tepsisi simgesi menüsünde ve Eşleştirme penceresinde gösterilir.

## Verileriniz

Her şey `%AppData%\MacroGrid\` içindedir. Sistem tepsisi simgesi menüsünden açabilirsiniz.

| Yol | İçerik |
|---|---|
| `profiles\` | Her profil için bir dosya |
| `devices.json` | Eşleşmiş cihazlar. Belirteçleri hesabınıza özel Windows DPAPI ile şifrelenmiştir. |
| `preferences.json` | Tercihleriniz |
| `plugins\<id>\` | Kurulu eklentiler ve ayarları |
| `plugin-permissions.json` | JavaScript eklentileri için onayladığınız izinler |
| `user-variables.json`, `automation.json` | Genel değişkenleriniz ve otomasyon kurallarınız |
| `restore-points\` | Geri yükleme, profil silme veya içe aktarma öncesinde ve güncellemeden sonra alınan otomatik veri kopyaları. Bkz. [Profiller ve sayfalar](/tr/guide/profiles#yedekle-ve-geri-yukle). |
| `logs\` | Günlükler. Sorun bildirirken ekleyin. |

Paylaşılan profiller tek bir `.mgprofile` dosyasıdır. İçe aktarma eski `.msprofile` dosyalarını ve `.json` dosyalarını da kabul eder.
