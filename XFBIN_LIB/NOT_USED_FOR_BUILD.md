# Source ini TIDAK dipakai untuk build NSC-ModManager

Source di folder ini (dari `XFBIN_LIB-main.zip`) adalah versi yang **tidak sinkron**
dengan yang dipakai NSC-ModManager. `NUS3BANKViewModel.cs` memanggil beberapa method
yang tidak ada di sini:

- `XFBIN_READER.FindChunks(...)` (dan nested type `FoundChunk`)
- `XFBIN_WRITER.RepackXfbinData(...)`
- `XFBIN_WRITER.ChangeChunkNameAndPath(...)`

Source di sini cuma punya `ReadXFBIN`, `GetXfbinChunkType`, `ReadDirectoryXFBIN`,
`RepackXFBIN` - versi lebih lama/berbeda.

NSC-ModManager saat ini build memakai `.dll` prebuilt di
`NSC-ModManager/Libs/XFBIN_LIB.dll`, yang sudah dikonfirmasi punya semua method
yang dibutuhkan (dicek lewat `strings` terhadap symbol name di dalam .dll).

**Kalau nanti dapat source XFBIN_LIB versi yang benar** (yang punya method-method
di atas), source itu bisa dipakai untuk ganti folder ini, lalu di
`NSC-ModManager.csproj` ganti balik dari `<Reference>` (HintPath ke Libs\XFBIN_LIB.dll)
jadi `<ProjectReference Include="..\XFBIN_LIB\XFBIN_LIB.csproj" />`, dan tambahkan
lagi entri proyeknya di `NSC-ModManager.sln`.
