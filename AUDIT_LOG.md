# AUDIT LOG — NSC-ModManager port ke Winlator (WinNative)

Dokumen ini rangkuman kronologis semua yang sudah dikerjakan di sesi-sesi
sebelumnya, supaya bisa dilanjutkan tanpa perlu mengulang dari nol.

**Target:** WinNative (fork Winlator+Pluvia, Wine di atas Android) —
https://github.com/WinNative-Emu/WinNative

**Struktur repo saat ini:**
```
/
├── .github/workflows/build.yml   <- CI build (GitHub Actions)
├── NSC-ModManager/                <- UI app (WPF, net8.0-windows10.0.19041.0)
└── XFBIN_LIB/                     <- source lama/TIDAK SINKRON, JANGAN dipakai build
                                       (lihat XFBIN_LIB/NOT_USED_FOR_BUILD.md)
```

---

## 0. Konteks awal

Project asli: **NSC-ModManager** (mod manager WPF untuk game Naruto Storm
Connections/Storm 4), dibangun pakai **ModernWpf** (Fluent Design UI), target
awal `net10.0-windows10.0.26100.0` (Windows 11 24H2 SDK).

Tujuan: port UI-nya supaya jalan stabil di **Winlator/WinNative** (Wine di
atas Android), tanpa mengubah tool/data pendukung (`CpkMaker.dll`,
`YACpkTool.exe`, `BinaryReader.cs`, `XfbinParser.cs`, `Model/`, `ParamFiles/`,
`ModdingAPIFiles/`), dan bisa di-build otomatis lewat GitHub Actions.

---

## 1. Analisis awal source & build

- Diketahui solusi (`NSC-ModManager.sln`) terdiri dari 2 proyek: `NSC-ModManager`
  (UI, MVVM: `View/` 3 file XAML, `ViewModel/` 40 file, `Model/` 37 file,
  `Controls/`, `Converter/`) dan `XFBIN_LIB` (library parsing format `.xfbin`,
  awalnya cuma tersedia hasil kompilasinya `.dll`, source belum ada).
- Tool eksternal yang dibundel (tidak disentuh): `CpkMaker.dll`,
  `YACpkTool.exe` (compile mod jadi `.cpk`), `vcredist_x86.exe`.
- Data pendukung (tidak disentuh): `ParamFiles/` (232MB, template param game),
  `ModdingAPIFiles/`.

## 2. Ganti UI ModernWpf → Winlator-friendly

**File baru:** `Resources/Styles/WinlatorStyle.xaml` — pengganti
`ui:ThemeResources`/`ui:XamlControlsResources` (Fluent/Mica, butuh DWM
composition yang sering gagal di Wine). Isinya style flat berbasis
`Border`+`Trigger` biasa (tanpa Storyboard/composisi) untuk: `Window`,
`Button`, `TextBox`, `ComboBox` (full `ControlTemplate` custom), `CheckBox`,
`ToggleSwitchStyle` (custom, pengganti `ui:ToggleSwitch`), `DataGrid`,
`DataGridColumnHeader` (full `ControlTemplate` custom), `TabControl`/`TabItem`,
`ScrollBar` (full `ControlTemplate` custom), `ListBox`, `GroupBox`, `Label`.

**`App.xaml`:** hapus `xmlns:ui`, `ui:ThemeResources`, `ui:XamlControlsResources`
→ merge `WinlatorStyle.xaml` sebagai gantinya.

**`App.xaml.cs`:**
- Hapus `using ModernWpf;`
- `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` di constructor
  (bisa dimatikan via env var `NSC_MM_FORCE_HWRENDER=1`) — GPU translation
  Wine (Turnip/DXVK) sering tidak stabil untuk WPF hardware compositing.
- `TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(Window),
  new FrameworkPropertyMetadata(TextFormattingMode.Display))` — paksa
  pipeline text-rendering lama (GDI-compatible), bukan `Ideal`/DirectWrite.
- **Global exception handler** (`AppDomain.UnhandledException`,
  `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) →
  catat ke `crash_log.txt` di folder app + tampilkan pesan, supaya crash
  tidak lagi "hilang senyap" tanpa jejak seperti sebelumnya.
  - **Dedup**: exception dengan signature sama cuma nongol popup SEKALI;
    kejadian berikutnya cuma dicatat ringkas ke log (mencegah spam popup).
  - **Self-healing khusus font**: kalau exception-nya `UriFormatException`
    dari `CombineUriWithFaceIndex` (bug font, lihat bagian 4), otomatis
    ganti resource `NarutoFont` ke font fallback sistem dan lanjut jalan
    TANPA popup sama sekali (supaya app tidak macet berulang di titik yang
    sama).

**448× `ModernWpf.MessageBox.Show`** di seluruh `ViewModel/*.cs` → diganti
`System.Windows.MessageBox.Show` (signature identik, sapu bersih pakai `sed`).

**`View/TitleView.xaml` (+`.cs`):**
- Hapus `xmlns:ui`, hapus `ui:WindowHelper.UseModernWindowStyle` (custom
  titlebar Fluent — dihapus, pakai window chrome default Windows).
- 2× `ui:ToggleSwitch` → `CheckBox` dengan `Style="{StaticResource
  ToggleSwitchStyle}"`.
- **Dihapus** dead code lama: `<Style TargetType="ToggleButton"
  BasedOn="{StaticResource NoChromeButton}"/>` — bug laten (BasedOn ke style
  `TargetType="Button"`, padahal `Button` bukan base type dari `ToggleButton`,
  keduanya sepupu turunan `ButtonBase`). Ini AMAN dihapus karena app lama
  tidak pernah punya elemen `ToggleButton` sungguhan (`ui:ToggleSwitch`
  ModernWpf bukan turunan `ToggleButton`) — baru "meledak" pas saya bikin
  `ToggleButton` asli di dalam template `ComboBox` custom.

`View/CharacterRosterEditorView.xaml` & `...NS4View.xaml`: cuma hapus
`xmlns:ui` (tidak ada elemen `ui:` lain di situ).

**`.csproj`:** hapus `PackageReference` `ModernWpfUI` & `ModernWpf.MessageBox`.

## 3. Target framework: net10 → net8

Alasan: `net10.0-windows10.0.26100.0` (Windows 11 24H2 SDK) terlalu baru,
lebih berisiko di Wine yang biasanya lag beberapa versi. Diganti:
- `TargetFramework`: `net8.0-windows10.0.19041.0` (.NET 8 LTS + Windows 10
  2004 SDK, kombinasi paling umum & lama teruji di Wine)
- `SupportedOSPlatformVersion`: `10.0.19041.0`
- Workflow CI: `dotnet-version: '8.0.x'`

## 4. XFBIN_LIB: dependency yang berubah-ubah (⚠️ PENTING, baca ini)

Riwayat (supaya tidak bolak-balik lagi):

1. Awalnya csproj punya `<ProjectReference Include="..\XFBIN_LIB\XFBIN_LIB.csproj" />`
   tapi source-nya **tidak ada** di repo asli (cuma hasil build `.dll`-nya).
2. Diganti jadi `<Reference>` langsung ke `.dll` prebuilt (disalin ke
   `NSC-ModManager/Libs/XFBIN_LIB.dll`) — ini **BEKERJA** (API lengkap,
   sudah diverifikasi via `strings` ada `FindChunks`, `FoundChunk`,
   `RepackXfbinData`, `ChangeChunkNameAndPath`).
3. User upload `XFBIN_LIB-main.zip` (source asli, target `net7.0`, murni
   cross-platform, tanpa dependency Windows). Sempat diganti balik ke
   `ProjectReference` ke source ini + target diselaraskan `net8.0`.
4. **GAGAL BUILD** — source itu ternyata **versi lama/tidak sinkron**:
   tidak punya `FindChunks`/`FoundChunk`/`RepackXfbinData`/
   `ChangeChunkNameAndPath` yang dipanggil `NUS3BANKViewModel.cs`.
5. **Dikembalikan ke `.dll` prebuilt** (`Reference` + `Libs\XFBIN_LIB.dll`) —
   ini status SAAT INI dan yang dipakai build.

**STATUS SEKARANG:** `NSC-ModManager.csproj` pakai
`<Reference Include="XFBIN_LIB"><HintPath>Libs\XFBIN_LIB.dll</HintPath></Reference>`.
`NSC-ModManager.sln` cuma 1 proyek (`XFBIN_LIB` TIDAK ada di solution).
Folder `XFBIN_LIB/` (source lama) masih disimpan di repo untuk referensi,
dengan catatan `XFBIN_LIB/NOT_USED_FOR_BUILD.md` yang jelasin situasi ini.

**Kalau nanti dapat source `XFBIN_LIB` yang BENAR** (yang punya 4 method di
atas): ganti isi folder `XFBIN_LIB/`, lalu di `NSC-ModManager.csproj` ganti
`<Reference>` balik ke `<ProjectReference Include="..\XFBIN_LIB\XFBIN_LIB.csproj" />`,
dan tambahkan lagi entri proyeknya di `.sln` (lihat riwayat git/percakapan
kalau perlu contoh GUID-nya).

## 5. Struktur repo & GitHub Actions

- `.github/workflows/build.yml` awalnya sempat ditaruh di dalam folder
  `NSC-ModManager/.github/` — **dipindah ke root repo** (sejajar
  `NSC-ModManager/` & `XFBIN_LIB/`) karena checkout Actions cuma narik 1
  repo, kalau workflow ada di dalam subfolder proyek tapi butuh sibling
  folder di luar itu, bakal gagal. `PROJECT_PATH` di workflow:
  `NSC-ModManager/NSC-ModManager.csproj`.
- ⚠️ **Jebakan zip**: pernah kejadian `.github` ke-exclude gara-gara filter
  `zip -x "*.git*"` (niatnya exclude `.git`, tapi ikut match `.github` juga).
  Kalau bikin ulang zip, JANGAN pakai pattern `*.git*` — pisahkan
  `.git` dan `.github` secara eksplisit kalau perlu exclude keduanya.

## 6. Debugging siklus crash (kronologis, dari log yang di-upload user)

| # | Gejala / Error | Root cause | Fix |
|---|---|---|---|
| 1 | App jalan (~ratusan MB di task manager) → messagebox "No mods found" → crash senyap tanpa jejak, hilang dari task manager | `CheckGitHubNewerVersion()` di `TitleViewModel` constructor manggil GitHub API (Octokit/HttpClient) fire-and-forget, tanpa try-catch. TLS/HTTPS stack (SChannel) Wine rapuh → native crash yang lolos semua try-catch managed | Dibungkus try-catch + timeout 8 detik + bisa di-skip via env var `NSC_MM_SKIP_UPDATE_CHECK=1`. **Juga** ditambah global exception handler di App.xaml.cs (baru pertama kali ada di app ini) |
| 2 | Setelah fix #1, crash tercatat ke `crash_log.txt`: `UriFormatException` di `Microsoft.Windows.Themes.DataGridHeaderBorder` (chrome native tema Aero) | `DataGridColumnHeader` cuma di-restyle warnanya (Setter biasa), tapi `ControlTemplate` bawaan tetap manggil chrome native `DataGridHeaderBorder` yang gagal resolve font URI di Wine | `DataGridColumnHeader` diberi `ControlTemplate` PENUH (Border+ContentPresenter polos). Sekalian preventif: `ComboBox` & `ScrollBar` juga diberi `ControlTemplate` penuh (pola sama, chrome native `ComboBoxChrome`/`ScrollChrome`) |
| 3 | Error sama (`UriFormatException`/font) muncul lagi, kali ini di `DataGridCellsPanel` (bukan header) | Ternyata bukan soal DataGridHeaderBorder spesifik — font custom `NarutoFont` (`Resources/Fonts/#CC2 RocknRoll Latin DB`, relative path) gagal resolve URI-nya di MANA SAJA dipakai | Coba ganti `NarutoFont` ke font sistem **"Arial"** — **ternyata TETAP crash** (petunjuk penting: bukan soal font custom vs sistem) |
| 4 | Error identik lagi walau sudah pakai Arial | Font-enumeration sistem Wine/WinNative sendiri yang bermasalah | Ganti `NarutoFont` ke pack URI **lengkap + nama file**: `pack://application:,,,/Resources/Fonts/Storm4_rus.ttf#CC2 RocknRoll Latin DB` — **ternyata masih salah syntax** |
| 5 | Error identik lagi | Sadar syntax pack URI WPF yang benar: bagian sebelum `#` harus **FOLDER** saja, bukan nama file spesifik (WPF scan semua font di folder itu cari nama family yang cocok) | Fix: `pack://application:,,,/Resources/Fonts/#CC2 RocknRoll Latin DB` (tanpa nama file). **Ditambah** `TextOptions.TextFormattingMode=Display` (Window-level Style + `OverrideMetadata` di kode) untuk hindari pipeline DirectWrite. **Ditambah** self-healing (lihat bagian 2) sebagai jaring pengaman terakhir |
| 6 | `XamlParseException`/`InvalidOperationException`: "Can only base on a Style with target type that is base type 'ToggleButton'" (BUG BARU, dari perbaikan #2's `ComboBox`) | Dead code lama di `TitleView.xaml`: `<Style TargetType="ToggleButton" BasedOn="{StaticResource NoChromeButton}"/>` — `NoChromeButton` targetnya `Button`, bukan basis valid untuk `ToggleButton`. Dulu tidak pernah kena karena app lama tidak punya `ToggleButton` asli; ketabrak begitu `ComboBox` custom saya bikin literal `<ToggleButton>` | Hapus dead code itu di `TitleView.xaml` + tambah `Style="{x:Null}"` eksplisit di `ToggleButton` internal template `ComboBox` (pertahanan lapis 2, kebal style implisit dari scope manapun) |

**Status per crash log TERAKHIR yang saya lihat:** bug #6 sudah hilang dari
log. Font (#3-5) — fix pack URI + TextFormattingMode + self-healing SUDAH
dikirim, **BELUM ada crash_log.txt baru untuk mengonfirmasi apakah ini
benar-benar tuntas**. Ini yang paling perlu ditest duluan di sesi berikutnya.

## 6b. Bug build (bukan runtime crash) setelah audit bagian 7

- `MessageInfoS4ViewModel.cs` ternyata berada di namespace **beda**
  (`NSC_Toolbox.ViewModel` — sisa nama lama project sebelum di-rename jadi
  `NSC-ModManager`, lihat juga catatan `.vs/` cache soal "NSC-Toolbox" di
  bagian awal audit dulu), bukan `NSC_ModManager.ViewModel` tempat
  `DialogHelper` ditaruh. Build gagal: `CS0103: The name 'DialogHelper' does
  not exist in the current context` (2 titik, baris 394 & 677).
  **Fix:** tambah `using NSC_ModManager.ViewModel;` di file itu. File-file
  lain pemakai `DialogHelper` (`TitleViewModel.cs`, `NUS3BANKViewModel.cs`,
  `MessageInfoViewModel.cs`) semuanya sudah senamespace jadi tidak butuh
  `using` tambahan — sudah dicek satu-satu, aman.
- **Pengingat:** kalau nanti nambah pemakai baru untuk `DialogHelper` (atau
  helper shared lain), selalu cek dulu `namespace` di file itu — jangan
  asumsi semua file di folder `ViewModel/` otomatis satu namespace, karena
  jejak rename project lama (`NSC_Toolbox` → `NSC_ModManager`) belum
  konsisten di semua file.

## 6c. Font bug: kesimpulan akhir (⚠️ PENTING kalau lanjut sesi berikutnya)

Setelah **5 percobaan FontFamily berbeda** (relative path asli, hardcode
"Arial", pack URI+nama file, pack URI folder-only sesuai dokumentasi resmi,
lalu self-heal ke daftar font sistem "Segoe UI, Tahoma, Verdana, Arial") —
**SEMUANYA crash identik** (`UriFormatException` di
`MS.Internal.FontCache.Util.CombineUriWithFaceIndex`, dipanggil dari
`MS.Internal.FontFace.PhysicalFontFamily.GetGlyphTypeface`), berulang di
HAMPIR SETIAP render pass (± tiap detik).

**Kesimpulan:** ini BUKAN soal font mana yang diminta (custom vs sistem,
syntax URI benar vs salah) — ini kemungkinan besar **bug di subsistem
resolusi font FISIK WPF sendiri di bawah WinNative/Wine** (kemungkinan
terkait DirectWrite/font-enumeration Wine yang belum lengkap). Tidak ada
lagi kombinasi string `FontFamily` yang masuk akal untuk dicoba — semua
jalur PhysicalFontFamily sama-sama rusak.

**Keputusan strategi (berubah dari sebelumnya):** stop coba nebak nama font
yang "benar". Self-healing di `App.xaml.cs` sekarang **PERMANEN** (bukan
cuma sekali) — exception ini SELALU ditangani diam-diam setiap kali muncul
(swap ke `SystemFonts.MessageFontFamily`, TANPA popup, log cuma 3 kejadian
pertama biar tidak membengkak). Prioritas digeser dari "font terlihat
sempurna" ke **"app tidak boleh macet/crash-loop"**.

**Kalau mau benar-benar menuntaskan (bukan cuma redam gejalanya):** ini
kemungkinan besar perlu diperbaiki dari sisi **WinNative/Wine**, bukan kode
C#. Yang bisa dicoba di luar app ini:
- Cek apakah WinNative punya opsi/patch terkait font (mirip
  `winetricks corefonts` di Wine biasa) untuk container yang dipakai.
- Laporkan ke proyek WinNative-Emu/WinNative sebagai bug report, sertakan
  stack trace `CombineUriWithFaceIndex` di atas — mungkin sudah ada yang
  pernah lapor hal serupa atau ada workaround khusus di level Wine prefix.
- **JANGAN** habiskan waktu lagi coba-coba ganti string `FontFamily` di kode
  app ini — sudah terbukti tidak ada bedanya.

## 6d. Generalisasi self-heal + fix Menu (⚠️ update strategi terbaru)

Setelah user memperbaiki bug font di sisi **WinNative sendiri (lewat registry
editor)** — bagus, tapi crash log berikutnya nunjukin **3 jenis error baru**:
`NullReferenceException` di `ContentPresenter.SelectTemplate`,
`NullReferenceException` di `XamlObjectWriter.LoadTemplateXaml` (saat load
template), dan `NullReferenceException` di
`Microsoft.Windows.Themes.ClassicBorderDecorator.MeasureOverride`. Ditambah
`UriFormatException` font yang trace-nya BEDA dari sebelumnya (tidak lewat
`CombineUriWithFaceIndex` lagi, langsung dari `GlyphTypeface..ctor`) —
artinya jangkar deteksi lama (`Contains("CombineUriWithFaceIndex")`) tidak
menangkap semua variannya.

**Pola yang ketemu:** SEMUA exception baru ini asalnya dari namespace
**internal WPF/.NET** (`System.Windows.*`, `System.Xaml.*`, `MS.Internal.*`,
`Microsoft.Windows.Themes.*`), bukan dari kode app kita
(`NSC_ModManager.*`/`NSC_Toolbox.*`). Ini pola yang sama persis dengan semua
bug font sebelumnya — mesin internal WPF yang rapuh di Wine, tapi kali ini
di titik-titik lain (bukan cuma font).

**Perubahan strategi (di `App.xaml.cs`):** self-heal digeneralisasi.
`IsFrameworkInternalFailure(ex)` mengecek `ex.TargetSite.DeclaringType.Namespace`
— kalau exception asalnya dari namespace internal WPF di atas, **otomatis
diredam diam-diam** (log cuma 5 kejadian pertama TOTAL - bukan per jenis -
lalu diam, tidak ada popup sama sekali). Kalau exception asalnya dari kode
app kita sendiri (`NSC_ModManager`/`NSC_Toolbox`), tetap lewat jalur lama
(dedup + popup sekali) karena itu KEMUNGKINAN bug asli yang perlu
diperbaiki, bukan cuma gejala environment.

**Fix akar penyebab (bukan cuma redam gejala) untuk `ClassicBorderDecorator`:**
ditemukan `<Menu>` dipakai di `TitleView.xaml` baris ~130 - `MenuItem`
default template (khususnya submenu popup) memang klasik pakai
`ClassicBorderDecorator`/`SystemDropShadowChrome` di baliknya. Diberi
`ControlTemplate` PENUH untuk `Menu` & `MenuItem` (pola sama seperti
`DataGridColumnHeader`/`ComboBox`/`ScrollBar` sebelumnya) di
`WinlatorStyle.xaml` - handle `Role` (`TopLevelHeader`/`TopLevelItem`/
`SubmenuHeader`/`SubmenuItem`) lewat `Trigger`, submenu render via `Popup`
custom, tanpa native chrome sama sekali.

Untuk `ContentPresenter.SelectTemplate` dan `XamlObjectWriter` NRE: stack
trace-nya generik (cuma internal Grid/Border/ContentPresenter measure
chain), TIDAK ada nama class app yang terlihat di trace, jadi tidak bisa
ditunjuk elemen XAML spesifik mana yang memicu. Untuk sekarang cukup
diredam lewat generalisasi self-heal di atas.

**⚠️ Kalau lanjut sesi berikutnya:** kalau crash log baru masih nunjuk ke
`ContentPresenter.SelectTemplate`/`XamlObjectWriter` DAN ternyata polanya
konsisten muncul di context yang sama tiap kali (misal selalu pas buka tab
tertentu), coba minta user reproduce sambil catat "lagi ngapain pas itu
muncul" - itu petunjuk paling kuat buat nunjuk elemen XAML spesifiknya,
karena stack trace WPF internal-nya sendiri tidak bisa dipakai buat itu.

## 6e. Crash native ACCESS_VIOLATION saat "Compile & Launch" (tanpa crash_log.txt!)

Beda dari semua bug sebelumnya: kali ini **tidak ada `crash_log.txt`** sama
sekali, karena ini **crash native (`EXCEPTION_ACCESS_VIOLATION`, code
`c0000005`)**, bukan exception .NET terkelola — handler
`AppDomain.UnhandledException`/`DispatcherUnhandledException` kita CUMA bisa
nangkep exception .NET, sama sekali tidak bisa nangkep native memory-access
crash kayak gini. User kirim log dari WinNative sendiri (`wine_wfm_*.txt` +
`box64_wfm_*.txt`) sebagai gantinya — ini pertama kalinya kita analisis dari
log level Wine, bukan `crash_log.txt` app.

**Cara baca log Wine kalau ada kasus serupa lagi:** cari
`EXCEPTION_ACCESS_VIOLATION exception (code=c0000005)` — itu titik crash
sungguhan. Cari juga thread ID yang sama (`XXXX:` di awal tiap baris) di
baris-baris SEBELUMNYA buat lihat modul/DLL apa yang lagi dimuat/dipakai
tepat sebelum crash - itu petunjuk utama, karena Wine TIDAK ngasih stack
trace .NET yang bisa dibaca (beda dari `crash_log.txt` kita).

**Temuan:** thread yang crash (background thread, dari
`bw_CompileModProcess` / `BackgroundWorker`) sempat memuat
`System.IO.Compression.dll` dan mencoba memuat
`System.IO.Compression.Native` — **GAGAL** (`status=c0000135` /
`STATUS_INVALID_IMAGE_FORMAT`). ~37 detik kemudian (kerja lain di
background), proses crash.

**Diperiksa dulu (supaya tidak salah tuduh):** sempat dicurigai
`CriCpkMaker.CpkMaker` (assembly x86 pihak ketiga, closed-source, dipanggil
langsung/in-process oleh `Properties/Program.cs`'s `YaCpkTool` class) - tapi
ternyata **TIDAK dipakai** oleh alur compile yang sebenarnya di
`TitleViewModel.cs`. Alur compile pakai `RepackHelper.RunRepackProcess`/
`RunExtractProcess`, yang **sudah benar** spawn `YACpkTool.exe` sebagai
**proses terpisah** (`Process.Start` + `WaitForExit`) - jadi SUDAH terisolasi
dengan benar, bukan sumber crash ini. `YaCpkTool`/`Program.cs`'s `CpkMaker`
in-process TIDAK dipanggil di manapun dalam alur compile - kemungkinan
sisa/dead code dari refactor sebelumnya.

**Kesimpulan & fix:** `System.IO.Compression.ZipFile.ExtractToDirectory`
(3 titik: 2× `TitleViewModel.cs` baris ~7805 & ~7849, 1×
`View/TitleView.xaml.cs` baris ~209) butuh native shim
`System.IO.Compression.Native.dll` yang **terbukti gagal load** di log Wine.
Kalau CLR mencoba manggil function pointer dari native module yang gagal
dimuat/corrupt, itu bisa jadi `ACCESS_VIOLATION` alih-alih exception .NET
yang bisa ditangkap - match persis sama gejalanya.

**Fix:** ketiga titik itu diganti ke `RepackHelper.ExtractZipSafe()` (method
baru), pakai **SharpZipLib** (`ICSharpCode.SharpZipLib.Zip.FastZip`) -
sudah ada di `PackageReference` (versi 1.4.2) tapi sebelumnya TIDAK
dipakai di manapun. SharpZipLib itu 100% pure-managed, TIDAK butuh native
shim sama sekali, jadi seharusnya kebal dari masalah loading native module
kayak gini.

**⚠️ Kalau masih crash setelah fix ini:** karena TIDAK ada `crash_log.txt`
buat kasus native crash begini, **minta log Wine/box64 lagi dari WinNative**
(seperti yang dikirim kali ini) - itu satu-satunya cara diagnosis untuk
native crash. Cari lagi `ACCESS_VIOLATION`, lihat modul apa yang dimuat
tepat sebelum itu di thread yang sama. Kandidat lain yang belum diperiksa
kalau ternyata bukan ini: `CpkMaker`/`YaCpkTool` class di `Properties/Program.cs`
(walau sepertinya dead code, worth di-grep ulang kalau-kalau ada
pemanggilan yang terlewat), atau native DLL lain yang di-P/Invoke.

## 6f. Macet (hang) tanpa crash setelah fix 6e

Fix ZIP di bagian 6e **berhasil** - log Wine berikutnya TIDAK ada
`ACCESS_VIOLATION` lagi. Tapi user lapor app jadi **macet total** (log Wine
berhenti nambah baris, memori di task manager malah turun pelan-pelan -
tanda proses idle/blocked, bukan crash) saat "Compile & Launch", sampai
harus di-force-close manual.

**Analisis:** pola "macet tanpa error apapun" = ciri khas **deadlock**, beda
dari crash native (bagian 6e) maupun exception .NET (bagian 6a-6d). Dicurigai
`RepackHelper.RunRepackProcess`/`RunExtractProcess` yang manggil
`YACpkTool.exe` lewat `Process.Start()` dengan **`UseShellExecute = true`**
+ `process.WaitForExit()` **tanpa timeout**. Kalau child process gagal
ke-launch dengan benar lewat shell (`explorer.exe`) di Wine/WinNative -
skenario yang cukup umum karena integrasi shell Wine kadang tidak stabil -
`WaitForExit()` nunggu **selamanya**, seluruh app (termasuk UI thread kalau
BackgroundWorker-nya di-`Wait` secara sinkron dari situ) ikut beku.

Bonus temuan: dari dokumentasi .NET, `CreateNoWindow` **"has no effect if
UseShellExecute is true"** - jadi setting `CreateNoWindow = true` yang
sudah ada sebelumnya sebenarnya SIA-SIA selama ini karena dipasangkan
dengan `UseShellExecute = true`.

**Fix (`ViewModel/TitleViewModel.cs`, class `RepackHelper`):**
- Refactor `RunRepackProcess`/`RunExtractProcess` jadi pakai helper baru
  `RunHiddenProcess()` bersama.
- `UseShellExecute` diganti ke **`false`** - proses di-launch langsung
  (`CreateProcess`-style), TIDAK lewat `explorer.exe`/shell sama sekali.
  Ini juga sekaligus benerin `CreateNoWindow` yang sebelumnya percuma.
- `WaitForExit()` diberi **timeout 3 menit** (`ProcessTimeoutMs`). Kalau
  timeout, proses dipaksa `Kill()` dan lempar `TimeoutException` yang jelas,
  BUKAN nunggu selamanya lagi. Jadi kalaupun akar masalahnya bukan
  `UseShellExecute` (belum 100% pasti tanpa hasil test lagi), app sekarang
  TIDAK BISA macet permanen lagi - paling parah gagal dengan pesan jelas
  setelah 3 menit.
- Fallback PowerShell `Unblock-File` di `RemoveZoneIdentifier` juga dikasih
  timeout (5 detik) dengan alasan sama - sebelumnya juga `WaitForExit()`
  tanpa timeout, dan ini dipanggil di AWAL tiap proses YACpkTool jadi kalau
  ini yang macet, dampaknya sama persis.

**⚠️ Kalau masih macet setelah fix ini:** karena sekarang ada timeout 3
menit, app HARUSNYA tidak lagi beku selamanya - tunggu sampai muncul pesan
error `TimeoutException` (atau minta user tunggu penuh 3 menit kalau
belum coba). Kalau pesan itu muncul, berarti dugaan `UseShellExecute`
benar tapi belum menuntaskan akar masalah childprocess-nya sendiri kenapa
macet - perlu digali lagi kenapa `YACpkTool.exe` sendiri macet di
lingkungan ini (kemungkinan masalah serupa dengan `CpkMaker`/native x86
lain, atau `YACpkTool.exe` juga pakai `System.IO.Compression` versi lama
yang punya masalah sama seperti di 6e - source `YACpkTool.exe` sendiri
tidak ada di repo ini, cuma binary).

## 6g. Crash lagi (native, wow64) - fix .bat wrapper + pisah Compile/Launch

Fix 6f (timeout + `UseShellExecute=false` di `RepackHelper`) belum
menuntaskan - user masih dapat crash saat "Compile & Launch". Tapi log Wine
kali ini kasih clue BARU yang lebih spesifik: backtrace crash-nya nunjuk
LANGSUNG ke **`wow64cpu.dll`**/**`wow64.dll`** - lapisan terjemahan
32-bit↔64-bit Windows ITU SENDIRI (bukan kode app, bukan `System.IO.Compression`
lagi). Ini classic tanda proses baru (`CreateProcess`) gagal di-spawn dengan
benar lewat WOW64 di lingkungan emulasi berlapis (box64 + wow64) kayak
WinNative.

**Ditemukan titik crash yang SEBENARNYA (belum pernah disentuh sebelumnya):**
di akhir `bw_CompileModProcess_NSC`/`_NS4`, SETELAH CPK selesai di-repack,
ada **auto-launch game** (`Process.Start()` ke `NSUNSC.exe`/`NSUNS4.exe`,
`UseShellExecute = true`, TANPA timeout) - persis pola yang sama dengan
`RunRepackProcess` sebelum diperbaiki di 6f, tapi titik ini belum pernah
disentuh karena bukan bagian dari `RepackHelper`, ini kode terpisah inline
di compile flow.

**User punya 2 usulan, keduanya diimplementasikan:**

1. **Panggil exe lewat file `.bat` + `cmd.exe`, bukan `Process.Start()`
   langsung.** Alasannya masuk akal secara teknis: `cmd.exe` adalah salah
   satu binary paling banyak & lama diuji di Wine (dipakai puluhan tahun
   oleh countless installer/launcher legacy), jadi jalur `CreateProcess`
   lewat `cmd.exe` kemungkinan lebih stabil dibanding P/Invoke
   `Process.Start()` .NET langsung di emulasi berlapis.
   **Diimplementasikan:** class baru `ProcessLauncher` (di
   `TitleViewModel.cs`, namespace `NSC_ModManager.ViewModel`) dengan 2
   method:
   - `RunAndWait(exePath, args, timeoutMs)` - tulis `.bat` sementara ke
     `%TEMP%`, jalankan lewat `cmd.exe /c batfile.bat`
     (`UseShellExecute=false`), tunggu dengan timeout, exit code
     dipropagasi lewat `exit /b %ERRORLEVEL%` di dalam `.bat`. Dipakai buat
     `RepackHelper.RunHiddenProcess` (YACpkTool.exe repack/extract).
   - `RunDetached(exePath)` - `.bat`-nya pakai `start "" "exe"` (fire-and-forget,
     tidak nunggu game keluar), cmd.exe wrapper-nya sendiri yang ditunggu
     (timeout pendek 30 detik, cuma buat mastiin cmd.exe-nya sendiri tidak
     macet). Dipakai buat launch game.

2. **Pisahkan Compile dari Launch** (juga alasan user: mungkin "Compile &
   Launch" sebagai satu operasi berat gabungan lebih rawan crash). Bagian
   auto-launch di akhir `bw_CompileModProcess_NSC`/`_NS4` **dihapus total**.
   Ditambah:
   - `LaunchGameCommand` (`RelayCommand` baru) - baca `StormVersion` buat
     nentuin `NSUNSC.exe`/`NSUNS4.exe` dan folder root yang sesuai, validasi
     folder/exe ada, lalu `ProcessLauncher.RunDetached(exePath)`.
   - Tombol baru **"Launch"** di `TitleView.xaml`, sebelahan sama tombol
     Compile (Compile `ColumnSpan` dikecilin dari 3 ke 2 buat kasih ruang).
     ⚠️ Teks tombolnya **hardcoded "Launch"** (bukan `DynamicResource`
     lokalisasi seperti tombol lain) - belum ditambahkan ke key lokalisasi
     `m_modmanager_XXX` di 80+ file bahasa, jadi tidak ikut berubah kalau
     user ganti bahasa app. Kalau mau rapi, perlu nambah key baru di
     `Resources/Localization/lang.xaml` + semua file bahasa terkait -
     di luar scope sesi ini, cukup dicatat dulu.

**⚠️ Kalau masih crash setelah fix ini:** ini sudah percobaan ke-3 buat
compile/launch flow (6e: ZIP, 6f: timeout+UseShellExecute, 6g: .bat wrapper
+ pisah command). Kalau MASIH crash di titik process-spawn yang sama,
kemungkinan besar ini bukan lagi sesuatu yang bisa diperbaiki dari pola
"cara manggil Process.Start" - mungkin perlu dicoba: (a) jalankan
NSC-ModManager.exe itu sendiri BUKAN sebagai x86 tapi coba build x64 kalau
memungkinkan (redesain besar, semua native dependency x86 - CpkMaker,
YACpkTool kemungkinan perlu diganti juga), atau (b) laporkan ke WinNative
sebagai bug report spesifik soal wow64 process-spawn dengan log yang sudah
dikumpulkan sejauh ini.

## 7. Audit tambahan (belum tentu ada di crash log, ditemukan lewat code review)

- **7× `CommonOpenFileDialog`** (folder picker gaya Vista, `Microsoft.WindowsAPICodePack.Dialogs`,
  berbasis COM `IFileDialog`) tersebar di 4 file: `TitleViewModel.cs` (4×),
  `MessageInfoS4ViewModel.cs` (2×), `NUS3BANKViewModel.cs` (2×),
  `MessageInfoViewModel.cs` (2×). COM shell interface ini historisnya rewel
  di Wine. **Fix:** dibuat `ViewModel/DialogHelper.cs` (class shared baru,
  method `TrySelectFolder(title, out path)`) yang coba `CommonOpenFileDialog`
  dulu, kalau exception → fallback ke `System.Windows.Forms.FolderBrowserDialog`
  (dialog folder klasik, jauh lebih tua & lebih luas didukung Wine). Semua
  7 titik sudah dialihkan ke helper ini. **Perlu** `<UseWindowsForms>true</UseWindowsForms>`
  di `.csproj` (sudah ditambahkan) supaya `System.Windows.Forms` bisa dipakai.
- **`DropShadowEffect`/`BlurEffect`** (30+ titik, terutama di
  `CharacterRosterEditorView.xaml` & `...NS4View.xaml`) — **BELUM disentuh**,
  prioritas rendah karena WPF Effect punya fallback software-rendering
  sendiri (harusnya tetap jalan meski mungkin lebih berat sedikit di bawah
  `RenderMode.SoftwareOnly` yang sudah kita paksa).
- **`Menu`/`MenuItem`** (native chrome popup, `Microsoft.Windows.Themes.*`
  serupa `DataGridHeaderBorder`/`ComboBoxChrome`) — dipakai di `TitleView.xaml`,
  **BELUM di-restyle**. Kelas risiko sama dengan yang sudah diperbaiki di
  bagian 6 baris #2. **Kalau crash log nanti nunjuk ke `Microsoft.Windows.Themes`
  lain (`SystemDropShadowChrome`, `ClassicBorderDecorator`, dll), ini
  kandidat pertama yang perlu di-restyle penuh** dengan pola yang sama
  seperti `DataGridColumnHeader`/`ComboBox`/`ScrollBar`.
- **`kernel32` DllImport** (`LoadLibrary`/`FreeLibrary` di `App.xaml.cs`
  untuk `IsDllPresent`, dan di `Properties/Program.cs`) — aman, Wine
  implementasi `kernel32` lengkap, tidak perlu diubah.

## 8. Cara build & test

- **Lokal:** tidak bisa — WPF cuma bisa dikompilasi di Windows (sandbox saya
  Linux, cuma bisa validasi XML well-formed + balance kurung C#, bukan
  compile sungguhan).
- **CI:** push ke `main`/tag `v*`/PR di GitHub → `.github/workflows/build.yml`
  jalan otomatis di `windows-latest`, `dotnet publish -r win-x86
  --self-contained false`, artifact di-upload.
- **Env var berguna untuk debugging di WinNative:**
  - `NSC_MM_FORCE_HWRENDER=1` — matikan software rendering paksa (default:
    software rendering AKTIF)
  - `NSC_MM_SKIP_UPDATE_CHECK=1` — skip network call cek update GitHub
- **`crash_log.txt`** muncul di folder yang sama dengan exe kalau ada
  unhandled exception — ini sumber informasi utama untuk debugging lanjutan,
  KIRIM FILE INI kalau masih ada masalah.

## 9. Yang PERLU dilakukan di sesi berikutnya (checklist)

1. **Test build CI** (push ke GitHub, cek Actions tab) — pastikan build
   masih hijau setelah semua perubahan `DialogHelper.cs` +
   `UseWindowsForms`.
2. **Jalankan lagi di WinNative**, khususnya cek:
   - Apakah font crash (`UriFormatException`) BENAR-BENAR tuntas dengan fix
     pack URI + `TextFormattingMode=Display` + self-healing.
   - Apakah folder picker (`DialogHelper`) berfungsi normal (baik lewat
     `CommonOpenFileDialog` atau fallback `FolderBrowserDialog`).
3. Kalau masih ada `crash_log.txt` baru → kirim, lanjut analisis sama pola
   di tabel bagian 6.
4. Kalau semua stabil, pertimbangkan: apakah perlu restyle `Menu`/`MenuItem`
   secara preventif, atau tunggu sampai benar-benar terbukti bermasalah
   (biar tidak over-engineering).
5. Kalau dapat source `XFBIN_LIB` yang benar (ada `FindChunks` dkk), ikuti
   panduan di bagian 4 untuk switch balik dari `.dll` prebuilt ke source.
