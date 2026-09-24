# Dosyalar ve portlar

## Ağ

| Ne | Nerede |
|---|---|
| Port | Bilgisayarınızda TCP **9820**. Kurulum programı bunu yalnızca özel ağlar için açar. |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |

Bilgisayarınızın adresi sistem tepsisi simgesi menüsünde ve Eşleştirme penceresinde gösterilir.

## Verileriniz

Her şey `%AppData%\MacroGrid\` içindedir. Sistem tepsisi simgesi menüsünden açabilirsiniz.

| Yol | İçerik |
|---|---|
| `profiles\` | Her profil için bir dosya |
| `devices.json` | Eşleşmiş cihazlar ve belirteçleri (düz metin) |
| `preferences.json` | Tercihleriniz |
| `plugins\<id>\` | Kurulu eklentiler ve ayarları |
| `plugin-permissions.json` | JavaScript eklentileri için onayladığınız izinler |
| `logs\` | Günlükler. Sorun bildirirken ekleyin. |

Dışa aktarılan profiller tek bir `.msprofile` dosyasıdır. İçe aktarma `.json` dosyalarını da kabul eder.
