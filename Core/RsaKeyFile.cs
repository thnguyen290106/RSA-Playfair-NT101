using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Đọc và ghi file khoá công khai (<c>publickey.txt</c>) mà bên gửi đưa cho bên
/// nhận khi demo chữ ký số.
/// </summary>
/// <remarks>
/// Định dạng cố ý là văn bản có nhãn, không phải PEM/DER: người xem cần thấy đúng
/// hai con số <c>n</c> và <c>e</c> bằng Notepad, không cần tương thích với công cụ
/// crypto nào khác. Vì file đến từ bên ngoài app nên <see cref="Parse"/> coi mọi
/// nội dung là không tin cậy và báo lỗi rõ ràng thay vì đoán.
/// </remarks>
public static class RsaKeyFile
{
    /// <summary>Mẫu định dạng, đính kèm mọi thông báo lỗi để người dùng sửa được ngay.</summary>
    private const string FormatSample = "n = 3233\ne = 17";

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

        BigInteger? n = null;
        BigInteger? e = null;
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

            if (string.Equals(label, "n", StringComparison.OrdinalIgnoreCase))
            {
                n = parsed;
            }
            else if (string.Equals(label, "e", StringComparison.OrdinalIgnoreCase))
            {
                e = parsed;
            }
        }

        // Dự phòng: file không có nhãn nào mà chỉ có đúng hai số thì hiểu là n rồi e.
        // Người dùng dán tay hai con số từ tab Khoá vẫn chạy được.
        if (n is null && e is null && bareNumbers.Count == 2)
        {
            n = bareNumbers[0];
            e = bareNumbers[1];
        }

        if (n is null || e is null)
        {
            throw new FormatException(
                "File khoá công khai phải có cả n và e. Định dạng đúng:"
                + Environment.NewLine + FormatSample);
        }

        if (n.Value < 2)
        {
            throw new FormatException($"Modulus n phải lớn hơn 1, đọc được {n.Value}.");
        }

        if (e.Value < 1)
        {
            throw new FormatException($"Số mũ công khai e phải lớn hơn 0, đọc được {e.Value}.");
        }

        return (n.Value, e.Value);
    }
}
