# UI Tasarım Kuralları — "AI Slop" Önleme

Bu proje iki farklı yüzeye sahip ve ikisinin tasarım mantığı farklı: **editör** (server tarafı, WebView2 içinde çalışan gerçek bir **masaüstü uygulaması**) ve **client** (telefon/tablette çalışan bir **dokunmatik uygulama**). Aşağıdaki kurallar öncelikle **editör** içindir; client için ayrı bir bölüm var. Her ikisini de yaparken/güncellerken bu dosya okunmalı.

## Editör = masaüstü uygulaması, web dashboard'u değil

Editör bir SaaS dashboard'u ya da landing page değil; kullanıcının saatlerce içinde kalacağı bir **masaüstü uygulaması** (VS Code, Figma, Obsidian, JetBrains IDE'leri, Steam, profesyonel yaratıcı yazılımlar gibi düşün — görsel dilini kopyalama, **etkileşim ilkelerini** al: kalıcı navigasyon, net çalışma alanı, bağlama duyarlı kontroller, kompakt bilgi sunumu, klavye dostu etkileşim, ekran alanının verimli kullanımı).

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
