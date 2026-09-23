# Renk Sistemi (Color Bible)

[ui-guidelines.md](ui-guidelines.md)'nin somut karşılığı. İki ayrı renk sistemi var ve **karıştırılmamalı**:

1. **Editör chrome paleti** — pencere, kenar çubuğu, araç çubuğu, panel, form, ikon rengi. Neredeyse tamamen gri tonları + tek bir vurgu rengi. Bu belge asıl bunu tanımlıyor.
2. **Widget/ikon içerik paleti** — kullanıcının butonlarına/ikon paketlerine seçtiği renkler. Bu, kullanıcının kendi seçimi olduğu için geniş ve renkli olabilir (guideline'daki "widget'ların kendi rengi kullanıcıya ait, bu istisna" maddesi). Editöre bir **başlangıç swatch seti** olarak sunulacak 16 renk aşağıda ayrıca tanımlı.

Neden mavi/mor/camgöbeği/neon **editör chrome'unda** yok ama vurgu rengi olarak amber (kehribar) seçildi: amber, endüstriyel/ölçüm cihazı (gösterge ışığı, kadran) çağrışımı yapıyor, "AI SaaS" mor-mavi-camgöbeği gradyan klişesine hiç girmiyor, ve tek başına kullanıldığında ölçülü kalıyor.

## Editör chrome — koyu tema (varsayılan)
CSS custom property adları `--ms-` önekiyle; editör projesi kurulunca (Aşama 4) `:root` içine bu şekilde tanımlanacak.

| Token | Hex | Kullanım |
|---|---|---|
| `--ms-bg-canvas` | `#1c1e22` | Ana çalışma alanı (grid tuvali) arka planı |
| `--ms-bg-surface` | `#232529` | Panel, kenar çubuğu, araç çubuğu arka planı |
| `--ms-bg-surface-raised` | `#2b2e33` | Popover, dropdown, dialog |
| `--ms-bg-inset` | `#16171a` | Input alanı, kod editörü (CSS kutusu) arka planı |
| `--ms-border` | `#35383e` | Varsayılan ayırıcı/kenarlık |
| `--ms-border-strong` | `#46494f` | Vurgulu kenarlık, focus ring tabanı |
| `--ms-text-primary` | `#e6e7ea` | Ana metin |
| `--ms-text-secondary` | `#9a9ea6` | İkincil metin, ikon rengi (varsayılan) |
| `--ms-text-disabled` | `#5b5e64` | Devre dışı metin/ikon |
| `--ms-accent` | `#d97706` | Aktif navigasyon, seçili öğe, birincil eylem, focus, "dikkat" durumu |
| `--ms-accent-hover` | `#f59e0b` | Accent öğesinin hover'ı |
| `--ms-accent-bg-muted` | `rgba(217,119,6,.15)` | Seçili satır arka planı gibi düşük yoğunluklu accent zemin |
| `--ms-accent-on` | `#1c1e22` | Accent zemin üzerindeki metin/ikon rengi |
| `--ms-success` | `#3f9142` | Bağlı cihaz, "aktif" toggle göstergesi |
| `--ms-danger` | `#c0392b` | Hata, bağlantı kopması, silme eylemi |

**Kural:** `--ms-accent`, `--ms-success`, `--ms-danger` dışında chrome'da **doygun renk yok**. Her buton/ikon/panel'i renklendirme — bu üç renk yalnızca durum/etkileşim anlatır.

## Editör chrome — açık tema
Aynı token'lar, ters gri skala + aynı üç anlamlı renk (kontrast için hafif koyulaştırılmış):

| Token | Hex |
|---|---|
| `--ms-bg-canvas` | `#f4f5f6` |
| `--ms-bg-surface` | `#ffffff` |
| `--ms-bg-surface-raised` | `#ffffff` (gölge ile ayrışır, `0 2px 8px rgba(0,0,0,.12)`) |
| `--ms-bg-inset` | `#eceef0` |
| `--ms-border` | `#d8dade` |
| `--ms-border-strong` | `#c2c5ca` |
| `--ms-text-primary` | `#1c1e22` |
| `--ms-text-secondary` | `#5b5e64` |
| `--ms-text-disabled` | `#a3a6ab` |
| `--ms-accent` | `#b45f04` |
| `--ms-accent-hover` | `#9a5203` |
| `--ms-accent-bg-muted` | `rgba(180,95,4,.10)` |
| `--ms-accent-on` | `#ffffff` |
| `--ms-success` | `#2f7a32` |
| `--ms-danger` | `#a8321f` |

## İkon kuralları
- **Chrome ikonları** (menü, araç çubuğu, panel başlığı): tek renk, `currentColor` veya `--ms-text-secondary`; aktif/seçili durumda `--ms-accent`. Sabit stroke kalınlığı (2px, 20/24px kutu), dolgu değil çizgi tabanlı (outline) ikon seti. Renkli/3D/gölgeli ikon yok.
- **Widget/ikon paketi ikonları** (kullanıcının butonuna koyduğu ikon): serbest — düz (flat) renkli SVG/PNG olabilir, kullanıcının widget rengiyle uyumlu olması onun tercihi. Yine de gradient/3D-bevel/gölge/parlama efekti **önerilmez** (guideline'daki genel "flat, dekoratif değil" ilkesi burada da geçerli, sadece renk sınırı yok).

## Widget varsayılan swatch seti (16 renk)
Editörde renk seçiciye "hazır palet" olarak sunulacak, düz (flat), beyaz metinle yeterli kontrastta 16 renk — etiket paleti mantığına yakın, ama neon değil:

| Ad | Hex | | Ad | Hex |
|---|---|---|---|---|
| Graphite | `#374151` | | Slate | `#475569` |
| Red | `#b91c1c` | | Orange | `#c2410c` |
| Amber | `#b45309` | | Olive | `#84761f` |
| Green | `#15803d` | | Teal | `#0f766e` |
| Cyan | `#0e7490` | | Blue | `#1d4ed8` |
| Indigo | `#4338ca` | | Violet | `#6d28d9` |
| Magenta | `#a21caf` | | Pink | `#be185d` |
| Brown | `#78350f` | | Charcoal | `#111827` |

Bu 16 renk sadece **başlangıç önerisi**; kullanıcı her widget için serbest hex girebilir (plandaki "buton rengi customize edilebilir" maddesi).

## Uygulama notu
Bu token'lar Aşama 4'te editör projesi kurulunca gerçek bir CSS dosyasına (`server/editor/src/theme.css` gibi) geçirilecek. `client/packages/renderer` widget'ların **kendi** render'ını yapar ve editör chrome renklerini bilmez/kullanmaz — sadece widget'ın kendi `style` alanındaki renkleri uygular.
