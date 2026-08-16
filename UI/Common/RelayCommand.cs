using System.Windows.Input;

namespace RSA_Playfair_NT101.UI.Common;

/// <summary>
/// <see cref="ICommand"/> đồng bộ, bọc một delegate.
/// </summary>
/// <remarks>
/// <see cref="CanExecuteChanged"/> nối vào <see cref="CommandManager.RequerySuggested"/>
/// nên WPF tự hỏi lại <see cref="CanExecute"/> mỗi khi người dùng tương tác;
/// không cần tự phát sự kiện sau mỗi lần đổi thuộc tính.
/// </remarks>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// <see cref="ICommand"/> bất đồng bộ. Trong lúc chạy, <see cref="CanExecute"/>
/// trả về <c>false</c> để nút tự khoá, tránh bấm hai lần sinh khoá cùng lúc.
/// </summary>
/// <param name="onError">
/// Nơi nhận lỗi không lường trước. Bắt buộc phải có chỗ nhận, vì thân
/// <c>async void</c> mà ném ra ngoài sẽ làm sập ứng dụng thay vì hiện thông báo.
/// </param>
public sealed class AsyncRelayCommand(
    Func<Task> execute, Action<Exception> onError, Func<bool>? canExecute = null) : ICommand
{
    private readonly Func<Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Action<Exception> _onError = onError ?? throw new ArgumentNullException(nameof(onError));

    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
