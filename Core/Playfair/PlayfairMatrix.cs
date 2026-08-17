using System.Text;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Ma trận chữ của Playfair, sinh từ khoá. Biết ký tự nào ở ô nào và ngược lại.
/// </summary>
/// <remarks>
/// Toàn bộ "khoá" của Playfair nằm ở thứ tự các ô trong ma trận này. Cách sinh:
/// viết khoá vào trước (bỏ ký tự lặp, giữ lần xuất hiện đầu), rồi điền tiếp những
/// chữ còn lại của bảng chữ theo thứ tự.
/// </remarks>
public sealed class PlayfairMatrix
{
    /// <summary>Bảng chữ của ma trận 5×5: 25 chữ, không có <c>J</c> vì J gộp vào I.</summary>
    public const string Alphabet5x5 = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

    /// <summary>Bảng chữ của ma trận 6×6: 26 chữ cái + 10 chữ số, đúng 36 ô.</summary>
    public const string Alphabet6x6 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Ký tự đệm mặc định, theo thông lệ của Playfair.</summary>
    public const char DefaultFiller = 'X';

    private readonly Dictionary<char, int> _positions;
    private readonly char[] _cells;

    private PlayfairMatrix(PlayfairVariant variant, string normalizedKey, char[] cells, Dictionary<char, int> positions)
    {
        Variant = variant;
        NormalizedKey = normalizedKey;
        _cells = cells;
        _positions = positions;
    }

    /// <summary>Biến thể đang dùng.</summary>
    public PlayfairVariant Variant { get; }

    /// <summary>Khoá sau chuẩn hoá và bỏ ký tự lặp — đúng chuỗi đã được viết vào ma trận.</summary>
    public string NormalizedKey { get; }

    /// <summary>Số hàng, cũng là số cột: 5 hoặc 6.</summary>
    public int Size => Variant == PlayfairVariant.Grid5x5MergeIJ ? 5 : 6;

    /// <summary>Bảng chữ của biến thể đang dùng.</summary>
    public string Alphabet => AlphabetOf(Variant);

    /// <summary>Ma trận trải phẳng theo hàng: ô <c>(r, c)</c> nằm ở chỉ số <c>r * Size + c</c>.</summary>
    public IReadOnlyList<char> Cells => _cells;

    /// <summary>Ma trận này có gộp <c>J</c> vào <c>I</c> hay không.</summary>
    public bool MergesIJ => Variant == PlayfairVariant.Grid5x5MergeIJ;

    /// <summary>Ký tự đệm chính.</summary>
    public char Filler => DefaultFiller;

    /// <summary>
    /// Ký tự đệm thay thế, dùng khi chính ký tự cần đệm là <see cref="Filler"/>.
    /// </summary>
    /// <remarks>
    /// Ma trận 5×5 dùng <c>Q</c> (chữ ít gặp nhất trong tiếng Anh); ma trận 6×6 dùng
    /// <c>9</c> vì có chữ số, và một chữ số lẫn trong chữ thì đọc ra ngay là ký tự
    /// chèn.
    /// </remarks>
    public char FillerFallback => Variant == PlayfairVariant.Grid5x5MergeIJ ? 'Q' : '9';

    /// <summary>Bảng chữ tương ứng với một biến thể.</summary>
    public static string AlphabetOf(PlayfairVariant variant) =>
        variant == PlayfairVariant.Grid5x5MergeIJ ? Alphabet5x5 : Alphabet6x6;

    /// <summary>
    /// Sinh ma trận từ khoá. Khoá rỗng hoặc không còn ký tự nào dùng được sẽ cho
    /// ma trận là bảng chữ theo thứ tự — vẫn mã hoá được, chỉ là khoá không còn
    /// tác dụng; việc cảnh báo là của <see cref="PlayfairCipher"/>.
    /// </summary>
    public static PlayfairMatrix Build(string key, PlayfairVariant variant)
    {
        ArgumentNullException.ThrowIfNull(key);

        string alphabet = AlphabetOf(variant);
        bool mergeIJ = variant == PlayfairVariant.Grid5x5MergeIJ;

        // J → I phải làm ở bước chuẩn hoá này, tức là trước khi bỏ ký tự lặp. Nếu bỏ
        // trùng trước rồi mới gộp thì khoá "JAIL" giữ lại cả J và I, và ma trận sẽ có
        // hai ô I — sai ngay từ ô đầu.
        string filteredKey = NormalizeTo(key, alphabet, mergeIJ);

        char[] cells = new char[alphabet.Length];
        Dictionary<char, int> positions = new(alphabet.Length);

        int next = 0;
        foreach (char ch in filteredKey.AsSpan())
        {
            if (positions.TryAdd(ch, next)) cells[next++] = ch;
        }

        // Những ô đã điền chính là khoá sau khi bỏ ký tự lặp, nên không cần dựng lại
        // chuỗi đó một lần nữa.
        string normalizedKey = new(cells, 0, next);

        // Điền phần còn lại của bảng chữ. Vì khoá đã được lọc theo đúng bảng chữ này,
        // hai vòng lặp cộng lại luôn điền vừa đủ số ô.
        foreach (char ch in alphabet.AsSpan())
        {
            if (positions.TryAdd(ch, next)) cells[next++] = ch;
        }

        return new PlayfairMatrix(variant, normalizedKey, cells, positions);
    }

    /// <summary>
    /// Chuẩn hoá văn bản theo ma trận này: bỏ dấu, in hoa, gộp <c>J</c> vào <c>I</c>
    /// nếu là 5×5, rồi bỏ mọi ký tự không có trong bảng chữ.
    /// </summary>
    public string Normalize(string text) => NormalizeTo(text, Alphabet, MergesIJ);

    /// <summary>Ma trận có chứa ký tự này hay không. Ký tự phải đã được chuẩn hoá.</summary>
    public bool Contains(char ch) => _positions.ContainsKey(ch);

    /// <summary>Chỉ số trải phẳng của ký tự, hoặc <c>-1</c> nếu không có trong ma trận.</summary>
    public int IndexOf(char ch) => _positions.TryGetValue(ch, out int index) ? index : -1;

    /// <summary>Toạ độ (hàng, cột) của ký tự, đếm từ 0.</summary>
    /// <exception cref="ArgumentException">Ký tự không có trong ma trận.</exception>
    public (int Row, int Col) PositionOf(char ch)
    {
        int index = IndexOf(ch);
        if (index < 0) throw new ArgumentException($"Ký tự '{ch}' không có trong ma trận Playfair.", nameof(ch));
        return (index / Size, index % Size);
    }

    /// <summary>Ký tự ở ô (hàng, cột), đếm từ 0.</summary>
    public char At(int row, int col)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(col);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Size);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(col, Size);
        return _cells[(row * Size) + col];
    }

    /// <summary>
    /// Ký tự đệm dùng cho một ký tự bị trùng: thường là <c>X</c>, nhưng nếu chính
    /// ký tự đó là <c>X</c> thì phải đổi sang ký tự khác — chèn X vào giữa "XX"
    /// vẫn ra một cặp trùng chữ, tức là không giải quyết được gì.
    /// </summary>
    public char FillerFor(char duplicated) => duplicated == Filler ? FillerFallback : Filler;

    private static string NormalizeTo(string text, string alphabet, bool mergeIJ)
    {
        string upper = TextNormalizer.ToPlainUpper(text);

        StringBuilder builder = new(upper.Length);
        foreach (char ch in upper)
        {
            char mapped = mergeIJ && ch == 'J' ? 'I' : ch;
            if (alphabet.Contains(mapped)) builder.Append(mapped);
        }

        return builder.ToString();
    }
}
