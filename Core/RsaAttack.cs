using System.Diagnostics;
using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Kết quả phân tích modulus thành hai thừa số nguyên tố.
/// </summary>
/// <param name="P">Thừa số nhỏ hơn.</param>
/// <param name="Q">Thừa số lớn hơn.</param>
/// <param name="Iterations">Số phép chia đã thử.</param>
/// <param name="Elapsed">Thời gian đã chạy.</param>
/// <param name="RecoveredPrivateExponent">
/// Khoá riêng <c>d</c> tính lại được từ <c>p</c>, <c>q</c> và <c>e</c>. Đây là
/// bằng chứng cụ thể nhất cho việc phân tích được <c>n</c> nghĩa là mất khoá.
/// </param>
public sealed record FactorResult(
    BigInteger P,
    BigInteger Q,
    long Iterations,
    TimeSpan Elapsed,
    BigInteger? RecoveredPrivateExponent);

/// <summary>
/// Tấn công RSA bằng cách phân tích modulus. Mục đích là chứng minh khoá ngắn
/// không an toàn: phân tích được <c>n</c> ra <c>p·q</c> là tính lại được
/// <c>φ(n)</c>, từ đó suy ra khoá riêng <c>d</c>.
/// </summary>
/// <remarks>
/// Dùng phép chia thử tới căn bậc hai của <c>n</c>. Đây là thuật toán đơn giản
/// nhất và cũng chậm nhất, đúng với mục đích minh hoạ: số phép thử tăng theo
/// <c>√n</c>, nên mỗi lần thêm 2 bit vào khoá là gấp đôi công sức phá. Các thuật
/// toán thật (Pollard rho, đường cong elliptic, sàng bậc hai) nhanh hơn nhiều
/// nhưng vẫn không đủ để phá khoá 2048 bit.
/// </remarks>
public static class RsaAttack
{
    /// <summary>
    /// Độ dài modulus tối đa cho phép thử phân tích.
    /// </summary>
    /// <remarks>
    /// 64 bit đã cần tới cỡ 2^32 phép chia, mất nhiều phút. Chặn cứng ở đây để
    /// người dùng không thể vô tình bắt ứng dụng chạy vô hạn; giới hạn này là
    /// một phần của bài học, không phải một hạn chế kỹ thuật che giấu.
    /// </remarks>
    public const int MaxFactorableBits = 64;

    /// <summary>
    /// Phân tích <paramref name="n"/> thành hai thừa số, chạy trên thread nền.
    /// Trả về <c>null</c> khi không tìm được thừa số nào, tức <paramref name="n"/>
    /// là số nguyên tố.
    /// </summary>
    /// <param name="publicExponent">
    /// Số mũ công khai <c>e</c>. Khi có giá trị này, hàm tính luôn khoá riêng
    /// <c>d</c> để cho thấy hậu quả trực tiếp.
    /// </param>
    /// <exception cref="OperationCanceledException">Khi người dùng huỷ.</exception>
    public static Task<FactorResult?> FactorAsync(
        BigInteger n,
        BigInteger? publicExponent = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Factor(n, publicExponent, progress, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Phần thực thi đồng bộ của phép phân tích. Tách riêng để test gọi trực tiếp.
    /// </summary>
    public static FactorResult? Factor(
        BigInteger n,
        BigInteger? publicExponent = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (n < 4)
        {
            throw new ArgumentException(
                $"n = {n} quá nhỏ để phân tích thành hai thừa số ≥ 2.", nameof(n));
        }

        if (n.GetBitLength() > MaxFactorableBits)
        {
            throw new ArgumentException(
                $"n có {n.GetBitLength()} bit, vượt giới hạn {MaxFactorableBits} bit. "
                + "Phép chia thử sẽ chạy quá lâu — đó chính là lý do RSA an toàn khi khoá đủ dài.",
                nameof(n));
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        long iterations = 0;

        // Xử lý riêng thừa số 2, để vòng lặp chính chỉ cần bước qua số lẻ.
        if (n.IsEven)
        {
            stopwatch.Stop();
            return BuildResult(2, n / 2, 1, stopwatch.Elapsed, publicExponent);
        }

        BigInteger limit = BigIntegerMath.Sqrt(n);

        for (BigInteger candidate = 3; candidate <= limit; candidate += 2)
        {
            iterations++;

            // Kiểm tra huỷ và báo tiến trình định kỳ. Làm mỗi vòng lặp sẽ chậm
            // hơn hẳn phần việc chính.
            if ((iterations & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(iterations);
            }

            if (!(n % candidate).IsZero)
            {
                continue;
            }

            stopwatch.Stop();
            return BuildResult(candidate, n / candidate, iterations, stopwatch.Elapsed, publicExponent);
        }

        stopwatch.Stop();
        progress?.Report(iterations);
        return null;
    }

    /// <summary>
    /// Dựng kết quả, sắp thừa số nhỏ trước và tính lại khoá riêng nếu có
    /// <paramref name="publicExponent"/>.
    /// </summary>
    private static FactorResult BuildResult(
        BigInteger a, BigInteger b, long iterations, TimeSpan elapsed, BigInteger? publicExponent)
    {
        (BigInteger p, BigInteger q) = a <= b ? (a, b) : (b, a);

        BigInteger? recoveredD = null;
        if (publicExponent is { } e)
        {
            BigInteger phi = (p - BigInteger.One) * (q - BigInteger.One);

            // Chỉ tính được d khi e khả nghịch modulo φ(n). Với n là tích của hai
            // số nguyên tố và e hợp lệ thì luôn được, nhưng n có thể là hợp số
            // dạng khác nếu người dùng tự nhập.
            if (phi > BigInteger.One && BigInteger.GreatestCommonDivisor(e, phi) == BigInteger.One)
            {
                recoveredD = BigIntegerMath.ModInverse(e, phi);
            }
        }

        return new FactorResult(p, q, iterations, elapsed, recoveredD);
    }
}
