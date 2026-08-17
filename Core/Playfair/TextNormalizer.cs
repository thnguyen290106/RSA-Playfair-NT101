using System.Globalization;
using System.Text;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Chuẩn hoá văn bản trước khi đưa vào bảng chữ của Playfair.
/// </summary>
/// <remarks>
/// Playfair chỉ làm việc được với đúng các ký tự có mặt trong ma trận, nên mọi
/// thứ khác phải được quy về hoặc bỏ đi <em>trước</em> khi mã hoá. Việc bỏ ký tự
/// là mất thông tin thật (giải mã không lấy lại được dấu tiếng Việt hay khoảng
/// trắng), nên ứng dụng luôn hiện văn bản sau chuẩn hoá cho người dùng thấy.
/// </remarks>
public static class TextNormalizer
{
    /// <summary>
    /// Bỏ dấu tiếng Việt: "Tiếng Việt" → "Tieng Viet". Không đổi chữ hoa/thường.
    /// </summary>
    /// <remarks>
    /// Cách làm: tách ký tự có dấu thành "chữ gốc + dấu kết hợp" (dạng NFD) rồi
    /// bỏ mọi dấu kết hợp. Riêng <c>Đ</c>/<c>đ</c> phải xử lý bằng tay trước:
    /// Unicode coi đây là một chữ cái riêng chứ không phải "D + dấu", nên NFD trả
    /// về đúng nó và bộ lọc dấu ở dưới không thấy gì để bỏ.
    /// </remarks>
    public static string StripDiacritics(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string prepared = text.Replace('Đ', 'D').Replace('đ', 'd');
        string decomposed = prepared.Normalize(NormalizationForm.FormD);

        StringBuilder builder = new(decomposed.Length);
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        // Ghép lại về dạng NFC để chuỗi trả về là dạng thường thấy, không phải một
        // chuỗi nửa tách nửa ghép.
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Bỏ dấu rồi chuyển sang chữ in hoa. Đây là bước đầu của mọi lần chuẩn hoá.</summary>
    /// <remarks>
    /// Dùng <c>ToUpperInvariant</c> chứ không theo văn hoá máy: văn hoá Thổ Nhĩ Kỳ
    /// đổi <c>i</c> thành <c>İ</c> (I có dấu chấm), và ký tự đó không có trong bảng
    /// nên sẽ bị bỏ — cùng một văn bản sẽ mã hoá khác nhau trên hai máy.
    /// </remarks>
    public static string ToPlainUpper(string text) => StripDiacritics(text).ToUpperInvariant();
}
