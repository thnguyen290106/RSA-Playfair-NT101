# Changelog

Ghi lại các thay đổi đáng kể của app. Đây là mục đầu tiên của file — phần lịch sử trước đó nằm
trong `git log`.

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/). Project không đánh số
phiên bản (đây là bài tập, không phát hành), nên mỗi mục được đánh dấu bằng ngày.

## 2026-08-17 — Lưu khoá ra file và tải lại

### Thêm

- **Hai nút "Lưu khoá ra file" / "Tải khoá từ file" ở tab 1 · Khoá.** Trước đây khoá chỉ sống trong bộ
  nhớ: mã hoá, bấm "Lưu bản mã", đóng app — mở lại thì `banma.txt` còn đó nhưng khoá đã tạo ra nó thì
  không, và bấm Giải mã chỉ nhận được thông báo "không khớp khoá". Đường duy nhất để lấy lại khoá cũ là
  chép tay `p`, `q` (155 chữ số với khoá 1024 bit) vào chế độ Thủ công. Giờ nó là hai cái nút.
- **Định dạng `privatekey.txt`** — `Core/RsaKeyFile.FormatPrivate` / `ParsePrivate`. File chỉ chứa ba
  số `p`, `q`, `e`; `n`, `φ(n)` và `d` được `RsaKeyFactory.FromPrimes` tính lại khi tải nên không ghi
  vào file. Ghi thêm chúng là tạo nguồn sự thật thứ hai: sửa tay một dòng là các số không còn khớp mà
  không biết nên tin dòng nào. `n` vẫn có mặt ở **dòng chú thích** để người xem ghép được file này với
  `publickey.txt` tương ứng.
- **Chặn số nguyên tố quá lớn khi đọc file** — `RsaKeyFile.MaxPrimeBits = 2048`. File khoá được đọc
  tối đa 64 KB, tức chứa được một con số ~212.000 bit, mà `FromPrimes` thử số nguyên tố bằng
  Miller–Rabin 40 nhân chứng ngay trên thread giao diện: không chặn thì cửa sổ đứng im rất lâu trước
  khi báo lỗi. Giới hạn là gấp đôi `p`, `q` của khoá 2048 bit — khoá lớn nhất app sinh được.

### Ghi chú

- **File này chứa khoá riêng, và đó là chủ ý.** Ai có `p` và `q` là có `d`. Nên file có ba dòng cảnh
  báo BÍ MẬT ở đầu, nút và phần chú thích trong app nói rõ phải giữ nó như mật khẩu, và `publickey.txt`
  mới là file đem đi. Đây cũng là một điểm dạy được: khoá riêng là một file phải giữ kín, đúng như
  `id_rsa` mặc định của SSH.
- Không đặt mật khẩu cho file (PBKDF2 + AES): app dạy RSA, không phải quản lý khoá. Cảnh báo bằng chữ,
  giống hành vi mặc định của `ssh-keygen`.
- Không phải PEM/PKCS#8: cả project cố ý dùng văn bản đọc được bằng Notepad.
- Tải khoá mới xoá bản mã và chữ ký của khoá cũ đang hiện trên màn hình (dùng chung `SetKey` với hai
  đường có khoá kia), vì chúng thuộc về một khoá khác.
- Việc kiểm tra chia hai tầng, không làm hai lần: `ParsePrivate` chỉ kiểm "có mặt / là số nguyên /
  trong khoảng bit hợp lý" → `FormatException`; còn `p ≠ q`, `p` và `q` có thật là số nguyên tố,
  `gcd(e, φ(n)) = 1` là việc của `FromPrimes` → `ArgumentException`. Người dùng thấy thông báo như nhau.
- Tab 2 · Mã hoá có thêm một dòng nói thẳng rằng khoá sinh tự động mất khi đóng app, để người đang
  đứng ở tab đó biết phải quay lại tab 1 lưu khoá.

## 2026-08-17 — Mở app không tự tạo khoá nữa

### Sửa

- **Mở app không còn tự tạo khoá 61 × 53.** Trước đây constructor của `RsaViewModel` tạo sẵn khoá từ
  hai số điền mặc định, nên sau khi thêm hộp thoại thì vừa mở app đã bật lên "Đã tạo khoá từ p = 61,
  q = 53." — một việc chưa ai yêu cầu. Giờ hai ô `p`, `q`, `e` vẫn điền sẵn 61, 53, 17 theo ví dụ
  giáo trình, nhưng khoá chỉ được tạo khi người dùng bấm "Tạo khoá".

### Thay đổi

- `KeyNotes` lúc chưa có khoá nói rõ đang chưa có khoá và cần bấm gì, thay vì mô tả một khoá không
  tồn tại.
- Thẻ "Khoá hiện tại" chỉ hiện sáu ô `p`, `q`, `n`, `φ(n)`, `e`, `d` khi đã có khoá — không ai còn
  nhìn sáu ô trống rồi tưởng khoá vừa tạo ra là rỗng. Dùng `ContentControl` + `DataTemplate` chứ
  không phải `Visibility`: ẩn bằng `Visibility` thì sáu binding vẫn tồn tại và vẫn bám vào một nguồn
  `null`, sinh `PathError` mà `UiSmokeTests` bắt được.

## 2026-08-17 — Mọi thông báo hiện thêm bằng hộp thoại

### Thêm

- **Mọi thông báo giờ hiện đồng thời hai chỗ: băng thông báo trong tab như cũ, và một hộp thoại
  (`MessageBox`).** Băng thông báo nằm trong tab nên người đang xem tab khác không thấy nó; hộp
  thoại thì chắc chắn thấy. Không băng nào bị bỏ đi — băng vẫn còn đó để đọc lại sau khi bấm OK.
  Áp dụng cho: lỗi sinh khoá, lỗi và trạng thái file của tab mã hoá, lỗi và trạng thái file của cột
  bên gửi, lỗi của cột bên nhận, và phán quyết HỢP LỆ / KHÔNG HỢP LỆ.
- Ba mốc kết thúc của việc sinh khoá cũng có hộp thoại: đã sinh xong khoá tự động, đã tạo khoá từ
  `p`, `q` nhập tay, và đã huỷ giữa lúc sinh.
- `UI.Common.Notifier` — chỗ duy nhất gọi `MessageBox`, có cờ `Enabled` để test tắt hộp thoại. Hộp
  thoại modal chặn thread giao diện, nên không tắt thì `UiSmokeTests` treo tới lúc hết giờ.

### Ghi chú

- Ô tiến trình sinh khoá **không** hiện hộp thoại theo từng bước: mỗi lần thử một số nguyên tố là
  một lần cập nhật, hiện hết thì thành hàng chục hộp thoại cho một lần bấm.
- Nhắc "Chưa có khoá công khai" ở cột bên nhận vẫn chỉ là băng vàng: nó bám theo nội dung hai ô
  `n`, `e` nên nếu hiện hộp thoại thì sẽ bật lên giữa lúc người dùng đang gõ.
- Mã hoá, giải mã và ký số khi thành công vẫn không có thông báo nào — kết quả hiện ngay trong ô
  bên dưới. Đây là hành vi cũ, không phải thứ vừa bỏ đi.

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
