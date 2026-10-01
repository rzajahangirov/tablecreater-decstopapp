Note: layihede movcud olan seylere toxunma elaveler ele (login ve security cehetden dediyim deyisiklikler isdisnadir ) eger verdiyim tasklarda normal olan seyleri deyisdirecek onu mene denen sonra ele


🛠️ Layihənin Memarlıq və Task Planı
Faza 1: Bulud İnfrastrukturu və Mikro-Backend-in Qurulması
Məqsəd: C# proqramından bütün həssas məlumatları (SMTP, Statik Email və Master Key) ayırıb təhlükəsiz buludda saxlamaq.
•	[ ] Task 1.1: Mikro-Backend Platformasının Seçilməsi və Qurulması
o	Supabase Edge Functions, Firebase Functions və ya kiçik bir .NET Web API mühitini yarat.
o	Mühit dəyişənlərini (Environment Variables - .env) sazla: SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS.
•	[ ] Task 1.2: Server tərəfində Statik İstifadəçi Məlumatlarının Saxlanması
o	Bulud bazasında yalnız 1 sətirlik məlumat saxla:
	OwnerEmail: Sahibin gizli statik emaili (Məsələn: muhasib_sahib@gmail.com).
	MasterKey: Sistem ilk qurulanda yaranan və lokal bazanın DEK açarını xilas edə biləcək 256-bitlik şifrəli ana açar.
•	[ ] Task 1.3: POST /api/auth/request-otp Endoint-inin yazılması
o	C# proqramından bu endpointə sorğu gələndə, server daxilində təsadüfi 6 rəqəmli OTP kod generasiya et.
o	Bu OTP kodunu və onun bitmə vaxtını (3 dəqiqə) bulud bazasında müvəqqəti yaddaşa yaz.
o	Masaüstü proqramdan gələn hər hansı email parametrinə baxma. Server öz .env faylındakı OwnerEmail-ə SMTP vasitəsilə OTP-ni göndərsin.
•	[ ] Task 1.4: POST /api/auth/verify-otp Endpoint-inin yazılması
o	C# tərəfindən göndərilən OTP-ni yoxla.
o	Əgər OTP düzgündürsə, cavab olaraq yalnız bir dəfə istifadə edilə bilən təhlükəsizlik tokeni ilə birlikdə Master Key-i C# tətbiqinə geri qaytar.
Faza 2: C# Masaüstü Proqramı və Lokal Baza (SQLCipher)
Məqsəd: Məlumatların lokal kompüterdə hərbi səviyyədə şifrəli saxlanması və offline işləmə mexanizmi.
•	[ ] Task 2.1: NuGet Paketlərinin və Kitabxanaların Qoşulması
o	Microsoft.Data.Sqlite və SQLitePCLRaw.bundle.sqlcipher paketlərini layihəyə əlavə et.
o	Şifrə xəşləmə üçün Konscious.Security.Cryptography.Argon2 (tövsiyə olunan) və ya System.Security.Cryptography (PBKDF2 üçün) əlavə et.
•	[ ] Task 2.2: Açar Törəmə (Key Derivation Function - KDF) Sisteminin Yazılması
o	İstifadəçinin daxil etdiyi parolu birbaşa bazaya vermək olmaz. Formula:
$$\text{DerivedKey} = \text{Argon2id}(\text{Password}, \text{Salt}, \text{Iterations}=4, \text{Memory}=65536)$$
o	Bu funksiyanı RAM-da işləyəcək şəkildə optimize et.
•	[ ] Task 2.3: Zərf Şifrələməsi (Envelope Encryption) Mexanizminin Qurulması
o	Lokalda security.json faylı yarat. Faylın strukturu belə olacaq:
	Salt: KDF üçün istifadə olunan təsadüfi baytlar.
	EncryptedDEK: Əsl baza açarının (DEK) istifadəçinin parolu ilə şifrələnmiş halı.
o	İstifadəçi proqrama girəndə parolundan DerivedKey yaranır ➡️ Bu key EncryptedDEK-i açır ➡️ Alınan DEK ilə SQLCipher bazası qoşulur.
Faza 3: Giriş Paneli (Login UI) və İdentifikasiya
Məqsəd: İstifadəçinin qarşılaşacağı ilk ekranın minimum məlumat sorğusu ilə tam təhlükəsiz işləməsi.
•	[ ] Task 3.1: Minimalist Login Ekranının Dizaynı
o	Username inputunu vizual olaraq ləğv et və ya statik göstər (Sistem tək adamlıqdır).
o	Yalnız Password input sahəsi və "Daxil ol" düyməsi yerləşdir.
o	"Parolumu Unutdum" keçid düyməsini əlavə et.
•	[ ] Task 3.2: Giriş Doğrulanması (Offline Auth)
o	İstifadəçi parolu yazanda Task 2.3-dəki mexanizmi işə sal.
o	Əgər daxil edilən parol EncryptedDEK-i düzgün aça bilirsə və SQLCipher bazası PRAGMA key = 'DEK' əmri ilə uğurla açılırsa, istifadəçini daxil et.
o	Əgər baza file is not a database və ya encrypted xətası verirsə, ekranda "Parol yanlışdır!" xəbərdarlığını göstər.
Faza 4: Secure OTP və Parol Sıfırlama Mexanizmi (Xilasetmə)
Məqsəd: Parol unudulanda, məlumatları itirmədən bazanı yeni şifrə ilə yenidən kilidləmək.
•	[ ] Task 4.1: "Parolumu Unutdum" UI Axınının Qurulması
o	İstifadəçi keçidə basanda C# tətbiqi Faza 1.3-dəki API-ya (/request-otp) sorğu atsın.
o	Ekranda "Statik email ünvanınıza OTP kod göndərildi" mesajı ilə 6 xanalı OTP input pəncərəsi aç.
•	[ ] Task 4.2: OTP Təsdiqi və Master Key-in Alınması
o	İstifadəçi kodu yazandan sonra API-ya (/verify-otp) göndər.
o	Uğurlu cavab gəldikdə, serverdən gələn Master Key-i müvəqqəti olaraq proqramın RAM-ında (Memory) saxla (Heç vaxt diskə yazma!).
•	[ ] Task 4.3: Bazanın Yeni Parolla Yenidən Şifrələnməsi (Re-encryption)
o	RAM-dakı Master Key ilə lokal bazanı açan əsl Ana Açarı (DEK) deşifrə et (Xilas et).
o	İstifadəçiyə ekranda "Yeni Parol təyin edin" pəncərəsi göstər.
o	İstifadəçi məsələn ilkin123 yazdıqda:
1.	Yeni bir Salt generasiya et.
2.	ilkin123 formulundan yeni DerivedKey yarat.
3.	Əlimizdəki o dəyişməyən əsl DEK-i bu yeni key ilə şifrələyib security.json daxilindəki EncryptedDEK sahəsinə yaz.
o	Köhnə məlumatlar itmədi, çünki bazanın daxili şifrəsi (DEK) dəyişmədi, sadəcə onun üstündəki qoruyucu qabı yeni parolla əvəzlədik!
Faza 5: Google Drive API ilə Ehtiyat Nüsxələmə (Backup & Versioning)
Məqsəd: Liquibase məntiqində olduğu kimi, məlumatları buludda versiyalar halında ehtiyatda saxlamaq.
•	[ ] Task 5.1: Google Cloud Console-da Şəxsi Sahənin Qurulması
o	Google Cloud-da proyekt yarat, Google Drive API-nı aktivləşdir və Service Account (Servis Hesabı) yaradaraq .json açar faylını yüklə.
•	[ ] Task 5.2: C# Proqramına Google Drive Kitabxanasının İnteqrasiyası
o	Google.Apis.Drive.v3 NuGet paketini layihəyə yüklə.
o	Service Account JSON-u vasitəsilə Drive-a arxa planda qoşulma metodunu yaz.
•	[ ] Task 5.3: Şifrəli Faylın Versiyalarla Yüklənməsi Mexanizmi
o	Proqram hər bağ崭ananda və ya istifadəçi "Yenilə/Ehtiyat Nüsxə" düyməsinə basanda:
	Lokalda tam qapalı olan şifrəli .db faylını götür.
	Faylın adını zaman damğası ilə formatla: muhasibat_backup_2026_06_07_1812.db
	Google Drive API-ın Files.Create metodu ilə bu faylı təyin olunmuş gizli qovluğa yüklə.
o	Nəticə: Kompüter yansa, yeni tətbiqdə "Bərpa et" düyməsinə basılacaq, proqram Drive-dakı ən son zaman damğalı .db faylını endirəcək və Faza 4-dəki OTP axını ilə bazanı açacaq.
Faza 6: Kodun Mühafizəsi (Obfuscation) və Yekunlaşdırma
Məqsəd: Hakerin .NET kodunu dekompilyasiya edib API endpointlərini asanlıqla tapmasının qarşısını almaq.
•	[ ] Task 6.1: API Linklərinin və Sorğuların Şifrələnməsi
o	Bulud serverinin URL ünvanını kodun içinə düz mətn (plain text) kimi yazma. Onu sadə bir XOR və ya Base64 alqoritmi ilə sındırılaraq oxunmayacaq hala sal və işə düşəndə RAM-da həll et.
•	[ ] Task 6.2: Obfuscator Alətinin Tətbiqi
o	Layihəni yekun .exe faylı halına gətirməzdən əvvəl Obfuscar (açıq mənbəli) və ya Dotfuscator alətlərindən istifadə et.
o	Bu alət sənin C# kodundakı funksiya adlarını, sinifləri (classes) qarışdıraraq A(), B(), C() halına salacaq. Haker dotPeek ilə kodu açsa belə, heç bir məntiqi başa düşə bilməyəcək.
📌 Konfliktlərin Həlli Haqqında Memarlıq Qeydi:
Sənin ilkin planındakı "Kodu yeniləmək üçün düymə olacaq və proqram daxilindən emaili dəyişmək olacaq" istəyini təhlükəsizlik xatirinə dəyişdik. Əgər haker proqramı dekompilyasiya edib o "Emaili dəyiş" funksiyasının API-ya atdığı sorğunu manipulyasiya etsəydi, sistemi sındıra bilərdi. Yeni arxitekturada email Masaüstü proqram üçün tamamilə görünməz və toxunulmazdır. Hər şey buluddakı o kiçik serverin nəzarətindədir.

### 🔴 2. Dashboard / Ana Səhifə (Ümumi Baxış)

**Hazırki problem:** Tətbiq açıldıqda birbaşa Müştərilər siyahısı göstərilir. Biznesin ümumi vəziyyətini göstərən heç bir panel yoxdur.

**Təklif — "Dashboard" səhifəsi yaratmaq:**

```
┌─────────────────────────────────────────────────────────┐
│  📊 Dashboard                                           │
├──────────┬──────────┬──────────┬──────────┬─────────────┤
│  Ümumi   │  Aktiv   │  Bu Ay   │  Bu Ay   │  Ümumi      │
│  Müştəri │  Müştəri │  Xərc    │  Gəlir   │  Balans     │
│  12      │  10      │  $45,200 │  $52,800 │  +$7,600    │
├──────────┴──────────┴──────────┴──────────┴─────────────┤
│                                                         │
│  📋 Son 10 Tranzaksiya (cədvəl)                        │
│  ┌─────┬────────┬──────────┬─────────┬────────────────┐ │
│  │ #   │ Tarix  │ Müştəri  │ Məhsul  │ Məbləğ (USD)   │ │
│  ├─────┼────────┼──────────┼─────────┼────────────────┤ │
│  │ ...                                               │ │
│  └───────────────────────────────────────────────────┘ │
│                                                         │
│  ⚠️ Mənfi gəlirdə / zərərdə olan müştərilər (cədvəl)    │
│  ┌──────────┬───────────────────┬──────────────────┐    │
│  │ Müştəri  │ Gəlir/Zərər (USD) │ Son tranzaksiya  │    │
│  └──────────┴───────────────────┴──────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

**Göstəriciləri (KPI kartları):**
- Ümumi müştəri sayı
- Aktiv müştəri sayı
- Bu aykı ümumi xərc (USD)
- Bu aykı ümumi ödəniş (USD)
- Xalis gəlir / zərər
- Bu aykı tranzaksiya sayı

**Əlavə hissələr:**
- Son 10 tranzaksiya cədvəli (sürətli baxış)
- Zərərdə olan müştərilərin siyahısı (HistoricalRemainingDebtUsd < 0)

> [!TIP]
> Dashboard sidebar-da ilk element olaraq əlavə edilsin və tətbiq açıldıqda ilk bu göstərilsin.

---

### 🔴 3. Silmə Təsdiq Dialogları (Confirmation Dialogs)
 Bu dediyin zaten vardi tesdiq isdeyir silmezden once bunu eleme ona gore elemeyin lazim deyl. 

---

Sistemdən Çıxarılanlar (Olmamalı Olanlar)
❌ Task 4.2 — Defolt Məzənnə (Exchange Rate): Layihə daxilində bu funksionallığa ehtiyac olmadığı üçün tamamilə çıxarıldı.

❌ Task 4.4 — İstifadəçi İdarəetməsi (Admin/User rolları): Proqram yalnız bir nəfər (tək istifadəçi) üçün nəzərdə tutulduğundan, çoxlu istifadəçi yaradılması, rolların paylanması və aktiv/deaktiv edilməsi məntiqləri sistemə əlavə edilməyəcək.

🛠️ Yenidən İşlənmiş Səhifə 4: Settings (Parametrlər) — Detallı Task Siyahısı
Faza 4.1: Hesab və Təhlükəsizlik Tənzimləmələri
Məqsəd: İstifadəçinin ad/soyadını redaktə etməsi, lokal bazanın şifrəsini yeniləməsi və buluddakı bərpa emailini təhlükəsiz şəkildə dəyişməsi.

[ ] Task 4.1.1: İstifadəçi Məlumatlarının Redaktəsi (Lokal/Profil)

İstifadəçinin Ad və Soyad məlumatlarını dəyişə biləcəyi sadə input sahələri yarat və bu məlumatları SQLCipher bazasında Settings cədvəlində saxla.

[ ] Task 4.1.2: Şifrə Dəyişdirmə Mexanizmi (Lokal Re-encryption)

UI-da 3 input sahəsi yarat: Cari Şifrə, Yeni Şifrə, Yeni Şifrə (Təsdiq).

Arxa plan məntiqi:

İstifadəçinin yazdığı Cari Şifrə-ni götür və Argon2/PBKDF2-dən keçirərək açar yarat. Bu açarla security.json daxilindəki EncryptedDEK-i açmağa çalış. Şifrə səhvdirsə, prosesi dayandır və xəta ver.

Yeni Şifrə təsdiqlə uyğun gəlirsə, yeni bir Salt (duz) generasiya et.

Yeni şifrədən yeni DerivedKey törət.

Əlimizdəki əsl verilənlər bazası açarını (DEK) bu yeni açarla şifrələ və security.json faylına yenidən yaz.

[ ] Task 4.1.3: Təhlükəsiz Bərpa Emailinin Dəyişdirilməsi (Cloud-Safe Update)

UI daxilində "Bərpa Emailini Yenilə" bölməsi qur (Yeni Email inputu və "Dəyiş" düyməsi).

Təhlükəsizlik axını:

İstifadəçi yeni emaili yazıb düyməyə basanda, C# proqramı Cloud API-ya POST /api/settings/request-email-change sorğusu göndərir.

Cloud API hakerlərin sızma ehtimalına qarşı Köhnə (hazırda sistemdə qeydiyyatda olan) Emailə təsdiq OTP-si göndərir.

C# proqramında "Köhnə emailinizə göndərilən OTP-ni daxil edin" pəncərəsi açılır.

OTP düzgün daxil edildikdə, Cloud API bulud bazasındakı/mühitindəki statik OwnerEmail dəyişənini yeni email ilə əvəzləyir. (Beləcə masaüstü proqram heç bir zaman daxilində email şifrəsi və ya ünvanı daşımır).

Faza 4.3: Google Drive İnteqrasiyalı Avtomatik və Manual Yedəkləmə
Məqsəd: Lokal SQLCipher tablecreater.db faylının itməməsi üçün buludda professional versiya nəzarəti sisteminin qurulması.

[ ] Task 4.3.1: "İndi Yedəklə" (Manual Backup) Düyməsinin Qurulması

Settings səhifəsinə bir "İndi Yedəklə" düyməsi yerləşdir.

Düyməyə basıldıqda proqram internet əlaqəsini yoxlasın. İnternet varsa, Google.Apis.Drive.v3 kitabxanası vasitəsilə lokal tablecreater.db faylını zaman damğası ilə (tablecreater_backup_2026_06_07.db) birbaşa Google Drive-a yükləsin.

[ ] Task 4.3.2: Avtomatik Smart-Yedəkləmə Mexanizmi (Event-Driven on Close)

Proqram daxilində qlobal bir isDataChanged (bool) dəyişəni yarat. Proqramda hər hansı mühasibatlıq tranzaksiyası (əlavə etmə, silmə, düzəliş) baş verəndə bu dəyişəni true et.

Proqram Bağlanarkən (Application_Exit Event):

Əgər isDataChanged == true olarsa, proqram dərhal bağlanmasın, arxa planda interneti yoxlasın.

İnternet varsa: Səssizcə (background thread) şifrəli .db faylını Google Drive-a yeni versiya olaraq yükləsin və proqramı bağlasın.

İnternet yoxdursa: Lokalda, gizli bir tənzimləmə faylında pendingBackup = true qeydini aparsın və bağlansın.

[ ] Task 4.3.3: Proqram Açılarkən Sinxronizasiya Yoxlanışı (Startup Sync Check)

Proqram Hər Açılarkən:

İnternet bağlantısını və lokalda pendingBackup == true olub-olmadığını yoxlasın.

Əgər keçən dəfə internet olmadığı üçün yüklənə bilməyən yedək varsa və indi internet gəlibsə, istifadəçini gözlətmədən arxa planda həmin şifrəli faylı dərhal Google Drive-a yükləsin və pendingBackup = false etsin.

[ ] Task 4.3.4: "Bərpa Et" (Restore from Cloud) Mexanizmi

Settings səhifəsinə "Buluddan Bərpa Et" düyməsi qoy.

Düyməyə basıldıqda, Google Drive API vasitəsilə həmin qovluqdakı son 5 yedək faylının siyahısını (tarixləri ilə birlikdə) bir Grid və ya ComboBox-da istifadəçiyə göstər.

İstifadəçi istədiyi tarixi seçib "Bərpa et" dedikdə:

Hazırki lokal tablecreater.db faylının adını tablecreater_OLD.db olaraq dəyiş (hər ehtimala qarşı təhlükəsizlik nüsxəsi).

Drive-dan seçilən faylı endir və adını tablecreater.db et.

İstifadəçidən həmin tarixdə istifadə etdiyi parolu istəyərək bazanı yenidən dövriyyəyə burax.

[ ] Task 4.3.5: Son Yedəkləmə Tarixinin UI-da Göstərilməsi

Hər uğurlu bulud yedəklənməsindən sonra, uğurlu əməliyyatın tarix və saatını lokal Settings cədvəlinə yaz.

Settings səhifəsi hər açılanda ekranda "Son uğurlu bulud yedəklənməsi: 07.06.2026 - 18:30" mətnini dinamik olaraq göstər.

### 🟡 5. Müştəri Siyahısı Səhifəsinə Əlavələr

**Hazırki problem:** Müştəri siyahısında yalnız Ad və Telefon göstərilir. Müştərinin maliyyə vəziyyəti haqqında heç bir məlumat yoxdur.

**Təklif:**

#### 5.1 — Müştəri Kartında Əlavə Göstəricilər
Hər müştəri sırasında aşağıdakılar da göstərilsin:
- **Tranzaksiya sayı** — (Transaction count)
- **Ümumi gəlir/balans** — (HistoricalRemainingDebtUsd cəmi)
- **Status badge** — Aktiv (yaşıl) / Deaktiv (boz)

#### 5.2 — Müştəri Filtirləri
- Status-a görə filter: "Hamısı / Aktiv / Deaktiv"
- Sıralama: Ad (A-Z), Ad (Z-A), Gəlir (çox→az), Son tranzaksiya

#### 5.3 — ToggleStatus Düzəlişi
- `ToggleStatusAsync()` metodu hazırda işləmir — `CustomerReadResponse`-da `Type` sahəsi yoxdur
- `CustomerReadResponse`-a `CustomerType Type` əlavə edilməli (və ya ayrı sorğu ilə)
- Toggle düyməsi düzgün işləsin

> [!WARNING]
> `ToggleStatusAsync()` metodu hazırda boş funksionallıq — heç bir şey etmir, sadəcə siyahını yenidən yükləyir.

---
### 🟡 6. Müştəri Detalları Səhifəsinin Təkmilləşdirilməsi

**Hazırki problem:** Müştəri detalları səhifəsində tranzaksiya siyahısı və ümumi maliyyə kartları var. Amma bəzi funksionallıqlar çatışmır.

**Təklif:**

#### 6.1 — Excel İxracı Düyməsi
- CustomerDetailsPage-dən birbaşa Excel ixracı (hal-hazırda yalnız Hesabatlar səhifəsindən mümkündür)
- "📥 Excel-ə ixrac et" düyməsi

#### 6.2 — Tranzaksiya Filtrləmə
- Tarix aralığına görə filter
- Status-a görə: "Hamısı / Tamamlanmış / Davam edən"
- Axtarış: Məhsul adı və ya qəbul edən firma üzrə

#### 6.3 — Tranzaksiyanı "Tamamlandı" İşarələmə
- Hər tranzaksiya sırasında "✅ Tamamla" düyməsi
- `IsCompleted = true` olaraq yeniləsin
- Tamamlanmış tranzaksiyalar fərqli rəngdə (yaşıl fon) göstərilsin

#### 6.4 — Müştəri Məlumatlarını Redaktə Etmə
_bu zaten var ona gore bu hisseye toxunma._

---

### 🟡 7. Hesabatlar Səhifəsinin Təkmilləşdirilməsi

**Hazırki problem:** Hesabatlar səhifəsi əsas funksionallığa malikdir, amma professional bir mühasibatlıq sistemi üçün kifayət deyil.

**Təklif:**

#### 7.1 — Əvvəlcədən Hazır Tarix Filtirləri
- "Bu gün", "Bu həftə", "Bu ay", "Bu il", "Keçən ay", "Keçən il" düymələri
- Bir kliklə tarix aralığını seçmə

#### 7.2 — Bütün Müştərilər Üzrə Ümumi Hesabat
- Müştəri seçmədən ümumi hesabat generasiya etmə
- Bütün müştərilərin xülasəsi: hər müştəri üçün Xərc, Gəlir, Balans bir cədvəldə

#### 7.3 — Çap Funksiyası
-bu ehdiyac deyl bunu eleme

---
### 🟡 8. Excel İxracının (TransactionService.ExportCustomerTransactions) Tamamlanması

**Hazırki problem:** `TransactionService.ExportCustomerTransactions()` metodu **stub** olaraq qalıb — heç bir ixrac etmir, sadəcə validasiya edir.

```csharp
// TODO: Step 6 — Implement ClosedXML export here.
await Task.CompletedTask;  // ← Bu sətir heç bir iş görmür
```

**Təklif:**
- Bu metod `ExcelService.ExportToExcel()`-i çağırmalıdır (artıq tam implementasiya olunub)
- Və ya bu metod tamamilə silinib, hər yerdə birbaşa `IExcelService.ExportToExcel()` istifadə edilsin

> [!WARNING]
> Hal-hazırda `ReportsViewModel` düzgün olaraq `IExcelService.ExportToExcel()` çağırır. Amma `ITransactionService.ExportCustomerTransactions()` interfeys metodu hələ stubdur — bu ya tamamlanmalı, ya da interfeysdən silinməlidir.

--- eger bu task hazirki funksiyalara tesir elemirse sil edirse silme yeni hazrki funksiyanalliqa bir tesiri olmasin

### 🟢 9. Giriş Axını və İstifadəçi Təcrübəsi (UX) Yaxşılaşdırmaları

#### 9.1 — Yüklənmə Animasiyaları
- Əməliyyatlar zamanı (Save, Delete, Load) spinner/progress göstəricisi
- `IsLoading` property-si artıq ViewModellardə var — UI-da ProgressRing bağlanmalıdır

#### 9.2 — Toast / Snackbar Bildirişləri
- `MessageBox` əvəzinə modern toast bildirişləri
- Uğurlu əməliyyat: yaşıl toast (3 san sonra itsin)
- Xəta: qırmızı toast (istifadəçi bağlayana qədər qalsın)

#### 9.3 — Sidebar Aktiv Vəziyyət
- Hal-hazırda sidebar düymələrinin hansının seçili olduğu vizual olaraq göstərilmir
- Aktiv səhifənin düyməsi fərqli arxa fon rəngində olmalıdır

#### 9.4 — Boş Vəziyyət Ekranları (Empty States)
- Müştəri yoxdursa: "Hələ heç bir müştəri əlavə edilməyib. İlk müştərinizi yaradın →"
- Tranzaksiya yoxdursa: "Bu müştərinin hələ tranzaksiyası yoxdur. Yeni tranzaksiya əlavə edin →"
- Sadəcə boş cədvəl yerinə təşviqedici mesajlar

---

### 🟢 10. CustomColumn (Dinamik Sütunlar) İdarəetmə UI-ı

**Hazırki problem:** `CustomColumn` və `CustomFieldValue` entity-ləri verilənlər bazasında tam konfiqurasiya olunub, amma **heç bir UI-da istifadə olunmur**. Bu, spesifikasiyanın "Dynamic Schema" hissəsidir.

**Təklif:**
- Settings-dən dinamik sütunları idarə etmə: əlavə etmə, silmə, sıralama
- TransactionEntryPage-də dinamik sütunların formda göstərilməsi
- CustomerDetailsPage-də dinamik sütun dəyərlərinin cədvəldə göstərilməsi
- Formula əsaslı sütunlar üçün dəstək (Calculated tipli sütunlar)

> [!NOTE]
> Entity-lər hazırdır: `CustomColumn` (Name, InputType, Formula, DataType, SortOrder) və `CustomFieldValue` (TransactionId, CustomColumnId, Value). Sadəcə Service və UI yazılmalıdır.

---

### 🟢 11. Keyboard Shortcuts (Klaviatura Qısa Yolları)

Professional proqramlarda istifadəçilər klaviaturadan istifadə etməyi gözləyirlər:

| Qısa Yol | Əməliyyat |
|-----------|-----------|
| `Ctrl+N` | Yeni tranzaksiya |
| `Ctrl+S` | Saxla (formda) |
| `Ctrl+F` | Axtarış sahəsinə fokus |
| `Ctrl+E` | Excel ixracı |
| `Ctrl+P` | Çap |
| `F5` | Yenilə (siyahını reload) |
| `Escape` | Dialogu bağla / Geri |
| `Delete` | Seçilmiş elementi sil (+ təsdiq dialogu) |

---

### 🟢 12. Verilənlər Bazası Avtomatik Yedəkləmə

**Təklif:**
- Tətbiq bağlanarkən avtomatik olaraq `tablecreater_backup_{yyyy-MM-dd}.db` faylı yaratmaq
- Son 5 yedəkləməni saxlamaq, köhnələri silmək
- İstifadəçi tərəfindən manual yedəkləmə (Ctrl+B və ya Settings-dən)

--- bu barede danismisiq onsuz ona gore bu edilmeyecek


