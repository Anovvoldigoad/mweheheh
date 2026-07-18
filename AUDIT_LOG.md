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
