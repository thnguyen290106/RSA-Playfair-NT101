using System.Numerics;
using System.Text;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Mã hoá và giải mã văn bản bằng RSA theo từng block.
/// </summary>
/// <remarks>
/// <para>
/// Đây là RSA "sách giáo khoa" (textbook RSA): <em>không có padding</em>. Bản mã
/// được tính trực tiếp <c>c = m^e mod n</c>. Cách này đúng về toán học và dễ
/// giảng, nhưng <strong>không an toàn</strong> để dùng thật, vì nó có tính tất
/// định: cùng một bản rõ với cùng một khoá luôn cho ra cùng một bản mã, nên kẻ
/// tấn công có thể nhận ra các bản tin lặp lại hoặc dò từ điển với các bản tin
/// ngắn. Chuẩn thực tế (PKCS#1 v1.5 hoặc OAEP) thêm phần đệm ngẫu nhiên để phá
/// tính chất này.
/// </para>
/// <para>
/// Định dạng bản mã tự định nghĩa, mã hoá Base64:
/// <c>[4 byte độ dài bản rõ, big-endian][các block bản mã, mỗi block cố định
/// CipherBlockBytes byte]</c>.
/// </para>
/// </remarks>
public static class RsaCipher
{
    /// <summary>Số byte của phần header chứa độ dài bản rõ.</summary>
    private const int LengthHeaderBytes = 4;

    /// <summary>
    /// Mã hoá văn bản, trả về chuỗi Base64.
    /// </summary>
    public static string EncryptText(string plainText, BigInteger n, BigInteger e)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        return Convert.ToBase64String(EncryptToBytes(plainText, n, e));
    }

    /// <summary>
    /// Mã hoá văn bản, trả về mảng byte thô theo định dạng container.
    /// </summary>
    public static byte[] EncryptToBytes(string plainText, BigInteger n, BigInteger e)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        int plainBlockBytes = GetPlainBlockBytes(n);
        int cipherBlockBytes = GetCipherBlockBytes(n);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        int blockCount = (plainBytes.Length + plainBlockBytes - 1) / plainBlockBytes;

        byte[] output = new byte[LengthHeaderBytes + blockCount * cipherBlockBytes];
        WriteLengthHeader(output, plainBytes.Length);

        byte[] blockBuffer = new byte[plainBlockBytes];

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            // Block cuối được đệm 0 ở phía sau cho đủ plainBlockBytes byte. Nhờ
            // header lưu độ dài thật, phần đệm này bị cắt chính xác khi giải mã.
            // Nếu không đệm, việc bù 0 phía trước lúc giải mã sẽ chèn byte 0 vào
            // sai vị trí và làm hỏng bản rõ.
            Array.Clear(blockBuffer);
            int sourceOffset = blockIndex * plainBlockBytes;
            int byteCount = Math.Min(plainBlockBytes, plainBytes.Length - sourceOffset);
            Array.Copy(plainBytes, sourceOffset, blockBuffer, 0, byteCount);

            BigInteger m = new(blockBuffer, isUnsigned: true, isBigEndian: true);
            BigInteger c = BigInteger.ModPow(m, e, n);

            WriteFixedWidth(c, output, LengthHeaderBytes + blockIndex * cipherBlockBytes, cipherBlockBytes);
        }

        return output;
    }

    /// <summary>
    /// Giải mã chuỗi Base64 về văn bản gốc.
    /// </summary>
    /// <exception cref="FormatException">
    /// Khi chuỗi không phải Base64 hợp lệ, hoặc cấu trúc container không khớp với
    /// độ dài khoá.
    /// </exception>
    public static string DecryptText(string base64CipherText, BigInteger n, BigInteger d)
    {
        ArgumentNullException.ThrowIfNull(base64CipherText);

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(base64CipherText.Trim());
        }
        catch (FormatException ex)
        {
            throw new FormatException(
                "Bản mã không phải chuỗi Base64 hợp lệ. Hãy dán lại đúng chuỗi đã được sinh ra khi mã hoá.",
                ex);
        }

        return DecryptFromBytes(cipherBytes, n, d);
    }

    /// <summary>
    /// Giải mã mảng byte theo định dạng container về văn bản gốc.
    /// </summary>
    public static string DecryptFromBytes(byte[] cipherBytes, BigInteger n, BigInteger d)
    {
        ArgumentNullException.ThrowIfNull(cipherBytes);

        int plainBlockBytes = GetPlainBlockBytes(n);
        int cipherBlockBytes = GetCipherBlockBytes(n);

        if (cipherBytes.Length < LengthHeaderBytes)
        {
            throw new FormatException(
                $"Bản mã quá ngắn: cần ít nhất {LengthHeaderBytes} byte header, chỉ có {cipherBytes.Length} byte.");
        }

        int plainLength = ReadLengthHeader(cipherBytes);
        int payloadLength = cipherBytes.Length - LengthHeaderBytes;

        if (payloadLength % cipherBlockBytes != 0)
        {
            throw new FormatException(
                $"Bản mã hỏng hoặc không khớp khoá: phần dữ liệu {payloadLength} byte "
                + $"không chia hết cho kích thước block {cipherBlockBytes} byte.");
        }

        int blockCount = payloadLength / cipherBlockBytes;
        int expectedBlockCount = (plainLength + plainBlockBytes - 1) / plainBlockBytes;

        if (blockCount != expectedBlockCount)
        {
            throw new FormatException(
                $"Bản mã hỏng: header khai báo bản rõ {plainLength} byte (cần {expectedBlockCount} block) "
                + $"nhưng dữ liệu chứa {blockCount} block.");
        }

        byte[] plainBytes = new byte[blockCount * plainBlockBytes];

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            BigInteger c = ReadFixedWidth(cipherBytes, LengthHeaderBytes + blockIndex * cipherBlockBytes, cipherBlockBytes);
            BigInteger m = BigInteger.ModPow(c, d, n);

            // m chỉ được đảm bảo nhỏ hơn n, mà block bản rõ hẹp hơn n (chỉ
            // PlainBlockBytes byte). Khi giải mã bằng khoá sai hoặc bản mã bị sửa,
            // m có thể vượt khỏi độ rộng đó. Đây là lỗi dữ liệu vào, không phải
            // lỗi logic, nên phải báo bằng FormatException với thông báo hiểu được.
            if (m.GetByteCount(isUnsigned: true) > plainBlockBytes)
            {
                throw new FormatException(
                    $"Giải mã thất bại ở block {blockIndex + 1}: giá trị thu được vượt quá "
                    + $"{plainBlockBytes} byte cho phép mỗi block. Nguyên nhân thường gặp là "
                    + "dùng sai khoá riêng, hoặc bản mã đã bị sửa đổi.");
            }

            // Bắt buộc bù 0 phía trước cho đủ plainBlockBytes byte. BigInteger
            // không giữ byte 0 dẫn đầu, nên một block bản rõ bắt đầu bằng 0x00 sẽ
            // trả về mảng ngắn hơn; ghép thẳng sẽ làm lệch toàn bộ phần sau.
            WriteFixedWidth(m, plainBytes, blockIndex * plainBlockBytes, plainBlockBytes);
        }

        if (plainLength > plainBytes.Length)
        {
            throw new FormatException(
                $"Bản mã hỏng: header khai báo {plainLength} byte nhưng chỉ giải ra được {plainBytes.Length} byte.");
        }

        return Encoding.UTF8.GetString(plainBytes, 0, plainLength);
    }

    /// <summary>
    /// Mã hoá và trả về vết từng block để hiển thị: bản rõ, giá trị số
    /// <c>m</c>, và bản mã <c>c</c>.
    /// </summary>
    /// <param name="maxBlocks">
    /// Số block tối đa được ghi vết. Văn bản dài với khoá nhỏ sinh ra rất nhiều
    /// block, hiển thị hết là vô nghĩa.
    /// </param>
    public static IReadOnlyList<RsaBlockTrace> TraceEncrypt(
        string plainText, RsaKeyPair key, int maxBlocks = 64)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(key);

        if (maxBlocks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBlocks), maxBlocks, "maxBlocks phải ít nhất là 1.");
        }

        int plainBlockBytes = GetPlainBlockBytes(key.N);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        int blockCount = (plainBytes.Length + plainBlockBytes - 1) / plainBlockBytes;
        int traceCount = Math.Min(blockCount, maxBlocks);

        List<RsaBlockTrace> traces = new(traceCount);
        byte[] blockBuffer = new byte[plainBlockBytes];

        for (int blockIndex = 0; blockIndex < traceCount; blockIndex++)
        {
            Array.Clear(blockBuffer);
            int sourceOffset = blockIndex * plainBlockBytes;
            int byteCount = Math.Min(plainBlockBytes, plainBytes.Length - sourceOffset);
            Array.Copy(plainBytes, sourceOffset, blockBuffer, 0, byteCount);

            BigInteger m = new(blockBuffer, isUnsigned: true, isBigEndian: true);
            BigInteger c = BigInteger.ModPow(m, key.E, key.N);

            byte[] significantBytes = new byte[byteCount];
            Array.Copy(blockBuffer, significantBytes, byteCount);

            traces.Add(new RsaBlockTrace(
                blockIndex + 1,
                significantBytes,
                DescribeBytes(significantBytes),
                m,
                c));
        }

        return traces;
    }

    /// <summary>
    /// Số byte bản rõ mỗi block ứng với modulus <paramref name="n"/>, kèm kiểm
    /// tra khoá có đủ lớn để mã hoá văn bản.
    /// </summary>
    private static int GetPlainBlockBytes(BigInteger n)
    {
        if (n < 2)
        {
            throw new ArgumentException($"Modulus n phải lớn hơn 1, nhận được {n}.", nameof(n));
        }

        int plainBlockBytes = ((int)n.GetBitLength() - 1) / 8;

        if (plainBlockBytes < 1)
        {
            throw new ArgumentException(
                $"Khoá quá nhỏ để mã hoá văn bản: n = {n} chỉ có {n.GetBitLength()} bit, "
                + "cần ít nhất 9 bit (n ≥ 256) để chứa được 1 byte mỗi block.",
                nameof(n));
        }

        return plainBlockBytes;
    }

    /// <summary>Số byte cố định mỗi block bản mã ứng với modulus <paramref name="n"/>.</summary>
    private static int GetCipherBlockBytes(BigInteger n) => ((int)n.GetBitLength() + 7) / 8;

    /// <summary>
    /// Ghi một <see cref="BigInteger"/> không dấu vào mảng đích với độ rộng cố
    /// định, big-endian, bù 0 phía trước.
    /// </summary>
    private static void WriteFixedWidth(BigInteger value, byte[] destination, int offset, int width)
    {
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (bytes.Length > width)
        {
            throw new InvalidOperationException(
                $"Giá trị cần {bytes.Length} byte nhưng chỗ chứa chỉ có {width} byte. "
                + "Đây là lỗi logic tính kích thước block, không phải lỗi dữ liệu vào.");
        }

        Array.Clear(destination, offset, width);
        Array.Copy(bytes, 0, destination, offset + width - bytes.Length, bytes.Length);
    }

    /// <summary>Đọc một <see cref="BigInteger"/> không dấu big-endian có độ rộng cố định.</summary>
    private static BigInteger ReadFixedWidth(byte[] source, int offset, int width)
    {
        return new BigInteger(
            new ReadOnlySpan<byte>(source, offset, width),
            isUnsigned: true,
            isBigEndian: true);
    }

    /// <summary>Ghi độ dài bản rõ vào header dưới dạng big-endian 4 byte.</summary>
    private static void WriteLengthHeader(byte[] destination, int length)
    {
        destination[0] = (byte)(length >> 24);
        destination[1] = (byte)(length >> 16);
        destination[2] = (byte)(length >> 8);
        destination[3] = (byte)length;
    }

    /// <summary>Đọc độ dài bản rõ từ header.</summary>
    private static int ReadLengthHeader(byte[] source)
    {
        int length = (source[0] << 24) | (source[1] << 16) | (source[2] << 8) | source[3];

        if (length < 0)
        {
            throw new FormatException($"Header khai báo độ dài không hợp lệ: {length}.");
        }

        return length;
    }

    /// <summary>
    /// Diễn giải một block byte thành chuỗi đọc được: ưu tiên chữ nếu là UTF-8
    /// hợp lệ, nếu không thì hiện dạng hex. Một block thường cắt giữa một ký tự
    /// nhiều byte, nên trường hợp hex là bình thường, không phải lỗi.
    /// </summary>
    private static string DescribeBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return "(rỗng)";
        }

        try
        {
            string text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);

            if (!text.Any(char.IsControl))
            {
                return text;
            }
        }
        catch (DecoderFallbackException)
        {
            // Block cắt giữa ký tự nhiều byte: bình thường, chuyển sang hiện hex.
        }

        return "0x" + Convert.ToHexString(bytes);
    }
}
