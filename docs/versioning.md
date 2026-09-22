# Sürümleme (Semantic Versioning)

Proje [SemVer](https://semver.org/) kullanır: `MAJOR.MINOR.PATCH`. Henüz 1.0.0 öncesindeyiz (`0.x.y`); SemVer'e göre 0.x'te her şey serbesttir ama bu projede **0.x'te de disiplinli davranıyoruz** — aşağıdaki MAJOR/MINOR/PATCH ayrımı 0.x için de geçerli, sadece ilk sayı 0'da sabit kalıyor (`0.MINOR.PATCH`).

## Nerede kaç versiyon var?
Tek bir "proje versiyonu" yok — birbirinden bağımsız dört versiyon takip edilir, çünkü biri değişse bile diğerleri uyumlu kalabilir:

| Ne | Versiyon nerede yaşar | Şu an |
|---|---|---|
| **Server (Host)** | `MacroStation.Core.Sessions.ClientHub.ServerVersion` sabiti | `0.1.0` |
| **Client** | `client/package.json` → `version` (client kurulunca) | — |
| **Plugin SDK** (`MacroStation.Plugin.Abstractions`) | Kendi paket versiyonu (NuGet paketi olduğunda `.csproj` → `<Version>`) | `0.1.0` (henüz paketlenmedi, kod hâlâ proje referansıyla kullanılıyor) |
| **Her plugin** | Kendi `plugin.json` → `version` | plugin'ler henüz yazılmadı (Aşama 6) |

**Protokol versiyonu ayrı bir kavram:** WebSocket mesaj şeması (`hello`/`welcome` içindeki `clientVersion`/`serverVersion`) şu an sadece bilgi amaçlı gönderiliyor, uyumluluk kontrolü yapmıyor. İleride (Aşama 5, eşleştirme) bir `protocolVersion` tamsayısı eklenip sunucu/istemci uyuşmazsa kullanıcıya net bir "istemcini güncelle" mesajı gösterilmesi planlanıyor — bu SemVer'den bağımsız, basit artan bir sayı olacak (protokol her değiştiğinde +1).

## MAJOR / MINOR / PATCH neyi ifade eder
- **MAJOR:** Geriye uyumsuz bir değişiklik.
  - Server/Client: mevcut bir WebSocket mesajının alanı kaldırılıyor/anlamı değişiyor, `hello` akışı değişiyor.
  - Plugin SDK: `IActionHandler`, `IVariableStore`, `IVariableProvider`, `IDeviceController`, `ActionContext` gibi arayüzlerden biri kırılıyor (imza değişiyor, üye kaldırılıyor). **Bu, o SDK sürümüne yazılmış tüm plugin'lerin yeniden derlenmesini gerektirir.**
  - Plugin: kendi `plugin.json` ayar şemasını veya action/variable adlarını geriye uyumsuz değiştiriyor (kullanıcının kayıtlı profilindeki aksiyon ayarları artık anlamsızlaşıyor).
- **MINOR:** Geriye uyumlu yeni özellik.
  - Yeni bir widget tipi, yeni bir built-in aksiyon (`core.*`), yeni bir opsiyonel protokol alanı/mesaj tipi, SDK'ya yeni bir opsiyonel arayüz/üye eklenmesi.
- **PATCH:** Davranış değişmeden hata düzeltmesi, performans, iç refactor.

## Plugin uyumluluk beyanı
Her plugin, hangi Plugin SDK sürümüyle derlendiğini `plugin.json` içinde beyan eder (henüz uygulanmadı, Aşama 6'da eklenecek şema):

```json
{
  "id": "obs",
  "name": "OBS Kontrolü",
  "version": "1.2.0",
  "sdkVersion": "^1.0.0",
  "minServerVersion": "0.4.0"
}
```

- `sdkVersion`: npm tarzı caret aralığı (`^1.0.0` → `1.x.x` ile uyumlu, `2.0.0` ile değil). Host, plugin'i yüklemeden önce kendi Plugin SDK sürümüyle bu aralığı karşılaştırır; uyuşmazsa plugin'i **yüklemez** ve editörde net bir uyarı gösterir ("Bu plugin SDK 2.x istiyor, sunucu 1.x kullanıyor").
- `minServerVersion`: plugin'in ihtiyaç duyduğu asgari server (Host) sürümü — ör. plugin bir `IDeviceController` metodunu kullanıyorsa ve o metot server 0.4.0'da eklendiyse.
- Bu iki alan sayesinde "hangi plugin hangi sürümle çalışıyor" editördeki plugin listesinde tek bakışta görülebilir (yükleniyor/uyumsuz/güncel değil).

## Branch → main geçişinde versiyon bump'ı
**Kural: `development` (veya çalışılan branch) `main`'e merge edilmeden önce, versiyonun bump edilip edilmeyeceği ve MAJOR/MINOR/PATCH'ten hangisi olacağı HER SEFERİNDE kullanıcıya sorulur.** Otomatik/sessiz bump yapılmaz — kullanıcı onaylamadan sürüm numarası değiştirilmez ve `main`'e merge edilmez.

Henüz `development`/`main` branch'leri kurulmadı (repo `master` üzerinde, ilk commit atılmadı). Bu branch'ler kurulduğunda bu kural geçerli olacak.

## Changelog
`docs/CHANGELOG.md` [Keep a Changelog](https://keepachangelog.com/) formatında tutulur; bir versiyon bump'ı onaylandığında o girdi de eklenir.
