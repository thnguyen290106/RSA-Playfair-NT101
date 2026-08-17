# Changelog

Ghi lại các thay đổi đáng kể của app. Đây là mục đầu tiên của file — phần lịch sử trước đó nằm
trong `git log`.

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/). Project không đánh số
phiên bản (đây là bài tập, không phát hành), nên mỗi mục được đánh dấu bằng ngày.

## 2026-08-17 — Tab chữ ký số chia hai bên, thêm trao đổi qua file

### Thêm

- **Tab chữ ký số chia hai cột: bên gửi và bên nhận.** Cột trái giữ khoá riêng và ký; cột phải chỉ
  có dữ liệu, chữ ký số và khoá công khai — đúng những gì một người nhận thật có trong tay.
- **Cột phải chạy được ba bước riêng lẻ:** băm lại dữ liệu (SHA-256), giải mã chữ ký bằng khoá công
  khai để lấy `H′ = sᵉ mod n`, rồi so hai bản băm. Bấm "Xác minh" một mình vẫn chạy đủ cả ba bước.
- **Trao đổi qua file cho tab chữ ký số.** Nút "Xuất file" ghi ba file vào thư mục người dùng chọn:
  `filedulieu.signed` (tài liệu), `chukyso.txt` (chữ ký dạng thập phân), `publickey.txt` (`n` và `e`
  có nhãn, đọc được bằng Notepad). Có hỏi trước khi ghi đè file đã tồn tại.
- **Mọi ô ở cột phải đều tải được từ file hoặc nhập tay**, kể cả `n` và `e`. Thiếu khoá công khai thì
  hiện nhắc nhở, không chặn nút nào.
- **Nút sao chép chữ ký** ở cột trái.
- **Tab mã hoá: bốn nút file** — tải bản rõ, lưu bản mã, tải bản mã, sao chép bản mã. Bản mã lưu ra
  file ở dạng Base64 vì chỉ dạng đó giải mã lại được.
- `Core.RsaSignature.RecoverHashHex` — lấy lại bản băm từ chữ ký để hiện bước trung gian, có đệm 0
  cho đủ 64 ký tự hex nên so bằng mắt với ô băm bên trên được.
- `Core.RsaKeyFile` — đọc/ghi file khoá công khai, coi mọi nội dung file là không tin cậy và báo lỗi
  tiếng Việt kèm mẫu định dạng thay vì đoán.
- `UI.Common.TextFileDialogs` — hộp thoại chọn file dùng chung, có chặn dung lượng ngay tại biên:
  1 MB cho file tài liệu, 64 KB cho file chỉ chứa một con số (một số thập phân dài hàng triệu chữ số
  làm `BigInteger.Parse` treo cửa sổ).

### Thay đổi

- Ô thông điệp của tab chữ ký số **bắt đầu trống** thay vì có sẵn câu mẫu, để không ai ký một câu
  mình chưa đọc.
- Ký khi ô thông điệp trống giờ báo lỗi nói rõ phải làm gì, thay vì ký một chuỗi rỗng.
- Băng kết quả xác minh không lặp lại hai chuỗi hex trong câu thông báo nữa — hai ô ngay trên đã hiện
  chúng.
- Hàng nút của cả hai tab dùng `WrapPanel` để tự xuống dòng ở cửa sổ hẹp nhất (1000px).

### Sửa

- **Đổi thông điệp mà ô SHA-256 vẫn giữ bản băm của thông điệp cũ.** Giờ đổi thông điệp là xoá luôn
  bản băm và chữ ký, nên không còn hiện một giá trị không thuộc về nội dung đang xem.

### Xoá

- **Nút "Sửa 1 ký tự rồi xác thực lại".** Sau khi tách hai cột, chỉ cần sửa ô dữ liệu bên phải rồi
  băm lại là thấy chữ ký mất hiệu lực — thật hơn một nút tự sửa hộ.
