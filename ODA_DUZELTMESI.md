# Aynı aktif odaya katılma düzeltmesi

## Kullanım

1. Çalışan uygulamayı durdurun. ZIP'i ayrı bir klasöre çıkarın.
2. Asp.NetCore10.0_MeetSync_Project.slnx dosyasını Visual Studio ile açın.
3. Web projesinin mevcut bağlantı ayarlarını kendi SQL Server ortamınıza göre kullanın.
4. Web projesini başlangıç projesi seçip çalıştırın. İki tarayıcı da aynı uygulama adresine bağlansın.
5. Yunus hesabıyla Yazılım_Toplantısı odasına girin. Diğer tarayıcıda Ahmet hesabıyla aynı oda adını girin.

Beklenen: İki kullanıcı aynı aktif oda kaydını ve katılımcı listesini kullanır; ikinci kullanıcı için yeni oda eklenmez. Başta/sonda boşluklar ve Türkçe büyük/küçük harfler eşleşir.

Migration veya Update-Database gerekmez. Mevcut tablolar ve kayıtlar değiştirilmedi.
Geçmişte aynı isimle oluşturulmuş aktif kayıtlar varsa uygulama en eski aktif kaydı seçer; eski kayıtları silmez.
Aktif kayıt yoksa yeni oda oluşturulur. Moderatör toplantıyı bitirince ilgili kaydın IsActive alanı false olur.
Sadece tarayıcının kapanması veya son kişinin çıkması, veritabanındaki odayı pasifleştirmez.

## Neler değişti?

- RoomService: bütün aktif oda aramaları aynı tam eşitlik kuralını kullanır. SQL LIKE kaldırıldı; alt çizgi artık joker karakter değildir. Türkçe karşılaştırma için açık collation kullanılır.
- Oluşturma: transaction içindeki SQL Server sp_getapplock kilidi, aynı anda çalışan isteklerin önce kontrol edip sonra iki ayrı oda eklemesini önler. Bu koruma uygulamanın RoomService üzerinden yaptığı oluşturmalara uygulanır; elle SQL INSERT işlemlerine uygulanmaz.
- Dashboard: AJAX isteği JSON yanıt ister ve sunucunun döndürdüğü gerçek oda adresini kullanır.
- Odayı oluşturan kullanıcının kimliği oturumdan alınır; mevcut odaya katılan kişi oda sahibini değiştirmez.
- Ortak RoomName yardımcı sınıfı: sunucu, SignalR ve katılımcı deposunda tutarlı ad işleme. Tekrarlanan URL decode kaldırıldı.
- Meeting görünümü: JavaScript değerleri JSON ile güvenli biçimde yazılır; güncel JavaScript için dosya sürümü URL'ye eklenir.
- webrtc.js: kullanılan ancak tanımlanmamış normalizeRoomKey fonksiyonu eklendi.
- Hub: katılım sırasında yeni oda oluşturmaz; mevcut aktif odayı kontrol eder. Oda kilidi ve toplantıyı bitirme veritabanıyla uyumludur.

## Doğrulama

- .NET 10 derleme: başarılı, 0 hata. Projede önceden bulunan iki NU1901 paket uyarısı devam ediyor.
- JavaScript sözdizimi kontrolü: başarılı.
- 7 regresyon kontrolü: başarılı. Türkçe harfler, boşluklar, alt çizgi, tekrar URL decode edilmemesi, ilk kişinin moderatörlüğü ve iki farklı bağlantının aynı katılımcı listesine girmesi doğrulandı.
- SQL entegrasyon testi çalıştırılmaya çalışıldı ancak bu çalışma ortamında LocalDB bağlantısı açılamadı. Canlı veritabanı eşzamanlılığı ve iki tarayıcıyla uçtan uca ses/görüntü testi doğrulanmadı.

Eklenen testleri çözüm klasöründe çalıştırmak için:

```powershell
dotnet run --project MeetSync.RegressionTests
dotnet run --project MeetSync.RegressionTests -- --sql
```

SQL testi varsayılan olarak (localdb)\MSSQLLocalDB kullanır. Başka bir SQL Server için:

```powershell
$env:MEETSYNC_TEST_SERVER = 'SUNUCU_ADINIZ'
dotnet run --project MeetSync.RegressionTests -- --sql
```

SQL testi Windows kimlik doğrulamasıyla sadece benzersiz isimli bir test veritabanı oluşturur ve sonunda kaldırır. MeetSyncDB'ye bağlanmaz. Test hesabının veritabanı oluşturma yetkisi gerekir.
SQL senaryoları: aynı isimle farklı kullanıcı, alt çizginin tam eşleşmesi, 12 eşzamanlı istek, pasif odadan sonra yeni oda ve eski mükerrer kayıtların tutarlı seçimi.
