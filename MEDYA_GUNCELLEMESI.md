# Kayıt, oda kilidi ve ekran paylaşımı

Bu paket önceki aynı aktif odaya katılma düzeltmesini de içerir.

## Çalıştırma

Uygulamayı durdurun, yeni ZIP'i ayrı klasöre çıkarın ve çözüm dosyasını açın.
Mevcut SQL Server bağlantı ayarlarınızla web projesini çalıştırın.
Her iki tarayıcıdaki eski toplantı sayfasını kapatıp yeniden katılın.
Sunucu ve tarayıcı dosyaları birlikte güncellenmelidir. Migration gerekmez.

## Kayıt

- Moderatör kayıt düğmesiyle başlatır, tekrar basarak durdurur.
- Katılımcı görüntüleri (paylaşılan ekran dahil) 1280 × 720 toplantı düzeninde kaydedilir.
- Yerel mikrofon ve katılımcılardan alınan sesler birleştirilir. Ekran paylaşımında sistem/sekme sesi alınmaz.
- Video parçaları, SignalR mesaj sınırını aşmayan 12 KiB ham veri parçaları halinde sırayla gönderilir.
- Son video parçası ve tüm yüklemeler tamamlandıktan sonra sunucu kaydı kapatır.
- Başarılı kayıtta “Kaydı indir” bağlantısı görünür. Boş veya başarısız kayıt tamamlandı diye gösterilmez.
- Kamera izni yoksa kayıt işlevi kamera görüntüsüne bağımlı değildir; mevcut toplantı görüntülerini kullanır.
- Normal Ayrıl/Bitir akışı önce kaydın tamamlanmasını bekler. Sekmenin zorla kapatılması, bağlantının kesilmesi veya sunucunun kapanması eksik kayıt bırakabilir; otomatik devam ettirme yapılmaz.
- Dosyalar web projesindeki wwwroot/recordings klasöründe tutulur. Uygulama kullanıcısının bu klasöre yazma izni olmalıdır.

## Oda kilidi

- Yazısız kilit düğmesi diğer araç çubuğu düğmeleriyle aynı boyuttadır. Açık kilit nötr, kapalı kilit amber renktedir; üzerine gelince durum ve sonraki eylem görünür.
- Üst bölümde bütün katılımcılar için açık/kilitli etiketi bulunur.
- Durum, sunucu onayından sonra güncellenir. İşlem sürerken düğme tekrar tıklanamaz.
- Yeni katılımda mevcut kilit durumu sunucudan alınır.

## Ekran paylaşımı

Araç çubuğundaki simgeli çözünürlük düğmesi yukarı açılan kalite panelini gösterir. Seçili seçenek işaretlidir. Paylaşım sırasında da değiştirilebilir:

- 480p: 854 × 480
- 720p: 1280 × 720
- 1080p: 1920 × 1080 (varsayılan)
- 2K / QHD: 2560 × 1440
- 4K / UHD: 3840 × 2160

Hedef kare hızı 30 FPS'dir. Gerçek yakalama çözünürlüğü ekranda gösterilir.
Bu seçimler hedef/üst sınırdır: kaynak ekran/pencere, tarayıcı, cihaz ve bağlantı daha düşük kaliteye yol açabilir.
4K seçimi düşük çözünürlüklü bir kaynağı gerçek 4K ayrıntıya dönüştürmez.
Paylaşım kalitesi toplantı kaydının 720p çözünürlüğünden bağımsızdır.
Paylaşımı iptal etme, tarayıcıdan durdurma, kamerasız paylaşma ve sonradan katılan kişiye ekran gönderme akışları ele alındı.

## Doğrulama

- .NET 10 derlemesi başarılı; yeni C# uyarısı yok. Önceden bulunan iki NU1901 paket uyarısı sürüyor.
- JavaScript sözdizimi denetimleri başarılı.
- Node testleri aktarım sırasını/veri bütünlüğünü, son parçayı, yükleme hatasını, kilit göstergesini, beş kalite profilini, paylaşım iptalini/temizliğini, indirme durumunu ve iki betiğin birlikte yüklenmesini kontrol eder.
- Testlerde tarayıcı medya ve ağ API'leri taklit edilir. Gerçek ekran seçimi, kodlama kalitesi, mikrofon ve SQL Server ile uçtan uca test bu ortamda yapılmadı.

Test komutu:

```powershell
node --test tests/meeting-media.test.cjs
```

Kendi ortamınızda kısa bir toplantı kaydı oluşturup durdurun, çıkan bağlantıdan WebM dosyasını indirin ve ses/görüntüyü kontrol edin. Kilitliyken üçüncü bir tarayıcıdan katılımın reddedildiğini ve kilidi kaldırınca katılımın açıldığını deneyin.

Teknik referanslar: [SignalR yapılandırması](https://learn.microsoft.com/aspnet/core/signalr/configuration), [ekran yakalama kısıtları](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getDisplayMedia).

