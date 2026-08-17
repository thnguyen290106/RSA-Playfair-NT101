using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Đọc và ghi hai loại file khoá: <c>publickey.txt</c> (khoá công khai mà bên gửi
/// đưa cho bên nhận khi demo chữ ký số) và <c>privatekey.txt</c> (khoá đầy đủ mà
/// người dùng tự lưu để lần sau giải mã lại được bản mã đã lưu).
/// </summary>
/// <remarks>
/// Định dạng cố ý là văn bản có nhãn, không phải PEM/DER: người xem cần thấy đúng
/// những con số đó bằng Notepad, không cần tương thích với công cụ crypto nào khác.
/// Vì file đến từ bên ngoài app nên <see cref="Parse"/> và <see cref="ParsePrivate"/>
/// coi mọi nội dung là không tin cậy và báo lỗi rõ ràng thay vì đoán.
/// </remarks>
public static class RsaKeyFile
{
    /// <summary>Mẫu định dạng, đính kèm mọi thông báo lỗi để người dùng sửa được ngay.</summary>
    private const string FormatSample = "n = 3233\ne = 17";

    /// <summary>Mẫu định dạng của file khoá đầy đủ.</summary>
    private const string PrivateFormatSample = "p = 61\nq = 53\ne = 17";

    /// <summary>Độ dài tối đa của mỗi số nguyên tố đọc từ file khoá đầy đủ.</summary>
    /// <remarks>
    /// Đây là chặn ở biên tin cậy chứ không phải giới hạn tuỳ ý. Khoá lớn nhất app
    /// sinh được là 2048 bit, tức <c>p</c> và <c>q</c> khoảng 1024 bit; để gấp đôi
    /// cho thoải mái. Không chặn thì một file 64 KB chứa được con số ~212.000 bit, mà
    /// <see cref="RsaKeyFactory.FromPrimes"/> thử số nguyên tố bằng Miller–Rabin ngay
    /// trên thread giao diện — cửa sổ sẽ đứng im rất lâu trước khi báo lỗi.
    /// </remarks>
    public const int MaxPrimeBits = 2048;

    /// <summary>Nội dung file khoá công khai cho cặp <c>(n, e)</c>.</summary>
    public static string Format(BigInteger n, BigInteger e)
        => string.Join(
            Environment.NewLine,
            "# Khoá công khai RSA (public key) — dữ liệu công khai, không phải bí mật.",
            $"n = {n}",
            $"e = {e}",
            string.Empty);

    /// <summary>Đọc <c>n</c> và <c>e</c> từ nội dung file khoá công khai.</summary>
    /// <exception cref="FormatException">
    /// Khi thiếu <c>n</c> hoặc <c>e</c>, giá trị không phải số nguyên, hoặc số đọc được
    /// không dùng được làm khoá công khai.
    /// </exception>
    public static (BigInteger N, BigInteger E) Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        (Dictionary<string, BigInteger> labels, List<BigInteger> bareNumbers) = ReadLabels(text);

        bool hasN = labels.TryGetValue("n", out BigInteger n);
        bool hasE = labels.TryGetValue("e", out BigInteger e);

        // Dự phòng: file không có nhãn nào mà chỉ có đúng hai số thì hiểu là n rồi e.
        // Người dùng dán tay hai con số từ tab Khoá vẫn chạy được.
        if (!hasN && !hasE && bareNumbers.Count == 2)
        {
            (n, e) = (bareNumbers[0], bareNumbers[1]);
            (hasN, hasE) = (true, true);
        }

        if (!hasN || !hasE)
        {
            throw new FormatException(
                "File khoá công khai phải có cả n và e. Định dạng đúng:"
                + Environment.NewLine + FormatSample);
        }

        if (n < 2)
        {
            throw new FormatException($"Modulus n phải lớn hơn 1, đọc được {n}.");
        }

        if (e < 1)
        {
            throw new FormatException($"Số mũ công khai e phải lớn hơn 0, đọc được {e}.");
        }

        return (n, e);
    }

    /// <summary>Nội dung file khoá đầy đủ: <c>p</c>, <c>q</c>, <c>e</c>.</summary>
    /// <remarks>
    /// Chỉ ghi ba số đó vì <see cref="RsaKeyFactory.FromPrimes"/> tính lại được
    /// <c>n</c>, <c>φ(n)</c> và <c>d</c>. Ghi thêm chúng là tạo nguồn sự thật thứ hai:
    /// sửa tay một dòng là các số không còn khớp nhau mà không biết nên tin dòng nào.
    /// <c>n</c> vẫn có mặt ở dòng chú thích để người xem ghép được file này với
    /// <c>publickey.txt</c> tương ứng — <see cref="ParsePrivate"/> bỏ qua dòng đó.
    /// </remarks>
    public static string FormatPrivate(RsaKeyPair key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return string.Join(
            Environment.NewLine,
            "# Khoá RSA đầy đủ — BÍ MẬT. Ai có file này là có khoá riêng: đọc được mọi",
            "# bản mã của bạn và ký được thay bạn. Giữ như mật khẩu, đừng gửi cho ai.",
            "# File để chia sẻ là publickey.txt, không phải file này.",
            "#",
            $"# n = {key.N}",
            "# (n, φ(n) và d không ghi vào file vì tính lại được từ p, q, e.)",
            $"p = {key.P}",
            $"q = {key.Q}",
            $"e = {key.E}",
            string.Empty);
    }

    /// <summary>Đọc <c>p</c>, <c>q</c>, <c>e</c> từ nội dung file khoá đầy đủ.</summary>
    /// <exception cref="FormatException">
    /// Khi thiếu <c>p</c>, <c>q</c> hoặc <c>e</c>, giá trị không phải số nguyên, hoặc
    /// số nguyên tố dài hơn <see cref="MaxPrimeBits"/> bit.
    /// </exception>
    public static (BigInteger P, BigInteger Q, BigInteger E) ParsePrivate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        (Dictionary<string, BigInteger> labels, _) = ReadLabels(text);

        // Không có nhánh dự phòng "ba số trần" như Parse: ba con số không nhãn thì
        // không biết số nào là p, mà đoán sai là dựng ra một khoá khác hẳn.
        if (!labels.TryGetValue("p", out BigInteger p)
            || !labels.TryGetValue("q", out BigInteger q)
            || !labels.TryGetValue("e", out BigInteger e))
        {
            throw new FormatException(
                "File khoá phải có cả p, q và e. Định dạng đúng:"
                + Environment.NewLine + PrivateFormatSample);
        }

        RequirePrimeInRange(p, "p");
        RequirePrimeInRange(q, "q");

        if (e < 1)
        {
            throw new FormatException($"Số mũ công khai e phải lớn hơn 0, đọc được {e}.");
        }

        // p ≠ q, p và q có thật là số nguyên tố hay không, gcd(e, φ(n)) = 1: để
        // FromPrimes kiểm, nó đã làm đúng việc đó và báo lỗi bằng tiếng Việt sẵn.
        return (p, q, e);
    }

    /// <summary>Chặn số dùng làm <c>p</c>/<c>q</c> về khoảng mà app thử được nhanh.</summary>
    private static void RequirePrimeInRange(BigInteger value, string label)
    {
        if (value < 2)
        {
            throw new FormatException($"Số nguyên tố {label} phải lớn hơn 1, đọc được {value}.");
        }

        long bits = value.GetBitLength();

        if (bits > MaxPrimeBits)
        {
            throw new FormatException(
                $"Số {label} dài {bits} bit, vượt giới hạn {MaxPrimeBits} bit mỗi số nguyên "
                + "tố của app. File này có thể không phải file khoá do app tạo ra.");
        }
    }

    /// <summary>
    /// Quét nội dung file thành các cặp "nhãn = số". Dòng trống và dòng bắt đầu bằng
    /// <c>#</c> bị bỏ qua; số đứng một mình không nhãn trả về riêng cho nhánh dự phòng
    /// của <see cref="Parse"/>. Nhãn không phân biệt hoa thường, trùng nhãn thì dòng
    /// sau thắng.
    /// </summary>
    /// <exception cref="FormatException">Khi giá trị của một nhãn không phải số nguyên.</exception>
    private static (Dictionary<string, BigInteger> Labels, List<BigInteger> BareNumbers) ReadLabels(
        string text)
    {
        Dictionary<string, BigInteger> labels = new(StringComparer.OrdinalIgnoreCase);
        List<BigInteger> bareNumbers = [];

        // Tách theo '\n' rồi Trim: Trim bỏ luôn '\r' nên chịu được cả CRLF và LF.
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int separator = line.IndexOf('=');

            if (separator < 0)
            {
                if (BigInteger.TryParse(line, out BigInteger bare))
                {
                    bareNumbers.Add(bare);
                }

                continue;
            }

            string label = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (!BigInteger.TryParse(value, out BigInteger parsed))
            {
                throw new FormatException(
                    $"Giá trị của \"{label}\" không phải số nguyên: \"{value}\".");
            }

            labels[label] = parsed;
        }

        return (labels, bareNumbers);
    }
}
