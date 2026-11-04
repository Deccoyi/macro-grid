# Voicemod: API anahtarınızı alın ve Macro Grid'i bağlayın

Sesinizi değiştirin, ses efektleri arasında geçin ve sessize alın; hepsi telefonunuzdan. **Voice Changer Control** eklentisi, PC'nizdeki Voicemod uygulamasıyla Control API üzerinden konuşur. Voicemod bu API'yi yalnızca anahtar isteyenlere verir, bu yüzden **kendi anahtarınıza ihtiyacınız var**. Macro Grid içine anahtar koyamaz.

**Gerekenler:** Windows, Voicemod uygulaması, Voicemod Control API anahtarı (ücretsiz) ve Macro Grid'de Voice Changer Control eklentisi.

::: info Bağlantı yok
Voice Changer Control resmî olmayan bir topluluk eklentisidir. Macro Grid ve eklenti, Voicemod ile bağlantılı değildir; Voicemod tarafından onaylanmamış veya desteklenmemiştir. Voicemod, sahibinin ticari markasıdır.
:::

## 1. Voicemod'u kurun ve başlatın {#install-voicemod}

Voicemod'un Control API'si **yalnızca Windows'ta** çalışır. Voicemod uygulamasını kurun, başlatın ve açık bırakın. Uygulama kapalıyken eklenti bağlanamaz.

## 2. API anahtarınızı isteyin {#request-key}

1. [Voicemod Control API istek formunu](https://voicemod.typeform.com/to/Zh5ZHRED) açın.
2. Formu kendi bilgilerinizle doldurun. Ne yapacağınız sorulursa, Macro Grid ile Voicemod'u telefonunuzdan kontrol etmek istediğinizi, kişisel kullanım için olduğunu yazın.
3. [Voicemod Control API Şartları'nı](https://docs.google.com/document/d/11KFv4jpeV4ALN5omHVSPepiSTwYNXsoXSAxNhkto_XY/edit) okuyun ve kabul edin. Anahtarı kullanımınız Macro Grid'in değil bu şartların kapsamındadır.
4. Formu gönderin. Erişim ücretsizdir.

::: warning Anahtar kişiseldir
Anahtarınızı paylaşmayın, internette yayımlamayın, başkasına verdiğiniz bir profile koymayın. Sızarsa Voicemod'dan yeni anahtar isteyin.
:::

## 3. E-postayı bekleyin {#wait-for-email}

Voicemod anahtarı e-postayla gönderir. Anında gelmez. Birkaç gün sonra hâlâ gelmediyse spam klasörüne bakın.

## 4. Anahtarı Macro Grid'e girin {#enter-key}

1. Henüz kurmadıysanız Voice Changer Control eklentisini kurun (**Eklentiler → Keşfet**).
2. Editörde **Eklentiler**'i açın ve Voice Changer Control'ün yanındaki dişliye tıklayın.
3. **Etkin**'i açın.
4. Anahtarı API anahtarı alanına yapıştırın ve **Kaydet**'e basın.

## 5. Bağlantıyı kontrol edin {#check-connection}

Önce Voicemod'u başlatın. Kaydettikten birkaç saniye sonra eklenti durumu bağlı olduğunu gösterir. Editörün durum çubuğunda görmek için Eklentiler penceresinde Voice Changer Control için **Durum çubuğu**'nu açın. Bağlanmazsa sonraki bölüme bakın.

## Sorun giderme {#troubleshooting}

**Bağlı değil, Voicemod çalışmıyor.** Voicemod uygulamasını başlatın. Eklenti denemeye devam eder, uygulama açılınca bağlanır.

**Yanlış anahtar (401 hatası).** Voicemod anahtarı reddetti. Anahtarı e-postadan yeniden yapıştırın; fazladan boşluk veya satır sonu kopyalanmadığından emin olun. Yine olmuyorsa anahtarınızı Voicemod'a sorun.

**Port engelli.** Voicemod uygulaması yalnızca bu PC'de, önce 59129 portunda, sonra bazı yedek portlarda dinler. Yerel bağlantıları engelleyen bir güvenlik duvarı veya güvenlik programı buna engel olabilir. Macro Grid ve Voicemod için yerel bağlantılara izin verin, ikisini de yeniden başlatın.

## Gizlilik {#privacy}

- Eklenti yalnızca bu PC'deki Voicemod uygulamasıyla konuşur. Anahtarınızı veya başka bir şeyi internetteki bir sunucuya göndermez.
- Anahtarınız, geçerli Windows kullanıcısı için şifrelenmiş olarak saklanır. Aynı PC'deki başka bir Windows kullanıcısı onu okuyamaz.
