# UI Tasarım Kuralları — "AI Slop" Önleme

Bu proje iki farklı yüzeye sahip ve ikisinin tasarım mantığı farklı: **editör** (server tarafı, WebView2 içinde çalışan gerçek bir **masaüstü uygulaması**) ve **client** (telefon/tablette çalışan bir **dokunmatik uygulama**). Aşağıdaki kurallar öncelikle **editör** içindir; client için ayrı bir bölüm var. Her ikisini de yaparken/güncellerken bu dosya okunmalı.

## Editör = masaüstü uygulaması, web dashboard'u değil

Editör bir SaaS dashboard'u ya da landing page değil; kullanıcının saatlerce içinde kalacağı bir **masaüstü uygulaması** (kod editörleri, IDE'ler, not alma/tasarım araçları, oyun launcher'ları, profesyonel yaratıcı yazılımlar gibi düşün — hiçbirinin görsel dilini kopyalama, sadece **etkileşim ilkelerini** al: kalıcı navigasyon, net çalışma alanı, bağlama duyarlı kontroller, kompakt bilgi sunumu, klavye dostu etkileşim, ekran alanının verimli kullanımı).

Masaüstü uygulaması gibi düşün: pencere, kenar çubuğu (sidebar), araç çubuğu (toolbar), komut çubuğu, çalışma alanı (workspace), paneller, bölünmüş görünümler, yeniden boyutlandırılabilir paneller, bağlam menüleri, özellik panelleri, listeler, grid'ler, detay görünümleri. 1080p/1440p masaüstü ekranında düzgün görünmeli; **duyarlı (responsive) bir mobil sitenin masaüstüne gerilmiş hali gibi görünmemeli.**

### Kaçınılacaklar (editör)
- Jenerik SaaS dashboard düzenleri, web landing-page estetiği, "hero" bölümler, dev sayfa başlıkları
- Dev KPI kartları, pazarlama tarzı bölümler
- Aşırı yuvarlatılmış kartlar, her yerde büyük `border-radius`
- Her öğenin bir kart içinde olması, kart-içinde-kart düzenleri, yüzen "cam" paneller
- Glassmorphism, buzlu cam efektleri, gradient arka planlar/butonlar, neon gradyanlar
- Mor/mavi/camgöbeği "AI" renk şemaları, dekoratif parlayan öğeler, arka plan blob'ları, soyut gradient daireler
- Dekoratif illüstrasyonlar, rastgele 3D nesneler, dev dekoratif ikonlar, emoji'nin arayüz öğesi olarak kullanılması
- Aşırı badge/pill/durum çipi, aşırı gölge, aşırı yüzen öğe, aşırı boşluk
- Dev tipografi, dev yuvarlak butonlar, mobil-uygulama tarzı kontroller, alt navigasyon çubuğu, floating action button
- Jenerik Tailwind/shadcn/Material Design şablon estetiği, kalıp admin panelleri
- Her metriğin ayrı renkli bir kart olduğu dashboard'lar, dekoratif grafikler, sahte istatistikler, gereksiz grafik
- Gereksiz sekme/modal/tooltip/ayırıcı/kenarlık/animasyon, aşırı mikro-etkileşim

### Yerleşim (editör)
Tercih et: güçlü soldan-sağa hiyerarşi, kalıcı navigasyon, kompakt araç çubukları, net tanımlanmış çalışma alanları, pratik panel düzenleri, tutarlı hizalama, öngörülebilir etkileşim bölgeleri, mantıklı bilgi yoğunluğu, kompakt kontroller, hassas boşluklandırma.

Kaçın: her şeyi ortalamak, dev boş alanlar, aşırı büyük kartlar, dev kenar boşlukları/padding, salt estetik kaygıyla simetrik düzen, gereksiz görsel hiyerarşi. Mevcut masaüstü ekran alanı verimli kullanılmalı.

### Bileşenler (editör)
Bir bileşen ancak bir amaca hizmet ediyorsa var olmalı. Tercih et: sade yüzeyler, düz (flat) alanlar, ince ayırıcılar, ölçülü kenarlıklar, kompakt butonlar, dikdörtgen veya hafif yuvarlatılmış kontroller, net hover/seçili durumları, bağlama duyarlı eylemler, tanıdık masaüstü etkileşim kalıpları. Her bileşeni kart/pill/yüzen panel/aşırı yuvarlak/aşırı gölgeli yapma — her bölümün görünür bir konteynerе ihtiyacı yok.

### Renk (editör)
Ölçülü bir masaüstü uygulaması renk sistemi kullan. Varsayılan olarak mor/violet/elektrik mavi/camgöbeği/pembe/neon'a veya mavi-mor gradyanlara gitme; istenmedikçe gradient yok. Vurgu rengini şunlar için kullan: aktif navigasyon, seçili öğeler, odak (focus), birincil eylemler, önemli durumlar. Arayüzün geri kalanı nötr kalmalı; her bileşeni renklendirme.

### Tipografi (editör)
Dev başlıklardan, aşırı büyük sayılardan, aşırı ince fontlardan, aşırı font-boyutu çeşitliliğinden kaçın. Önceliğin: mükemmel okunabilirlik, net hiyerarşi, kompakt masaüstü tipografisi, tutarlı satır yükseklikleri, mantıklı font ağırlıkları. Uygulama görsel gürültü olmadan bilgi zengin hissettirmeli.

### Son kontrol (editör)
Uygulamadan önce sor: "Gölgeleri, gradyanları, yuvarlatılmış kartları, dekoratif ikonları ve büyük tipografiyi kaldırsam arayüz hâlâ iyi görünür mü?" Cevap hayırsa tasarım dekorasyona fazla yaslanıyor demektir. Sonuç şu yollarla çekici kalmalı: düzen, tipografi, boşluklandırma, hiyerarşi, hizalama, etkileşim tasarımı, ölçülü renk, kullanışlı bilgi yoğunluğu — görsel efektlerle değil. Hedef **cilalanmış masaüstü yazılımı**, "AI tarafından üretilmiş UI konsept görseli" değil.

## Client (telefon/tablet) için farklı kurallar

Client bir masaüstü uygulaması değil, **tam ekran çalışan bir dokunmatik deck'tir** — kullanıcı ona parmağıyla dokunacak, elinde tutacak. Yukarıdaki "kompakt kontroller / küçük dokunma alanları" kuralı client'a **uygulanmaz**: widget'lar dokunmaya yetecek kadar büyük olmalı (bu zaten grid hücreleri eliyle çözülüyor). Yine de aynı "AI slop" karşıtı ruh geçerli:
- Widget'ların kendi rengi/stili kullanıcıya ait (butonun rengini kullanıcı seçiyor) — bu istisna. Ama **chrome** (üst bilgi çubuğu, bağlantı göstergesi, profil drawer'ı, ayarlar ekranı) sade ve nötr kalmalı; gradient, glow, dekoratif ikon yok.
- Bağlantı durumu tek bir küçük nokta/etiketle anlatılır, büyük renkli banner ile değil (mevcut test sayfasındaki `.dot` yaklaşımı doğru, korunmalı).
- Profil drawer'ı ve ayarlar ekranı bir liste/tablo mantığıyla kurulmalı, kart-grid ile değil.
- Widget içeriği dışında dekoratif hiçbir görsel öğe (illüstrasyon, blob, dev ikon) olmamalı — ekranın tamamı fonksiyonel grid'e ait.

## Bu projeye özel uygulama
- **Editör** (`server/editor`, React, WebView2 içinde): koyu/nötr zemin, ince kenarlıklı grid ve panel ayırıcılar, düz butonlar, sol tarafta sayfa/profil ağacı + orta çalışma alanı (canvas) + sağda özellik paneli gibi klasik 3 panelli IDE düzeni düşünülmeli.
- **Test client / gerçek client** (`server/src/MacroStation.Host/wwwroot/index.html`, sonra `client/`): zaten sade (düz köşeye yakın radius, tek durum noktası, gradient yok) — yeni widget tipleri (slider, toggle, web) eklenirken bu sadelik korunmalı, dokunma hedefleri küçültülmemeli.
- Cihaz listesi, plugin listesi gibi paneller editörde tablo/liste yoğunluklu olmalı, kart-grid değil.

## Pencere / Modal / Overlay Kuralları (editör)

Editör bir web uygulaması gibi hissettirilmemeli. Özellikle modal, popup ve overlay kullanımı masaüstü uygulaması hissini bozabileceği için varsayılan olarak kullanılmamalıdır.

### Modal kullanımı

**Modal kullanma.** Bir işlemin yapılabilmesi için mümkünse mevcut çalışma alanı, sağ özellik paneli, alt panel veya ayrı bir uygulama görünümü kullanılmalı.

Özellikle şu amaçlarla modal oluşturma:
- Basit ayarlar
- Form doldurma
- Profil düzenleme
- Widget özellikleri
- Plugin ayarları
- Cihaz bilgileri
- Sayfa/profil bilgileri
- Basit onay işlemleri
- Liste veya tablo görüntüleme
- Küçük bilgi mesajları

Bunlar mümkün olduğunca mevcut masaüstü çalışma alanında çözülmeli.

### Modal gerçekten zorunluysa

Modal kaçınılmazsa bunu bir **web modalı gibi değil, masaüstü uygulaması penceresi gibi** tasarla.

Masaüstü penceresi davranışı:
- Belirgin bir pencere başlığı olmalı.
- Başlık çubuğu ve içerik alanı birbirinden ayrılmalı.
- Pencerenin amacı başlıktan açıkça anlaşılmalı.
- İçerik düzeni kompakt olmalı.
- Gereksiz büyük padding kullanılmamalı.
- Aşırı `border-radius` kullanılmamalı.
- Glassmorphism kullanılmamalı.
- Arkaya dev bir blur efekti uygulanmamalı.
- Dekoratif backdrop kullanılmamalı.
- Pencere gerektiğinde sürüklenebilir bir desktop-window hissi verebilir.
- Kapatma eylemi açık ve tahmin edilebilir olmalı.
- Escape ile kapatma desteklenmeli.
- Klavye odağı mantıklı şekilde yönetilmeli.
- Modal içindeki kontroller normal masaüstü kontrolleri gibi davranmalı.

Modal:
`rounded-xl + backdrop-blur + shadow-2xl + centered card`
şeklinde tipik web uygulaması kalıbına dönüşmemeli.

Bir modal ekran görüntüsünde "web sitesi üzerindeki popup" gibi değil, "uygulama içinde açılmış küçük bir pencere" gibi görünmeli.

### Overlay

Overlay'leri minimumda tut.

Kullanıcıyı ana çalışma alanından koparan tam ekran overlay'ler oluşturma.

Şunlardan kaçın:
- Tam ekran karartma
- Büyük blur katmanı
- Ortada yüzen dev kart
- Overlay üzerinde overlay
- Modal içinde modal
- Popup içinde popup

Bir işlem mevcut çalışma alanında çözülebiliyorsa overlay kullanma.

### Toast / notification

Web uygulamalarındaki klasik:

`bottom-right → floating rounded notification card`

kalıbını varsayılan olarak kullanma.

Bildirim gerekiyorsa masaüstü uygulamasına uygun, küçük ve düşük dikkat çeken bir durum mesajı tercih et.

Örneğin:
- status bar
- alt durum alanı
- toolbar status
- küçük inline feedback

Bildirim kullanıcıyı çalışma alanından koparmamalı.

Hata durumlarında da dev kırmızı banner veya dev alert kartları oluşturma.

## Drawer / Side Panel

Drawer kullanımı web uygulaması hissi oluşturabileceğinden varsayılan çözüm değildir.

Editörde zaten kalıcı bir sağ/sol panel bulunuyorsa drawer yerine bu panel kullanılmalı.

Örneğin:
- Widget özellikleri → sağ Properties paneli
- Sayfa/profil → sol navigation/tree
- Cihazlar → sol veya alt panel
- Plugin ayarları → mevcut workspace/panel

Drawer yalnızca gerçekten geçici ve bağlama bağlı bir panel olması gerektiğinde kullanılabilir.

Drawer kullanılıyorsa:
- Ekranın tamamını kaplamamalı.
- Mobil uygulama drawer'ı gibi davranmamalı.
- Büyük yuvarlatılmış köşeler kullanılmamalı.
- Glassmorphism kullanılmamalı.
- İçerik bir liste/özellik paneli gibi görünmeli.
- Desktop panelinin doğal bir parçası gibi hissettirmeli.

## Web Uygulaması Hissini Özellikle Engelle

Aşağıdaki kombinasyonları özellikle kullanma:

- centered modal card
- backdrop blur
- floating rounded cards
- bottom-right toast
- floating action button
- hamburger navigation
- mobile bottom navigation
- giant dropdown menus
- full-screen command palette for simple actions
- excessive popovers
- tooltip everywhere
- card-based settings pages
- card-based device lists
- card-based plugin lists
- dashboard-style overview pages

Bunların yerine masaüstü uygulaması kalıplarını tercih et:

- persistent sidebar
- toolbar
- menu bar
- context menu
- properties panel
- split pane
- tree view
- table/list view
- status bar
- tabs where genuinely useful
- resizable panels
- inline editing
- inline validation
- desktop-style dialog when unavoidable

## Context Menu

Sağ tık / context menu gerekiyorsa web sitesi dropdown'u gibi değil, masaüstü işletim sistemi uygulamalarındaki context menu mantığında tasarlanmalı.

- Kompakt olmalı.
- Liste tabanlı olmalı.
- Gereksiz ikonlarla doldurulmamalı.
- Gruplandırma gerekiyorsa ince separator kullanılabilir.
- Menü öğeleri kısa ve net olmalı.
- Hover/selection durumu açık olmalı.
- Gereksiz pill/badge kullanılmamalı.

## Dropdown / Select

Select ve dropdown kontrolleri de web formu estetiğine kaymamalı.

Avoid:
- dev rounded dropdown
- floating card dropdown
- excessive shadow
- oversized options
- colorful option cards

Prefer:
- kompakt masaüstü menü
- net hover state
- seçili öğe göstergesi
- klavye ile gezinme
- mümkün olduğunda native desktop davranışına yakın interaction

## Scroll / Panel Davranışı

Editör tek bir uzun web sayfası gibi tasarlanmamalı.

Yanlış yaklaşım:

`body → uzun sayfa → sürekli vertical scroll`

Doğru yaklaşım:

`application shell → fixed navigation → workspace → bağımsız scroll alanları`

Örneğin:
- Sol tree kendi içinde scroll edebilir.
- Orta canvas/workspace kendi davranışına sahip olabilir.
- Sağ Properties paneli bağımsız scroll edebilir.
- Alt log/output paneli ayrı scroll edebilir.

Tüm uygulamanın tek bir web sayfası gibi aşağı doğru uzaması engellenmeli.

## Application Shell

Editörün bütün ekranlarında ortak bir application shell korunmalı.

Shell mümkün olduğunca sabit kalmalı:
- üst toolbar
- sol navigation
- ana workspace
- sağ properties paneli
- alt status/output alanı

Sayfalar arasında geçerken uygulamanın tamamı yeniden çizilmiş veya başka bir web sayfasına gidilmiş hissi oluşmamalı.

Kullanıcı her zaman aynı uygulamanın içinde olduğunu hissetmeli.

## Navigation

Navigation web sitesi menüsü gibi tasarlanmamalı.

Avoid:
- büyük navigation cards
- navigation için dev ikonlar
- pill-shaped navigation items
- her navigation item için farklı renk
- aşırı padding

Prefer:
- kompakt tree/list
- net selected state
- hover state
- gerektiğinde collapse/expand
- keyboard navigation
- context menu

Navigation, uygulamanın çalışma alanına hizmet etmeli; başlı başına dekoratif bir bölüm olmamalı.

## Desktop Interaction

Editör için masaüstü etkileşimleri önceliklidir.

Mümkün olduğunda destekle:
- keyboard shortcuts
- Enter ile düzenleme/onay
- Escape ile iptal/kapatma
- Ctrl/Cmd + S
- Ctrl/Cmd + Z / Shift + Ctrl/Cmd + Z
- Delete
- F2 ile rename
- sağ tık context menu
- mouse wheel
- drag & drop
- multi-selection
- focus states
- resizable panels

Klavye ile kullanılabilecek bir masaüstü uygulamasını sadece mouse ile kullanılabilen bir web uygulamasına dönüştürme.

## Form Tasarımı

Ayarlar veya özellik panelleri form gerektiğinde:

Avoid:
- her input'u ayrı karta koymak
- dev input alanları
- dev form başlıkları
- her alanın altında gereksiz açıklama metinleri
- rounded input container'ları aşırı kullanmak
- formu ortalanmış web sayfası haline getirmek

Prefer:
- label + control düzeni
- kompakt satırlar
- mantıklı gruplar
- iki kolonlu property editor gerektiğinde kullanılabilir
- section header
- inline validation
- keyboard navigation

Özellikle Properties paneli bir web formu değil, **masaüstü uygulamasındaki property inspector** gibi görünmeli.

## Confirmation / Destructive Actions

Basit işlemler için confirmation modal oluşturma.

Örneğin:
- bir öğeyi yeniden adlandırmak
- küçük bir ayarı değiştirmek
- profil seçmek

gibi işlemler doğrudan yapılmalı veya undo mekanizması kullanılmalı.

Yıkıcı ve geri alınamaz işlemler için confirmation gerekiyorsa masaüstü uygulaması tarzı küçük bir dialog kullanılabilir.

Dialog:
- kısa
- açık
- doğrudan
- gereksiz açıklamalardan arındırılmış

olmalı.

"Are you sure?" başlıklı jenerik web modalı oluşturma.

## Animasyon

Animasyon yalnızca interaction feedback veya state transition için kullanılmalı.

Avoid:
- sayfa geçiş animasyonları
- kartların tek tek fade-in olması
- staggered animations
- sürekli hareket eden dekoratif öğeler
- parlayan border
- gradient animation
- hover sırasında büyük scale efektleri
- gereksiz spring/bounce efektleri

Desktop application hissi için animasyonlar:
- kısa
- kontrollü
- işlevsel
- düşük dikkat çekicilikte

olmalı.

## Genel "Desktop First" Kuralı

Her yeni UI bileşeni için şu soruyu sor:

"Bu bileşen bir web sitesinde mi, yoksa masaüstü uygulamasında mı daha doğal görünür?"

Eğer cevap web sitesi ise tasarımı yeniden değerlendir.

Öncelik sırası:

1. Masaüstü kullanım modeli
2. İşlevsellik
3. Bilgi hiyerarşisi
4. Klavye/mouse etkileşimi
5. Ekran alanının verimli kullanımı
6. Görsel tutarlılık
7. Estetik efektler

Estetik, masaüstü kullanım modelinin önüne geçmemeli.

## Pencere / Modal / Overlay Kuralları (editör)

Editör gerçek bir masaüstü uygulaması gibi davranmalıdır. Web uygulamalarındaki modal, popup ve overlay kalıpları varsayılan çözüm değildir.

### Modal yerine Window

**Modal kullanmak yerine mümkün olduğunda ayrı bir desktop window aç.**

Özellikle kendi içinde bağımsız bir çalışma alanı, ayar grubu veya yönetim ekranı olan işlemler modal içine sıkıştırılmamalıdır.

Örneğin:

- Settings
- Plugin Manager
- Device Manager
- Profile Manager
- Page Manager
- Keyboard Shortcuts
- About
- Import / Export
- Connection Settings
- User/Profile configuration
- Detaylı cihaz veya plugin konfigürasyonu

gibi ekranlar gerektiğinde **ayrı bir uygulama penceresi** olarak açılabilir.

Amaç:

`Ana Editör → yeni masaüstü penceresi`

hissidir.

Şu hissi vermemelidir:

`Web sayfası → arka plan karardı → ortada modal kart açıldı`

### Yeni Window davranışı

Yeni pencere:

- Masaüstü uygulamasının doğal bir parçası gibi görünmeli.
- Kendi başlığı olmalı.
- Kendi pencere sınırları olmalı.
- Ana uygulamadan görsel olarak ayrılmalı.
- İçeriğine göre uygun boyutta açılmalı.
- Gerektiğinde yeniden boyutlandırılabilmeli.
- Gerekirse minimize/maximize/close davranışlarına sahip olmalı.
- Açıldığında ekranın ortasına rastgele dev bir kart olarak yerleşmemeli.
- Ana pencerenin tamamını karartmamalı.
- Glassmorphism kullanılmamalı.
- Backdrop blur kullanılmamalı.
- Dev `border-radius` kullanılmamalı.
- Web modalı gibi `rounded-xl + shadow-2xl + backdrop` kombinasyonu kullanılmamalı.

Pencere mümkün olduğunca işletim sisteminin ve masaüstü uygulamasının doğal window davranışına yakın olmalıdır.

### Window boyutu

Her yeni pencere 1920×1080 ekranı doldurmak zorunda değildir.

İçeriğin gerektirdiği minimum makul boyut kullanılmalı.

Örneğin:

- Basit ayarlar → küçük/orta pencere
- Plugin Manager → orta pencere
- Device Manager → orta/büyük pencere
- Detaylı editor → büyük pencere

Pencere boyutu içeriğin ihtiyaçlarına göre belirlenmeli.

Gereksiz yere dev bir pencere açma.

### Window ve ana editor ilişkisi

Yeni pencere açıldığında ana editor hâlâ aynı uygulamanın parçası gibi hissettirmeli.

Örneğin:

`MacroStation Editor`
    `└── Settings`
    `└── Plugin Manager`
    `└── Device Manager`

gibi düşün.

Yeni pencere farklı bir web sitesi veya farklı bir SaaS uygulaması gibi tasarlanmamalıdır.

Aynı:
- typography
- color system
- spacing
- control styles
- iconography
- interaction patterns

korunmalıdır.

Ancak pencere kendi bağımsız çalışma alanına sahip olabilir.

### Modal ne zaman kullanılabilir?

Modal **istisnai** bir bileşendir.

Modal yalnızca kullanıcının mevcut işlemine doğrudan bağlı, çok küçük ve kısa süreli bir karar/uyarı gerekiyorsa kullanılabilir.

Örneğin:

- Silme onayı
- Geri alınamaz işlem onayı
- Çok kısa hata mesajı
- Kritik bir uyarı
- Küçük bir seçim

gibi.

Bunun dışında modal kullanma.

Bir ekran kendi başına bir iş yapıyorsa modal yerine **window veya mevcut workspace/panel** kullan.

### Modal tasarımı

Modal kullanılması gerçekten gerekiyorsa bile:

- küçük
- kompakt
- düz
- masaüstü uygulaması estetiğinde
- net başlıklı
- net aksiyonlu

olmalı.

Kesinlikle:

- backdrop blur
- cam efekti
- dev rounded corners
- dev shadow
- gradient
- dekoratif ikon
- dev başlık
- aşırı padding
- ekranın ortasında yüzen SaaS kartı

kullanılmamalı.

Modal, web uygulaması modalı gibi görünmemeli.

### Overlay

Tam ekran overlay mümkün olduğunca kullanılmamalıdır.

Avoid:

- tüm ekranı karartan backdrop
- blur edilmiş ana uygulama
- ortada dev panel
- overlay üzerinde overlay
- modal içinde modal

Yeni bir çalışma alanına ihtiyaç varsa overlay yerine **yeni window** aç.

### Drawer

Drawer da varsayılan çözüm değildir.

Editörde zaten kalıcı bir sidebar veya properties paneli varsa drawer oluşturma.

Örneğin:

`Widget seçildi → sağ Properties paneli`

kullan.

Şunu yapma:

`Widget seçildi → sağdan web drawer kaydı`

Drawer ancak gerçekten geçici ve bağımsız bir panel davranışı gerekiyorsa kullanılabilir.

### Toast / Notification

Web uygulamalarındaki klasik floating toast tasarımını mümkün olduğunca kullanma.

Örneğin:

`bottom-right → rounded card → shadow → "Saved successfully"`

gibi bir tasarım varsayılan olmamalıdır.

Bunun yerine:
- status bar
- toolbar status
- inline feedback
- kısa durum göstergesi

tercih edilmeli.

Kritik bildirim gerekiyorsa küçük bir desktop notification kullanılabilir.

### Özet karar ağacı

Yeni bir UI ekranına ihtiyaç olduğunda:

1. Mevcut Properties panelinde çözülebiliyor mu?
   → **Properties panelini kullan.**

2. Mevcut workspace içinde çözülebiliyor mu?
   → **Workspace kullan.**

3. Ayrı bir çalışma alanı gerekiyor mu?
   → **Yeni desktop window aç.**

4. Sadece kısa bir onay/uyarı mı gerekiyor?
   → **Küçük modal/dialog kullan.**

5. Sadece anlık durum bilgisi mi gerekiyor?
   → **Inline/status bar feedback kullan.**

Varsayılan tercih:

**Workspace / Panel → Window → küçük Dialog → Modal**

Modal son seçenek olmalıdır.

### En önemli kural

Yeni bir UI tasarlarken kendine şunu sor:

"Bu bir web uygulaması olsaydı nasıl yapardım?"

Bu soruyu kullanma.

Şunu sor:

"Windows üzerinde çalışan gerçek bir masaüstü programı bunu nasıl çözerdi?"

Tasarım kararı buna göre verilmelidir.

## Properties Panel — Desktop Inspector

Properties paneli bir web settings sayfası veya SaaS sidebar'ı gibi değil, gerçek bir **desktop property inspector** gibi görünmelidir.

- Sağda sabit/persistent panel olarak çalışmalı; floating card gibi görünmemeli.
- Kart içinde kart, glassmorphism, büyük shadow, gradient ve büyük border-radius kullanma.
- Her property'yi ayrı card yapma.
- Özellikleri kompakt `Label | Value` satırları halinde göster.
- Label mümkün olduğunca solda, değer/control sağda olmalı.
- Input'lar küçük ve kompakt desktop kontrolleri olmalı; dev web form input'ları kullanma.
- Property gruplarını `GENERAL`, `APPEARANCE`, `LAYOUT`, `BEHAVIOR` gibi sade section'larla ayır.
- Section'ları card içine alma; ince separator ve küçük section header yeterli.
- Basit değer değişiklikleri inline yapılmalı; bunun için modal açma.
- Numeric değerlerde kompakt input + birim kullan: `Width [120] px`.
- Select'ler kompakt desktop dropdown gibi görünmeli.
- Boolean değerlerde dev mobil/SaaS switch'leri kullanma; checkbox veya küçük toggle tercih et.
- Color için dev color card yerine küçük swatch + hex/value kullan.
- İleri seviye özellikler gerektiğinde sade collapse/expand kullanılabilir; her şeyi accordion yapma.
- Hiçbir öğe seçili değilse dev empty-state, illüstrasyon veya büyük ikon gösterme. Basit `No selection` yeterli.
- Birden fazla öğe seçildiğinde ortak özellikleri göster; yeni modal veya ekran açma.
- Panel genişliği makul olmalı ve mümkünse kullanıcı tarafından resize edilebilmeli.
- Panel kendi içinde bağımsız scroll alanına sahip olabilir; tüm uygulamayı uzun bir web sayfasına dönüştürme.
- Keyboard navigation, Enter, Escape, F2 ve inline editing gibi desktop etkileşimlerini destekle.

Hedef görünüm:

`Canvas | Properties`

ve Properties panelinin hissi:

**"Seçili nesnenin özelliklerini düzenliyorum."**

Kesinlikle:

**"Bir web sitesinin Settings sayfasındayım."**

gibi görünmemeli.

## Menu Bar — Desktop Application

Üst menubar gerçek bir masaüstü uygulamasının menubar'ı gibi davranmalı; web sitesi navigation bar'ı gibi görünmemeli.

- Sol tarafta uygulama adı/logo + klasik menüler: `File`, `Edit`, `View`, `Window`, `Help` vb.
- Menü öğeleri kompakt, sade ve metin ağırlıklı olmalı.
- Büyük butonlar, pill'ler, kartlar veya renkli navigation item'ları kullanma.
- Menubar yüksekliği kompakt olmalı; dev padding kullanılmamalı.
- Menü açıldığında web dropdown'u değil, klasik desktop context/menu görünümü kullanılmalı.
- Menü öğelerinde keyboard shortcut gösterilebilir: `Save    Ctrl+S`.
- Hover ve selected durumları sade ve net olmalı.
- Menubar'da gradient, shadow, glassmorphism, blur veya dekoratif ikon kullanma.
- Sağ tarafta bağlantı durumu, kullanıcı, pencere kontrolleri gibi sistemsel bilgiler gerekiyorsa kompakt tutulmalı.
- Menubar ile toolbar birbirine karıştırılmamalı: **menubar = komutlar**, **toolbar = sık kullanılan aksiyonlar**.
- Menü yapısı gerçek desktop uygulaması mantığında olmalı; her şeyi menüye doldurma.

Hedef:

`File   Edit   View   Window   Help`

hissi vermeli.

Şu hissi vermemeli:

`Home   Dashboard   Features   Settings` adlı bir web sitesi navigation barı.
