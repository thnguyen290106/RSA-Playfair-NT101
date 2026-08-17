using System.Text;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Mã hoá và giải mã Playfair, kèm vết của từng cặp ký tự.
/// </summary>
/// <remarks>
/// Playfair mã hoá theo <em>cặp</em> ký tự (digram) chứ không theo từng ký tự, nên
/// nó che được tần suất chữ đơn — điểm mạnh so với mã Caesar. Nhưng nó vẫn là mã
/// cổ điển: tần suất cặp ký tự vẫn còn nguyên, và việc chuẩn hoá + chèn ký tự đệm
/// làm mất thông tin không lấy lại được. Ứng dụng nói thẳng cả hai điều đó qua
/// <see cref="PlayfairResult.Warnings"/> thay vì che.
/// </remarks>
public static class PlayfairCipher
{
    /// <summary>Mã hoá văn bản. Ký tự đệm được chèn khi cần và được ghi rõ trong kết quả.</summary>
    public static PlayfairResult Encrypt(string plain, string key, PlayfairVariant variant) =>
        Run(plain, key, variant, encrypting: true);

    /// <summary>
    /// Giải mã bản mã. Kết quả là bản rõ <em>thô</em>: ký tự đệm vẫn còn nguyên vì
    /// không có cách nào phân biệt ký tự đệm với ký tự thật.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Bản mã sau chuẩn hoá có số ký tự lẻ. Playfair mã hoá theo cặp nên bản mã thật
    /// luôn có số ký tự chẵn; đoán bừa một ký tự để cho chẵn là làm sai kết quả.
    /// </exception>
    public static PlayfairResult Decrypt(string cipher, string key, PlayfairVariant variant) =>
        Run(cipher, key, variant, encrypting: false);

    private static PlayfairResult Run(string text, string key, PlayfairVariant variant, bool encrypting)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(key);

        PlayfairMatrix matrix = PlayfairMatrix.Build(key, variant);
        string normalized = matrix.Normalize(text);
        List<string> warnings = [];

        if (matrix.NormalizedKey.Length == 0)
        {
            warnings.Add("Khoá không có ký tự nào dùng được, nên ma trận chỉ là bảng chữ theo thứ tự. "
                + "Vẫn mã hoá được, nhưng khoá không còn tác dụng gì.");
        }

        // Đếm phần bị bỏ trên chuỗi đã in hoa và bỏ dấu: lúc đó độ dài chỉ còn giảm vì
        // ký tự bị loại khỏi bảng, còn J → I là phép đổi chỗ nên không đổi độ dài.
        int dropped = TextNormalizer.ToPlainUpper(text).Length - normalized.Length;
        if (dropped > 0)
        {
            warnings.Add($"Đã bỏ {dropped} ký tự không có trong ma trận (khoảng trắng, dấu câu, ký tự lạ"
                + (matrix.MergesIJ ? ", chữ số" : string.Empty)
                + "). Giải mã sẽ không lấy lại được chúng.");
        }

        if (matrix.MergesIJ && TextNormalizer.ToPlainUpper(text).Contains('J'))
        {
            warnings.Add("Ma trận 5×5 gộp I/J: mọi chữ J đã thành I. Giải mã sẽ trả về I, "
                + "người đọc phải tự suy ra chữ nào vốn là J.");
        }

        if (normalized.Length == 0)
        {
            warnings.Add("Sau chuẩn hoá không còn ký tự nào để xử lý.");
            return new PlayfairResult(text, normalized, string.Empty, string.Empty, [], [], warnings);
        }

        List<(char A, char B, bool Filler)> pairs;
        List<int> fillerPositions;

        if (encrypting)
        {
            pairs = BuildPairs(normalized, matrix, out fillerPositions);
            if (fillerPositions.Count > 0)
            {
                warnings.Add($"Đã chèn {fillerPositions.Count} ký tự đệm ('{matrix.Filler}', đổi sang "
                    + $"'{matrix.FillerFallback}' nếu chính ký tự bị trùng là '{matrix.Filler}') vì có cặp trùng chữ "
                    + "hoặc văn bản có số ký tự lẻ. Giải mã sẽ trả lại cả những ký tự này.");
            }
        }
        else
        {
            if (normalized.Length % 2 != 0)
            {
                throw new ArgumentException(
                    $"Bản mã Playfair phải có số ký tự chẵn vì mã hoá theo từng cặp, nhưng sau chuẩn hoá còn {normalized.Length} ký tự. "
                    + "Kiểm tra lại bản mã đã dán đủ chưa.");
            }

            pairs = new List<(char, char, bool)>(normalized.Length / 2);
            for (int index = 0; index < normalized.Length; index += 2)
            {
                pairs.Add((normalized[index], normalized[index + 1], false));
            }

            fillerPositions = [];
            if (pairs.Exists(pair => pair.A == pair.B))
            {
                warnings.Add("Bản mã có cặp gồm hai ký tự giống nhau. Mã hoá Playfair không bao giờ sinh ra "
                    + "cặp như vậy, nên bản mã này có thể đã bị sửa hoặc dán thiếu.");
            }
        }

        List<PlayfairStep> steps = new(pairs.Count);
        StringBuilder output = new(pairs.Count * 2);

        // Cùng một công thức cho hai chiều, chỉ khác bước dịch: mã hoá đi tới 1 ô, giải
        // mã đi lui 1 ô. Viết phép lui thành "+ Size − 1" để phép % luôn nhận số không
        // âm — trong C#, (0 - 1) % 5 ra −1 chứ không ra 4.
        int shift = encrypting ? 1 : matrix.Size - 1;

        for (int index = 0; index < pairs.Count; index++)
        {
            (char a, char b, bool filler) = pairs[index];
            (int rowA, int colA) = matrix.PositionOf(a);
            (int rowB, int colB) = matrix.PositionOf(b);

            DigramRule rule;
            int outRowA, outColA, outRowB, outColB;
            string explanation;

            if (rowA == rowB)
            {
                rule = DigramRule.SameRow;
                outRowA = rowA;
                outRowB = rowB;
                outColA = (colA + shift) % matrix.Size;
                outColB = (colB + shift) % matrix.Size;
                explanation = $"Cùng hàng {rowA + 1}: lấy ký tự bên {(encrypting ? "phải" : "trái")}, "
                    + $"quá {(encrypting ? "cuối" : "đầu")} hàng thì vòng lại.";
            }
            else if (colA == colB)
            {
                rule = DigramRule.SameColumn;
                outColA = colA;
                outColB = colB;
                outRowA = (rowA + shift) % matrix.Size;
                outRowB = (rowB + shift) % matrix.Size;
                explanation = $"Cùng cột {colA + 1}: lấy ký tự bên {(encrypting ? "dưới" : "trên")}, "
                    + $"quá {(encrypting ? "cuối" : "đầu")} cột thì vòng lại.";
            }
            else
            {
                rule = DigramRule.Rectangle;
                outRowA = rowA;
                outColA = colB;
                outRowB = rowB;
                outColB = colA;
                explanation = $"Khác hàng khác cột: giữ nguyên hàng, đổi cột {colA + 1} ↔ {colB + 1}. "
                    + "Quy tắc này giống nhau ở cả mã hoá và giải mã.";
            }

            char outA = matrix.At(outRowA, outColA);
            char outB = matrix.At(outRowB, outColB);
            output.Append(outA).Append(outB);

            steps.Add(new PlayfairStep(
                index + 1, a, b, outA, outB, rule,
                rowA, colA, rowB, colB,
                outRowA, outColA, outRowB, outColB,
                FillerInserted: filler,
                explanation));
        }

        string result = output.ToString();
        IReadOnlyList<int> suspects = encrypting ? fillerPositions : FindSuspectFillers(result, matrix);

        if (!encrypting && suspects.Count > 0)
        {
            warnings.Add($"Có {suspects.Count} vị trí nghi là ký tự đệm: "
                + string.Join(", ", suspects.Select(position => position + 1))
                + " (đếm từ 1). Ứng dụng không tự xoá vì không phân biệt được ký tự đệm với ký tự thật.");
        }

        return new PlayfairResult(text, normalized, JoinPairs(pairs), result, steps, suspects, warnings);
    }

    /// <summary>
    /// Chia văn bản thành từng cặp, chèn ký tự đệm ngay trong lúc chia.
    /// </summary>
    /// <remarks>
    /// Phải chèn trong lúc chia, không được chèn thành một lượt riêng trước đó: chèn
    /// một ký tự làm mọi cặp phía sau lệch đi một chỗ, nên một lượt duyệt "tìm chữ
    /// trùng nhau" trên văn bản gốc sẽ tìm sai từ cặp thứ hai trở đi. Ví dụ "AAA":
    /// cặp đầu là (A, X), sau đó chữ A thứ hai lại đứng đầu cặp mới.
    /// </remarks>
    private static List<(char A, char B, bool Filler)> BuildPairs(string normalized, PlayfairMatrix matrix, out List<int> fillerPositions)
    {
        List<(char, char, bool)> pairs = [];
        fillerPositions = [];

        int index = 0;
        while (index < normalized.Length)
        {
            char a = normalized[index];
            char b;
            bool filler;

            if (index + 1 < normalized.Length && normalized[index + 1] != a)
            {
                b = normalized[index + 1];
                filler = false;
                index += 2;
            }
            else
            {
                // Hai trường hợp, cùng một cách xử lý: cặp trùng chữ (hai ký tự giống
                // nhau nằm cùng một ô nên không có quy tắc nào áp được), và ký tự lẻ ở
                // cuối văn bản.
                b = matrix.FillerFor(a);
                filler = true;
                fillerPositions.Add((pairs.Count * 2) + 1);
                index += 1;
            }

            pairs.Add((a, b, filler));
        }

        return pairs;
    }

    /// <summary>
    /// Đoán những vị trí có thể là ký tự đệm trong bản rõ vừa giải mã.
    /// </summary>
    /// <remarks>
    /// Chỉ là phỏng đoán, và cố ý phỏng đoán rộng. Ký tự đệm được chèn ở đúng hai chỗ:
    /// giữa hai ký tự giống nhau, và ở cuối văn bản có số ký tự lẻ — nên chỉ xét hai
    /// chỗ đó. Một chữ X thật nằm đúng vào một trong hai chỗ này vẫn bị nghi oan, và
    /// đó là giới hạn thật của thuật toán, không phải lỗi của hàm.
    /// </remarks>
    private static List<int> FindSuspectFillers(string output, PlayfairMatrix matrix)
    {
        List<int> suspects = [];

        for (int index = 0; index < output.Length; index++)
        {
            char ch = output[index];
            if (ch != matrix.Filler && ch != matrix.FillerFallback) continue;

            bool betweenTwins = index > 0
                && index + 1 < output.Length
                && output[index - 1] == output[index + 1];
            bool atEnd = index == output.Length - 1;

            if (betweenTwins || atEnd) suspects.Add(index);
        }

        return suspects;
    }

    private static string JoinPairs(List<(char A, char B, bool Filler)> pairs) =>
        string.Join(' ', pairs.Select(pair => $"{pair.A}{pair.B}"));
}
