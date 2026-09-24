# Uygulamaya göre otomatik geçiş

Bir cihaz, bilgisayarınızdaki **etkin pencereyi** izleyebilir: medya oynatıcınızı öne getirdiğinizde telefon oynatıcının profiline geçer; oynatıcıyı kapattığınızda telefon geri döner.

## Kurulum

1. **Bir profile kural ekleyin.** Profili seçin, **Otomatik geçiş kuralları** bölümünü açın, ardından **Çalışan uygulamadan seç…** deyin ya da `Player.exe` gibi bir uygulama adı yazın ve **Ekle**'ye tıklayın.
2. **Cihaz için açın.** **Eşleştirme** bölümünde o cihaz için **Aktif pencereyi takip et** seçeneğini açın. Bu, cihaz başına isteğe bağlıdır.
3. **Kaydedin.**

## Nasıl davranır

- Önde olan pencere belirler. Kuralı olan bir pencere öne gelince cihaz o profile geçer.
- Kuralı **olmayan** bir pencereye geçtiğinizde cihaz, en son elle seçtiğiniz profile (ya da varsayılana) döner.
- Birkaç profil aynı pencereyle eşleşirse ilki geçerli olur.
- Bir programın görünür penceresi kalmadığında pencere kapanmış sayılır. Sistem tepsisine gizlenmiş bir oynatıcı kapalı sayılır.
- Elle seçtiğiniz profil temel olur: "yayın profili, oynatıcıyı aç, oynatıcıyı kapat, yayın profiline dön" akışı doğal biçimde çalışır.

## Kilit

Telefondaki profil çekmecesinde bir **kilit** anahtarı vardır. Açıkken otomatik geçiş duraklar, ancak profilleri yine elle seçebilirsiniz. Kapattığınızda geçerli durum bir kez uygulanır.

## Kapsam dışı

Tam ekran ya da oyun algılama, düzenli ifadeyle (regex) eşleştirme ve sayfa bazında geçiş. Tarayıcı deck'i buna katılmaz.
