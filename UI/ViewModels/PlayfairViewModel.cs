using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using RSA_Playfair_NT101.Core;
using RSA_Playfair_NT101.UI.Common;

namespace RSA_Playfair_NT101.UI.ViewModels;

/// <summary>
/// Một ô của ma trận trên giao diện.
/// </summary>
/// <param name="Symbol">Ký tự trong ô.</param>
/// <param name="Highlight">
/// Trạng thái tô sáng theo cặp đang chọn: <c>In</c> (ô của cặp đi vào), <c>Out</c>
/// (ô của cặp đi ra), <c>Both</c> (vừa vào vừa ra — có thật, ví dụ hai ký tự cạnh
/// nhau trên cùng một hàng), hoặc chuỗi rỗng. Dùng chuỗi thay vì hai cờ bool để
/// <c>DataTrigger</c> trong XAML không phải xử lý trường hợp hai cờ cùng bật.
/// </param>
public sealed record MatrixCell(char Symbol, string Highlight);

/// <summary>
/// ViewModel cho màn hình Playfair: ma trận, mã hoá/giải mã, và vết từng cặp ký tự.
/// </summary>
/// <remarks>
/// Chỉ có một ô nhập cho cả hai chiều. Playfair đối xứng, và bản mã của nó vẫn là
/// chữ cái như bản rõ, nên tách hai ô nhập chỉ làm người dùng phải copy qua lại.
/// Nút "Đưa kết quả sang ô nhập" là đủ để đi vòng tròn mã hoá → giải mã.
/// </remarks>
public sealed class PlayfairViewModel : ViewModelBase
{
    private PlayfairVariant _variant = PlayfairVariant.Grid5x5MergeIJ;
    private string _key = "MONARCHY";
    private string _inputText = "HELLO";

    private PlayfairMatrix _matrix;
    private PlayfairResult? _result;
    private PlayfairStep? _selectedStep;
    private string _direction = string.Empty;
    private string _error = string.Empty;
    private string _status = string.Empty;

    public PlayfairViewModel()
    {
        EncryptCommand = new RelayCommand(() => Run(encrypting: true));
        DecryptCommand = new RelayCommand(() => Run(encrypting: false));
        UseOutputAsInputCommand = new RelayCommand(UseOutputAsInput, () => Output.Length > 0);
        CopyOutputCommand = new RelayCommand(CopyOutput, () => Output.Length > 0);

        _matrix = PlayfairMatrix.Build(_key, _variant);
        RefreshCells();
    }

    public ICommand EncryptCommand { get; }
    public ICommand DecryptCommand { get; }
    public ICommand UseOutputAsInputCommand { get; }
    public ICommand CopyOutputCommand { get; }

    /// <summary>Các ô của ma trận, xếp theo hàng.</summary>
    public ObservableCollection<MatrixCell> MatrixCells { get; } = [];

    /// <summary>Vết của từng cặp ký tự, hiện trong bảng.</summary>
    public ObservableCollection<PlayfairStep> Steps { get; } = [];

    /// <summary>Biến thể ma trận. Đổi biến thể là đổi cả bảng chữ, nên kết quả cũ bỏ đi.</summary>
    public PlayfairVariant Variant
    {
        get => _variant;
        set
        {
            if (SetProperty(ref _variant, value))
            {
                RebuildMatrix();
            }
        }
    }

    /// <summary>Khoá sinh ma trận.</summary>
    public string Key
    {
        get => _key;
        set
        {
            if (SetProperty(ref _key, value))
            {
                RebuildMatrix();
            }
        }
    }

    /// <summary>Văn bản cần mã hoá hoặc bản mã cần giải mã.</summary>
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    /// <summary>Cặp đang chọn trong bảng vết; đổi cặp thì đổi luôn ô được tô sáng.</summary>
    public PlayfairStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetProperty(ref _selectedStep, value))
            {
                RefreshCells();
                OnPropertyChanged(nameof(StepExplanation));
            }
        }
    }

    /// <summary>Số cột của ma trận, cũng là số hàng. Dùng cho <c>UniformGrid</c>.</summary>
    public int MatrixColumns => _matrix.Size;

    /// <summary>Khoá sau chuẩn hoá và bỏ ký tự lặp — đúng chuỗi đã viết vào ma trận.</summary>
    public string NormalizedKey => _matrix.NormalizedKey;

    /// <summary>Câu mô tả bảng chữ và ký tự đệm của biến thể đang chọn.</summary>
    public string VariantNote => _matrix.MergesIJ
        ? $"Ma trận 5×5 có 25 ô nên chữ J gộp vào I, và chữ số bị bỏ. Ký tự đệm '{_matrix.Filler}', "
            + $"đổi sang '{_matrix.FillerFallback}' khi chính ký tự bị trùng là '{_matrix.Filler}'."
        : $"Ma trận 6×6 có 36 ô, vừa đủ 26 chữ cái và 10 chữ số nên giữ được cả J. Ký tự đệm "
            + $"'{_matrix.Filler}', đổi sang '{_matrix.FillerFallback}' khi cần.";

    /// <summary>Văn bản sau chuẩn hoá: đây là thứ thật sự đi vào thuật toán.</summary>
    public string Normalized => _result?.Normalized ?? string.Empty;

    /// <summary>Văn bản đã chia cặp, đã chèn ký tự đệm khi mã hoá.</summary>
    public string PairedText => _result?.PairedText ?? string.Empty;

    public string Output => _result?.Output ?? string.Empty;

    /// <summary>Nhãn cho ô kết quả, để không phải đoán kết quả đang là của chiều nào.</summary>
    public string OutputLabel => _direction.Length == 0
        ? "Kết quả"
        : $"Kết quả sau khi {_direction}";

    public string WarningText => _result?.WarningText ?? string.Empty;

    /// <summary>Lỗi khiến không chạy được, khác với cảnh báo mất thông tin.</summary>
    public string Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>Thông báo việc vừa làm xong (sao chép, chuyển kết quả sang ô nhập).</summary>
    public string Status
    {
        get => _status;
        private set
        {
            SetProperty(ref _status, value);
            Notifier.Info(value);
        }
    }

    /// <summary>Câu giải thích quy tắc của cặp đang chọn.</summary>
    public string StepExplanation => _selectedStep is null
        ? string.Empty
        : $"Cặp {_selectedStep.Index}: {_selectedStep.InPair} → {_selectedStep.OutPair}. "
            + $"{_selectedStep.RuleName}. {_selectedStep.Explanation}";

    /// <summary>
    /// Chạy một chiều. Hai chiều dùng chung đúng một đường xử lý kết quả, khác nhau
    /// duy nhất ở hàm Core được gọi.
    /// </summary>
    private void Run(bool encrypting)
    {
        Error = string.Empty;
        Status = string.Empty;

        try
        {
            _result = encrypting
                ? PlayfairCipher.Encrypt(InputText, Key, Variant)
                : PlayfairCipher.Decrypt(InputText, Key, Variant);

            _direction = encrypting ? "mã hoá" : "giải mã";
        }
        catch (ArgumentException ex)
        {
            // Bản mã có số ký tự lẻ là lỗi của dữ liệu vào, không phải lỗi lập trình:
            // hiện nguyên văn lời giải thích của Core thay vì nuốt đi.
            _result = null;
            _direction = string.Empty;
            Error = ex.Message;
        }

        Steps.Clear();
        foreach (PlayfairStep step in _result?.Steps ?? [])
        {
            Steps.Add(step);
        }

        SelectedStep = Steps.FirstOrDefault();
        NotifyResultChanged();
    }

    private void UseOutputAsInput()
    {
        InputText = Output;
        Status = "Đã đưa kết quả sang ô nhập. Bấm chiều còn lại để đi vòng ngược.";
    }

    private void CopyOutput()
    {
        Error = string.Empty;
        Status = string.Empty;

        try
        {
            Clipboard.SetText(Output);
            Status = "Đã sao chép kết quả vào clipboard.";
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            // Clipboard do cả hệ điều hành giữ nên có thể đang bị chương trình khác chiếm.
            Error = $"Không mở được clipboard: {ex.Message}";
        }
    }

    /// <summary>
    /// Dựng lại ma trận sau khi đổi khoá hoặc biến thể, và bỏ kết quả cũ.
    /// </summary>
    /// <remarks>
    /// Kết quả cũ được tính bằng ma trận cũ nên không còn khớp gì với ma trận đang
    /// hiện; giữ lại chỉ làm người xem tin vào một cặp không còn đúng nữa.
    /// </remarks>
    private void RebuildMatrix()
    {
        _matrix = PlayfairMatrix.Build(_key, _variant);
        _result = null;
        _direction = string.Empty;
        Error = string.Empty;
        Steps.Clear();
        SelectedStep = null;

        RefreshCells();
        NotifyResultChanged();
        OnPropertyChanged(nameof(MatrixColumns));
        OnPropertyChanged(nameof(NormalizedKey));
        OnPropertyChanged(nameof(VariantNote));
    }

    /// <summary>
    /// Dựng lại danh sách ô kèm trạng thái tô sáng của cặp đang chọn.
    /// </summary>
    /// <remarks>
    /// Dựng lại cả danh sách thay vì sửa từng ô: ma trận chỉ có 25 hoặc 36 ô, nên
    /// cách này không cần thêm thông báo thay đổi cho từng ô.
    /// </remarks>
    private void RefreshCells()
    {
        MatrixCells.Clear();

        for (int index = 0; index < _matrix.Cells.Count; index++)
        {
            (int row, int col) = (index / _matrix.Size, index % _matrix.Size);

            bool isInput = _selectedStep is { } step
                && ((row, col) == (step.RowA, step.ColA) || (row, col) == (step.RowB, step.ColB));
            bool isOutput = _selectedStep is { } outStep
                && ((row, col) == (outStep.OutRowA, outStep.OutColA)
                    || (row, col) == (outStep.OutRowB, outStep.OutColB));

            MatrixCells.Add(new MatrixCell(
                _matrix.Cells[index],
                (isInput, isOutput) switch
                {
                    (true, true) => "Both",
                    (true, false) => "In",
                    (false, true) => "Out",
                    _ => string.Empty,
                }));
        }
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(Normalized));
        OnPropertyChanged(nameof(PairedText));
        OnPropertyChanged(nameof(Output));
        OnPropertyChanged(nameof(OutputLabel));
        OnPropertyChanged(nameof(WarningText));
    }
}
