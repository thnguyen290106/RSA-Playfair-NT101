using System.Numerics;

namespace RSA_Playfair_NT101.Core;

/// <summary>
/// Cặp khoá RSA kèm các giá trị trung gian dùng để hiển thị và giảng giải.
/// </summary>
/// <remarks>
/// Một khoá RSA "thật" chỉ cần <c>(n, e)</c> để mã hoá và <c>(n, d)</c> để giải
/// mã. Ở đây giữ cả <c>p</c>, <c>q</c>, <c>φ(n)</c> vì ứng dụng cần hiện toàn bộ
/// quá trình dẫn xuất khoá. Trong hệ thống thật, <c>p</c> và <c>q</c> phải bị
/// xoá sau khi sinh khoá vì biết chúng là biết ngay khoá riêng.
/// </remarks>
/// <param name="N">Modulus, <c>n = p·q</c>. Công khai.</param>
/// <param name="E">Số mũ công khai, khả nghịch modulo <c>φ(n)</c>.</param>
/// <param name="D">Số mũ riêng, <c>d ≡ e⁻¹ (mod φ(n))</c>. Bí mật.</param>
/// <param name="P">Số nguyên tố thứ nhất. Bí mật.</param>
/// <param name="Q">Số nguyên tố thứ hai. Bí mật.</param>
/// <param name="Phi">Hàm Euler <c>φ(n) = (p-1)(q-1)</c>.</param>
/// <param name="Lambda">
/// Hàm Carmichael <c>λ(n) = lcm(p-1, q-1)</c>. Chuẩn PKCS#1 thực tế dùng giá trị
/// này để cho <c>d</c> nhỏ hơn. Ứng dụng tính <c>d</c> theo <c>φ(n)</c> — công thức
/// quen hơn và không thêm khái niệm mới — nhưng vẫn hiện <c>λ(n)</c> để đối chiếu.
/// </param>
public sealed record RsaKeyPair(
    BigInteger N,
    BigInteger E,
    BigInteger D,
    BigInteger P,
    BigInteger Q,
    BigInteger Phi,
    BigInteger Lambda)
{
    /// <summary>Độ dài modulus tính theo bit. Đây là "độ dài khoá".</summary>
    public int KeySizeBits => (int)N.GetBitLength();

    /// <summary>
    /// Số byte bản rõ tối đa cho mỗi block.
    /// </summary>
    /// <remarks>
    /// Công thức <c>(bitLength(n) - 1) / 8</c> đảm bảo mọi block đều nhỏ hơn
    /// <c>n</c> với <em>mọi</em> giá trị byte có thể. Ví dụ n có 12 bit
    /// (n = 3233): block 1 byte có giá trị tối đa 255, luôn nhỏ hơn 3233. Nếu
    /// lấy 2 byte thì giá trị tối đa 65535 lớn hơn n, và phép modulo sẽ làm mất
    /// thông tin không thể phục hồi.
    /// </remarks>
    public int PlainBlockBytes => (KeySizeBits - 1) / 8;

    /// <summary>
    /// Số byte cố định cho mỗi block bản mã.
    /// </summary>
    /// <remarks>
    /// Bản mã có thể là bất kỳ giá trị trong <c>[0, n)</c>, nên cần đủ byte để
    /// chứa <c>n - 1</c>. Kích thước phải cố định để lúc giải mã biết cắt block
    /// ở đâu.
    /// </remarks>
    public int CipherBlockBytes => (KeySizeBits + 7) / 8;

    /// <summary>
    /// Khoá có đủ lớn để mã hoá văn bản hay không. Cần ít nhất 1 byte bản rõ mỗi
    /// block, tức <c>n ≥ 256</c>.
    /// </summary>
    public bool CanEncryptText => PlainBlockBytes >= 1;

    /// <summary>
    /// Khoá có đủ lớn để ký bằng SHA-256 hay không.
    /// </summary>
    /// <remarks>
    /// Giá trị băm SHA-256 là số 256 bit, phải nhỏ hơn <c>n</c> mới ký được.
    /// Không được "chữa" bằng cách lấy <c>hash mod n</c>: làm vậy phá tính an
    /// toàn của chữ ký, vì nhiều bản tin khác nhau sẽ cho cùng giá trị ký.
    /// </remarks>
    public bool CanSign => KeySizeBits >= RsaSignature.MinimumKeySizeBitsForSigning;

    /// <summary>Khoá công khai: cặp <c>(n, e)</c>.</summary>
    public (BigInteger N, BigInteger E) PublicKey => (N, E);

    /// <summary>Khoá riêng: cặp <c>(n, d)</c>.</summary>
    public (BigInteger N, BigInteger D) PrivateKey => (N, D);
}

/// <summary>
/// Vết mã hoá/giải mã của một block: giá trị trước và sau phép luỹ thừa modulo,
/// kèm bản rõ dạng đọc được nếu có.
/// </summary>
/// <param name="BlockIndex">Số thứ tự block, bắt đầu từ 1.</param>
/// <param name="PlainBytes">Các byte bản rõ của block.</param>
/// <param name="PlainPreview">
/// Bản rõ dạng chữ nếu giải mã UTF-8 được, ngược lại là dạng hex.
/// </param>
/// <param name="PlainValue">Block bản rõ dưới dạng số nguyên <c>m</c>.</param>
/// <param name="CipherValue">Bản mã <c>c = m^e mod n</c>.</param>
public sealed record RsaBlockTrace(
    int BlockIndex,
    IReadOnlyList<byte> PlainBytes,
    string PlainPreview,
    BigInteger PlainValue,
    BigInteger CipherValue);
