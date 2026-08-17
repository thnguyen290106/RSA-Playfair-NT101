using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Tạo cặp khoá RSA: từ hai số nguyên tố cho trước (chế độ thủ công, để nhìn rõ
/// toán học) hoặc sinh ngẫu nhiên theo độ dài bit (chế độ tự động).
/// </summary>
public static class RsaKeyFactory
{
    /// <summary>
    /// Số mũ công khai mặc định. 65537 = 2^16 + 1, chỉ có 2 bit bằng 1 nên luỹ
    /// thừa rất nhanh, và đủ lớn để tránh các tấn công vào số mũ nhỏ.
    /// </summary>
    public static readonly BigInteger DefaultPublicExponent = 65537;

    /// <summary>
    /// Các độ dài khoá cho phép ở chế độ tự động.
    /// </summary>
    public static readonly int[] SupportedKeySizes = [512, 1024, 2048];

    /// <summary>
    /// Dựng cặp khoá từ hai số nguyên tố cho trước.
    /// </summary>
    /// <param name="p">Số nguyên tố thứ nhất.</param>
    /// <param name="q">Số nguyên tố thứ hai, phải khác <paramref name="p"/>.</param>
    /// <param name="e">
    /// Số mũ công khai. Để <c>null</c> thì hàm tự chọn: ưu tiên 65537, nếu 65537
    /// không dùng được (quá lớn hoặc không nguyên tố cùng nhau với φ(n)) thì
    /// quét số lẻ nhỏ nhất từ 3 lên.
    /// </param>
    /// <param name="verifyPrimes">
    /// Có kiểm tra <paramref name="p"/>, <paramref name="q"/> là số nguyên tố
    /// hay không. Chế độ thủ công nên bật để báo lỗi sớm cho người dùng.
    /// </param>
    public static RsaKeyPair FromPrimes(
        BigInteger p,
        BigInteger q,
        BigInteger? e = null,
        bool verifyPrimes = true)
    {
        if (p < 2)
        {
            throw new ArgumentException($"p phải là số nguyên tố ≥ 2, nhận được {p}.", nameof(p));
        }

        if (q < 2)
        {
            throw new ArgumentException($"q phải là số nguyên tố ≥ 2, nhận được {q}.", nameof(q));
        }

        if (p == q)
        {
            // n = p² thì φ(n) = p(p-1), không phải (p-1)², và biết n là tính được
            // ngay p bằng cách lấy căn bậc hai. Khoá vô dụng.
            throw new ArgumentException("p và q phải khác nhau, nếu không khoá bị phá ngay bằng phép lấy căn.", nameof(q));
        }

        if (verifyPrimes)
        {
            if (!PrimeGenerator.IsProbablePrime(p))
            {
                throw new ArgumentException($"p = {p} không phải số nguyên tố.", nameof(p));
            }

            if (!PrimeGenerator.IsProbablePrime(q))
            {
                throw new ArgumentException($"q = {q} không phải số nguyên tố.", nameof(q));
            }
        }

        BigInteger n = p * q;
        BigInteger phi = (p - BigInteger.One) * (q - BigInteger.One);
        BigInteger lambda = Lcm(p - BigInteger.One, q - BigInteger.One);

        BigInteger publicExponent = e ?? ChoosePublicExponent(phi);

        if (publicExponent <= BigInteger.One || publicExponent >= phi)
        {
            throw new ArgumentException(
                $"e = {publicExponent} phải nằm trong khoảng (1, φ(n)) với φ(n) = {phi}.", nameof(e));
        }

        if (BigInteger.GreatestCommonDivisor(publicExponent, phi) != BigInteger.One)
        {
            throw new ArgumentException(
                $"e = {publicExponent} phải nguyên tố cùng nhau với φ(n) = {phi}, "
                + $"nhưng gcd = {BigInteger.GreatestCommonDivisor(publicExponent, phi)}.", nameof(e));
        }

        BigInteger d = BigIntegerMath.ModInverse(publicExponent, phi);

        // Kiểm tra lại ngay: nếu e·d mod φ(n) ≠ 1 thì toàn bộ khoá vô nghĩa.
        // Rẻ hơn nhiều so với việc phát hiện lỗi sau khi đã mã hoá.
        if ((publicExponent * d) % phi != BigInteger.One)
        {
            throw new InvalidOperationException(
                $"Sinh khoá thất bại: e·d mod φ(n) = {(publicExponent * d) % phi}, phải bằng 1.");
        }

        return new RsaKeyPair(n, publicExponent, d, p, q, phi, lambda);
    }

    /// <summary>
    /// Sinh cặp khoá ngẫu nhiên có modulus đúng <paramref name="keySizeBits"/>
    /// bit. Chạy trên thread nền vì với 2048 bit có thể mất nhiều giây.
    /// </summary>
    /// <param name="progress">
    /// Nhận thông báo tiến trình dạng chữ để hiển thị lên giao diện.
    /// </param>
    public static Task<RsaKeyPair> GenerateAsync(
        int keySizeBits,
        BigInteger? e = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (keySizeBits < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keySizeBits), keySizeBits, "Độ dài khoá phải ít nhất là 16 bit.");
        }

        return Task.Run(
            () => Generate(keySizeBits, e, progress, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Phần thực thi đồng bộ của việc sinh khoá. Tách riêng để test gọi được
    /// trực tiếp mà không qua <see cref="Task"/>.
    /// </summary>
    public static RsaKeyPair Generate(
        int keySizeBits,
        BigInteger? e = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Chia đôi độ dài bit. Với số bit lẻ, q nhận thêm 1 bit để tổng đúng bằng
        // keySizeBits.
        int pBits = keySizeBits / 2;
        int qBits = keySizeBits - pBits;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report($"Đang sinh số nguyên tố p ({pBits} bit)...");
            BigInteger p = PrimeGenerator.GenerateProbablePrime(
                pBits, PrimeGenerator.DefaultWitnessCount, cancellationToken);

            progress?.Report($"Đang sinh số nguyên tố q ({qBits} bit)...");
            BigInteger q;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                q = PrimeGenerator.GenerateProbablePrime(
                    qBits, PrimeGenerator.DefaultWitnessCount, cancellationToken);
            }
            while (q == p);

            BigInteger n = p * q;

            // Hai bit cao nhất của p và q đều được bật nên tích gần như luôn có
            // đúng keySizeBits bit. "Gần như" là chưa đủ khi độ dài khoá là một
            // cam kết với người dùng, nên kiểm tra và sinh lại nếu lệch.
            if (n.GetBitLength() != keySizeBits)
            {
                progress?.Report($"Modulus lệch {n.GetBitLength()} bit, đang sinh lại...");
                continue;
            }

            progress?.Report("Đang tính φ(n) và khoá riêng d...");

            // p, q đã được PrimeGenerator kiểm tra, không cần kiểm tra lại.
            return FromPrimes(p, q, e, verifyPrimes: false);
        }
    }

    /// <summary>
    /// Chọn số mũ công khai phù hợp với <paramref name="phi"/>.
    /// </summary>
    /// <remarks>
    /// Ưu tiên 65537. Nhưng với khoá nhỏ dùng để giảng dạy, ví dụ p=61, q=53 thì
    /// φ(n) = 3120 &lt; 65537, nên 65537 không dùng được. Khi đó quét số lẻ nhỏ
    /// nhất từ 3 lên tìm số nguyên tố cùng nhau với φ(n).
    /// </remarks>
    public static BigInteger ChoosePublicExponent(BigInteger phi)
    {
        if (phi <= 2)
        {
            throw new ArgumentException(
                $"φ(n) = {phi} quá nhỏ, không tồn tại e hợp lệ trong khoảng (1, φ(n)).", nameof(phi));
        }

        if (DefaultPublicExponent < phi
            && BigInteger.GreatestCommonDivisor(DefaultPublicExponent, phi) == BigInteger.One)
        {
            return DefaultPublicExponent;
        }

        for (BigInteger candidate = 3; candidate < phi; candidate += 2)
        {
            if (BigInteger.GreatestCommonDivisor(candidate, phi) == BigInteger.One)
            {
                return candidate;
            }
        }

        throw new ArgumentException(
            $"Không tìm được e hợp lệ cho φ(n) = {phi}.", nameof(phi));
    }

    /// <summary>Bội số chung nhỏ nhất, dùng để tính λ(n).</summary>
    private static BigInteger Lcm(BigInteger a, BigInteger b)
    {
        if (a.IsZero || b.IsZero)
        {
            return BigInteger.Zero;
        }

        return BigInteger.Abs(a / BigInteger.GreatestCommonDivisor(a, b) * b);
    }
}
