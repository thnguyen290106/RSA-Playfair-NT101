using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Kết quả ký một bản tin, kèm giá trị băm để hiển thị.
/// </summary>
/// <param name="Signature">Chữ ký <c>s = H^d mod n</c>.</param>
/// <param name="HashHex">Giá trị băm SHA-256 dạng hex.</param>
/// <param name="HashValue">Giá trị băm dưới dạng số nguyên.</param>
public sealed record RsaSignatureResult(
    BigInteger Signature,
    string HashHex,
    BigInteger HashValue);

/// <summary>
/// Kết quả kiểm tra chữ ký, kèm hai giá trị băm để người dùng thấy chúng khớp
/// hay lệch nhau ở đâu.
/// </summary>
/// <param name="IsValid">Chữ ký có hợp lệ hay không.</param>
/// <param name="ExpectedHashHex">Băm tính lại từ bản tin hiện tại.</param>
/// <param name="RecoveredHashHex">Băm phục hồi từ chữ ký bằng khoá công khai.</param>
public sealed record RsaVerificationResult(
    bool IsValid,
    string ExpectedHashHex,
    string RecoveredHashHex);

/// <summary>
/// Chữ ký số RSA: băm bản tin bằng SHA-256 rồi ký giá trị băm bằng khoá riêng.
/// Kiểm tra bằng khoá công khai.
/// </summary>
/// <remarks>
/// <para>
/// Vì sao phải băm trước khi ký? Ba lý do: bản tin dài hơn <c>n</c> thì không ký
/// trực tiếp được; băm cho độ dài cố định nên chỉ cần một phép luỹ thừa; và băm
/// làm chữ ký phụ thuộc toàn bộ nội dung, sửa một ký tự là giá trị băm đổi hoàn
/// toàn.
/// </para>
/// <para>
/// Đây là chữ ký không có padding, giống <see cref="RsaCipher"/>. Chuẩn thật
/// (PKCS#1 v1.5, PSS) bọc giá trị băm trong một cấu trúc có định dạng trước khi
/// ký. Không có bước đó, cách ký này minh hoạ đúng nguyên lý nhưng không đạt
/// chuẩn để dùng thật.
/// </para>
/// </remarks>
public static class RsaSignature
{
    /// <summary>
    /// Độ dài khoá tối thiểu để ký được SHA-256.
    /// </summary>
    /// <remarks>
    /// Giá trị băm SHA-256 là số 256 bit, bắt buộc phải nhỏ hơn <c>n</c>. Mốc 512
    /// bit vừa thoả điều kiện đó vừa để dư khoảng an toàn. Không được lách bằng
    /// cách lấy <c>hash mod n</c>: khi đó nhiều bản tin khác nhau cho cùng giá
    /// trị ký, và chữ ký mất hoàn toàn ý nghĩa.
    /// </remarks>
    public const int MinimumKeySizeBitsForSigning = 512;

    /// <summary>
    /// Ký bản tin bằng khoá riêng.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Khi khoá nhỏ hơn <see cref="MinimumKeySizeBitsForSigning"/> bit.
    /// </exception>
    public static RsaSignatureResult Sign(string message, RsaKeyPair key)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(key);

        EnsureKeyLargeEnough(key.KeySizeBits);

        byte[] hash = ComputeHash(message);
        BigInteger hashValue = ToBigInteger(hash);
        BigInteger signature = BigInteger.ModPow(hashValue, key.D, key.N);

        return new RsaSignatureResult(signature, Convert.ToHexString(hash), hashValue);
    }

    /// <summary>
    /// Kiểm tra chữ ký bằng khoá công khai.
    /// </summary>
    /// <remarks>
    /// Không ném ngoại lệ khi chữ ký sai — chữ ký sai là một kết quả bình thường,
    /// không phải lỗi. Chỉ ném khi tham số khoá không dùng được.
    /// </remarks>
    public static RsaVerificationResult Verify(
        string message, BigInteger signature, BigInteger n, BigInteger e)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (n < 2)
        {
            throw new ArgumentException($"Modulus n phải lớn hơn 1, nhận được {n}.", nameof(n));
        }

        EnsureKeyLargeEnough((int)n.GetBitLength());

        byte[] expectedHash = ComputeHash(message);
        string expectedHex = Convert.ToHexString(expectedHash);

        // Chữ ký nằm ngoài [0, n) là sai cấu trúc, không cần tính tiếp.
        if (signature.Sign < 0 || signature >= n)
        {
            return new RsaVerificationResult(false, expectedHex, "(chữ ký nằm ngoài khoảng hợp lệ [0, n))");
        }

        BigInteger recovered = BigInteger.ModPow(signature, e, n);
        BigInteger expectedValue = ToBigInteger(expectedHash);

        // So sánh trên số nguyên, không so trên chuỗi hex: giá trị băm phục hồi
        // có thể có ít byte hơn nếu byte đầu bằng 0, nên độ dài hex khác nhau
        // trong khi giá trị vẫn khớp.
        bool isValid = recovered == expectedValue;

        string recoveredHex = Convert.ToHexString(
            recovered.ToByteArray(isUnsigned: true, isBigEndian: true));

        return new RsaVerificationResult(isValid, expectedHex, recoveredHex);
    }

    /// <summary>Băm SHA-256 nội dung UTF-8 của bản tin.</summary>
    public static byte[] ComputeHash(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SHA256.HashData(Encoding.UTF8.GetBytes(message));
    }

    /// <summary>
    /// Chuyển mảng byte băm thành số nguyên không dấu, big-endian.
    /// </summary>
    /// <remarks>
    /// Phải chỉ rõ <c>isUnsigned: true</c>. Nếu không, byte đầu ≥ 0x80 sẽ được
    /// hiểu là dấu âm và giá trị băm thành số âm — luỹ thừa modulo của số âm cho
    /// kết quả khác, và chữ ký sẽ không bao giờ kiểm tra được.
    /// </remarks>
    private static BigInteger ToBigInteger(byte[] hash)
        => new(hash, isUnsigned: true, isBigEndian: true);

    private static void EnsureKeyLargeEnough(int keySizeBits)
    {
        if (keySizeBits < MinimumKeySizeBitsForSigning)
        {
            throw new InvalidOperationException(
                $"Khoá {keySizeBits} bit quá nhỏ để ký. Giá trị băm SHA-256 là số 256 bit và bắt buộc "
                + $"phải nhỏ hơn n, nên cần khoá ít nhất {MinimumKeySizeBitsForSigning} bit. "
                + "Hãy chuyển sang chế độ tự động và sinh khoá 512 bit trở lên.");
        }
    }
}
