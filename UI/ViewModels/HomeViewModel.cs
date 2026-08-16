using System.Windows.Input;
using RSA_Playfair_NT101.UI.Common;

namespace RSA_Playfair_NT101.UI.ViewModels;

/// <summary>
/// Trang chủ: nói ngay ứng dụng làm gì và mở thẳng hai thuật toán. Người xem tự
/// bấm thử mà không có ai giải thích, nên mọi câu chữ ở đây phải tự đủ nghĩa.
/// </summary>
public sealed class HomeViewModel(Action openRsa, Action openPlayfair) : ViewModelBase
{
    public ICommand OpenRsaCommand { get; } = new RelayCommand(openRsa);

    public ICommand OpenPlayfairCommand { get; } = new RelayCommand(openPlayfair);
}
