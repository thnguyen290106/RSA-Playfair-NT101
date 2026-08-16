using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Một bước trong phép luỹ thừa modulo theo thuật toán bình phương-và-nhân
/// (square-and-multiply). Mỗi bước ứng với đúng một bit của số mũ, quét từ bit
/// cao nhất xuống bit 0.
/// </summary>
/// <param name="StepIndex">Số thứ tự bước, bắt đầu từ 1.</param>
/// <param name="BitIndex">Vị trí bit trong số mũ (bit cao nhất trước).</param>
/// <param name="Bit">Giá trị bit: 0 hoặc 1.</param>
/// <param name="AfterSquare">Giá trị tích luỹ sau khi bình phương.</param>
/// <param name="AfterMultiply">
/// Giá trị sau khi nhân thêm cơ số; <c>null</c> khi bit = 0 (không nhân).
/// </param>
/// <param name="Accumulator">Giá trị tích luỹ khi kết thúc bước này.</param>
public sealed record ModPowStep(
    int StepIndex,
    int BitIndex,
    int Bit,
    BigInteger AfterSquare,
    BigInteger? AfterMultiply,
    BigInteger Accumulator);

/// <summary>
/// Kết quả luỹ thừa modulo kèm vết tính toán từng bước.
/// </summary>
/// <param name="Value">Kết quả cuối cùng, luôn đầy đủ và chính xác.</param>
/// <param name="TotalBits">Tổng số bit của số mũ, tức tổng số bước thật sự.</param>
/// <param name="Truncated">
/// <c>true</c> khi <see cref="Steps"/> chỉ chứa phần đầu và phần cuối vì số bước
/// vượt giới hạn hiển thị.
/// </param>
public sealed record ModPowTrace(
    BigInteger Value,
    int TotalBits,
    bool Truncated,
    IReadOnlyList<ModPowStep> Steps);

/// <summary>
/// Các phép toán số học trên <see cref="BigInteger"/> mà RSA cần: thuật toán
/// Euclid mở rộng, nghịch đảo modulo, luỹ thừa modulo có vết, và căn bậc hai
/// nguyên. Toàn bộ tự cài đặt, không dùng thư viện mật mã có sẵn.
/// </summary>
public static class BigIntegerMath
{
    /// <summary>
    /// Thuật toán Euclid mở rộng: trả về <c>(g, x, y)</c> sao cho
    /// <c>a·x + b·y = g</c> với <c>g = gcd(a, b)</c>.
    /// </summary>
    public static (BigInteger G, BigInteger X, BigInteger Y) ExtendedGcd(BigInteger a, BigInteger b)
    {
        BigInteger oldR = a, r = b;
        BigInteger oldS = BigInteger.One, s = BigInteger.Zero;
        BigInteger oldT = BigInteger.Zero, t = BigInteger.One;

        while (!r.IsZero)
        {
            BigInteger quotient = oldR / r;
            (oldR, r) = (r, oldR - quotient * r);
            (oldS, s) = (s, oldS - quotient * s);
            (oldT, t) = (t, oldT - quotient * t);
        }

        return (oldR, oldS, oldT);
    }

    /// <summary>
    /// Nghịch đảo modulo: tìm <c>x</c> sao cho <c>a·x ≡ 1 (mod m)</c>.
    /// Kết quả luôn nằm trong khoảng <c>[0, m)</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Khi <paramref name="m"/> không lớn hơn 1, hoặc <c>gcd(a, m) ≠ 1</c> nên
    /// nghịch đảo không tồn tại.
    /// </exception>
    public static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        if (m <= BigInteger.One)
        {
            throw new ArgumentException($"Modulus phải lớn hơn 1, nhận được {m}.", nameof(m));
        }

        // Đưa a về khoảng [0, m) trước, vì a có thể âm.
        BigInteger normalized = ((a % m) + m) % m;
        (BigInteger g, BigInteger x, _) = ExtendedGcd(normalized, m);

        if (g != BigInteger.One)
        {
            throw new ArgumentException(
                $"Không tồn tại nghịch đảo modulo: gcd({a}, {m}) = {g}, phải bằng 1.",
                nameof(a));
        }

        // x của Euclid mở rộng có thể âm, phải chuẩn hoá về [0, m).
        return ((x % m) + m) % m;
    }

    /// <summary>
    /// Luỹ thừa modulo bằng thuật toán bình phương-và-nhân, có ghi lại vết từng
    /// bước để hiển thị. Kết quả trả về luôn trùng với
    /// <see cref="BigInteger.ModPow"/>; hàm này tồn tại vì cần vết tính toán,
    /// còn đường chạy thật của mã hoá dùng trực tiếp <c>BigInteger.ModPow</c>
    /// cho nhanh.
    /// </summary>
    /// <param name="maxSteps">
    /// Số bước tối đa được ghi lại. Số mũ 1024 bit sinh ra hơn 1000 bước, không
    /// thể hiển thị hết, nên chỉ giữ nửa đầu và nửa cuối.
    /// </param>
    public static ModPowTrace TracedModPow(
        BigInteger value,
        BigInteger exponent,
        BigInteger modulus,
        int maxSteps = 256)
    {
        if (modulus <= BigInteger.One)
        {
            throw new ArgumentException($"Modulus phải lớn hơn 1, nhận được {modulus}.", nameof(modulus));
        }

        if (exponent.Sign < 0)
        {
            throw new ArgumentException($"Số mũ không được âm, nhận được {exponent}.", nameof(exponent));
        }

        if (maxSteps < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), maxSteps, "maxSteps phải ít nhất là 2.");
        }

        BigInteger baseValue = ((value % modulus) + modulus) % modulus;

        if (exponent.IsZero)
        {
            // Quy ước: x^0 = 1, và 1 mod modulus với modulus > 1 vẫn là 1.
            return new ModPowTrace(BigInteger.One % modulus, 0, false, []);
        }

        int totalBits = (int)exponent.GetBitLength();
        int headCount = maxSteps / 2;
        int tailCount = maxSteps - headCount;
        bool truncated = totalBits > maxSteps;

        List<ModPowStep> steps = new(truncated ? maxSteps : totalBits);
        BigInteger accumulator = BigInteger.One;
        int stepIndex = 0;

        for (int bitIndex = totalBits - 1; bitIndex >= 0; bitIndex--)
        {
            int bit = (int)((exponent >> bitIndex) & BigInteger.One);

            BigInteger afterSquare = (accumulator * accumulator) % modulus;
            accumulator = afterSquare;

            BigInteger? afterMultiply = null;
            if (bit == 1)
            {
                accumulator = (accumulator * baseValue) % modulus;
                afterMultiply = accumulator;
            }

            stepIndex++;

            // Giữ nửa đầu và nửa cuối; phần giữa bị bỏ khi số bước quá lớn.
            bool keep = !truncated || stepIndex <= headCount || stepIndex > totalBits - tailCount;
            if (keep)
            {
                steps.Add(new ModPowStep(stepIndex, bitIndex, bit, afterSquare, afterMultiply, accumulator));
            }
        }

        return new ModPowTrace(accumulator, totalBits, truncated, steps);
    }

    /// <summary>
    /// Căn bậc hai nguyên (làm tròn xuống) bằng phương pháp Newton.
    /// <see cref="BigInteger"/> không có sẵn phép này.
    /// </summary>
    public static BigInteger Sqrt(BigInteger n)
    {
        if (n.Sign < 0)
        {
            throw new ArgumentException($"Không lấy căn bậc hai của số âm: {n}.", nameof(n));
        }

        if (n.IsZero)
        {
            return BigInteger.Zero;
        }

        // Chặn trên: 2^ceil(bitLength/2) luôn ≥ sqrt(n).
        BigInteger x = BigInteger.One << (((int)n.GetBitLength() + 1) / 2);

        while (true)
        {
            BigInteger next = (x + n / x) >> 1;
            if (next >= x)
            {
                return x;
            }

            x = next;
        }
    }
}
