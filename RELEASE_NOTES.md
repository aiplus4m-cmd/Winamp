## RetroAmp — trình nghe nhạc & xem phim phong cách cổ điển
Phát triển bởi **NhảmStudio** · https://topvl.net

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
