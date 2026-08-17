using System.IO;
using Microsoft.Win32;

namespace RSA_Playfair_NT101.UI.Common;

/// <summary>
/// Hộp thoại chọn file cho các nút tải/lưu văn bản.
/// </summary>
/// <remarks>
/// Nằm ở <c>UI/</c> vì phụ thuộc WPF: <c>Core/</c> phải giữ nguyên là logic thuần.
/// Gom vào một chỗ vì có sáu nút cùng cần đúng ba việc này.
/// </remarks>
public static class TextFileDialogs
{
    /// <summary>Giới hạn cho file tài liệu (nội dung cần ký hoặc cần mã hoá).</summary>
    public const int DocumentMaxBytes = 1024 * 1024;

    /// <summary>
    /// Giới hạn cho file chỉ chứa một con số (chữ ký, bản mã Base64, khoá công khai).
    /// </summary>
    /// <remarks>
    /// Đây là chốt an toàn, không phải chốt tiện dụng: một số thập phân dài hàng triệu
    /// chữ số làm <c>BigInteger.Parse</c> treo cửa sổ. File từ bên ngoài là dữ liệu
    /// không tin cậy nên phải chặn ngay tại biên.
    /// </remarks>
    public const int NumberMaxBytes = 64 * 1024;

    /// <summary>Bộ lọc cho file tài liệu — chấp nhận cả <c>.signed</c>.</summary>
    public const string DocumentFilter =
        "File tài liệu (*.signed;*.txt)|*.signed;*.txt|Tất cả file (*.*)|*.*";

    /// <summary>Bộ lọc cho file văn bản thường.</summary>
    public const string TextFilter = "File văn bản (*.txt)|*.txt|Tất cả file (*.*)|*.*";

    /// <summary>Cho người dùng chọn một file văn bản và trả về nội dung.</summary>
    /// <returns><c>null</c> khi người dùng bấm Huỷ.</returns>
    /// <exception cref="InvalidOperationException">Khi file vượt <paramref name="maxBytes"/>.</exception>
    public static string? ReadText(string title, string filter, int maxBytes)
    {
        OpenFileDialog dialog = new()
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        long length = new FileInfo(dialog.FileName).Length;

        if (length > maxBytes)
        {
            throw new InvalidOperationException(
                $"File \"{Path.GetFileName(dialog.FileName)}\" nặng {length / 1024} KB, "
                + $"vượt giới hạn {maxBytes / 1024} KB của ô này. Hãy chọn file nhỏ hơn.");
        }

        // ReadAllText tự nhận BOM nếu file có, và tự bỏ nó khỏi nội dung đọc ra.
        return File.ReadAllText(dialog.FileName);
    }

    /// <summary>Cho người dùng chọn nơi lưu rồi ghi <paramref name="content"/> ra file.</summary>
    /// <returns>Đường dẫn đã ghi, hoặc <c>null</c> khi người dùng bấm Huỷ.</returns>
    public static string? WriteText(
        string title, string suggestedName, string filter, string content)
    {
        // SaveFileDialog bật OverwritePrompt sẵn nên không tự ghi đè file người dùng.
        SaveFileDialog dialog = new()
        {
            Title = title,
            FileName = suggestedName,
            Filter = filter,
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        // Mặc định của WriteAllText là UTF-8 không BOM: giữ nguyên byte của nội dung
        // để bên nhận băm lại ra đúng giá trị cũ.
        File.WriteAllText(dialog.FileName, content);

        return dialog.FileName;
    }

    /// <summary>Cho người dùng chọn một thư mục.</summary>
    /// <returns>Đường dẫn thư mục, hoặc <c>null</c> khi người dùng bấm Huỷ.</returns>
    public static string? PickFolder(string title)
    {
        OpenFolderDialog dialog = new() { Title = title };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
