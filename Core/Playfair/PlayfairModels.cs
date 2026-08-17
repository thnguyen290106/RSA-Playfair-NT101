namespace RSA_Playfair_NT101.Core;

/// <summary>Hai biến thể ma trận Playfair mà ứng dụng hỗ trợ.</summary>
public enum PlayfairVariant
{
    /// <summary>
    /// Ma trận 5×5 kinh điển. Bảng chữ Latin có 26 chữ mà ô chỉ có 25, nên
    /// <c>J</c> bị gộp vào <c>I</c>. Đây là biến thể trong hầu hết giáo trình.
    /// </summary>
    Grid5x5MergeIJ,

    /// <summary>
    /// Ma trận 6×6: 26 chữ cái + 10 chữ số, vừa đúng 36 ô. Không phải gộp chữ
    /// nào, và mã hoá được cả chữ số.
    /// </summary>
    Grid6x6Alphanumeric,
}

/// <summary>Ba quy tắc biến đổi một cặp ký tự của Playfair.</summary>
public enum DigramRule
{
    /// <summary>Hai ký tự cùng hàng: lấy ký tự bên phải (mã hoá) hoặc bên trái (giải mã).</summary>
    SameRow,

    /// <summary>Hai ký tự cùng cột: lấy ký tự bên dưới (mã hoá) hoặc bên trên (giải mã).</summary>
    SameColumn,

    /// <summary>
    /// Hai ký tự khác hàng khác cột: giữ hàng, đổi cột cho nhau. Quy tắc này giống
    /// nhau ở cả hai chiều nên tự nó là phép nghịch đảo của chính nó.
    /// </summary>
    Rectangle,
}

/// <summary>
/// Vết biến đổi của một cặp ký tự: vào gì, ra gì, theo quy tắc nào, ở ô nào.
/// </summary>
/// <remarks>
/// Bản ghi này tồn tại để hiển thị: ứng dụng phải trả lời được "hai ký tự này
/// đến từ đâu", nên phải giữ cả toạ độ trước và sau, không chỉ kết quả. Tám tham số
/// <c>Row*</c>/<c>Col*</c> là toạ độ hàng và cột, đếm từ 0: không có tiền tố
/// <c>Out</c> là ô đi vào, có <c>Out</c> là ô đi ra.
/// </remarks>
/// <param name="Index">Số thứ tự cặp, bắt đầu từ 1.</param>
/// <param name="InA">Ký tự thứ nhất đi vào.</param>
/// <param name="InB">Ký tự thứ hai đi vào (có thể là ký tự đệm vừa chèn).</param>
/// <param name="OutA">Ký tự thứ nhất đi ra.</param>
/// <param name="OutB">Ký tự thứ hai đi ra.</param>
/// <param name="Rule">Quy tắc đã áp dụng cho cặp này.</param>
/// <param name="FillerInserted">
/// Ký tự thứ hai của cặp là ký tự đệm do ứng dụng chèn, không phải của người dùng.
/// Chỉ xảy ra khi mã hoá.
/// </param>
/// <param name="Explanation">Câu giải thích quy tắc, viết sẵn để hiện thẳng lên bảng.</param>
public sealed record PlayfairStep(
    int Index,
    char InA,
    char InB,
    char OutA,
    char OutB,
    DigramRule Rule,
    int RowA,
    int ColA,
    int RowB,
    int ColB,
    int OutRowA,
    int OutColA,
    int OutRowB,
    int OutColB,
    bool FillerInserted,
    string Explanation)
{
    /// <summary>Cặp đi vào, dạng hai ký tự liền nhau.</summary>
    public string InPair => $"{InA}{InB}";

    /// <summary>Cặp đi ra, dạng hai ký tự liền nhau.</summary>
    public string OutPair => $"{OutA}{OutB}";

    /// <summary>Tên quy tắc bằng tiếng Việt, dùng cho bảng trên giao diện.</summary>
    public string RuleName => Rule switch
    {
        DigramRule.SameRow => "Cùng hàng",
        DigramRule.SameColumn => "Cùng cột",
        DigramRule.Rectangle => "Hình chữ nhật",
        _ => Rule.ToString(),
    };

    /// <summary>Toạ độ hai ô đi vào, đếm từ 1 cho khớp với ma trận người dùng đang nhìn.</summary>
    public string InCells => $"({RowA + 1},{ColA + 1}) ({RowB + 1},{ColB + 1})";

    /// <summary>Toạ độ hai ô đi ra, đếm từ 1.</summary>
    public string OutCells => $"({OutRowA + 1},{OutColA + 1}) ({OutRowB + 1},{OutColB + 1})";
}

/// <summary>
/// Kết quả một lần mã hoá hoặc giải mã Playfair, kèm mọi thứ cần để hiện lại
/// đường đi và để nói thẳng những gì đã mất.
/// </summary>
/// <param name="Normalized">
/// Văn bản sau chuẩn hoá: bỏ dấu, in hoa, bỏ mọi ký tự không có trong ma trận.
/// </param>
/// <param name="PairedText">
/// Văn bản đã chia thành từng cặp, các cặp cách nhau bằng khoảng trắng. Khi mã hoá
/// thì đây là văn bản <em>đã chèn</em> ký tự đệm, tức đúng thứ thực sự được mã hoá.
/// </param>
/// <param name="Output">Kết quả: bản mã (khi mã hoá) hoặc bản rõ thô (khi giải mã).</param>
/// <param name="Steps">Vết của từng cặp, theo đúng thứ tự.</param>
/// <param name="SuspectFillerPositions">
/// Vị trí (đếm từ 0) của ký tự đệm ở phía bản rõ. Khi mã hoá đây là danh sách
/// <em>chắc chắn</em>: ứng dụng vừa chèn nên biết chính xác, và vị trí tính trên
/// <paramref name="PairedText"/> khi bỏ hết khoảng trắng. Khi giải mã đây chỉ là
/// <em>phỏng đoán</em> trên <paramref name="Output"/> — không có cách nào phân biệt
/// một chữ X thật với một chữ X do máy chèn.
/// </param>
/// <param name="Warnings">
/// Những gì người dùng cần biết: đã bỏ bao nhiêu ký tự, đã gộp J thành I, đã chèn
/// bao nhiêu ký tự đệm. Không có cái nào là lỗi — chúng là tính chất của thuật toán.
/// </param>
public sealed record PlayfairResult(
    string Normalized,
    string PairedText,
    string Output,
    IReadOnlyList<PlayfairStep> Steps,
    IReadOnlyList<int> SuspectFillerPositions,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Các cảnh báo gộp thành một khối chữ, mỗi cảnh báo một dòng.</summary>
    public string WarningText => string.Join(Environment.NewLine, Warnings);
}
