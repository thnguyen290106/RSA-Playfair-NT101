using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RSA_Playfair_NT101.UI.Common;

/// <summary>
/// Lớp nền cho mọi ViewModel: phát <see cref="PropertyChanged"/> để binding của
/// WPF tự cập nhật giao diện.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gán giá trị mới cho một field và phát thông báo nếu giá trị thực sự đổi.
    /// Trả về <c>true</c> khi có thay đổi, để lớp con nối thêm việc phụ (ví dụ
    /// tính lại giá trị dẫn xuất) mà không cần so sánh lại.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
