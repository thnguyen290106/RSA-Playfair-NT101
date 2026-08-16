using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using RSA_Playfair_NT101.Core;
using RSA_Playfair_NT101.UI.Common;

namespace RSA_Playfair_NT101.UI.ViewModels;

/// <summary>Cách lấy khoá: sinh tự động hay nhập tay p, q.</summary>
public enum RsaKeyMode { Auto, Manual }

/// <summary>Cách hiển thị bản mã. Base64 là dạng gốc, hai dạng còn lại chỉ để xem.</summary>
public enum CipherFormat { Base64, Hex, Decimal }

/// <summary>
/// ViewModel cho toàn bộ màn hình RSA (khoá, mã hoá, chữ ký, phân tích).
/// </summary>
/// <remarks>
/// Bốn tab dùng chung một ViewModel vì chúng dùng chung đúng một thứ quan trọng:
/// cặp khoá hiện tại. Tách thành bốn ViewModel con sẽ phải dựng thêm cơ chế
/// đồng bộ khoá giữa chúng mà không đổi lại được gì.
/// </remarks>
public sealed class RsaViewModel : ViewModelBase
{
    /// <summary>Số byte header độ dài trong container của <see cref="RsaCipher"/>.</summary>
    private const int LengthHeaderBytes = 4;

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
    private RsaBlockTrace? _selectedBlock;
    private string _modPowSummary = string.Empty;

    // ---- Tab Chữ ký
    private string _signMessage = "Chuyển 5.000.000 VND cho Nguyễn Văn A";
    private string _signatureText = string.Empty;
    private string _signHashHex = string.Empty;
    private string _signError = string.Empty;
    private string _verifyStatus = string.Empty;
    private bool _verifyPassed;
    private bool _hasVerified;

    // ---- Tab Phân tích
    private string _factorInput = "3233";
    private string _factorStatus = string.Empty;
    private long _factorIterations;
    private string _factorElapsed = string.Empty;
    private bool _isFactoring;
    private CancellationTokenSource? _factorCts;
    private string _determinismText = string.Empty;

    public RsaViewModel()
    {
        GenerateKeyCommand = new AsyncRelayCommand(
            GenerateKeyAsync, ex => KeyError = Describe(ex), () => !IsGeneratingKey);
        CancelKeyCommand = new RelayCommand(() => _keyCts?.Cancel());

        EncryptCommand = new RelayCommand(Encrypt);
        DecryptCommand = new RelayCommand(Decrypt);

        SignCommand = new RelayCommand(Sign);
        VerifyCommand = new RelayCommand(Verify);
        TamperCommand = new RelayCommand(Tamper);

        FactorCommand = new AsyncRelayCommand(
            FactorAsync, ex => FactorStatus = Describe(ex), () => !IsFactoring);
        CancelFactorCommand = new RelayCommand(() => _factorCts?.Cancel());
        DeterminismCommand = new RelayCommand(ProveDeterminism);

        // Khởi động với khoá giáo trình 61 × 53: mọi tab dùng được ngay mà không
        // phải chờ sinh khoá, và các con số khớp ví dụ trong sách.
        ApplyManualKey();
    }

    public ICommand GenerateKeyCommand { get; }
    public ICommand CancelKeyCommand { get; }
    public ICommand EncryptCommand { get; }
    public ICommand DecryptCommand { get; }
    public ICommand SignCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand TamperCommand { get; }
    public ICommand FactorCommand { get; }
    public ICommand CancelFactorCommand { get; }
    public ICommand DeterminismCommand { get; }

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
    /// </summary>
    public string KeyNotes
    {
        get
        {
            if (_key is null)
            {
                return string.Empty;
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

            if (_key.Lambda != _key.Phi)
            {
                notes += $" Ghi chú: λ(n) = {_key.Lambda} nhỏ hơn φ(n) = {_key.Phi}; "
                    + "chuẩn PKCS#1 tính d theo λ(n), ứng dụng này tính theo φ(n) như giáo trình. "
                    + "Cả hai đều giải mã đúng.";
            }

            return notes;
        }
    }

    public string KeyProgress
    {
        get => _keyProgress;
        private set => SetProperty(ref _keyProgress, value);
    }

    public string KeyError
    {
        get => _keyError;
        private set => SetProperty(ref _keyError, value);
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
        }
        catch (OperationCanceledException)
        {
            KeyProgress = "Đã huỷ việc sinh khoá.";
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
    }

    /// <summary>
    /// Nhận khoá mới và xoá mọi kết quả sinh ra từ khoá cũ. Giữ lại bản mã cũ sẽ
    /// khiến lần giải mã sau báo lỗi mà người dùng không hiểu vì sao.
    /// </summary>
    private void SetKey(RsaKeyPair key)
    {
        Key = key;

        CipherText = string.Empty;
        DecryptedText = string.Empty;
        CipherError = string.Empty;
        BlockTraces.Clear();
        SelectedBlock = null;

        SignatureText = string.Empty;
        SignHashHex = string.Empty;
        SignError = string.Empty;
        VerifyStatus = string.Empty;
        HasVerified = false;

        DeterminismText = string.Empty;
        FactorStatus = string.Empty;
        FactorIterations = 0;
        FactorElapsed = string.Empty;

        // Chỉ nạp sẵn n vào tab Phân tích khi n còn trong tầm phân tích được.
        if (key.KeySizeBits <= RsaAttack.MaxFactorableBits)
        {
            FactorInput = key.N.ToString();
        }
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
        private set => SetProperty(ref _cipherError, value);
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

    // ================================================================ Tab Chữ ký

    public string SignMessage
    {
        get => _signMessage;
        set => SetProperty(ref _signMessage, value);
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
        private set => SetProperty(ref _signError, value);
    }

    public string VerifyStatus
    {
        get => _verifyStatus;
        private set => SetProperty(ref _verifyStatus, value);
    }

    /// <summary>Kết quả kiểm tra gần nhất, dùng để chọn màu băng thông báo.</summary>
    public bool VerifyPassed
    {
        get => _verifyPassed;
        private set => SetProperty(ref _verifyPassed, value);
    }

    /// <summary>Đã bấm kiểm tra lần nào chưa, để chưa bấm thì không hiện băng nào.</summary>
    public bool HasVerified
    {
        get => _hasVerified;
        private set => SetProperty(ref _hasVerified, value);
    }

    private void Sign()
    {
        SignError = string.Empty;
        VerifyStatus = string.Empty;
        HasVerified = false;
        SignatureText = string.Empty;
        SignHashHex = string.Empty;

        if (_key is null)
        {
            SignError = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
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

    private void Verify()
    {
        SignError = string.Empty;
        VerifyStatus = string.Empty;
        HasVerified = false;

        if (_key is null)
        {
            SignError = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
            return;
        }

        if (!BigInteger.TryParse(SignatureText?.Trim(), out BigInteger signature))
        {
            SignError = "Chữ ký phải là một số nguyên. Hãy ký trước, hoặc dán lại đúng chữ ký.";
            return;
        }

        try
        {
            RsaVerificationResult result = RsaSignature.Verify(
                SignMessage, signature, _key.N, _key.E);

            VerifyPassed = result.IsValid;
            HasVerified = true;
            VerifyStatus = result.IsValid
                ? $"HỢP LỆ — chữ ký khớp với thông điệp. Hash = {result.ExpectedHashHex}"
                : $"KHÔNG HỢP LỆ — hash của thông điệp là {result.ExpectedHashHex} "
                    + $"nhưng lấy từ chữ ký ra được {result.RecoveredHashHex}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SignError = Describe(ex);
        }
    }

    /// <summary>
    /// Sửa đúng một ký tự trong thông điệp rồi kiểm tra lại. Đây là phần đáng xem
    /// nhất của chữ ký số: đổi một ký tự là chữ ký sai ngay, không cần đổi nhiều.
    /// </summary>
    private void Tamper()
    {
        if (string.IsNullOrEmpty(SignMessage))
        {
            SignMessage = "0";
        }
        else
        {
            char last = SignMessage[^1];
            SignMessage = SignMessage[..^1] + (last == '0' ? '9' : '0');
        }

        Verify();
    }

    // ================================================================ Tab Phân tích

    public string FactorInput
    {
        get => _factorInput;
        set => SetProperty(ref _factorInput, value);
    }

    public string FactorStatus
    {
        get => _factorStatus;
        private set => SetProperty(ref _factorStatus, value);
    }

    public long FactorIterations
    {
        get => _factorIterations;
        private set => SetProperty(ref _factorIterations, value);
    }

    public string FactorElapsed
    {
        get => _factorElapsed;
        private set => SetProperty(ref _factorElapsed, value);
    }

    public bool IsFactoring
    {
        get => _isFactoring;
        private set => SetProperty(ref _isFactoring, value);
    }

    public string DeterminismText
    {
        get => _determinismText;
        private set => SetProperty(ref _determinismText, value);
    }

    /// <summary>
    /// Phân tích n bằng chia thử. Chạy nền và huỷ được, vì với n 64 bit việc này
    /// có thể mất rất lâu — chính điểm đó là nội dung cần cho thấy.
    /// </summary>
    private async Task FactorAsync()
    {
        FactorStatus = string.Empty;
        FactorIterations = 0;
        FactorElapsed = string.Empty;

        BigInteger n = ParseBigInteger(FactorInput, "n");

        // Chỉ suy ra được d khi n đúng là modulus của khoá đang dùng (mới biết e).
        BigInteger? publicExponent = _key is not null && _key.N == n ? _key.E : null;

        using CancellationTokenSource cts = new();
        _factorCts = cts;
        IsFactoring = true;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            Progress<long> progress = new(count =>
            {
                FactorIterations = count;
                FactorElapsed = FormatElapsed(stopwatch.Elapsed);
            });

            FactorResult? result = await RsaAttack.FactorAsync(
                n, publicExponent, progress, cts.Token);

            FactorIterations = result?.Iterations ?? FactorIterations;
            FactorElapsed = FormatElapsed(result?.Elapsed ?? stopwatch.Elapsed);

            FactorStatus = result is null
                ? $"Không tìm được thừa số: {n} không phải tích của hai số nguyên tố."
                : $"Tìm được p = {result.P} và q = {result.Q} sau {result.Iterations:N0} phép thử."
                    + (result.RecoveredPrivateExponent is { } d
                        ? $" Từ đó suy ra khoá riêng d = {d} — khoá đã bị phá."
                        : " Muốn suy ra d thì cần thêm e.");
        }
        catch (OperationCanceledException)
        {
            FactorStatus = $"Đã huỷ sau {FactorIterations:N0} phép thử.";
        }
        finally
        {
            IsFactoring = false;
            _factorCts = null;
        }
    }

    private static string FormatElapsed(TimeSpan elapsed) => $"{elapsed.TotalSeconds:0.000} giây";

    /// <summary>
    /// Mã hoá cùng một bản rõ hai lần rồi so sánh, cho thấy RSA không padding là
    /// tất định: kẻ nghe được có thể nhận ra hai bản tin giống nhau mà không cần
    /// giải mã.
    /// </summary>
    private void ProveDeterminism()
    {
        DeterminismText = string.Empty;

        if (_key is null)
        {
            DeterminismText = "Chưa có khoá. Hãy tạo khoá ở tab Khoá trước.";
            return;
        }

        string sample = string.IsNullOrEmpty(PlainText) ? "TAN CONG LUC 5 GIO" : PlainText;

        try
        {
            string first = RsaCipher.EncryptText(sample, _key.N, _key.E);
            string second = RsaCipher.EncryptText(sample, _key.N, _key.E);

            DeterminismText =
                $"Bản rõ: {sample}{Environment.NewLine}"
                + $"Lần mã hoá 1: {first}{Environment.NewLine}"
                + $"Lần mã hoá 2: {second}{Environment.NewLine}{Environment.NewLine}"
                + (first == second
                    ? "Hai bản mã giống nhau từng ký tự. RSA sách giáo khoa không có phần đệm "
                        + "ngẫu nhiên, nên cùng bản rõ và cùng khoá luôn cho cùng bản mã. "
                        + "Chuẩn thực tế (PKCS#1 v1.5, OAEP) thêm phần đệm ngẫu nhiên để phá "
                        + "tính chất này."
                    : "Hai bản mã khác nhau — điều này không đúng với RSA không padding.");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            DeterminismText = Describe(ex);
        }
    }
}
