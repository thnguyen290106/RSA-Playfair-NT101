# RSA & Playfair — NT101

App desktop Windows minh hoạ hai thuật toán mã hoá: **RSA** (hiện đại, khoá công khai) và
**Playfair** (cổ điển, mã hoá theo cặp ký tự).

Bài tập cùng đề thường chỉ hiện input và output, nên người chấm không thấy được con số ở giữa đến
từ đâu, và cũng không thấy được thuật toán yếu ở chỗ nào. App này hiện **từng bước biến đổi**, và
nói thẳng những giới hạn của thuật toán thay vì che.

![Trang chủ](docs/images/01-home.png)

## Chạy thử

Cần .NET 10 SDK trên Windows (app dùng WPF).

```bash
dotnet run
```

Không cần cài gì thêm — không có dependency NuGet nào cho app, không có thư viện crypto ngoài
`SHA256` và `RandomNumberGenerator` của .NET.

## RSA

### 1. Sinh khoá

Chọn **Auto** (512 / 1024 / 2048 bit) hoặc **Manual** (`p`, `q`, `e` — mặc định 61, 53, 17, khớp
ví dụ giáo trình). Auto chạy async, có progress và huỷ được vì 2048 bit mất 5–20 giây.

Sáu ô giá trị hiện đủ `p`, `q`, `n`, `φ(n)`, `e`, `d` — không có con số nào bị ẩn.

![Sinh khoá RSA](docs/images/02-rsa-key.png)

### 2. Mã hoá / giải mã

Văn bản → UTF-8 bytes → cắt thành block nhỏ hơn `n` → `c = mᵉ mod n` từng block. Bảng vết hiện
từng block, và chọn một block thì thấy luôn **từng bước square-and-multiply** của phép luỹ thừa
modulo đó.

![Mã hoá RSA](docs/images/03-rsa-encrypt.png)

Container bản mã tự định nghĩa, xuất ra Base64:

```
[4 byte độ dài bản rõ, big-endian][block bản mã, mỗi block cố định CipherBlockBytes byte]
```

4 byte header giữ độ dài thật nên block cuối không cần padding. Ô "xem bản mã dưới dạng" đổi được
Base64 / hex / thập phân, nhưng **chỉ Base64 giải mã lại được** — hai dạng kia là để xem.

### 3. Chữ ký số

Tab này chia **hai cột cố ý**: cột trái là bên gửi (có khoá riêng), cột phải là bên nhận
(**chỉ có** tài liệu, chữ ký và khoá công khai).

Cột phải không đọc khoá đang có trong bộ nhớ, kể cả khi khoá đó đang nằm ngay ở tab 1. Nó chỉ đọc
`n`, `e` từ ô nhập hoặc từ `publickey.txt`. Nếu nó đọc khoá trong bộ nhớ thì demo chỉ chứng minh
được "một máy ký rồi tự kiểm lại chính mình" — không phải chuyện mà chữ ký số giải quyết.

![Chữ ký số](docs/images/04-rsa-signature.png)

Bên gửi: `SHA-256(thông điệp)` → `s = Hᵈ mod n`. Bên nhận: băm lại dữ liệu nhận được, lấy
`H′ = sᵉ mod n`, rồi so `H` với `H′`. Ba bước bấm riêng được để thấy từng bước, nhưng phán quyết
hợp lệ/không vẫn do `Core.RsaSignature.Verify` đưa ra — nó so trên số nguyên, không so hai chuỗi
hex đang hiện trên màn hình.

Ba file trao đổi: `filedulieu.signed` (tài liệu, UTF-8 không BOM), `chukyso.txt` (chữ ký, số thập
phân), `publickey.txt` (`n` và `e` có nhãn). Khoá riêng **không bao giờ** nằm trong ba file đó.

Muốn thấy xác minh thất bại: sửa một ký tự ở ô dữ liệu bên phải → kết quả băm bị xoá → băm lại →
xác minh → KHÔNG HỢP LỆ.

## Playfair

### 1. Ma trận

Khoá sinh ra ma trận 5×5 (gộp I/J) hoặc 6×6 (thêm chữ số). Đổi khoá hoặc đổi biến thể là dựng lại
ma trận và **xoá kết quả cũ**, vì kết quả cũ tính bằng ma trận cũ.

![Ma trận Playfair](docs/images/05-playfair-matrix.png)

### 2. Mã hoá và giải mã

Tab chia **hai làn cạnh nhau**: mã hoá bên trái, giải mã bên phải. Mã hoá xong thì bản mã tự sang
ô nhập của làn giải mã, nên vòng mã hoá → giải mã không cần nút chuyển tay nào. Lỗi cũng theo làn:
bản mã lẻ ký tự chỉ làm đỏ làn giải mã.

Bảng vết dùng chung một bảng cho hai làn — nó chỉ có nghĩa khi đặt cạnh ma trận sinh ra nó, mà ma
trận của hai chiều là một. Chọn một cặp thì các ô của cặp đó **sáng lên trên ma trận**, kèm câu
giải thích quy tắc nào đã áp dụng.

![Mã hoá Playfair](docs/images/06-playfair-encrypt.png)

Giải mã **không** lấy lại được bản rõ gốc, chỉ lấy lại được văn bản đã chuẩn hoá và đã đệm
(`HELLO` → `CFSUPM` → `HELXLO`). App nói thẳng phần mất đó ở băng cảnh báo (đã bỏ bao nhiêu ký tự,
đã gộp I/J, đã chèn bao nhiêu ký tự đệm) và chỉ **phỏng đoán** vị trí ký tự đệm — không có cách
nào phân biệt chữ `X` thật với chữ `X` do máy chèn, nên app không xoá hộ.

## Kiến trúc

```
Core/                 Toán học thuần. Không tham chiếu WPF. Đây là ràng buộc cứng.
Core/Key_Generation/  Sinh nguyên tố, dẫn xuất khoá, lưu/tải file khoá.
Core/Rsa/             Hai phép RSA (mã hoá, chữ ký) và các record dùng chung.
Core/Playfair/        Ma trận chữ, chia cặp, ba quy tắc, vết từng cặp.
UI/Common/            Hạ tầng MVVM + thứ phụ thuộc WPF (hộp thoại file, converter).
UI/ViewModels/        Trạng thái và lệnh của từng màn hình.
UI/Views/             XAML.
UI/Theme/             Design system tự viết (Palette.xaml, Controls.xaml).
```

`Core/` phải sạch WPF vì phần đáng soi nhất là toán học: tách ra thì test được bằng xUnit mà không
cần dựng cửa sổ. `Microsoft.Win32.OpenFileDialog`, `Clipboard`, `MessageBox` vì vậy nằm ở
`UI/Common/TextFileDialogs.cs`.

MVVM đầy đủ (`ViewModelBase`, `RelayCommand` / `AsyncRelayCommand`), không code-behind nào chứa
logic. Điều hướng không có navigation service: `MainViewModel` giữ danh sách `NavItem`, vùng nội
dung bind thẳng vào `SelectedNav.Content`.

## Vài quyết định đáng nhớ

1. **Textbook RSA, không padding.** Đúng toán, dễ giảng, và app nói thẳng là không an toàn (tất
   định: cùng bản rõ + cùng khoá → cùng bản mã). Chuẩn thật dùng PKCS#1 v1.5 hoặc OAEP.
2. **Chặn cứng khoá dưới 512 bit khi ký.** Bản băm SHA-256 là số 256 bit, phải nhỏ hơn `n`. Không
   lách bằng `H mod n` vì như vậy nhiều bản tin khác nhau cho cùng một giá trị ký.
3. **Dùng `φ(n) = (p−1)(q−1)`, không dùng `λ(n)`.** Khớp giáo trình phổ biến.
4. **Header 4 byte trong container bản mã** thay cho padding, để round-trip không mất byte nào.
5. **Playfair chèn ký tự đệm ngay trong lúc chia cặp**, không phải một lượt quét trước đó. Chèn
   trước thì ký tự vừa chèn lại tạo ra cặp trùng mới ở phía sau (`AAA` là ví dụ).
6. **Giải mã không tự xoá ký tự đệm.** Xoá hộ là đoán, và đoán sai thì bản rõ hiện ra sai mà không
   ai biết.

Vài bẫy đã xử lý: số Carmichael phải bị Miller–Rabin loại; byte `0x00` đầu block không được mất khi
round-trip; bản băm phải đọc dạng **unsigned** big-endian, nếu không byte đầu ≥ `0x80` thành số âm;
`J → I` phải làm **trước** khi bỏ ký tự lặp, nếu không khoá `JAIL` cho hai ô `I`; ký tự đệm cho cặp
`XX` phải đổi sang `Q`/`9`; lui một bước phải viết `+ Size − 1` vì `(0 − 1) % 5 == −1` trong C#.

## Test

**521 test xUnit.** Nguyên tắc là "đúng toán học trước": logic `Core/` phải xanh trước khi bind vào
UI. Test phủ các bẫy kể trên.

`UiSmokeTests` dựng cửa sổ WPF thật trên thread STA, đi hết các tab, chạy các lệnh và soi trạng
thái **mọi `BindingExpression`** đang sống — nhờ vậy sai tên property trong XAML là test đỏ, không
phải một ô trắng im lặng. Nó cũng thu cửa sổ về 1000 × 640 để bắt lỗi layout ở kích thước nhỏ nhất
theo thiết kế.

```bash
dotnet test
```

## Điều đã biết là còn thiếu

- **Phân tích tần suất cặp ký tự của Playfair không làm.** Muốn nói "cặp này bất thường" thì phải
  có bảng tần suất digram tiếng Anh làm mốc; số liệu đó không có trong dự án, và bịa một bảng để
  giao diện có thêm biểu đồ thì con số hiện lên không dựa trên gì. Điểm yếu của Playfair vì vậy
  được nói bằng chữ (bảng chữ chỉ 25/36 ô, mỗi cặp luôn ánh xạ về đúng một cặp) chứ không bằng
  biểu đồ.
- App chỉ ký được **nội dung văn bản**: file chọn ở hộp thoại được đọc dưới dạng text, không ký
  file nhị phân.

Chi tiết hơn: `PROJECT_CONTEXT.md` (kiến trúc, lý do, trade-off) và `CHANGELOG.md` (lịch sử thay
đổi).
