using System.Numerics;
using System.Security.Cryptography;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Kiểm tra và sinh số nguyên tố lớn cho RSA. Dùng kiểm tra Miller-Rabin, tự
/// cài đặt trên <see cref="BigInteger"/>; nguồn ngẫu nhiên là
/// <see cref="RandomNumberGenerator"/> (an toàn về mật mã) chứ không phải
/// <c>Random</c>.
/// </summary>
public static class PrimeGenerator
{
    /// <summary>
    /// Số nhân chứng (witness) mặc định cho Miller-Rabin. Mỗi nhân chứng độc
    /// lập giảm xác suất một hợp số bị nhận nhầm xuống dưới 1/4, nên 40 nhân
    /// chứng cho xác suất sai dưới 4^-40 — nhỏ hơn nhiều so với xác suất lỗi
    /// phần cứng.
    /// </summary>
    public const int DefaultWitnessCount = 40;

    /// <summary>
    /// Số nguyên tố nhỏ dùng để chia thử. Loại nhanh phần lớn ứng viên hợp số
    /// trước khi phải chạy Miller-Rabin (đắt hơn nhiều).
    /// </summary>
    private static readonly int[] SmallPrimes =
    [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71,
        73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151,
        157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233,
        239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317,
        331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419,
        421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503,
        509, 521, 523, 541
    ];

    /// <summary>
    /// Kiểm tra một số có phải số nguyên tố (theo xác suất) bằng Miller-Rabin.
    /// Trả về <c>false</c> là chắc chắn hợp số; trả về <c>true</c> là "gần như
    /// chắc chắn nguyên tố".
    /// </summary>
    /// <remarks>
    /// Thứ tự kiểm tra rất quan trọng: phải loại các trường hợp biên (n &lt; 2,
    /// n = 2, n = 3, n chẵn) trước khi vào vòng lặp chính, vì công thức
    /// n - 1 = 2^s·d và việc chọn nhân chứng trong [2, n-2] chỉ đúng khi n là
    /// số lẻ ≥ 5.
    /// </remarks>
    public static bool IsProbablePrime(BigInteger n, int witnessCount = DefaultWitnessCount)
    {
        if (witnessCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(witnessCount), witnessCount, "Số nhân chứng phải ít nhất là 1.");
        }

        if (n < 2)
        {
            return false;
        }

        // Chia thử: bắt được cả trường hợp n chính là một số nguyên tố nhỏ.
        foreach (int smallPrime in SmallPrimes)
        {
            if (n == smallPrime)
            {
                return true;
            }

            if ((n % smallPrime).IsZero)
            {
                return false;
            }
        }

        // Sau bước trên, n là số lẻ và lớn hơn mọi số nguyên tố trong bảng.
        // Phân tích n - 1 = 2^s · d với d lẻ.
        BigInteger nMinusOne = n - BigInteger.One;
        BigInteger d = nMinusOne;
        int s = 0;
        while (d.IsEven)
        {
            d >>= 1;
            s++;
        }

        for (int i = 0; i < witnessCount; i++)
        {
            BigInteger a = RandomInRange(2, nMinusOne - BigInteger.One);
            if (!IsWitnessForCompositeness(a, d, s, n, nMinusOne))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Một vòng Miller-Rabin với nhân chứng <paramref name="a"/>. Trả về
    /// <c>true</c> nghĩa là <paramref name="a"/> chứng minh
    /// <paramref name="n"/> là hợp số.
    /// </summary>
    private static bool IsWitnessForCompositeness(
        BigInteger a, BigInteger d, int s, BigInteger n, BigInteger nMinusOne)
    {
        BigInteger x = BigInteger.ModPow(a, d, n);

        if (x.IsOne || x == nMinusOne)
        {
            return false;
        }

        for (int r = 1; r < s; r++)
        {
            x = BigInteger.ModPow(x, 2, n);
            if (x == nMinusOne)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sinh một số nguyên tố (theo xác suất) có đúng
    /// <paramref name="bitLength"/> bit.
    /// </summary>
    /// <remarks>
    /// Hai bit cao nhất đều được bật, không chỉ bit cao nhất. Lý do: khi nhân
    /// hai số như vậy, tích luôn có đúng <c>2·bitLength</c> bit. Nếu chỉ bật bit
    /// cao nhất, tích của hai số nhỏ (mỗi số hơi lớn hơn 2^(k-1)) có thể chỉ có
    /// <c>2k-1</c> bit, khiến khoá bị ngắn hơn yêu cầu.
    /// </remarks>
    public static BigInteger GenerateProbablePrime(
        int bitLength,
        int witnessCount = DefaultWitnessCount,
        CancellationToken cancellationToken = default)
    {
        if (bitLength < 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitLength), bitLength, "Độ dài bit phải ít nhất là 8.");
        }

        int byteCount = (bitLength + 7) / 8;
        byte[] buffer = new byte[byteCount];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RandomNumberGenerator.Fill(buffer);

            // Dựng số không dấu, big-endian, rồi cắt về đúng bitLength bit.
            BigInteger candidate = new(buffer, isUnsigned: true, isBigEndian: true);
            candidate &= (BigInteger.One << bitLength) - BigInteger.One;

            // Bật bit cao nhất và bit cao thứ hai: đảm bảo đúng độ dài bit và
            // đảm bảo tích p·q có đúng 2·bitLength bit.
            candidate |= BigInteger.One << (bitLength - 1);
            candidate |= BigInteger.One << (bitLength - 2);

            // Bật bit thấp nhất: số nguyên tố > 2 luôn là số lẻ.
            candidate |= BigInteger.One;

            if (IsProbablePrime(candidate, witnessCount))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Số nguyên ngẫu nhiên trong khoảng đóng
    /// <c>[minInclusive, maxInclusive]</c>, lấy từ nguồn ngẫu nhiên an toàn.
    /// </summary>
    /// <remarks>
    /// Dùng vòng lặp loại bỏ (rejection sampling) thay vì lấy phần dư, để phân
    /// bố đều tuyệt đối — lấy phần dư sẽ làm lệch về phía các giá trị nhỏ.
    /// </remarks>
    private static BigInteger RandomInRange(BigInteger minInclusive, BigInteger maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentException(
                $"Khoảng không hợp lệ: [{minInclusive}, {maxInclusive}].", nameof(maxInclusive));
        }

        BigInteger range = maxInclusive - minInclusive;
        if (range.IsZero)
        {
            return minInclusive;
        }

        int bitLength = (int)range.GetBitLength();
        int byteCount = (bitLength + 7) / 8;
        byte[] buffer = new byte[byteCount];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            BigInteger value = new(buffer, isUnsigned: true, isBigEndian: true);
            value &= (BigInteger.One << bitLength) - BigInteger.One;

            if (value <= range)
            {
                return minInclusive + value;
            }
        }
    }
}
