using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using RSA_Playfair_NT101.Core;
using RSA_Playfair_NT101.UI.Common;

namespace RSA_Playfair_NT101.UI.ViewModels;

/// <summary>Cách lấy khoá: sinh tự động hay nhập tay p, q.</summary>
public enum RsaKeyMode { Auto, Manual }

/// <summary>Cách hiển thị bản mã. Base64 là dạng gốc, hai dạng còn lại chỉ để xem.</summary>
public enum CipherFormat { Base64, Hex, Decimal }

/// <summary>
/// ViewModel cho toàn bộ màn hình RSA (khoá, mã hoá, chữ ký).
/// </summary>
/// <remarks>
/// Ba tab dùng chung một ViewModel vì chúng dùng chung đúng một thứ quan trọng:
/// cặp khoá hiện tại. Tách thành ba ViewModel con sẽ phải dựng thêm cơ chế
/// đồng bộ khoá giữa chúng mà không đổi lại được gì.
/// </remarks>
public sealed class RsaViewModel : ViewModelBase
{
    /// <summary>Số byte header độ dài trong container của <see cref="RsaCipher"/>.</summary>
    private const int LengthHeaderBytes = 4;

    /// <summary>Tên ba file bên gửi xuất ra cho bên nhận.</summary>
    private const string DataFileName = "filedulieu.signed";
    private const string SignatureFileName = "chukyso.txt";
    private const string PublicKeyFileName = "publickey.txt";

    /// <summary>
    /// File khoá đầy đủ, chỉ của riêng người dùng. Cố ý không nằm trong bộ ba file
    /// trên: ba file kia là để đưa cho bên nhận, file này thì không đưa cho ai.
    /// </summary>
    private const string PrivateKeyFileName = "privatekey.txt";

    // ---- Tab Khoá
    private RsaKeyMode _keyMode = RsaKeyMode.Manual;
    private int _keySizeBits = 1024;
    private string _manualP = "61";
    private string _manualQ = "53";
    private string _manualE = "17";
    private RsaKeyPair? _key;
    private string _keyProgress = string.Empty;
    private string _keyError = string.Empty;
    private bool _isGeneratingKey;
    private CancellationTokenSource? _keyCts;

    // ---- Tab Mã hoá
    private string _plainText = "Xin chào!";
    private string _cipherText = string.Empty;
    private CipherFormat _cipherFormat = CipherFormat.Base64;
    private string _decryptedText = string.Empty;
    private string _cipherError = string.Empty;
    private string _cipherFileStatus = string.Empty;
    private RsaBlockTrace? _selectedBlock;
    private string _modPowSummary = string.Empty;

    // ---- Tab Chữ ký, cột trái (bên gửi: có khoá riêng)
    private string _signMessage = string.Empty;
    private string _signatureText = string.Empty;
    private string _signHashHex = string.Empty;
    private string _signError = string.Empty;
    private string _signFileStatus = string.Empty;

    // ---- Tab Chữ ký, cột phải (bên nhận: chỉ có dữ liệu, chữ ký và khoá công khai)
    private string _verifyMessage = string.Empty;
    private string _verifyHashHex = string.Empty;
    private string _verifySignatureText = string.Empty;
    private string _recoveredHashHex = string.Empty;
    private string _verifyPublicKeyN = string.Empty;
    private string _verifyPublicKeyE = string.Empty;
    private string _verifyError = string.Empty;
    private string _verifyStatus = string.Empty;
    private bool _verifyPassed;
    private bool _hasVerified;

    // Ba giá trị vừa đọc được từ ba ô bên phải. Giữ lại để bước "Xác minh" không
    // phải phân tích lại chuỗi mà bước "Giải mã" ngay trước đó đã phân tích xong.
    private BigInteger _verifyN;
    private BigInteger _verifyE;
    private BigInteger _verifySignature;

    public RsaViewModel()
    {
        GenerateKeyCommand = new AsyncRelayCommand(
            GenerateKeyAsync, ex => KeyError = Describe(ex), () => !IsGeneratingKey);
        CancelKeyCommand = new RelayCommand(() => _keyCts?.Cancel());
        SaveKeyFileCommand = new RelayCommand(SaveKeyFile);
        LoadKeyFileCommand = new RelayCommand(LoadKeyFile);

        EncryptCommand = new RelayCommand(Encrypt);
        DecryptCommand = new RelayCommand(Decrypt);
        LoadPlainTextFileCommand = new RelayCommand(LoadPlainTextFile);
        SaveCipherFileCommand = new RelayCommand(SaveCipherFile);
        LoadCipherFileCommand = new RelayCommand(LoadCipherFile);
        CopyCipherCommand = new RelayCommand(CopyCipher);

        SignCommand = new RelayCommand(Sign);
        LoadMessageFileCommand = new RelayCommand(LoadMessageFile);
        CopySignatureCommand = new RelayCommand(CopySignature);
        ExportSignatureFilesCommand = new RelayCommand(ExportSignatureFiles);

        LoadVerifyMessageFileCommand = new RelayCommand(LoadVerifyMessageFile);
        LoadVerifySignatureFileCommand = new RelayCommand(LoadVerifySignatureFile);
        LoadPublicKeyFileCommand = new RelayCommand(LoadPublicKeyFile);
        HashVerifyMessageCommand = new RelayCommand(HashVerifyMessage);
        DecryptSignatureCommand = new RelayCommand(DecryptSignature);
        VerifyCommand = new RelayCommand(Verify);

        // Không tự tạo khoá lúc mở app. Ô p, q, e đã điền sẵn 61, 53, 17 (khớp ví dụ
        // giáo trình) nên chỉ cần bấm "Tạo khoá" là có khoá — nhưng bấm hay không là
        // việc của người dùng, app không tự làm hộ rồi thông báo một việc chưa ai yêu cầu.
    }

    public ICommand GenerateKeyCommand { get; }
    public ICommand CancelKeyCommand { get; }
    public ICommand SaveKeyFileCommand { get; }
    public ICommand LoadKeyFileCommand { get; }

    public ICommand EncryptCommand { get; }
    public ICommand DecryptCommand { get; }
    public ICommand LoadPlainTextFileCommand { get; }
    public ICommand SaveCipherFileCommand { get; }
    public ICommand LoadCipherFileCommand { get; }
    public ICommand CopyCipherCommand { get; }

    public ICommand SignCommand { get; }
    public ICommand LoadMessageFileCommand { get; }
    public ICommand CopySignatureCommand { get; }
    public ICommand ExportSignatureFilesCommand { get; }

    public ICommand LoadVerifyMessageFileCommand { get; }
    public ICommand LoadVerifySignatureFileCommand { get; }
    public ICommand LoadPublicKeyFileCommand { get; }
    public ICommand HashVerifyMessageCommand { get; }
    public ICommand DecryptSignatureCommand { get; }
    public ICommand VerifyCommand { get; }

    /// <summary>Vết từng block khi mã hoá, hiển thị trong bảng.</summary>
    public ObservableCollection<RsaBlockTrace> BlockTraces { get; } = [];

    /// <summary>Các bước bình phương-và-nhân của block đang chọn.</summary>
    public ObservableCollection<ModPowStep> ModPowSteps { get; } = [];

    // ================================================================ Tab Khoá

    public RsaKeyMode KeyMode
    {
        get => _keyMode;
        set
        {
            if (SetProperty(ref _keyMode, value))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(IsAutoMode));
            }
        }
    }

    /// <summary>Dùng cho việc ẩn/hiện nhóm ô nhập p, q, e.</summary>
    public bool IsManualMode => _keyMode == RsaKeyMode.Manual;

    /// <summary>Dùng cho việc ẩn/hiện nhóm chọn độ dài khoá.</summary>
    public bool IsAutoMode => _keyMode == RsaKeyMode.Auto;

    public int KeySizeBits
    {
        get => _keySizeBits;
        set => SetProperty(ref _keySizeBits, value);
    }

    public string ManualP
    {
        get => _manualP;
        set => SetProperty(ref _manualP, value);
    }

    public string ManualQ
    {
        get => _manualQ;
        set => SetProperty(ref _manualQ, value);
    }

    public string ManualE
    {
        get => _manualE;
        set => SetProperty(ref _manualE, value);
    }

    public RsaKeyPair? Key
    {
        get => _key;
        private set
        {
            if (SetProperty(ref _key, value))
            {
                OnPropertyChanged(nameof(HasKey));
                OnPropertyChanged(nameof(KeyNotes));
                OnPropertyChanged(nameof(CanSign));
            }
        }
    }

    public bool HasKey => _key is not null;

    public bool CanSign => _key?.CanSign == true;

    /// <summary>
    /// Giải thích khoá hiện tại bằng lời: mỗi block chứa bao nhiêu byte và có ký
    /// được hay không. Người xem tự bấm thử nên phải nói rõ giới hạn ngay tại đây.
    /// Chưa có khoá thì nói luôn là chưa có, và nói cần bấm gì.
    /// </summary>
    public string KeyNotes
    {
        get
        {
            if (_key is null)
            {
                return "Chưa có khoá. Hai ô p và q ở trên đã điền sẵn 61 và 53 theo ví dụ "
                    + "giáo trình — bấm \"Tạo khoá\" là dùng được ngay. Muốn khoá ký được thì "
                    + "chọn chế độ Tự động, 1024 bit, rồi bấm \"Tạo khoá\". Lần trước đã lưu "
                    + "khoá ra file thì bấm \"Tải khoá từ file\" để dùng lại đúng khoá đó.";
            }

            string notes =
                $"Khoá {_key.KeySizeBits} bit. Mỗi block bản rõ {_key.PlainBlockBytes} byte, "
                + $"mỗi block bản mã {_key.CipherBlockBytes} byte.";

            if (!_key.CanEncryptText)
            {
                notes += " Khoá quá nhỏ để mã hoá văn bản (cần n ≥ 256).";
            }

            if (!_key.CanSign)
            {
                notes += $" Chưa ký được: chữ ký SHA-256 cần khoá ≥ "
                    + $"{RsaSignature.MinimumKeySizeBitsForSigning} bit. Hãy chọn chế độ Tự động "
                    + "và sinh khoá 1024 bit.";
            }

            return notes;
        }
    }

    /// <summary>Tiến trình sinh khoá.</summary>
    /// <remarks>
    /// Ô này không tự hiện hộp thoại: nó nhận cả tiến trình đang chạy, mỗi lần thử một
    /// số nguyên tố là một lần gán. Chỉ ba mốc kết thúc mới gọi <c>Notifier</c> tay.
    /// </remarks>
    public string KeyProgress
    {
        get => _keyProgress;
        private set => SetProperty(ref _keyProgress, value);
    }

    /// <remarks>
    /// Gán giá trị mới là hiện luôn hộp thoại, kể cả khi nội dung trùng lần trước:
    /// bấm nút hai lần mà chỉ hiện một lần thì lần thứ hai trông như nút bị kẹt.
    /// Vì vậy <c>Notifier</c> gọi ngoài kết quả trả về của <c>SetProperty</c>.
    /// </remarks>
    public string KeyError
    {
        get => _keyError;
        private set
        {
            SetProperty(ref _keyError, value);
            Notifier.Error(value);
        }
    }

    public bool IsGeneratingKey
    {
        get => _isGeneratingKey;
        private set => SetProperty(ref _isGeneratingKey, value);
    }

    /// <summary>
    /// Tạo khoá theo chế độ đang chọn. Chế độ thủ công chạy tức thì, chế độ tự
    /// động chạy trên thread nền để cửa sổ không đóng băng khi sinh khoá 2048 bit.
    /// </summary>
    private async Task GenerateKeyAsync()
    {
        KeyError = string.Empty;

        if (KeyMode == RsaKeyMode.Manual)
        {
            ApplyManualKey();
            return;
        }

        using CancellationTokenSource cts = new();
        _keyCts = cts;
        IsGeneratingKey = true;
        KeyProgress = $"Đang sinh khoá {KeySizeBits} bit…";

        try
        {
            Progress<string> progress = new(text => KeyProgress = text);
            RsaKeyPair key = await RsaKeyFactory.GenerateAsync(
                KeySizeBits, e: null, progress, cts.Token);

            SetKey(key);
            KeyProgress = $"Đã sinh khoá {key.KeySizeBits} bit.";
            Notifier.Info(KeyProgress);
        }
        catch (OperationCanceledException)
        {
            KeyProgress = "Đã huỷ việc sinh khoá.";
            Notifier.Info(KeyProgress);
        }
        finally
        {
            IsGeneratingKey = false;
            _keyCts = null;
        }
    }

    /// <summary>Dựng khoá từ p, q, e nhập tay.</summary>
    private void ApplyManualKey()
    {
        BigInteger p = ParseBigInteger(ManualP, "p");
        BigInteger q = ParseBigInteger(ManualQ, "q");
        BigInteger? e = string.IsNullOrWhiteSpace(ManualE)
            ? null
            : ParseBigInteger(ManualE, "e");

        SetKey(RsaKeyFactory.FromPrimes(p, q, e));
        KeyProgress = $"Đã tạo khoá từ p = {p}, q = {q}.";
        Notifier.Info(KeyProgress);
    }

    /// <summary>
    /// Ghi khoá hiện tại ra file để lần sau dùng lại. Đây là đường duy nhất giải mã
    /// lại được bản mã đã lưu: khoá sinh tự động không có cách nào dựng lại từ đầu.
    /// </summary>
    /// <remarks>
    /// File này chứa khoá riêng nên cảnh báo nằm ngay dưới hai nút trong giao diện,
    /// không chỉ trong tài liệu.
    /// </remarks>
    private void SaveKeyFile() => TryFileAction(
        () =>
        {
            KeyProgress = string.Empty;

            if (_key is null)
            {
                KeyError = "Chưa có khoá để lưu. Hãy bấm \"Tạo khoá\" trước.";
                return;
            }

            string? path = TextFileDialogs.WriteText(
                "Lưu khoá (chứa khoá riêng)",
                PrivateKeyFileName,
                TextFileDialogs.TextFilter,
                RsaKeyFile.FormatPrivate(_key));

            if (path is not null)
            {
                KeyProgress = $"Đã lưu khoá vào {path}";
                Notifier.Info(KeyProgress);
            }
        },
        error => KeyError = error);

    /// <summary>Dựng lại khoá từ p, q, e trong file — đi qua đúng FromPrimes như khoá nhập tay.</summary>
    private void LoadKeyFile() => TryFileAction(
        () =>
        {
            KeyProgress = string.Empty;

            string? text = TextFileDialogs.ReadText(
                "Chọn file khoá", TextFileDialogs.TextFilter, TextFileDialogs.NumberMaxBytes);

            if (text is null)
            {
                return;   // người dùng bấm Cancel
            }

            (BigInteger p, BigInteger q, BigInteger e) = RsaKeyFile.ParsePrivate(text);

            SetKey(RsaKeyFactory.FromPrimes(p, q, e));
            KeyProgress = $"Đã tải khoá {_key!.KeySizeBits} bit từ file.";
            Notifier.Info(KeyProgress);
        },
        error => KeyError = error);

    /// <summary>
    /// Nhận khoá mới và xoá mọi kết quả sinh ra từ khoá cũ. Giữ lại bản mã cũ sẽ
    /// khiến lần giải mã sau báo lỗi mà người dùng không hiểu vì sao.
    /// </summary>
    /// <remarks>
    /// Không đụng tới cột phải của tab chữ ký: nó xác minh bằng khoá công khai trong
    /// ô nhập của chính nó, không dùng khoá này, nên kết quả ở đó vẫn còn đúng.
    /// </remarks>
    private void SetKey(RsaKeyPair key)
    {
        Key = key;

        CipherText = string.Empty;
        DecryptedText = string.Empty;
        CipherError = string.Empty;
        CipherFileStatus = string.Empty;
        BlockTraces.Clear();
        SelectedBlock = null;

        SignatureText = string.Empty;
        SignHashHex = string.Empty;
        SignError = string.Empty;
        SignFileStatus = string.Empty;
    }

    private static BigInteger ParseBigInteger(string text, string label)
    {
        if (!BigInteger.TryParse(text?.Trim(), out BigInteger value))
        {
            throw new FormatException($"Giá trị {label} không phải số nguyên hợp lệ: \"{text}\".");
        }

        return value;
    }

    /// <summary>Lấy thông báo lỗi sạch, bỏ phần "(Parameter '…')" của .NET.</summary>
    private static string Describe(Exception ex)
        => ex is ArgumentException { ParamName: { } paramName } argument
            ? argument.Message.Replace($" (Parameter '{paramName}')", string.Empty)
            : ex.Message;

    /// <summary>
    /// Chạy một việc có đụng tới file hoặc clipboard, và đưa mọi lỗi ra băng thông báo
    /// tương ứng thay vì để ngoại lệ làm sập cửa sổ.
    /// </summary>
    /// <remarks>
    /// Chỉ bắt các loại lỗi biết trước của việc đọc/ghi file, phân tích nội dung file
    /// và mở clipboard — lỗi lập trình vẫn phải nổ ra để còn sửa được. Lỗi luôn được
    /// hiện lên giao diện, không có nhánh nào nuốt lỗi im lặng.
    /// </remarks>
    private static void TryFileAction(Action action, Action<string> showError)
    {
        showError(string.Empty);

        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or FormatException or InvalidOperationException or ArgumentException
            or NotSupportedException or ExternalException)
        {
            showError(Describe(ex));
        }
    }

    // ================================================================ Tab Mã hoá

    public string PlainText
    {
        get => _plainText;
        set => SetProperty(ref _plainText, value);
    }

    /// <summary>Bản mã ở dạng gốc Base64. Đây là ô dùng cho cả mã hoá và giải mã.</summary>
    public string CipherText
    {
        get => _cipherText;
        set
        {
            if (SetProperty(ref _cipherText, value))
            {
                OnPropertyChanged(nameof(CipherView));
            }
        }
    }

    public CipherFormat CipherFormat
    {
        get => _cipherFormat;
        set
        {
            if (SetProperty(ref _cipherFormat, value))
            {
                OnPropertyChanged(nameof(CipherView));
            }
        }
    }

    /// <summary>
    /// Bản mã ở dạng đang chọn, chỉ để xem. Chỉ Base64 mới quay lại được thành
    /// bản rõ, nên ô nhập giải mã luôn dùng Base64 — không cần viết bộ phân tích
    /// ngược cho hex và thập phân.
    /// </summary>
    public string CipherView
    {
        get
        {
            if (string.IsNullOrEmpty(_cipherText))
            {
                return string.Empty;
            }

            if (_cipherFormat == CipherFormat.Base64)
            {
                return _cipherText;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(_cipherText.Trim());
            }
            catch (FormatException)
            {
                return "(bản mã hiện tại không phải Base64 hợp lệ)";
            }

            return _cipherFormat == CipherFormat.Hex
                ? Convert.ToHexString(bytes)
                : DescribeBlocksAsDecimal(bytes);
        }
    }

    /// <summary>
    /// Tách phần dữ liệu thành từng block và in giá trị <c>c</c> dạng thập phân.
    /// Đây là dạng gần nhất với công thức trên bảng, nên dễ đối chiếu bằng mắt.
    /// </summary>
    private string DescribeBlocksAsDecimal(byte[] cipherBytes)
    {
        if (_key is null)
        {
            return "(chưa có khoá để biết kích thước block)";
        }

        int width = _key.CipherBlockBytes;
        int payloadLength = cipherBytes.Length - LengthHeaderBytes;

        if (payloadLength < 0 || payloadLength % width != 0)
        {
            return "(bản mã không khớp kích thước block của khoá hiện tại)";
        }

        return string.Join(
            Environment.NewLine,
            Enumerable.Range(0, payloadLength / width).Select(index =>
            {
                BigInteger c = new(
                    new ReadOnlySpan<byte>(cipherBytes, LengthHeaderBytes + (index * width), width),
                    isUnsigned: true,
                    isBigEndian: true);

                return $"c{index + 1} = {c}";
            }));
    }

    public string DecryptedText
    {
        get => _decryptedText;
        private set => SetProperty(ref _decryptedText, value);
    }

    public string CipherError
    {
        get => _cipherError;
        private set
        {
            SetProperty(ref _cipherError, value);
            Notifier.Error(value);
        }
    }

    /// <summary>Báo đã lưu/tải/sao chép cái gì. Rỗng thì caption tự ẩn.</summary>
    public string CipherFileStatus
    {
        get => _cipherFileStatus;
        private set
        {
            SetProperty(ref _cipherFileStatus, value);
            Notifier.Info(value);
        }
    }

    /// <summary>Block đang chọn trong bảng vết; đổi block thì đổi luôn vết modpow.</summary>
    public RsaBlockTrace? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (SetProperty(ref _selectedBlock, value))
            {
                RefreshModPowTrace();
            }
        }
    }

    public string ModPowSummary
    {
        get => _modPowSummary;
        private set => SetProperty(ref _modPowSummary, value);
    }

    private void Encrypt()
    {
        CipherError = string.Empty;
        DecryptedText = string.Empty;
        BlockTraces.Clear();
        SelectedBlock = null;

        if (_key is null)
        {
            CipherError = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
            return;
        }

        try
        {
            CipherText = RsaCipher.EncryptText(PlainText, _key.N, _key.E);

            foreach (RsaBlockTrace trace in RsaCipher.TraceEncrypt(PlainText, _key))
            {
                BlockTraces.Add(trace);
            }

            SelectedBlock = BlockTraces.FirstOrDefault();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            CipherError = Describe(ex);
        }
    }

    private void Decrypt()
    {
        CipherError = string.Empty;
        DecryptedText = string.Empty;

        if (_key is null)
        {
            CipherError = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
            return;
        }

        try
        {
            DecryptedText = RsaCipher.DecryptText(CipherText, _key.N, _key.D);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            CipherError = Describe(ex);
        }
    }

    private void LoadPlainTextFile() => TryFileAction(
        () =>
        {
            CipherFileStatus = string.Empty;

            string? text = TextFileDialogs.ReadText(
                "Chọn file bản rõ",
                TextFileDialogs.TextFilter,
                TextFileDialogs.DocumentMaxBytes);

            if (text is not null)
            {
                PlainText = text;
                CipherFileStatus = "Đã tải bản rõ từ file.";
            }
        },
        error => CipherError = error);

    /// <summary>
    /// Lưu bản mã ra file. Luôn lưu dạng Base64 dù ô đang xem là hex hay thập phân:
    /// chỉ Base64 mới giải mã lại được.
    /// </summary>
    private void SaveCipherFile() => TryFileAction(
        () =>
        {
            CipherFileStatus = string.Empty;

            if (string.IsNullOrWhiteSpace(CipherText))
            {
                CipherError = "Chưa có bản mã để lưu. Hãy bấm Mã hoá trước.";
                return;
            }

            string? path = TextFileDialogs.WriteText(
                "Lưu bản mã (Base64)", "banma.txt", TextFileDialogs.TextFilter, CipherText);

            if (path is not null)
            {
                CipherFileStatus = $"Đã lưu bản mã vào {path}";
            }
        },
        error => CipherError = error);

    private void LoadCipherFile() => TryFileAction(
        () =>
        {
            CipherFileStatus = string.Empty;

            string? text = TextFileDialogs.ReadText(
                "Chọn file bản mã (Base64)",
                TextFileDialogs.TextFilter,
                TextFileDialogs.NumberMaxBytes);

            if (text is not null)
            {
                CipherText = text.Trim();
                DecryptedText = string.Empty;
                CipherFileStatus = "Đã tải bản mã từ file.";
            }
        },
        error => CipherError = error);

    private void CopyCipher() => TryFileAction(
        () =>
        {
            CipherFileStatus = string.Empty;

            if (string.IsNullOrWhiteSpace(CipherText))
            {
                CipherError = "Chưa có bản mã để sao chép. Hãy bấm Mã hoá trước.";
                return;
            }

            Clipboard.SetText(CipherText);
            CipherFileStatus = "Đã sao chép bản mã (Base64) vào clipboard.";
        },
        error => CipherError = error);

    /// <summary>
    /// Dựng lại vết bình phương-và-nhân cho block đang chọn. Số bước bằng số bit
    /// của <c>e</c>, nên với e = 65537 chỉ có 17 bước — vừa đủ xem hết.
    /// </summary>
    private void RefreshModPowTrace()
    {
        ModPowSteps.Clear();
        ModPowSummary = string.Empty;

        if (_selectedBlock is null || _key is null)
        {
            return;
        }

        ModPowTrace trace = BigIntegerMath.TracedModPow(
            _selectedBlock.PlainValue, _key.E, _key.N);

        foreach (ModPowStep step in trace.Steps)
        {
            ModPowSteps.Add(step);
        }

        ModPowSummary =
            $"Block {_selectedBlock.BlockIndex}: m = {_selectedBlock.PlainValue}, "
            + $"e có {trace.TotalBits} bit → c = {trace.Value}"
            + (trace.Truncated ? " (đã lược bớt các bước ở giữa)" : string.Empty);
    }

    // ============================================ Tab Chữ ký — cột trái (bên gửi)

    public string SignMessage
    {
        get => _signMessage;
        set
        {
            if (SetProperty(ref _signMessage, value))
            {
                // Đổi thông điệp thì bản băm và chữ ký cũ không còn thuộc về nó nữa.
                // Giữ chúng trên màn hình là nói dối về việc vừa ký cái gì.
                SignHashHex = string.Empty;
                SignatureText = string.Empty;
                SignFileStatus = string.Empty;
            }
        }
    }

    public string SignatureText
    {
        get => _signatureText;
        set => SetProperty(ref _signatureText, value);
    }

    public string SignHashHex
    {
        get => _signHashHex;
        private set => SetProperty(ref _signHashHex, value);
    }

    public string SignError
    {
        get => _signError;
        private set
        {
            SetProperty(ref _signError, value);
            Notifier.Error(value);
        }
    }

    /// <summary>Báo đã ghi file hay sao chép cái gì. Rỗng thì caption tự ẩn.</summary>
    public string SignFileStatus
    {
        get => _signFileStatus;
        private set
        {
            SetProperty(ref _signFileStatus, value);
            Notifier.Info(value);
        }
    }

    private void Sign()
    {
        SignError = string.Empty;
        SignFileStatus = string.Empty;
        SignatureText = string.Empty;
        SignHashHex = string.Empty;

        if (_key is null)
        {
            SignError = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
            return;
        }

        if (string.IsNullOrEmpty(SignMessage))
        {
            SignError = "Chưa có thông điệp để ký. Hãy nhập vào ô trên hoặc bấm Tải file.";
            return;
        }

        try
        {
            RsaSignatureResult result = RsaSignature.Sign(SignMessage, _key);
            SignatureText = result.Signature.ToString();
            SignHashHex = result.HashHex;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SignError = Describe(ex);
        }
    }

    private void LoadMessageFile() => TryFileAction(
        () =>
        {
            string? text = TextFileDialogs.ReadText(
                "Chọn file cần ký",
                TextFileDialogs.DocumentFilter,
                TextFileDialogs.DocumentMaxBytes);

            if (text is not null)
            {
                // Setter của SignMessage tự xoá bản băm và chữ ký của nội dung cũ.
                SignMessage = text;
                SignFileStatus = "Đã tải nội dung cần ký từ file.";
            }
        },
        error => SignError = error);

    private void CopySignature() => TryFileAction(
        () =>
        {
            SignFileStatus = string.Empty;

            if (string.IsNullOrWhiteSpace(SignatureText))
            {
                SignError = "Chưa có chữ ký để sao chép. Hãy bấm Ký số trước.";
                return;
            }

            Clipboard.SetText(SignatureText);
            SignFileStatus = "Đã sao chép chữ ký vào clipboard.";
        },
        error => SignError = error);

    /// <summary>
    /// Ghi ra ba file mà bên nhận cần: nội dung đã ký, chữ ký, và khoá công khai.
    /// Khoá riêng không nằm trong file nào — đó mới là điểm của chữ ký số.
    /// </summary>
    private void ExportSignatureFiles() => TryFileAction(
        () =>
        {
            SignFileStatus = string.Empty;

            if (_key is null || string.IsNullOrWhiteSpace(SignatureText))
            {
                SignError = "Chưa có chữ ký để xuất. Hãy bấm Ký số trước.";
                return;
            }

            string? folder = TextFileDialogs.PickFolder("Chọn thư mục để lưu 3 file");

            if (folder is null)
            {
                return;
            }

            string[] names = [DataFileName, SignatureFileName, PublicKeyFileName];
            string[] existing = [.. names.Where(name => File.Exists(Path.Combine(folder, name)))];

            // Ghi đè im lặng lên file của người khác là phá dữ liệu: phải hỏi trước.
            if (existing.Length > 0
                && MessageBox.Show(
                    $"Thư mục này đã có {string.Join(", ", existing)}.\nGhi đè?",
                    "Ghi đè file?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                SignFileStatus = "Đã dừng, không ghi file nào.";
                return;
            }

            File.WriteAllText(Path.Combine(folder, DataFileName), SignMessage);
            File.WriteAllText(Path.Combine(folder, SignatureFileName), SignatureText);
            File.WriteAllText(
                Path.Combine(folder, PublicKeyFileName), RsaKeyFile.Format(_key.N, _key.E));

            SignFileStatus = $"Đã ghi 3 file vào {folder}";
        },
        error => SignError = error);

    // =========================================== Tab Chữ ký — cột phải (bên nhận)

    // Cột này cố ý không dùng _key: bên nhận chỉ có dữ liệu, chữ ký và khoá công
    // khai lấy từ ba file. Nếu nó đọc _key thì demo mất ý nghĩa — thành ra một máy
    // tự ký rồi tự kiểm lại chính mình.

    /// <summary>Dữ liệu bên nhận nhận được, cần băm lại để so.</summary>
    public string VerifyMessage
    {
        get => _verifyMessage;
        set
        {
            if (SetProperty(ref _verifyMessage, value))
            {
                // Đổi dữ liệu là phải băm lại; đây cũng chính là chỗ làm được demo
                // "sửa 1 ký tự → xác minh thất bại" mà không cần nút riêng.
                VerifyHashHex = string.Empty;
                HasVerified = false;
            }
        }
    }

    public string VerifyHashHex
    {
        get => _verifyHashHex;
        private set => SetProperty(ref _verifyHashHex, value);
    }

    public string VerifySignatureText
    {
        get => _verifySignatureText;
        set
        {
            if (SetProperty(ref _verifySignatureText, value))
            {
                RecoveredHashHex = string.Empty;
                HasVerified = false;
            }
        }
    }

    /// <summary>Giá trị băm lấy lại được từ chữ ký: <c>s^e mod n</c>.</summary>
    public string RecoveredHashHex
    {
        get => _recoveredHashHex;
        private set => SetProperty(ref _recoveredHashHex, value);
    }

    public string VerifyPublicKeyN
    {
        get => _verifyPublicKeyN;
        set
        {
            if (SetProperty(ref _verifyPublicKeyN, value))
            {
                InvalidateRecoveredHash();
            }
        }
    }

    public string VerifyPublicKeyE
    {
        get => _verifyPublicKeyE;
        set
        {
            if (SetProperty(ref _verifyPublicKeyE, value))
            {
                InvalidateRecoveredHash();
            }
        }
    }

    /// <summary>
    /// Nhắc khi chưa có khoá công khai. Không chặn nút nào — người dùng vẫn được tự
    /// nhập <c>n</c>, <c>e</c> bằng tay, chỉ là nếu thiếu thì nói rõ đang thiếu gì.
    /// </summary>
    public string VerifyKeyNotice
        => string.IsNullOrWhiteSpace(_verifyPublicKeyN)
            || string.IsNullOrWhiteSpace(_verifyPublicKeyE)
                ? "Chưa có khoá công khai (public key) — bấm \"Tải public key\" để đọc "
                    + "publickey.txt, hoặc tự nhập n và e vào hai ô dưới."
                : string.Empty;

    public string VerifyError
    {
        get => _verifyError;
        private set
        {
            SetProperty(ref _verifyError, value);
            Notifier.Error(value);
        }
    }

    /// <remarks>
    /// <see cref="Verify"/> gán <see cref="VerifyPassed"/> trước ô này, nên biểu tượng
    /// của hộp thoại lấy theo giá trị đã đúng của lần xác minh vừa xong.
    /// </remarks>
    public string VerifyStatus
    {
        get => _verifyStatus;
        private set
        {
            SetProperty(ref _verifyStatus, value);
            Notifier.Result(value, _verifyPassed);
        }
    }

    /// <summary>Kết quả kiểm tra gần nhất, dùng để chọn màu băng thông báo.</summary>
    public bool VerifyPassed
    {
        get => _verifyPassed;
        private set => SetProperty(ref _verifyPassed, value);
    }

    /// <summary>Đã bấm xác minh lần nào chưa, để chưa bấm thì không hiện băng nào.</summary>
    public bool HasVerified
    {
        get => _hasVerified;
        private set => SetProperty(ref _hasVerified, value);
    }

    private void InvalidateRecoveredHash()
    {
        RecoveredHashHex = string.Empty;
        HasVerified = false;
        OnPropertyChanged(nameof(VerifyKeyNotice));
    }

    private void LoadVerifyMessageFile() => TryFileAction(
        () =>
        {
            string? text = TextFileDialogs.ReadText(
                "Chọn file dữ liệu nhận được",
                TextFileDialogs.DocumentFilter,
                TextFileDialogs.DocumentMaxBytes);

            if (text is not null)
            {
                VerifyMessage = text;
            }
        },
        error => VerifyError = error);

    private void LoadVerifySignatureFile() => TryFileAction(
        () =>
        {
            string? text = TextFileDialogs.ReadText(
                "Chọn file chữ ký số",
                TextFileDialogs.TextFilter,
                TextFileDialogs.NumberMaxBytes);

            if (text is not null)
            {
                VerifySignatureText = text.Trim();
            }
        },
        error => VerifyError = error);

    private void LoadPublicKeyFile() => TryFileAction(
        () =>
        {
            string? text = TextFileDialogs.ReadText(
                "Chọn file khoá công khai",
                TextFileDialogs.TextFilter,
                TextFileDialogs.NumberMaxBytes);

            if (text is null)
            {
                return;
            }

            (BigInteger n, BigInteger e) = RsaKeyFile.Parse(text);
            VerifyPublicKeyN = n.ToString();
            VerifyPublicKeyE = e.ToString();
        },
        error => VerifyError = error);

    private void HashVerifyMessage()
    {
        VerifyError = string.Empty;
        HasVerified = false;
        TryHashVerifyMessage();
    }

    private void DecryptSignature()
    {
        VerifyError = string.Empty;
        HasVerified = false;
        TryDecryptSignature();
    }

    /// <summary>
    /// Chạy đủ ba bước của bên nhận rồi phán quyết. Bấm riêng "Băm dữ liệu" và
    /// "Giải mã" chỉ để xem từng bước; bấm "Xác minh" một mình vẫn điền cả hai ô.
    /// </summary>
    private void Verify()
    {
        VerifyError = string.Empty;
        VerifyStatus = string.Empty;
        HasVerified = false;

        if (!TryHashVerifyMessage() || !TryDecryptSignature())
        {
            return;
        }

        // Phán quyết vẫn để Core.RsaSignature.Verify đưa ra — nó so trên số nguyên,
        // không so hai chuỗi hex đang hiện trên màn hình.
        RsaVerificationResult result = RsaSignature.Verify(
            VerifyMessage, _verifySignature, _verifyN, _verifyE);

        VerifyPassed = result.IsValid;
        HasVerified = true;
        VerifyStatus = result.IsValid
            ? "HỢP LỆ — chữ ký khớp với dữ liệu nhận được."
            : "KHÔNG HỢP LỆ — bản băm tính lại và bản băm lấy từ chữ ký không khớp.";
    }

    /// <summary>Băm lại dữ liệu nhận được. Trả về <c>false</c> khi chưa có dữ liệu.</summary>
    private bool TryHashVerifyMessage()
    {
        if (string.IsNullOrEmpty(VerifyMessage))
        {
            VerifyHashHex = string.Empty;
            VerifyError = "Chưa có dữ liệu để băm. Hãy bấm \"Tải file dữ liệu\" "
                + "hoặc gõ vào ô dữ liệu nhận được.";
            return false;
        }

        VerifyHashHex = Convert.ToHexString(RsaSignature.ComputeHash(VerifyMessage));
        return true;
    }

    /// <summary>
    /// Đọc <c>n</c>, <c>e</c>, chữ ký từ ba ô rồi lấy lại giá trị băm bằng khoá công
    /// khai. Trả về <c>false</c> và điền <see cref="VerifyError"/> khi ô nào sai.
    /// </summary>
    private bool TryDecryptSignature()
    {
        RecoveredHashHex = string.Empty;

        try
        {
            _verifyN = ParseBigInteger(VerifyPublicKeyN, "n");
            _verifyE = ParseBigInteger(VerifyPublicKeyE, "e");
            _verifySignature = ParseBigInteger(VerifySignatureText, "chữ ký");

            RecoveredHashHex = RsaSignature.RecoverHashHex(_verifySignature, _verifyN, _verifyE);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException
            or InvalidOperationException)
        {
            VerifyError = Describe(ex);
            return false;
        }
    }
}
