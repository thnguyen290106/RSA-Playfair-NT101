using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RSA_Playfair_NT101.UI.Common;

/// <summary>
/// So sánh giá trị nguồn với <c>ConverterParameter</c>. Dùng cho nhóm
/// <see cref="System.Windows.Controls.RadioButton"/> bind vào một thuộc tính enum
/// hoặc số, thay vì phải tạo một thuộc tính bool cho từng phương án.
/// </summary>
public sealed class ValueEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Chỉ ô được chọn mới ghi giá trị về; ô vừa bị bỏ chọn thì không làm gì,
        // nếu không hai ô sẽ ghi tranh nhau và giá trị cuối phụ thuộc thứ tự.
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return type.IsEnum
            ? Enum.Parse(type, parameter.ToString()!)
            : System.Convert.ChangeType(parameter, type, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Chuỗi rỗng thì ẩn hẳn phần tử. Dùng cho các băng thông báo chỉ hiện khi có nội
/// dung, tránh để lại khoảng trống rỗng trên giao diện.
/// </summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Chỉ dùng một chiều để hiện/ẩn.");
}
