# 🔍 TableCreater — Senior-Level Biznes Analizi

## Layihənin Mövcud Vəziyyəti

| Sahə | Status | Qeyd |
|------|--------|------|
| **Arxitektura (MVVM)** | ✅ Yaxşı | CommunityToolkit.Mvvm, DI, ayrı Service/ViewModel/View |
| **Entity-lər** | ✅ Tam | Customer, Transaction, User, Role, CustomColumn, CustomFieldValue |
| **Hesablama Mühərriki** | ✅ İşləyir | Section 5.1 — canlı hesablama TransactionEntryViewModel-də |
| **Excel İxracı** | ✅ İşləyir | ClosedXML ilə professional formatda |
| **Auth Sistemi** | ⚠️ Qismən | BCrypt var, amma Login UI yoxdur, auto-login hardcoded |
| **Settings Səhifəsi** | ⚠️ Statik | Yalnız "Haqqında" kartı — heç bir ayar yoxdur |
| **Dashboard** | ❌ Yoxdur | Ana səhifə olaraq ümumi biznes göstəriciləri yoxdur |
| **İstifadəçi İdarəetməsi** | ❌ Yoxdur | Admin paneli, istifadəçi CRUD yoxdur |
| **Data Backup** | ❌ Yoxdur | DB yedəkləmə/bərpa mexanizmi yoxdur |
| **Təsdiq Dialogları** | ❌ Yoxdur | Silmə əməliyyatlarında təsdiq soruşulmur |

---

## 🚀 PROFESSİONAL SEVİYYƏYƏ ÇATDIRMAQ ÜÇÜN TƏKLİFLƏR

Hər təklif **prioritet sırası** ilə verilir: 🔴 Kritik → 🟡 Vacib → 🟢 Bonus

---

### 🔴 1. Login / Giriş Ekranı

**Hazırki problem:** Tətbiq `App.xaml.cs`-də `admin@tablecreater.com / admin123` ilə avtomatik giriş edir. Heç bir login ekranı göstərilmir. Bu, çox istifadəçili mühitdə ciddi təhlükəsizlik problemidir.

**Təklif:**
- Tətbiq açıldıqda `LoginWindow` göstərilsin (Email + Şifrə sahələri)
- Giriş uğurlu olduqda `MainWindow` açılsın, `LoginWindow` bağlansın
- Sidebar-ın altında "Çıxış" düyməsi `MainWindow`-u bağlayıb yenidən `LoginWindow` açsın
- "Məni xatırla" checkbox-u ilə son giriş edən istifadəçinin email-i yadda saxlansın
- Uğursuz giriş cəhdlərində açıq error mesajları göstərilsin
- Şifrə sahəsində "Göstər/Gizlə" toggle düyməsi

> [!IMPORTANT]
> Bu olmadan tətbiq real istifadə üçün yararsızdır — hər kəs admin olaraq daxil olur.

---

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

**Hazırki problem:** Müştəri və ya tranzaksiya silindikdə heç bir təsdiq soruşulmur. `DeleteCustomer` CASCADE ilə bütün tranzaksiyaları silir — bu, böyük data itkisinə səbəb ola bilər.

**Təklif:**
- Hər silmə əməliyyatından əvvəl `ContentDialog` və ya `MessageBox` göstərilsin
- Müştəri silmədə: **"Bu müştərini silmək istədiyinizə əminsinizmi? Müştəriyə aid X tranzaksiya da silinəcək!"**
- Tranzaksiya silmədə: **"Bu tranzaksiyanı silmək istədiyinizə əminsinizmi?"**
- "Sil" düyməsi qırmızı rəngdə olsun

> [!CAUTION]
> Bu olmadan istifadəçi təsadüfən bir düyməyə basaraq illərlə yığılmış məlumatı itirə bilər.

---

### 🟡 4. Settings (Parametrlər) Səhifəsinin Genişləndirilməsi

**Hazırki problem:** Settings səhifəsi yalnız statik "Haqqında" məlumatı göstərir. Heç bir real ayar yoxdur.

**Təklif — Aşağıdakı bölmələr əlavə edilsin:**

#### 4.1 — Hesab Tənzimləmələri
- Cari istifadəçinin ad/soyadını redaktə etmə
- Şifrə dəyişdirmə (köhnə şifrə + yeni şifrə + təsdiq)

#### 4.2 — Defolt Məzənnə (Exchange Rate)
- Defolt RUB→USD məzənnəsinin saxlanması
- Yeni tranzaksiya yaratarkən bu məzənnə avtomatik doldurulsun
- İstifadəçi istəsə dəyişə bilsin

#### 4.3 — Məlumat Bazası Yedəkləmə (Database Backup)
- "Yedəklə" düyməsi — `tablecreater.db` faylını `SaveFileDialog` ilə istənilən yerə kopyalasın
- "Bərpa Et" düyməsi — `OpenFileDialog` ilə əvvəlki `.db` faylını bərpa etsin
- Son yedəkləmə tarixi göstərilsin

#### 4.4 — İstifadəçi İdarəetməsi (yalnız ADMIN rolu üçün)
- Bütün istifadəçilərin siyahısı
- Yeni istifadəçi yaratma
- İstifadəçini aktiv/deaktiv etmə
- İstifadəçinin rolunu dəyişmə (ADMIN / USER)

> [!NOTE]
> `User` entity-sində `IsActive` sahəsi artıq mövcuddur, istifadə olunmur. `Role` entity-si var amma UI-da heç bir rol idarəetməsi yoxdur.

---

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
- Bu səhifədən müştəri adı və telefonu redaktə etmə imkanı
- Kiçik "✏️ Redaktə" düyməsi

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
- Hesabat nəticəsini birbaşa çap etmə imkanı (Print)
- Çap öncəsi baxış (Print Preview)

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

---

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

---

## 📋 İMPLEMENTASİYA PRİORİTET CƏDVƏLİ

| # | Təklif | Prioritet | Çətinlik | Təsir |
|---|--------|-----------|----------|-------|
| 1 | Login / Giriş Ekranı | 🔴 Kritik | Orta | Təhlükəsizlik |
| 2 | Dashboard / Ana Səhifə | 🔴 Kritik | Orta | Biznes dəyəri |
| 3 | Silmə Təsdiq Dialogları | 🔴 Kritik | Asan | Data təhlükəsizliyi |
| 4 | Settings Səhifəsi (Genişləndirilmiş) | 🟡 Vacib | Orta | İstifadəçi təcrübəsi |
| 5 | Müştəri Siyahısı Əlavələri | 🟡 Vacib | Orta | Biznes dəyəri |
| 6 | Müştəri Detalları Təkmilləşdirmə | 🟡 Vacib | Orta | İstifadəçi təcrübəsi |
| 7 | Hesabatlar Təkmilləşdirmə | 🟡 Vacib | Asan | Biznes dəyəri |
| 8 | Export Stub Düzəlişi | 🟡 Vacib | Asan | Kod keyfiyyəti |
| 9 | UX Yaxşılaşdırmaları | 🟢 Bonus | Orta | Polish |
| 10 | CustomColumn UI | 🟢 Bonus | Çətin | Feature completeness |
| 11 | Keyboard Shortcuts | 🟢 Bonus | Asan | Power users |
| 12 | Avtomatik Backup | 🟢 Bonus | Asan | Data təhlükəsizliyi |

---

## 🐛 MÖVCUD BUGLAR / KOD PROBLEMLERİ

### Bug 1: ToggleStatusAsync işləmir
**Fayl:** [CustomerListViewModel.cs](file:///c:/Users/vcaha/Java Projects/teablecreater-dekstopapp/TableCreater.WPF/ViewModels/Customers/CustomerListViewModel.cs#L211-L232)

```csharp
// Bu metod heç bir status dəyişikliyi etmir — sadəcə yenidən yükləyir
var customer = SelectedCustomer;
await LoadCustomersAsync(); // ← Status dəyişmir!
```

`CustomerReadResponse`-da `Type` sahəsi yoxdur, ona görə cari statusu bilmək mümkün deyil.

### Bug 2: TransactionService.ExportCustomerTransactions stub-dur
**Fayl:** [TransactionService.cs](file:///c:/Users/vcaha/Java Projects/teablecreater-dekstopapp/TableCreater.WPF/Services/TransactionService.cs#L232-L249)

```csharp
// TODO: Step 6 — Implement ClosedXML export here.
await Task.CompletedTask; // ← Heç bir ixrac baş vermir
```

### Bug 3: Logout heç yerə naviqasiya etmir
**Fayl:** [MainViewModel.cs](file:///c:/Users/vcaha/Java Projects/teablecreater-dekstopapp/TableCreater.WPF/ViewModels/MainViewModel.cs#L93-L98)

```csharp
private void Logout()
{
    _authService.Logout();
    // In a full implementation, this would navigate back to the LoginView
    // ← Login ekranı olmadığına görə logout sessiyani silir amma istifadəçi elə tətbiqdə qalır
}
```

### Bug 4: DbContext Scoping problemi
**Fayl:** [App.xaml.cs](file:///c:/Users/vcaha/Java Projects/teablecreater-dekstopapp/TableCreater.WPF/App.xaml.cs#L76-L77)

`AppDbContext` **Scoped** olaraq qeydiyyatdan keçib, amma WPF-də HTTP request yoxdur, yəni scope-un həyat dövrü qeyri-müəyyəndir. `TransactionService` və `CustomerService` də Scoped-dur. Bu, uzun müddətli tətbiqlərdə **stale data** və **tracking conflict** problemlərinə səbəb ola bilər.

**Həll:** Hər əməliyyat üçün `IServiceScopeFactory` istifadə etmək (AuthService-in etdiyi kimi) və ya `DbContextFactory` pattern-inə keçmək.

---

## 📝 NƏTİCƏ

Bu tətbiq **yaxşı əsasa** malikdir — MVVM arxitekturası, DI, ayrı service qatı, BCrypt ilə şifrə hashingi, hesablama mühərriki. Amma **professional / production-ready** səviyyəyə çatdırmaq üçün yuxarıdakı 12 təklifin ən azı ilk 8-i tətbiq edilməlidir.

**Ən böyük çatışmazlıqlar:**
1. **Login ekranının olmaması** — hər kəs avtomatik admin olaraq daxil olur
2. **Dashboard-un olmaması** — biznesin ümumi vəziyyəti bir baxışda görünmür
3. **Təsdiq dialoglarının olmaması** — təsadüfi data itkisi riski
4. **Settings-in boş olması** — istifadəçi heç bir şeyi konfiqurasiya edə bilmir

Bu təkliflərin hansılarını tətbiq etmək istəyirsinizsə, mənə bildirin — hər biri üçün implementation plan hazırlayaram.
