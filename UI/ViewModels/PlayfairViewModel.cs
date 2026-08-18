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
/// Hai chiều có hai làn riêng, mỗi làn một ô nhập và một bộ ô kết quả: nhìn được
/// cả bản mã và bản rõ giải ra cùng lúc, không phải bấm qua lại rồi đoán kết quả
/// đang hiện là của chiều nào. Mã hoá xong, bản mã tự sang ô nhập của làn giải mã —
/// vòng tròn mã hoá → giải mã vì vậy không cần nút chuyển tay nào.
/// <para>
/// Bảng vết vẫn dùng chung một bảng cho hai làn: nó chỉ có nghĩa khi gắn với ma trận
/// đang hiện, và hai bảng vết cạnh nhau thì phải có hai ma trận, trong khi ma trận
/// của hai chiều là một. <see cref="TraceTitle"/> nói rõ vết đang là của chiều nào.
/// </para>
/// </remarks>
public sealed class PlayfairViewModel : ViewModelBase
{
    private PlayfairVariant _variant = PlayfairVariant.Grid5x5MergeIJ;
    private string _key = "MONARCHY";
    private string _encryptInput = "HELLO";
    private string _decryptInput = string.Empty;

    private PlayfairMatrix _matrix;
    private PlayfairResult? _encryptResult;
    private PlayfairResult? _decryptResult;
    private PlayfairStep? _selectedStep;
    private string _direction = string.Empty;
    private string _encryptError = string.Empty;
    private string _decryptError = string.Empty;
    private string _status = string.Empty;

    public PlayfairViewModel()
    {
        EncryptCommand = new RelayCommand(() => Run(encrypting: true));
        DecryptCommand = new RelayCommand(() => Run(encrypting: false));
        CopyEncryptOutputCommand = new RelayCommand(
            () => CopyOutput(encrypting: true), () => EncryptOutput.Length > 0);
        CopyDecryptOutputCommand = new RelayCommand(
            () => CopyOutput(encrypting: false), () => DecryptOutput.Length > 0);

        _matrix = PlayfairMatrix.Build(_key, _variant);
        RefreshCells();
    }

    public ICommand EncryptCommand { get; }
    public ICommand DecryptCommand { get; }
    public ICommand CopyEncryptOutputCommand { get; }
    public ICommand CopyDecryptOutputCommand { get; }

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

    /// <summary>Văn bản cần mã hoá.</summary>
    public string EncryptInput
    {
        get => _encryptInput;
        set => SetProperty(ref _encryptInput, value);
    }

    /// <summary>Bản mã cần giải mã. Mã hoá xong thì ô này được điền sẵn.</summary>
    public string DecryptInput
    {
        get => _decryptInput;
        set => SetProperty(ref _decryptInput, value);
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

    /// <summary>Bản rõ sau chuẩn hoá: đây là thứ thật sự đi vào thuật toán.</summary>
    public string EncryptNormalized => _encryptResult?.Normalized ?? string.Empty;

    /// <summary>Bản rõ đã chia cặp, đã chèn ký tự đệm.</summary>
    public string EncryptPaired => _encryptResult?.PairedText ?? string.Empty;

    public string EncryptOutput => _encryptResult?.Output ?? string.Empty;

    public string EncryptWarning => _encryptResult?.WarningText ?? string.Empty;

    /// <summary>Bản mã sau chuẩn hoá.</summary>
    public string DecryptNormalized => _decryptResult?.Normalized ?? string.Empty;

    /// <summary>Bản mã đã chia cặp. Giải mã không chèn thêm ký tự đệm nào.</summary>
    public string DecryptPaired => _decryptResult?.PairedText ?? string.Empty;

    public string DecryptOutput => _decryptResult?.Output ?? string.Empty;

    public string DecryptWarning => _decryptResult?.WarningText ?? string.Empty;

    /// <summary>Tiêu đề bảng vết, để không phải đoán vết đang là của chiều nào.</summary>
    public string TraceTitle => _direction.Length == 0
        ? "Vết từng cặp"
        : $"Vết từng cặp — lần {_direction} gần nhất";

    /// <summary>Lỗi của làn mã hoá. Khác với cảnh báo mất thông tin.</summary>
    public string EncryptError
    {
        get => _encryptError;
        private set => SetProperty(ref _encryptError, value);
    }

    /// <summary>Lỗi của làn giải mã, ví dụ bản mã có số ký tự lẻ.</summary>
    public string DecryptError
    {
        get => _decryptError;
        private set => SetProperty(ref _decryptError, value);
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
    /// duy nhất ở hàm Core được gọi và ở bộ ô nhận kết quả.
    /// </summary>
    private void Run(bool encrypting)
    {
        Status = string.Empty;

        if (encrypting)
        {
            EncryptError = string.Empty;
        }
        else
        {
            DecryptError = string.Empty;
        }

        PlayfairResult? result = null;

        try
        {
            result = encrypting
                ? PlayfairCipher.Encrypt(EncryptInput, Key, Variant)
                : PlayfairCipher.Decrypt(DecryptInput, Key, Variant);

            _direction = encrypting ? "mã hoá" : "giải mã";
        }
        catch (ArgumentException ex)
        {
            // Bản mã có số ký tự lẻ là lỗi của dữ liệu vào, không phải lỗi lập trình:
            // hiện nguyên văn lời giải thích của Core thay vì nuốt đi.
            _direction = string.Empty;

            if (encrypting)
            {
                EncryptError = ex.Message;
            }
            else
            {
                DecryptError = ex.Message;
            }
        }

        if (encrypting)
        {
            _encryptResult = result;
        }
        else
        {
            _decryptResult = result;
        }

        Steps.Clear();
        foreach (PlayfairStep step in result?.Steps ?? [])
        {
            Steps.Add(step);
        }

        SelectedStep = Steps.FirstOrDefault();
        NotifyResultChanged();

        // Chỉ chuyển tiếp và báo xong khi thật sự có kết quả: văn bản rỗng vẫn ra một
        // PlayfairResult kèm cảnh báo, nhưng điền chuỗi rỗng sang ô dưới rồi báo
        // "hoàn tất" là báo sai.
        if (encrypting && EncryptOutput.Length > 0)
        {
            DecryptInput = EncryptOutput;
            Status = "Mã hoá hoàn tất. Bản mã đã được chuyển sang ô nhập của phần Giải mã bên phải. "
                + "Bấm \"Giải mã\" để kiểm tra vòng ngược.";
        }
        else if (!encrypting && DecryptOutput.Length > 0)
        {
            // Có ký tự đệm hay không là chuyện của từng bản mã, không phải chuyện luôn
            // đúng: nói "vẫn còn ký tự đệm" cho một bản rõ không có ký tự đệm nào là
            // báo sai. Core đã phỏng đoán sẵn danh sách vị trí, chỉ cần đọc lại.
            int suspects = _decryptResult?.SuspectFillerPositions.Count ?? 0;

            Status = "Giải mã hoàn tất. " + (suspects > 0
                ? $"Kết quả là văn bản đã chuẩn hoá và còn {suspects} vị trí nghi là ký tự đệm — "
                    + "xem băng cảnh báo để biết vị trí nào."
                : "Kết quả là văn bản đã chuẩn hoá, và không có vị trí nào nghi là ký tự đệm.");
        }
    }

    /// <summary>Sao chép kết quả của một làn. Hai làn dùng chung một đường xử lý lỗi.</summary>
    private void CopyOutput(bool encrypting)
    {
        Status = string.Empty;

        try
        {
            Clipboard.SetText(encrypting ? EncryptOutput : DecryptOutput);
            Status = encrypting
                ? "Đã sao chép bản mã vào clipboard."
                : "Đã sao chép bản rõ vào clipboard.";
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            // Clipboard do cả hệ điều hành giữ nên có thể đang bị chương trình khác chiếm.
            string message = $"Không mở được clipboard: {ex.Message}";

            if (encrypting)
            {
                EncryptError = message;
            }
            else
            {
                DecryptError = message;
            }
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
        _encryptResult = null;
        _decryptResult = null;
        _direction = string.Empty;
        EncryptError = string.Empty;
        DecryptError = string.Empty;
        // Bản mã trong ô giải mã cũng do ma trận cũ sinh ra: để lại là mời người dùng
        // giải mã nó bằng một ma trận khác.
        DecryptInput = string.Empty;
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
            bool isInput = false, isOutput = false;

            if (_selectedStep is { } step)
            {
                isInput = (row, col) == (step.RowA, step.ColA) || (row, col) == (step.RowB, step.ColB);
                isOutput = (row, col) == (step.OutRowA, step.OutColA) || (row, col) == (step.OutRowB, step.OutColB);
            }

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
        OnPropertyChanged(nameof(EncryptNormalized));
        OnPropertyChanged(nameof(EncryptPaired));
        OnPropertyChanged(nameof(EncryptOutput));
        OnPropertyChanged(nameof(EncryptWarning));
        OnPropertyChanged(nameof(DecryptNormalized));
        OnPropertyChanged(nameof(DecryptPaired));
        OnPropertyChanged(nameof(DecryptOutput));
        OnPropertyChanged(nameof(DecryptWarning));
        OnPropertyChanged(nameof(TraceTitle));
    }
}
