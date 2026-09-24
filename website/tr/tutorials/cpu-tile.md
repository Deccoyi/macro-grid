# Canlı CPU kutucuğu

CPU yükünü gösteren ve yoğunlaşınca önce sarıya, sonra yanıp sönen kırmızıya dönen bir kutucuk. [Değişkenleri](/tr/guide/variables) ve [dinamik kuralları](/tr/guide/dynamic) öğretir.

1. Bir **Buton** ekleyin (basılabilir olması gerekmiyorsa bir **Etiket**).
2. **Metin**'ini `CPU {system.cpu|0}%` yapın. Değişkeni eklemek için **+ Değişken ekle**'ye tıklayın ve `system.cpu`'yu seçin. `|0` tam sayı gösterir.
3. Koyu bir **Arka plan** ve açık bir **Yazı** rengi belirleyin.
4. **Arka plan**'ın yanındaki **şimşek** simgesine tıklayın. **Mantık kur** penceresinde:
   - **Eğer** `system.cpu` **büyükse** `80` **İse** kırmızı.
   - **Koşul ekle**'ye tıklayıp **Yoksa eğer** `system.cpu` **büyükse** `50` **İse** sarı yapın.
   - **Yoksa** kısmını değiştirmeyin, koyu arka planınız görünsün. **Uygula**.
5. **Animasyon**'un yanındaki şimşeğe tıklayın: **Eğer** `system.cpu` **büyükse** `80` **İse** **Yanıp sönme**. **Uygula**.
6. İsteğe bağlı: **Basınca** olayına **Uygulama aç** aksiyonunu bağlayın ve Görev Yöneticisi'ni (`taskmgr.exe`) seçin.
7. **Kaydet**.

Bilgisayarda ağır bir şey başlatın ve kutucuğun telefonunuzda değişmesini izleyin.

## Fikirler

- `RAM {system.ram|0}% ({system.ram.used|0.0}/{system.ram.total|0} GB)`
- Saat etiketi olarak `{system.time|HH:mm}`.
