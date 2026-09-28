import type { MessageKey } from "./en";

export const tr: Record<MessageKey, string> = {
  "device.name": "Tarayıcı",
  "status.connected": "Bağlı",
  "status.connecting": "Bağlanıyor…",
  "status.offline": "Çevrimdışı",
  "connect.lost": "Bağlantı koptu, yeniden deneniyor…",
  "connect.pairHint": 'Bu tarayıcı henüz eşleşmemiş. Bilgisayarındaki Macro Grid düzenleyicisinde "Eşleştirme"ye tıkla ve orada gösterilen 6 haneli PIN\'i buraya gir.',
  "connect.pair": "Eşleştir",
  "connect.pairBlockedSeconds": "Çok fazla yanlış PIN girdin. {seconds} saniye sonra tekrar dene.",
  "connect.reason.wrong_pin": "Yanlış PIN. Bilgisayarındaki Macro Grid düzenleyicisinin Eşleştirme penceresinde gösterilen PIN'i gir.",
  "connect.reason.pairing_closed": 'Eşleştirme kapalı. Bilgisayarındaki Macro Grid düzenleyicisinde "Eşleştirme"yi aç ve orada gösterilen PIN\'i gir.',
  "connect.reason.not_paired": 'Eşleştirme gerekiyor. Bilgisayarındaki Macro Grid düzenleyicisinde "Eşleştirme"yi aç ve orada gösterilen PIN\'i gir.',
};
