# Düzenleyici

![Macro Grid düzenleyicisi: solda klasörlü Hiyerarşi ağacı, ortada açık sayfa, sağda Özellikler](/img/editor-overview.png)

Düzenleyici, sunucunun kendi penceresinde (sistem tepsisi simgesinden) açılır. Yalnızca bilgisayarın kendisinde çalışır. Ağınızdaki diğer cihazlar açamaz.

## Yerleşim

Düzenleyici, istediğiniz gibi düzenleyebileceğiniz panellerden oluşan bir çalışma alanıdır. Varsayılan yerleşimde solda Hiyerarşi ve Araç Kutusu, ortada sayfa sekmeleri ve ızgara, sağda Özellikler bulunur.

- **Menü çubuğu:** **Dosya** (profilleri dışa ve içe aktarma), **Görünüm** (panelleri gösterme veya gizleme, yerleşimleri kaydetme), **Ayarlar** (Tercihler), **Eklentiler** (Eklentileri Yönet), **Yardım** (sürüm, hakkında ve sorumluluk reddi, lisanslar, kullanıcı sözleşmesi).
- **Üst bölüm:** önizleme için cihaz boyutu, **Önizleme**, **Değişkenleri yenile**, **Eşleştirme** ve **Kaydet**.
- **Hiyerarşi:** tüm profillerin ve sayfalarının tek bir ağaçta gösterildiği panel. Ekleyebilir, yeniden adlandırabilir, çoğaltabilir, silebilir ve klasörlerde gruplayabilirsiniz. Bkz. [Profiller ve sayfalar](/tr/guide/profiles).
- **Araç Kutusu:** widget türleri. Birine tıklayarak açık sayfaya ekleyin.
- **Sayfa sekmeleri ve tuval:** açtığınız her sayfa bir sekme olur. Tuval ızgaradır: widget'ları sürükleyin, köşeden boyutlandırın, sağ tıklayarak menü açın.
- **Özellikler:** seçtiğiniz şeyin özellikleri: bir widget, sayfa ya da profil.
- **Hata Listesi:** önem derecesine göre süzülebilen sorun listesi. Pencerenin alt kenarında durur, tıklayınca yukarı açılır. Şimdilik bir şey bildirmedikçe "Sorun yok" gösterir.
- **Durum çubuğu:** sunucu sürümü, bağlı cihazlar, eklenti durumu ve aksiyon hataları.

## Panelleri düzenleme

- **Paneli taşıma:** sekmesini sürükleyin. Başka bir panelin yanına bırakırsanız alan bölünür, sekme çubuğuna bırakırsanız sekme olarak üst üste gelir.
- **Otomatik gizleme:** panelin sekmesindeki iğne simgesine tıklayınca panel pencerenin kenarına iner. Adına tıklayınca açılır, iğneye yeniden basınca yerine oturur. Hata Listesi böyle başlar.
- **Kapatma ve geri açma:** paneli sekmesindeki **✕** ile kapatın, **Görünüm** menüsünden geri getirin. Menü Hiyerarşi, Araç Kutusu, Özellikler ve Hata Listesi'ni listeler, açık olanların yanında onay işareti vardır.
- **Yerleşimler:** **Görünüm → Yerleşimler → Kaydet** mevcut düzeni bir adla saklar. Kayıtlı bir yerleşimi aynı menüden seçerek geçiş yapın, **Sil** birini kaldırır, **Varsayılan** özgün düzeni geri getirir.

![Yerleşimler alt menüsüyle Görünüm menüsü](/img/editor-layouts.png)

![Alt kenardan açılmış Hata Listesi](/img/editor-errorlist.png)

## Izgarada çalışma

Bir widget seçtiğinizde sağdaki Özellikler paneli görünümünü, içeriğini ve aksiyonlarını gösterir:

![Seçili bir widget için denetçi](/img/editor-inspector.png)

- **Araç Kutusu**'ndan bir tür seçin. Widget ilk boş hücreye yerleşir ("Sayfada boş hücre kalmadı" ızgaranın dolu olduğu anlamına gelir. Sayfa özelliklerinden büyütün).
- Taşımak için sürükleyin, boyutunu değiştirmek için **Boyutlandır** tutamacını sürükleyin. Widget'lar ızgaraya oturur ve üst üste binemez.
- Birden fazla widget seçerek bunları **çoğaltabilir**, başka bir sayfaya veya profile **taşıyabilir ya da kopyalayabilir** veya silebilirsiniz.
- Sayfayı seçin (Hiyerarşi'de adına tıklayın) ve özelliklerini düzenleyin: **İsim**, **Grid** (sütun ve satır), **Boşluk (gap)**, **Kenar boşluğu (padding)**, **Hizalama** (başa, ortaya, sona).

## Önizleme

Cihaz boyutu listesi (telefon veya tablet, dikey veya yatay, serbest ya da özel) önizleme tuvalinin görünümünü değiştirir. Böylece bir sayfanın telefonunuza nasıl sığdığını kontrol edebilirsiniz. Kendi cihaz boyutlarınızı [Tercihler](/tr/guide/preferences)'ten ekleyin. Önizlemede canlı değerler gösterilir. **Değişkenleri yenile**, kullanılabilir değişkenlerin listesini yeniden yükler, örneğin bir eklenti kurduktan sonra.

## Kaydetme

**Kaydet**'e tıklamadan hiçbir şey cihazlarınıza ulaşmaz. Kaydetme, bağlı cihazlara **yalnızca değişenleri** gönderir, bu yüzden deck yanıp sönmez ya da sıfırlanmaz. Kaydedilmemiş değişiklikler varken çıkarsanız düzenleyici önce sorar.

## Klavye kısayolları

| Tuşlar | Ne yapar |
|---|---|
| **Ctrl+S** | Kaydet |
| **Ctrl+C** / **Ctrl+V** | Hiyerarşi'de seçili sayfayı, profili ya da klasörü kopyalar ve yapıştırır |
| **F2** | Hiyerarşi'de seçili satırı yeniden adlandırır |

Bir metin alanına yazarken Ctrl+C ve Ctrl+V her zamanki gibi metni kopyalar ve yapıştırır.

## Dil ve tema

Düzenleyici Türkçe (varsayılan) ve İngilizce olarak, koyu veya açık temayla kullanılabilir: [Tercihler](/tr/guide/preferences).
