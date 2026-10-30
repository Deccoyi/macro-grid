# Profiller ve sayfalar

**Profil**, eksiksiz bir deck'tir. Bir ya da daha fazla **sayfa** içerir; her sayfa bir widget ızgarasıdır. Profilleri farklı durumlar için kullanın: "İş", "Yayın", "Medya".

## Hiyerarşi

**Hiyerarşi** paneli tüm profilleri ve sayfalarını tek bir ağaçta gösterir. Bir profili açıp kapatmak için yanındaki oka tıklayın. Henüz açılmamış bir profil, ilk açışınızda sayfalarını yükler. Bir sayfaya tıklarsanız sekme olarak açılır. Bir profile tıklarsanız ayarları **Özellikler**'de görünür.

![Hiyerarşi: profiller, sayfalar ve klasörler tek ağaçta, klasör menüsüyle](/img/editor-hierarchy-menu.png)

Panelin altındaki düğmelerle **yeni profil**, **sayfa** ve **yeni klasör** eklenir. Bir satıra sağ tıklayınca menüsü açılır (yeniden adlandır, sil, yeni sayfa ya da klasör). Yeniden adlandırmak için satıra çift tıklayın ya da seçip **F2**'ye basın.

## Profiller

Her profil kendi JSON dosyasında saklanır. Hiyerarşi'de bir profil seçtiğinizde **Özellikler**'de **İsim**, **otomatik geçiş kuralları** ve **Profili Sil** görünür. Son profili silemezsiniz.

Bir cihazın hangi profili açtığı:

1. [Eşleştirme](/tr/guide/pairing)'de o cihaza atadığınız profil, yoksa
2. [Tercihler](/tr/guide/preferences)'teki **varsayılan profil**, yoksa
3. ilk profil.

Telefon ayrıca cihazda en son seçtiğiniz profili hatırlar: bağlantı koptuktan ya da yeniden başlattıktan sonra o profili yeniden açar. Düzenleyicide atadığınız bir profil bu seçimin yerini alır.

Profil, bir düğmeyle (**Profil değiştir** eylemi), telefondaki çekmeceden ya da [uygulama kurallarıyla](/tr/guide/auto-switch) otomatik olarak da değiştirilebilir.

## Sayfalar

Bir profilin birçok sayfası olabilir. Telefonda aralarında geçmek için iki parmakla sola ya da sağa kaydırın. **Sayfa değiştir** eylemli bir düğme de ekleyebilirsiniz (bir sayfaya git, sonraki, önceki ya da geri).

Sayfayı ağacın altındaki **Sayfa** düğmesiyle ya da profilin sağ tık menüsünden ekleyin. Sayfa satırındaki simgelerle **çoğaltın** ya da **silin**. Seçili widget'ları başka bir sayfaya ya da profile **taşıyabilir veya kopyalayabilirsiniz**.

## Klasörler

Klasörler uzun listeyi derli toplu tutar. İki türü vardır:

- **Sayfa klasörleri** bir profilin içindedir ve sayfaları (ve başka klasörleri) tutar.
- **Profil klasörleri** ağacın en üstündedir ve profilleri tutar.

**Yeni Klasör** düğmesi ya da sağ tık menüsüyle oluşturun, sayfa gibi yeniden adlandırın. **Klasörü sil**, klasörün içindekilerle birlikte mi silineceğini, yoksa yalnızca klasörün kaldırılıp **içeriğin korunacağını** sorar.

Klasörler yalnızca düzenleyici içindir. Bir cihaz, sayfalar ve profiller klasörde olsa da olmasa da aynısını görür.

## Sürükle-bırak, kopyala ve yapıştır

- **Sürükleyin:** bir sayfayı klasörün üzerine bırakınca içine girer, klasörden dışarı bırakınca geri çıkar. Profiller ve profil klasörleri için de aynısı geçerlidir.
- **Kopyala ve yapıştır:** bir sayfaya, profile ya da klasöre tıklayıp **Ctrl+C**'ye basın, sonra konulacağı yere tıklayıp **Ctrl+V**'ye basın. Kopyalanan klasör içindekilerle birlikte gelir. Klasöre yapıştırırsanız kopya klasörün içine girer. Bir sayfayı başka bir profile yapıştırabilirsiniz, bir sayfayı yeniden kullanmanın en hızlı yolu budur.

## Dışa ve içe aktarma

- **Dosya → Profili Paylaş… (.mgprofile)** geçerli profili tek bir dosyaya kaydeder.
- **Dosya → Paylaşılan Profili İçe Aktar (.mgprofile / .json)…** bir profil yükler. Aynı adlı bir profil varsa **Üzerine yaz** ya da **Adı değiştir** seçeneğini seçersiniz. Eski `.msprofile` dosyaları da içe aktarılır.
- İçe aktarılan profil sahip olmadığınız bir eklenti kullanıyorsa Düzenleyici hangisi olduğunu listeler; eklentiyi kurunca düğmeler çalışır.
- Bu bilgisayarda bulunmayan bir dosyayı kullanan eylemler de listelenir.

Bir deck'i başkasıyla paylaşmanın en kolay yolu budur.

## Yedekle ve geri yükle

**Dosya → Her Şeyi Yedekle…** profillerinizi, profil ağacını, tercihlerinizi, genel değişkenlerinizi, otomasyon kurallarınızı, eşleşmiş cihazlardaki seçimlerinizi, eklenti ayarlarınızı ve dil paketlerinizi tek bir `.mgbackup` dosyasında kaydeder. **Dosya → Geri Yükle…** böyle bir dosyayı açar, içeriğini her öğe için bir onay kutusuyla listeler ve işaretlediğiniz öğeleri geri yükler. Geri yükleme hiçbir şeyi silmez.

Parolalar, cihaz belirteçleri ve eşleşmeler yedeğe dahil değildir. Genel değişkenler yedeğe yalnızca tanımlarıyla girer, geçerli değerleriyle girmez. Geri yükleme sırasında gelen otomasyon kuralları kapalı olarak gelir; açmadan önce kontrol edin.

Macro Grid ayrıca geri yükleme noktaları da tutar: geri yüklemeden, bir profil silinmeden ya da içe aktarma ile üzerine yazılmadan önce ve bir güncellemeden sonra bir kez alınan veri kopyaları. Bunlar veri klasörünüzdeki `restore-points` klasöründe saklanır (bkz. [Dosyalar ve portlar](/tr/reference/files)). Düzenleyici bunları henüz listelemez.
