<p align="center"><img src="assets/retroamp.png" width="96" alt="RetroAmp"></p>

<h1 align="center">RetroAmp</h1>

<p align="center">
  Phát triển bởi / Developed by <b>NhảmStudio</b> · <a href="https://topvl.net">https://topvl.net</a><br>
  <a href="#-tiếng-việt">🇻🇳 Tiếng Việt</a> · <a href="#-english">🇬🇧 English</a> · <a href="../../releases/latest"><b>⬇ Download / Tải về</b></a>
</p>

<p align="center">
  <img src="assets/screenshots/main-window.png" width="420" alt="RetroAmp main window">
  <img src="assets/screenshots/speakers.png" width="420" alt="Speakers visualizer"><br>
  <img src="assets/screenshots/visualizers.png" width="560" alt="Other visualizers"><br>
  <img src="assets/screenshots/lyrics.png" width="300" alt="Synced lyrics">
  <img src="assets/screenshots/skins.png" width="560" alt="Skins">
</p>

---

## 🇻🇳 Tiếng Việt

**Trình nghe nhạc & xem phim nhẹ cho Windows, theo phong cách các player huyền thoại thời Windows XP**: LCD xanh lá, nút bấm nổi, thanh EQ dọc, playlist nền đen chữ xanh. Thêm vào đó là những thứ của thời nay: **phát YouTube & SoundCloud**, visualizer **cặp loa đập theo nhạc**, **lời bài hát chạy theo nhạc**, đổi skin, crossfade, karaoke.

### Tải về

Vào trang **[Releases](../../releases/latest)**:

| File | Dùng khi |
|---|---|
| `RetroAmp-x.y.z-Setup.exe` | Cài đặt bình thường (không cần quyền admin, có shortcut và "Open with") |
| `RetroAmp-x.y.z-Portable.zip` | Bản portable: giải nén ở đâu cũng chạy, thiết lập lưu cạnh file exe (chạy được từ USB) |

Yêu cầu: **Windows 10/11** (đã có sẵn .NET Framework 4.8, app chỉ khoảng 2 MB). Windows 7/8.1 cần cài [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48).
Lần đầu chạy, Windows có thể hiện cảnh báo SmartScreen vì file chưa được ký số: bấm *More info → Run anyway*.

### Tính năng

- 🎵 **Nhạc trên máy**: MP3, FLAC, M4A/AAC, ALAC, WMA, WAV, AIFF, OGG/Opus\*
- 🎬 **Video**: MP4, MKV, AVI, WMV, MOV, WebM\*, TS… (cửa sổ video riêng, double-click để xem toàn màn hình)
- 🌐 **YouTube & SoundCloud**: dán link bài hát, playlist, kênh hoặc set, hoặc **tìm kiếm** ngay trong app; YouTube mở được dạng video. Bài kế tiếp được tải trước để chuyển bài mượt. App cũng nhận link radio internet và link file media trực tiếp.
- 🎚️ **Chỉnh âm**: EQ 10 băng (60 Hz → 16 kHz) + preamp, hơn 20 preset (Rock, Pop, Dance, Bass Boost, Bolero…) và lưu preset riêng; **Reverb**, **Stereo width**, **Karaoke** (khử giọng hát), **Crossfade** 0–10 s. Nút **AUTO** tự bù mức theo EQ, kèm limiter look-ahead, nên mở to nhạc rock cũng không vỡ tiếng (có bài kiểm tra tự động cho mọi preset).
- 🔊 **Visualizer**: *Speakers & VU Meters* (cặp loa với màng bass phập phồng, đèn LED theo nhịp, kim VU analog), *Spectrum Analyzer* cổ điển, *Oscilloscope*, *Radial Pulse*, *Starfield*. Có mini-visualizer trong màn LCD (bấm vào để đổi kiểu). Khi xem video, visualizer vẫn chạy.
- 🎤 **Lời bài hát chạy theo nhạc** (như jetAudio): tự tìm lyrics có mốc thời gian từ [LRCLIB](https://lrclib.net) hoặc dùng file `.lrc` cùng tên đặt cạnh bài hát. Có cửa sổ lyrics kiểu karaoke (dòng đang hát đổi màu dần, bấm vào dòng để tua tới) và **lyrics nổi trên desktop**.
- 🎨 **Đổi giao diện (skin)** ngay lập tức: *Retro Night, Classic 2000, Silver Audio, Amber Hi-Fi, Sakura, NhảmStudio, Matrix*, hoặc tự tạo skin bằng file `.ini`.
- 📃 **Playlist**: kéo-thả file, thư mục, link; tìm kiếm, sắp xếp, xóa trùng; lưu/mở M3U/M3U8/PLS; tự nhớ playlist khi tắt.
- ⌨️ Phím tắt kiểu cổ điển (`Z X C V B`), **phím media** trên bàn phím dùng được ở mọi nơi, nút Prev/Play/Next trên thumbnail taskbar, chỉnh kích thước 100–200%, luôn ở trên cùng, chọn thiết bị âm thanh.

\* Một số định dạng cần codec miễn phí từ Microsoft Store (Web Media Extensions, HEVC, AV1).

### Đổi giao diện
Bấm nút **≡** trên thanh tiêu đề (hoặc chuột phải vào khung player) → **Skins** → chọn skin. Muốn tự làm skin: **Skins → Create my own skin from the current one…**, đặt tên, Notepad sẽ mở file `.ini` chứa toàn bộ màu (`#RRGGBB`). Sửa, lưu, rồi chọn lại skin đó trong menu. File skin nằm trong thư mục `Skins` (mở nhanh bằng *Open skins folder*), có thể gửi cho người khác dùng.

### Lời bài hát
Bấm nút **LRC** (phím `Y`) để mở cửa sổ lyrics, `Shift+Y` để bật lyrics nổi trên desktop (kéo để di chuyển, lăn chuột để đổi cỡ chữ). Lyrics tải về được lưu lại để lần sau mở ngay.
- Tìm sai bài: chuột phải vào cửa sổ lyrics → *Search lyrics…*, sửa tên ca sĩ/bài hát rồi chọn kết quả khác.
- Lời chạy nhanh/chậm hơn nhạc: *Lyrics later/earlier* hoặc phím `+` / `−` (app nhớ mức chỉnh cho từng bài).
- Có thể đặt file `Tên bài.lrc` cạnh `Tên bài.mp3`, app sẽ ưu tiên file này.

### YouTube / SoundCloud
RetroAmp dùng công cụ mã nguồn mở [yt-dlp](https://github.com/yt-dlp/yt-dlp). Lần đầu mở link online, app hỏi rồi tự tải `yt-dlp.exe` (khoảng 18 MB) vào thư mục Tools.
- YouTube đổi cách hoạt động và không phát được: **Menu → Online → Update yt-dlp**.
- Có thể cài thêm **Deno** (menu Online) để yt-dlp lấy đủ định dạng YouTube.
- YouTube báo *"Sign in to confirm you're not a bot"*: **Menu → Online → YouTube: use sign-in cookies from browser**, chọn trình duyệt bạn đã đăng nhập YouTube (Firefox ổn định nhất).
- File đã tải được cache để nghe lại không phải tải lại; xóa cache trong cùng menu.

### Âm thanh bị rè hoặc lỗi?
**Menu → Options → Audio output device → Audio diagnostics…** cho xem trực tiếp đường xuất âm, mức đỉnh, limiter và số lần âm thanh bị ngắt quãng (bấm **COPY** để gửi cho nhà phát triển). Nếu máy gặp vấn đề với chế độ mặc định (WASAPI), thử **Compatibility mode (WaveOut)** trong cùng menu. Nếu app bị tắt đột ngột, lần mở sau app sẽ hiện thông tin lỗi; log nằm ở `%APPDATA%\RetroAmp\retroamp.log`.

### Phím tắt

| Phím | Chức năng | Phím | Chức năng |
|---|---|---|---|
| `Z` | Bài trước | `S` | Shuffle |
| `X` | Play | `R` | Repeat (tắt / tất cả / một bài) |
| `C` | Pause | `E` / `P` / `K` | Hiện EQ / Playlist / Visualizer |
| `V` | Stop | `L` / `Shift+L` | Thêm file / thư mục |
| `B` | Bài sau | `Ctrl+U` | Thêm link hoặc tìm YouTube, SoundCloud |
| `Space` | Play/Pause | `Ctrl+F` | Tìm trong playlist |
| `←` `→` | Tua 5 giây | `Ctrl+T` | Luôn ở trên cùng |
| `↑` `↓` | Âm lượng | `Ctrl+V` | Dán link vào playlist |
| `Y` | Cửa sổ lyrics | `Shift+Y` | Lyrics nổi trên desktop |

---

## 🇬🇧 English

**A lightweight music & video player for Windows in the style of the legendary Windows XP–era players**: green LCD, chunky bevelled buttons, vertical EQ sliders, a black-and-green playlist. On top of that come modern features: **YouTube & SoundCloud playback**, a **pumping-speakers visualizer**, **synced lyrics**, skins, crossfade and karaoke.

### Download

Go to **[Releases](../../releases/latest)**:

| File | Use it for |
|---|---|
| `RetroAmp-x.y.z-Setup.exe` | A normal install (no admin rights needed, Start-menu shortcut and "Open with") |
| `RetroAmp-x.y.z-Portable.zip` | Portable version: unzip anywhere and run; settings are stored next to the exe (works from a USB stick) |

Requirements: **Windows 10/11** (.NET Framework 4.8 is built in; the app itself is about 2 MB). Windows 7/8.1 needs [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48).
On first launch Windows SmartScreen may warn you because the file is not code-signed: click *More info → Run anyway*.

### Features

- 🎵 **Local music**: MP3, FLAC, M4A/AAC, ALAC, WMA, WAV, AIFF, OGG/Opus\*
- 🎬 **Video**: MP4, MKV, AVI, WMV, MOV, WebM\*, TS… (separate video window, double-click for full screen)
- 🌐 **YouTube & SoundCloud**: paste a link to a track, playlist, channel or set, or **search** right inside the app; YouTube can also be opened as video. The next track is downloaded ahead of time for smooth transitions. Internet radio and direct media links work too.
- 🎚️ **Sound shaping**: 10-band EQ (60 Hz → 16 kHz) + preamp, 20+ presets (Rock, Pop, Dance, Bass Boost, Bolero…) and your own presets; **Reverb**, **Stereo width**, **Karaoke** (vocal remover), **Crossfade** 0–10 s. The **AUTO** button compensates the EQ boost and a look-ahead limiter catches the rest, so loud rock stays clean at high volume (every preset is checked by an automated test).
- 🔊 **Visualizers**: *Speakers & VU Meters* (two cabinets with pumping woofers, beat-synced LEDs and analog VU needles), classic *Spectrum Analyzer*, *Oscilloscope*, *Radial Pulse*, *Starfield*. A mini visualizer sits in the LCD (click it to change style). Visualizers keep running while a video plays.
- 🎤 **Synced lyrics** (like jetAudio): time-coded lyrics are fetched from [LRCLIB](https://lrclib.net), or taken from a `.lrc` file with the same name as the song. Karaoke-style lyrics window (the current line fills with colour, click a line to jump there) and **floating desktop lyrics**.
- 🎨 **Skins**, switched instantly: *Retro Night, Classic 2000, Silver Audio, Amber Hi-Fi, Sakura, NhảmStudio, Matrix*, or make your own with an `.ini` file.
- 📃 **Playlist**: drag & drop files, folders and links; search, sort, remove duplicates; load/save M3U/M3U8/PLS; the playlist is remembered between sessions.
- ⌨️ Classic shortcuts (`Z X C V B`), global **keyboard media keys**, Prev/Play/Next buttons in the taskbar thumbnail, 100–200% size, always on top, output device selection.

\* Some formats need the free codec extensions from the Microsoft Store (Web Media Extensions, HEVC, AV1).

### Changing the skin
Click **≡** in the title bar (or right-click the player) → **Skins** → pick one. To make your own: **Skins → Create my own skin from the current one…**, give it a name, and Notepad opens an `.ini` file with every colour (`#RRGGBB`). Edit, save, then pick the skin again from the menu. Skins live in the `Skins` folder (*Open skins folder*) and can be shared with friends.

### Lyrics
Click **LRC** (key `Y`) for the lyrics window, `Shift+Y` for desktop lyrics (drag to move, mouse wheel to resize). Downloaded lyrics are cached, so they open instantly next time.
- Wrong song found: right-click the lyrics window → *Search lyrics…*, fix the artist/title and pick another result.
- Lyrics running early or late: *Lyrics later/earlier* or `+` / `−` (the offset is remembered per song).
- Put `Song name.lrc` next to `Song name.mp3` and it is used first.

### YouTube / SoundCloud
RetroAmp uses the open-source tool [yt-dlp](https://github.com/yt-dlp/yt-dlp). The first time you open an online link, the app asks and downloads `yt-dlp.exe` (about 18 MB) into its Tools folder.
- YouTube changed and nothing plays: **Menu → Online → Update yt-dlp**.
- Optionally install **Deno** (Online menu) so yt-dlp can get every YouTube format.
- YouTube says *"Sign in to confirm you're not a bot"*: **Menu → Online → YouTube: use sign-in cookies from browser** and choose the browser where you are signed in to YouTube (Firefox works best).
- Downloads are cached for replay; clear the cache from the same menu.

### Crackling sound or other problems?
**Menu → Options → Audio output device → Audio diagnostics…** shows the output path, peak level, limiter activity and the number of audio drop-outs (press **COPY** to send it to the developer). If the default mode (WASAPI) misbehaves on your machine, try **Compatibility mode (WaveOut)** in the same menu. If the app ever closes unexpectedly, the error is shown on the next start; the log is at `%APPDATA%\RetroAmp\retroamp.log`.

### Keyboard shortcuts

| Key | Action | Key | Action |
|---|---|---|---|
| `Z` | Previous | `S` | Shuffle |
| `X` | Play | `R` | Repeat (off / all / one) |
| `C` | Pause | `E` / `P` / `K` | Show EQ / Playlist / Visualizer |
| `V` | Stop | `L` / `Shift+L` | Add files / folder |
| `B` | Next | `Ctrl+U` | Add a link or search YouTube, SoundCloud |
| `Space` | Play/Pause | `Ctrl+F` | Search the playlist |
| `←` `→` | Seek 5 s | `Ctrl+T` | Always on top |
| `↑` `↓` | Volume | `Ctrl+V` | Paste links into the playlist |
| `Y` | Lyrics window | `Shift+Y` | Desktop lyrics |

---

## 🛠 Build từ mã nguồn / Building from source

Cần / Requires [.NET SDK 8+](https://dotnet.microsoft.com/download). Build được trên Windows, Linux và macOS vì giao diện viết thuần C#, không biên dịch XAML. / It builds on Windows, Linux and macOS because the UI is plain C# with no XAML compilation.

```bash
dotnet build src/RetroAmp/RetroAmp.csproj -c Release

# Kiểm tra chất lượng âm thanh (mọi OS) / audio-quality test (any OS)
dotnet run -c Release --project tests/DspTests/DspTests.csproj

# Self-test (chỉ Windows / Windows only): decode, DSP, playlist playback, UI + visualizer screenshots
src/RetroAmp/bin/Release/net48/RetroAmp.exe --selftest out
```

GitHub Actions build, test and package every push. Khi `<Version>` trong `src/RetroAmp/RetroAmp.csproj` tăng và được đẩy lên `main`, một Release mới (installer + portable zip) được tạo tự động. / When `<Version>` in `src/RetroAmp/RetroAmp.csproj` is raised and pushed to `main`, a new Release (installer + portable zip) is created automatically.

```
src/RetroAmp/
  Audio/   AudioEngine (WASAPI / WaveOut output, mixer), Deck (decoder + fades), DspChain (EQ, reverb, width,
           karaoke, auto gain, limiter), Analyzer (FFT, beat detection), VisBus, LoopbackTap, NullOutput
  Media/   Player (playlist, shuffle/repeat, crossfade, fallbacks), YtDlp + Tools (online), Lyrics + LyricsService,
           PlaylistIO, TagLoader, MediaBackend (video)
  UI/      MainWindow, 7-segment LCD, custom-drawn buttons/sliders, skins, visualizers, lyrics, video, dialogs
tests/DspTests/          audio-quality regression test
installer/RetroAmp.iss   Inno Setup script
```

## 👤 Tác giả / Author
**NhảmStudio** · https://topvl.net

<p><img src="assets/nhamstudio-logo.png" width="260" alt="NhảmStudio"></p>

## 📄 Giấy phép / License
MIT.

RetroAmp là dự án độc lập, lấy cảm hứng từ phong cách các media player cổ điển; không liên kết với Winamp, jetAudio hay chủ sở hữu các thương hiệu đó. / RetroAmp is an independent project inspired by the look of classic media players; it is not affiliated with Winamp, jetAudio or their trademark owners.

Sử dụng / Uses: [NAudio](https://github.com/naudio/NAudio) (MIT), [TagLib#](https://github.com/mono/taglib-sharp) (LGPL), [yt-dlp](https://github.com/yt-dlp/yt-dlp) (Unlicense, tải riêng khi cần / downloaded separately on demand), lyrics from [LRCLIB](https://lrclib.net). Hãy tôn trọng điều khoản của YouTube/SoundCloud và bản quyền nội dung. / Please respect YouTube/SoundCloud terms and content copyrights.
