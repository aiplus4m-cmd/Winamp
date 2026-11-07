## RetroAmp — trình nghe nhạc & xem phim phong cách cổ điển
Phát triển bởi **NhảmStudio** · https://topvl.net

**Mới trong 1.1.3 — sửa lỗi app tự tắt khi chuyển bài / fixes the app closing at the end of a track**
- Hết một bài, app dừng một lát rồi tự tắt: tín hiệu "hết bài" chạy trên luồng phụ rồi chạm vào giao diện → Windows đóng app. Đã sửa, và có bài test phát liên tục cả playlist (MP3, M4A, WAV, có và không có crossfade) chạy trong CI. / At the end of every track the "track ended" event ran on a background thread and touched the UI, which crashed the app. Fixed, with a CI test that plays whole playlists back to back.
- Không còn hiện hộp thoại lỗi từ bên trong bộ xử lý lỗi (có thể tự làm sập app); nếu app bị tắt đột ngột, lần mở sau sẽ hiện thông tin lỗi. / Crash reporting can no longer crash the app itself; unexpected exits are reported on the next start.
- Máy không có thiết bị âm thanh vẫn chạy playlist bình thường (phát im lặng). / Machines without an audio device keep playing silently instead of failing every track.
- README song ngữ Việt – Anh. / Bilingual Vietnamese–English README.

**Mới trong 1.1.2 — sửa rè/lụp bụp trên laptop**
- Xuất âm qua **WASAPI** (giống trình duyệt, Windows Media Player, VLC) thay cho WaveOut cũ, ở đúng tần số lấy mẫu của loa (thường 48 kHz) → Windows không phải chuyển đổi thêm.
- Bộ đệm lớn hơn, luồng âm thanh ưu tiên cao, giảm thời gian dừng của bộ dọn bộ nhớ → giao diện/visualizer/lyrics không thể làm ngắt âm.
- Tự kết nối lại khi rút/cắm tai nghe hoặc đổi thiết bị mặc định.
- **Options → Audio output device → Audio diagnostics…**: xem trực tiếp đường xuất âm, mức đỉnh, limiter và số lần bị ngắt quãng.
- Chế độ **Compatibility mode (WaveOut)** để dự phòng.

**Mới trong 1.1.1 — sửa vỡ tiếng / rè khi mở to**
- Thay bộ "soft clipper" (bẻ méo mọi mẫu trên −1,4 dB) bằng limiter look-ahead trong suốt: nhạc rock/EDM master to không còn bị rè.
- Sửa EQ: các băng 12k/14k/16k chồng nhau làm preset Rock thực tế tăng tới +24,7 dB → nay đường EQ khớp đúng vị trí thanh trượt.
- Nút **AUTO** mới trong EQ (mặc định bật): tự hạ mức đúng bằng phần EQ/hiệu ứng tăng thêm, nên không preset nào gây méo.
- Reverb nhẹ hơn; CI có bài kiểm tra chất lượng âm thanh cho mọi preset.

**Mới trong 1.1.0**
- 🎤 Lời bài hát chạy theo nhạc: tự tải từ LRCLIB hoặc file `.lrc`, cửa sổ karaoke + lyrics nổi trên desktop, chỉnh lệch thời gian, tìm thủ công.
- 🎨 Đổi skin ngay lập tức (7 skin có sẵn) và tự tạo skin bằng file `.ini`.
- ℹ️ Cửa sổ About với thông tin NhảmStudio.


**Tải về**
- `RetroAmp-x.y.z-Setup.exe` — bộ cài đặt (không cần quyền admin, có tùy chọn "Open with").
- `RetroAmp-x.y.z-Portable.zip` — bản portable: giải nén và chạy `RetroAmp.exe`, mọi thiết lập nằm trong thư mục `Data` cạnh file exe.

**Yêu cầu:** Windows 10/11 (có sẵn .NET Framework 4.8). Windows 7/8.1 cần cài .NET Framework 4.8.

**Tính năng**
- Nhạc local: MP3, FLAC, M4A/AAC, WMA, WAV, OGG/Opus*; Video: MP4, MKV, AVI, WMV, MOV, WebM*.
- YouTube & SoundCloud: dán link bài / playlist / kênh hoặc tìm kiếm ngay trong app (dùng yt-dlp, tự tải khi cần).
- EQ 10 băng + preamp, 20+ preset, reverb, độ rộng stereo, karaoke (khử giọng), crossfade.
- Visualizer: cặp loa đập theo nhạc + đồng hồ VU, phổ cổ điển, oscilloscope, vòng tròn, sao bay; toàn màn hình.
- Lời bài hát karaoke, 7 skin + skin tự tạo.
- Phím media toàn cục, nút điều khiển trên taskbar, kéo-thả file/thư mục/link.

\* Một số định dạng cần codec miễn phí từ Microsoft Store (HEVC/AV1/Web Media Extensions).
