# Aksiyonlar

Bir widget olayına bağlayabileceğiniz tüm aksiyonlar. Aşağıdaki adlar aksiyon seçicide göründükleri gibidir. Yerleşik ve OBS aksiyon adları şu an düzenleyici dilinden bağımsız olarak Türkçe görünür (ikinci sütun).

## Yerleşik

| Aksiyon | Seçicideki ad | Kategori | Ayarlar |
|---|---|---|---|
| Kısayol | Kısayol tuşu | Klavye | Tuş kombinasyonu |
| Metin yazma | Metin yaz | Klavye | Yazılacak metin |
| Uygulama açma | Uygulama aç | Sistem | Uygulama, argümanlar (isteğe bağlı) |
| URL açma | URL aç | Sistem | `http://` veya `https://` ile başlayan adres |
| Gecikme | Bekle | Sistem | Milisaniye, en fazla 60000 |
| Sayfa değiştirme | Sayfa değiştir | Sayfa ve profil | Bir sayfaya git, sonraki, önceki veya geri |
| Profil değiştirme | Profil değiştir | Sayfa ve profil | Profil |
| Ana ses düzeyi | Ana ses seviyesi | Ses | Slider veya knob değerini kullanır |
| Sesi kapat / aç | Sesi kapat/aç | Ses | Sustur veya sesi aç |
| Sesi aç/kapat (toggle) | Sesi sessize al/aç | Ses | Yok |

## OBS eklentisi

| Grup | Neler yapabilirsiniz |
|---|---|
| Sahneler | Sahne değiştirme, önizleme sahnesi, stüdyo modu ve geçiş, geçiş türü, OBS profili |
| Yayın ve kayıt | Yayını ve kaydı başlatma, durdurma veya aç/kapat, kaydı duraklatma veya sürdürme |
| Çıkışlar | Sanal kamera, tekrar tamponu (replay buffer), tekrarı kaydetme |
| Ses | Bir girişi susturma, sesini açma veya aç/kapat, ses düzeyini ayarlama, dB cinsinden adım adım değiştirme |
| Öğeler ve metin | Bir sahne öğesini gösterme, gizleme veya aç/kapat, bir metin kaynağını ayarlama |

## Olaylar

| Widget | Olaylar |
|---|---|
| Buton | Basınca, Bırakınca, Uzun basınca, Çift dokununca |
| Toggle | Açılınca, Kapanınca |
| Slider, Knob | Değer değişti |

Bir olaya bağlı aksiyonlar sırayla çalışır. Bkz. [Aksiyonlar ve makrolar](/tr/guide/actions).
