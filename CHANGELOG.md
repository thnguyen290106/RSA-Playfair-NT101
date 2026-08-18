# Changelog

Ghi lại các thay đổi đáng kể của app. Đây là mục đầu tiên của file — phần lịch sử trước đó nằm
trong `git log`.

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/). Project không đánh số
phiên bản (app minh hoạ, không phát hành), nên mỗi mục được đánh dấu bằng ngày.

## 2026-08-18 — Bỏ mọi tham chiếu tới môn học, coi đây là một app độc lập

### Thay đổi

- **Không còn chữ "NT101", "giáo trình", "bài tập lớn", "giảng viên" ở bất kỳ đâu** — kể cả trong
  comment, tài liệu, và `.gitignore`. Lý do chọn 61/53/17 giờ được nói bằng đúng lý do kỹ thuật
  ("hai số nguyên tố nhỏ, đủ để xem từng bước") thay vì "khớp ví dụ giáo trình"; biến thể Playfair
  5×5 là "biến thể phổ biến nhất" thay vì "biến thể trong hầu hết giáo trình"; chọn `φ(n)` thay
  `λ(n)` vì "công thức quen hơn và không thêm khái niệm mới".
- **Tiêu đề README bỏ hậu tố `— NT101`**, và câu mở đầu nói "phần lớn bản minh hoạ hai thuật toán
  này" thay vì "bài tập cùng đề".
- **Bỏ mục "Điều đã biết là còn thiếu" khỏi README.** Hai gạch đầu dòng trong đó (không làm phân
  tích tần suất digram, chỉ ký được nội dung văn bản) là ghi chú phạm vi nội bộ —
  `PROJECT_CONTEXT.md` đã giữ chúng, README không cần.
- **Ảnh minh hoạ chụp lại với dữ liệu mẫu trung tính.** `Xin chào` cho tab mã hoá RSA và
  `Chuyen khoan 5.000.000 dong cho Nguyen Van A.` cho tab chữ ký số — không còn câu nào nhắc tên
  môn học trong ảnh.

### Ghi chú

- Namespace `RSA_Playfair_NT101` và tên repo giữ nguyên: đổi namespace là refactor xuyên suốt mà
  không đổi được thứ người dùng nhìn thấy, còn tên repo thì nằm ngoài source code.
- Đây thuần là thay đổi câu chữ: `detect_changes` báo 5 symbol bị "touched" (comment và chuỗi hiện
  trên UI), 0 execution flow bị ảnh hưởng, risk **low**.
- `dotnet build` sạch (0 warning), `dotnet test` **521 pass**.

## 2026-08-18 — Sửa lỗi thông báo giải mã lúc nào cũng nói còn ký tự đệm

### Sửa lỗi

- **Hộp thoại sau khi giải mã nói "vẫn còn ký tự đệm" trong mọi trường hợp**, kể cả khi bản rõ không
  có ký tự đệm nào. Ca người dùng báo: `BUOMATHUOT` chia cặp thành `BU OM AT HU OT`, không chèn gì,
  nhưng thông báo vẫn khẳng định là còn.
- Câu đó giờ đọc `PlayfairResult.SuspectFillerPositions` — danh sách `Core` **đã** tính sẵn từ trước
  — rồi nói đúng một trong hai: còn bao nhiêu vị trí nghi là ký tự đệm, hoặc không có vị trí nào.
- Câu mô tả của thẻ Giải mã cũng khẳng định như vậy, đã sửa thành có điều kiện.

### Ghi chú

- Đây là lỗi ở tầng thông báo, không phải lỗi thuật toán: `Core/PlayfairCipher.FindSuspectFillers`
  vẫn luôn phỏng đoán đúng, và băng cảnh báo vàng vẫn luôn liệt kê đúng vị trí. Chỉ có câu trong hộp
  thoại là viết cứng.
- Cảnh báo bắn mọi lần thì không còn là cảnh báo. Điều đáng nói của Playfair là **không phân biệt
  được** ký tự đệm với ký tự thật; nói câu đó cả khi không có ký tự đệm nào làm người xem quen với
  việc bỏ qua nó, đúng lúc nó thật sự có ý nghĩa.
- `UiSmokeTests` thêm ca `BUOMATHUOT` (round-trip không mất gì, thông báo và băng cảnh báo đều không
  được nhắc ký tự đệm) và một assert ngược cho `HELLO → HELXLO` (phải nhắc). Hai assert so bằng cụm
  "không có vị trí nào" chứ không phải "nghi là ký tự đệm": câu phủ định cũng chứa cụm sau, nên tìm
  cụm sau thì hai câu không phân biệt được — chính bẫy này làm assert đầu tiên xanh sai một lần.
- `dotnet build` sạch (0 warning), `dotnet test` **521 pass**.

## 2026-08-18 — Hai làn Playfair đặt cạnh nhau

### Thay đổi

- **Hai thẻ Mã hoá và Giải mã của tab 2 chuyển từ xếp dọc sang hai cột cạnh nhau.** Xếp dọc thì
  thẻ Giải mã bắt đầu dưới tầm nhìn: không thấy được bản mã và bản rõ giải ra cùng lúc — đúng thứ
  mà việc tách hai làn định giải quyết.
- Dùng lại đúng `Grid` `* / 16 / *` của RSA tab 3 (`RsaView.xaml:375-380`), không thêm style hay
  hằng số khoảng cách nào mới.
- Câu trong hộp thoại sau khi mã hoá sửa theo layout: "bên dưới" → "bên phải".

### Ghi chú

- Hai thẻ đặt `VerticalAlignment="Top"`: hai cột cao khác nhau mỗi khi chỉ một bên có băng cảnh báo,
  để mặc định `Stretch` thì thẻ ngắn hơn bị kéo dài ra một khoảng trắng vô nghĩa.
- Hàng nút vẫn là `WrapPanel` (quyết định 6). Mỗi cột giờ chỉ còn khoảng một nửa bề rộng, nên đây
  là chỗ `WrapPanel` thật sự có việc: ở cửa sổ 1000px hai nút tự xuống dòng thay vì bị cắt.
- Thẻ "Vết từng cặp" **không đổi**, vẫn chiếm hết bề rộng bên dưới hai cột.
- `dotnet build` sạch (0 warning), `dotnet test` **521 pass** — `UiSmokeTests` chạy ở khổ 1000 × 640
  nên lỗi layout ở bề rộng nhỏ nhất sẽ thành test đỏ.

## 2026-08-18 — Playfair tab 2 chia hai làn

### Thay đổi

- **Tab `2 · Mã hoá và giải mã` tách thành hai thẻ độc lập.** Trước đó hai chiều dùng chung một ô
  nhập và một bộ ô kết quả, kèm nhãn động `OutputLabel` để người xem biết con số đang hiện là của
  chiều nào. Giờ mỗi chiều một thẻ đầy đủ: ô nhập, hàng nút, ba ô kết quả (sau chuẩn hoá / đã chia
  cặp / kết quả) và băng cảnh báo riêng.
- **Mã hoá xong thì bản mã tự sang ô nhập của thẻ Giải mã**, kèm hộp thoại xác nhận. Vòng tròn mã
  hoá → giải mã vì vậy không còn cần nút chuyển tay: `UseOutputAsInputCommand` đã bỏ. Cố ý **không**
  tự chạy giải mã luôn — bấm riêng mới thấy được hai bước là hai việc khác nhau.
- Giải mã xong cũng có hộp thoại, nói rõ kết quả là văn bản đã chuẩn hoá và vẫn còn ký tự đệm.
- `CopyOutputCommand` thành hai lệnh, một cho mỗi làn, dùng chung một hàm xử lý clipboard.
- **Lỗi tách theo làn.** Bản mã lẻ ký tự giờ chỉ làm đỏ thẻ Giải mã; ba ô kết quả của thẻ Mã hoá
  còn nguyên. Trước đây một lỗi xoá sạch kết quả của cả hai chiều.
- Đổi khoá hoặc biến thể còn xoá thêm **ô nhập của thẻ Giải mã**: bản mã trong đó do ma trận cũ sinh
  ra, để lại là mời người dùng giải mã nó bằng một ma trận khác.

### Ghi chú

- Đây là **đảo lại một quyết định đã ghi thành văn** ("chỉ có một ô nhập cho cả hai chiều", với lý do
  Playfair đối xứng nên tách ô chỉ làm người dùng copy qua lại). Lý do đảo: một ô nhập thì không bao
  giờ thấy được bản mã và bản rõ giải ra cùng lúc. `PROJECT_CONTEXT.md` §5 và doc comment của
  `PlayfairViewModel` đã cập nhật theo, không để lại hai nguồn sự thật.
- **Thẻ "Vết từng cặp" giữ nguyên layout.** Bảng vết chỉ có nghĩa khi đặt cạnh ma trận sinh ra nó,
  mà ma trận của hai chiều là một — hai bảng vết thì phải có hai ma trận. Thay vào đó tiêu đề thẻ
  bind vào `TraceTitle` (chính `OutputLabel` cũ, đổi tên) để nói vết đang là của chiều nào.
- `Run(bool encrypting)` vẫn là **một** đường xử lý cho cả hai chiều, chỉ khác bộ field nhận kết quả.
  Hộp thoại vẫn đi qua setter `Status` → `Notifier.Info` như cũ, không thêm helper thông báo nào.
- `Core/` không đổi một dòng: `PlayfairCipher` đã tách sẵn hai chiều từ trước.
- `dotnet build` sạch (0 warning), `dotnet test` **521 pass**. `UiSmokeTests` đổi theo API mới và
  thêm assert cho ba tính chất mới: bản mã tự sang ô giải mã, hai làn không xoá kết quả của nhau,
  và lỗi không lan từ làn này sang làn kia.

## 2026-08-18 — Mọi băng xanh đều có viền chạy

### Thay đổi

- **Thêm kiểu `MarchingInfoBanner` vào `UI/Theme/Controls.xaml`.** Trước đó hiệu ứng viền nét
  gạch chạy chỉ có ở **một** chỗ duy nhất: băng xanh đầu tab `3 · Chữ ký số` của RSA, và nó được
  dựng bằng tay — một `Grid` chứa `Border` kiểu `InfoBanner` với `Rectangle` kiểu `MarchingBorder`
  phủ lên. Ba băng xanh còn lại trong app chỉ là `Border` thường nên nằm im.
- Bọc cặp `Border` + `Rectangle` đó vào một `ControlTemplate` để nó thành **một** kiểu dùng chung.
  Bốn băng xanh giờ động giống nhau từ cùng một nguồn:

  | Màn hình | Băng |
  |---|---|
  | RSA tab 1 · Khoá | `KeyNotes` — lời nhắc về khoá hiện tại |
  | RSA tab 3 · Chữ ký số | Giải thích ký/xác thực (chỗ duy nhất trước đây có viền chạy) |
  | Playfair tab 1 · Ma trận | `VariantNote` — ghi chú về biến thể 5×5/6×6 |
  | Playfair tab 2 · Mã hoá | `StepExplanation` — giải thích cặp đang chọn |

### Ghi chú

- `TargetType` là `ContentControl` chứ không phải `Border`: animation chạy trên
  `Rectangle.StrokeDashOffset`, mà `Border` không vẽ được viền nét gạch.
- Phải đặt `HorizontalContentAlignment="Stretch"` — mặc định của `Control` là `Left`, để nguyên
  thì `TextBlock` bên trong không xuống dòng theo đúng bề rộng băng.
- Không đổi hành vi nào: `dotnet build` sạch (0 warning), `dotnet test` vẫn **521 pass**, trong đó
  `UiSmokeTests` soi mọi `BindingExpression` nên đổi kiểu XAML mà sai tên là test đỏ ngay.

## 2026-08-17 — Dọn phần Playfair vừa viết

### Thay đổi

- **Ba kiểu XAML dùng chung dời vào `UI/Theme/Controls.xaml`:** `FieldLabel`, `ReadOnlyValue`,
  `ErrorText`. Trước đó `RsaView.xaml` và `PlayfairView.xaml` mỗi file giữ một bản sao **y nguyên
  từng dòng** — hai màn hình phải giống nhau nhưng lại có hai nguồn sự thật, sửa một bên là lệch.
- `MatrixLegendTemplate` (`DataTemplate` không có binding nào, dùng đúng một chỗ) được viết thẳng
  tại chỗ dùng. Comment của nó nói "dùng ở cả hai tab" — không đúng.

### Xoá

- `PlayfairResult.RawInput` — không có chỗ nào đọc. Văn bản gốc vẫn nằm trong ô nhập của người dùng.
- `PlayfairMatrix.Contains` — chỉ có test gọi, mà `IndexOf(ch) >= 0` đã trả lời đúng câu đó.
- `PlayfairMatrix.DefaultFiller` — một `const` chỉ để `Filler` trả về, dùng đúng một lần.
- `PlayfairMatrix.AlphabetOf` chuyển thành `private`: không call site nào ngoài class.

### Ghi chú

- Vòng `for` bước 2 dựng cặp khi giải mã đổi thành `normalized.Chunk(2)` — `Enumerable.Chunk` làm
  đúng việc đó và không phải tự cộng chỉ số.
- `TextNormalizer.ToPlainUpper(text)` bị gọi hai lần cho cùng một chuỗi (một lần đếm ký tự bị bỏ,
  một lần tìm chữ `J`); giờ chỉ chuẩn hoá một lượt.
- Không đổi hành vi nào: `dotnet test` vẫn **521 pass**, trong đó `UiSmokeTests` soi mọi
  `BindingExpression` nên việc dời kiểu XAML mà sai tên là test đỏ ngay.

## 2026-08-17 — Playfair, hoàn chỉnh cả hai chiều

### Thêm

- **`Core/Playfair/` — bốn file, không tham chiếu WPF như phần còn lại của `Core/`:**

  | File | Việc |
  |---|---|
  | `TextNormalizer.cs` | Bỏ dấu tiếng Việt và in hoa. `Đ`/`đ` phải thay tay trước vì chúng không phân rã được bằng `FormD`; in hoa dùng `ToUpperInvariant` vì `ToUpper()` theo locale Thổ biến `i` thành `İ` |
  | `PlayfairModels.cs` | `PlayfairVariant`, `DigramRule`, `PlayfairStep`, `PlayfairResult` |
  | `PlayfairMatrix.cs` | Sinh ma trận từ khoá, tra ô ↔ toạ độ, chuẩn hoá văn bản theo bảng chữ của biến thể |
  | `PlayfairCipher.cs` | Chia cặp, chèn ký tự đệm, ba quy tắc, vết từng cặp, cảnh báo mất thông tin |

- **Hai biến thể ma trận.** 5×5 gộp `J` vào `I` và bỏ chữ số (biến thể phổ biến nhất); 6×6 có đủ 26
  chữ cái + 10 chữ số nên giữ được cả `J` và mã hoá được chữ số.
- **Màn hình Playfair, hai tab.** Tab 1 là khoá và ma trận; tab 2 là mã hoá/giải mã, kết quả từng
  bước (sau chuẩn hoá → đã chia cặp → kết quả) và bảng vết từng cặp. Bảng vết nằm cạnh một bản ma
  trận thứ hai: chọn một dòng thì đúng các ô của cặp đó sáng lên — ô đi vào, ô đi ra, và màu thứ ba
  cho ô vừa vào vừa ra (hai chữ cạnh nhau trên cùng hàng là có thật).
- **Một ô nhập cho cả hai chiều**, kèm nút "Đưa kết quả sang ô nhập". Playfair đối xứng và bản mã của
  nó vẫn là chữ cái, nên tách hai ô nhập chỉ làm người dùng phải copy qua lại.
- **Băng cảnh báo nói thẳng phần thông tin bị mất:** đã bỏ bao nhiêu ký tự, đã gộp `I/J`, đã chèn bao
  nhiêu ký tự đệm, và khi khoá không còn ký tự nào dùng được thì nói rõ là khoá mất tác dụng chứ
  không im lặng mã hoá bằng bảng chữ theo thứ tự.

### Thay đổi

- **Trang chủ**: bỏ băng "Phần Playfair chưa được cài đặt trong bản này", nút "Mở Playfair" thành nút
  chính như nút RSA.
- Gạch đầu dòng "Phân tích tần suất cặp ký tự" trên trang chủ được thay bằng thứ app làm thật (nói rõ
  những gì bị mất) — xem *Ghi chú*.

### Ghi chú

- **Ba cái bẫy đã trả giá để biết**, mỗi cái có test cố định lại:
  - `J → I` phải làm **trước** khi bỏ ký tự lặp. Làm ngược thì khoá `JAIL` giữ cả `J` và `I`, ma trận
    có hai ô `I` và thiếu một chữ khác — sai từ ô đầu.
  - Ký tự đệm cho cặp `XX` phải đổi sang `Q` (5×5) hoặc `9` (6×6). Chèn `X` vào giữa `XX` vẫn ra một
    cặp trùng chữ, tức là không giải quyết được gì.
  - Lui một bước ở biên ma trận phải viết `+ Size − 1`: trong C# `(0 − 1) % 5` ra `−1`, không ra `4`.
- **Giải mã không tự xoá ký tự đệm**, chỉ đánh dấu vị trí *nghi* là ký tự đệm. Không có cách nào phân
  biệt chữ `X` thật với chữ `X` do máy chèn, nên xoá hộ là đoán — và đoán sai thì bản rõ hiện ra sai
  mà không ai biết. Vì vậy `Decrypt(Encrypt("HELLO"))` trả `HELXLO`, đúng như test ghi lại.
- **Bản mã có số ký tự lẻ bị từ chối** kèm lời giải thích, không tự thêm một ký tự cho chẵn.
- **Phân tích tần suất cặp ký tự không làm.** Muốn nói "cặp này bất thường" thì phải có bảng tần suất
  digram tiếng Anh làm mốc; số liệu đó không có trong dự án và bịa ra một bảng thì con số hiện lên
  không dựa trên gì. Điểm yếu của Playfair được nói bằng chữ thay vì bằng biểu đồ.
- **Sửa một lỗi cũ trong lúc viết test:** `PlayfairMatrix.NormalizedKey` trả khoá *chưa* bỏ ký tự lặp
  trong khi tài liệu của nó hứa ngược lại. Giờ nó lấy thẳng từ các ô đã điền, nên không thể lệch với
  ma trận thật.
- `dotnet test`: **521 pass**, trong đó `UiSmokeTests` đi thêm một vòng Playfair (mã hoá, giải mã lại,
  bản mã lẻ ký tự, đổi biến thể) ở cửa sổ 1000 × 640.

## 2026-08-17 — Chia `Core/` thành thư mục con

### Thay đổi

- **Bảy file phẳng trong `Core/` giờ nằm theo nhóm việc.** Không có dòng code nào thay đổi, chỉ là
  đường dẫn file:

  | Thư mục | File | Vì sao |
  |---|---|---|
  | `Core/Key_Generation/` | `PrimeGenerator.cs`, `RsaKeyFactory.cs`, `RsaKeyFile.cs` | Cả vòng đời của một khoá: sinh `p`, `q` → dẫn xuất `n`, `φ(n)`, `d` → lưu ra file → tải lại |
  | `Core/Rsa/` | `RsaCipher.cs`, `RsaSignature.cs`, `RsaModels.cs` | Hai phép RSA của app, cộng với `RsaKeyPair`/`RsaBlockTrace` mà cả hai bên dùng chung |
  | `Core/` (giữ nguyên) | `BigIntegerMath.cs` | Cả hai nhóm trên đều gọi nó, nên nó là tầng nguyên thuỷ ở dưới chứ không thuộc bên nào |

- `Core/Playfair/` để trống, chờ phần Playfair. Git không theo dõi thư mục rỗng nên nó chỉ tồn tại ở
  máy cho tới khi có file đầu tiên.

### Ghi chú

- **Namespace không đổi**, cả `Core/` vẫn là `RSA_Playfair_NT101.Core`. C# không bắt namespace khớp
  thư mục; giữ phẳng thì không file nào ở `UI/` hay project test phải thêm `using`, nên mỗi lần dời
  file là một commit đổi tên thuần và tự build được. Đổi namespace theo thư mục là việc riêng, làm sau
  cũng được.
- `dotnet test` vẫn 391 pass sau khi dời: SDK glob quét `**/*.cs` nên không có file `.csproj` nào phải
  sửa.

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
  q = 53." — một việc chưa ai yêu cầu. Giờ hai ô `p`, `q`, `e` vẫn điền sẵn 61, 53, 17 — số nhỏ, dễ soi từng
  bước, nhưng khoá chỉ được tạo khi người dùng bấm "Tạo khoá".

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
