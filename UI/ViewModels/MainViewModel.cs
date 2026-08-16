using RSA_Playfair_NT101.UI.Common;

namespace RSA_Playfair_NT101.UI.ViewModels;

/// <summary>Một mục trên thanh điều hướng bên trái.</summary>
/// <param name="Badge">Nhãn ngắn hiện trong ô vuông (thay cho icon).</param>
public sealed record NavItem(string Badge, string Title, string Subtitle, ViewModelBase Content);

/// <summary>
/// ViewModel của cửa sổ chính. Giữ danh sách mục điều hướng và mục đang chọn;
/// vùng nội dung bind thẳng vào <c>SelectedNav.Content</c> nên không cần thêm
/// navigation service.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private NavItem _selectedNav;

    public MainViewModel()
    {
        NavItem rsa = new("RSA", "RSA", "Khoá công khai, chữ ký số", new RsaViewModel());
        NavItem playfair = new("PF", "Playfair", "Mã hoá cổ điển theo cặp", new PlayfairViewModel());

        HomeViewModel home = new(() => SelectedNav = rsa, () => SelectedNav = playfair);
        NavItem homeItem = new("★", "Trang chủ", "Giới thiệu & chọn thuật toán", home);

        NavItems = [homeItem, rsa, playfair];
        _selectedNav = homeItem;
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    public NavItem SelectedNav
    {
        get => _selectedNav;
        set => SetProperty(ref _selectedNav, value);
    }
}
