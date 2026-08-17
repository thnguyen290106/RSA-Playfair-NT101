using System.Windows;

namespace RSA_Playfair_NT101.UI.Common;

/// <summary>
/// Hiện thông báo bằng <c>MessageBox</c>, song song với băng thông báo trên màn hình.
/// </summary>
/// <remarks>
/// Băng thông báo nằm trong tab, nên người dùng đang nhìn tab khác sẽ không thấy nó.
/// Hộp thoại thì chắc chắn thấy. Hai thứ cùng hiện một nội dung — hộp thoại không thay
/// băng thông báo, vì băng còn đó để đọc lại sau khi bấm OK.
/// <para>
/// Nằm ở <c>UI/</c> vì phụ thuộc WPF: <c>Core/</c> phải giữ nguyên là logic thuần.
/// </para>
/// </remarks>
public static class Notifier
{
    /// <summary>Tắt hộp thoại đi. Dành cho test khỏi treo chờ một hộp thoại modal.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Thông báo việc đã làm xong (tải file, lưu file, sao chép…).</summary>
    public static void Info(string message) => Show(message, "Thông báo", MessageBoxImage.Information);

    /// <summary>Thông báo lỗi.</summary>
    public static void Error(string message) => Show(message, "Lỗi", MessageBoxImage.Error);

    /// <summary>Phán quyết của bước xác minh chữ ký số.</summary>
    public static void Result(string message, bool passed) => Show(
        message,
        "Kết quả xác minh",
        passed ? MessageBoxImage.Information : MessageBoxImage.Warning);

    /// <summary>Nội dung rỗng là lúc xoá thông báo cũ, không phải một thông báo mới.</summary>
    private static void Show(string message, string title, MessageBoxImage icon)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }
}
