# Sinh installer/app.ico từ màu PrimaryColor của UI/Theme/Palette.xaml.
# Chạy lại khi muốn đổi màu/chữ; file .ico được commit nên bình thường không cần chạy.
#
# Máy này không có ImageMagick/Pillow, nên dùng System.Drawing rồi tự ghép container ICO.
# ICO cho phép nhúng frame PNG từ Windows Vista trở lên — app chỉ hỗ trợ Win10/11 nên an toàn.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$Background = [System.Drawing.ColorTranslator]::FromHtml('#4F46E5')  # PrimaryColor
$Glyph      = 'RP'
$Sizes      = @(16, 32, 48, 256)
$OutPath    = Join-Path $PSScriptRoot 'app.ico'

function New-FramePng([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        # Nền: bo góc r = size/8, giống bo góc icon app trên Windows.
        $r = [Math]::Max(2, [int]($Size / 8))
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $r * 2
        $path.AddArc(0, 0, $d, $d, 180, 90)
        $path.AddArc($Size - $d - 1, 0, $d, $d, 270, 90)
        $path.AddArc($Size - $d - 1, $Size - $d - 1, $d, $d, 0, 90)
        $path.AddArc(0, $Size - $d - 1, $d, $d, 90, 90)
        $path.CloseFigure()
        $brush = New-Object System.Drawing.SolidBrush($Background)
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()

        # Chữ trắng canh giữa. 0.44 chọn bằng mắt cho glyph 2 ký tự.
        # Cast [single] rõ ràng: nếu để PowerShell tự chọn overload của Font thì nó bắt sai.
        [single]$emSize = $Size * 0.44
        $font = New-Object System.Drawing.Font('Segoe UI', $emSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = [System.Drawing.StringAlignment]::Center
        $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
        $g.DrawString($Glyph, $font, [System.Drawing.Brushes]::White, $rect, $fmt)
        $fmt.Dispose()
        $font.Dispose()
    } finally {
        $g.Dispose()
    }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $ms.ToArray()
}

$frames = @{}
foreach ($s in $Sizes) {
    # Cast [byte[]]: PowerShell làm phẳng mảng trả về từ function thành Object[], khiến
    # BinaryWriter.Write() bắt sai overload và chỉ ghi được vài byte.
    $frames[$s] = [byte[]](New-FramePng $s)
}

# ICONDIR (6 byte) + ICONDIRENTRY (16 byte mỗi frame) + dữ liệu PNG.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0)             # reserved
$w.Write([uint16]1)             # type = icon
$w.Write([uint16]$Sizes.Count)

$offset = 6 + 16 * $Sizes.Count
foreach ($s in $Sizes) {
    $png = $frames[$s]
    # 256 được ghi là 0 theo đặc tả — trường rộng/cao chỉ 1 byte.
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim)
    $w.Write([byte]$dim)
    $w.Write([byte]0)           # số màu trong palette (0 = truecolor)
    $w.Write([byte]0)           # reserved
    $w.Write([uint16]1)         # color planes
    $w.Write([uint16]32)        # bits per pixel
    $w.Write([uint32]$png.Length)
    $w.Write([uint32]$offset)
    $offset += $png.Length
}
foreach ($s in $Sizes) { $w.Write($frames[$s]) }
$w.Flush()

[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())
$w.Dispose()

# Kiểm tra ngay: đọc lại file, parse ICONDIR và decode từng frame. Nếu offset/độ dài
# sai thì bước này ném lỗi tại đây thay vì để icon hỏng lọt vào bản build.
# Không dùng System.Drawing.Icon để kiểm — GDI+ không decode được frame nén PNG.
$bytes = [System.IO.File]::ReadAllBytes($OutPath)
$count = [BitConverter]::ToUInt16($bytes, 4)
if ($count -ne $Sizes.Count) { throw "ICONDIR khai $count frame, ky vong $($Sizes.Count)" }
for ($i = 0; $i -lt $count; $i++) {
    $e = 6 + 16 * $i
    $expected = $Sizes[$i]
    $len = [BitConverter]::ToUInt32($bytes, $e + 8)
    $off = [BitConverter]::ToUInt32($bytes, $e + 12)
    if ($off + $len -gt $bytes.Length) { throw "Frame $i tro ra ngoai file" }
    $ms = New-Object System.IO.MemoryStream($bytes, [int]$off, [int]$len)
    $bmp = [System.Drawing.Image]::FromStream($ms)
    if ($bmp.Width -ne $expected -or $bmp.Height -ne $expected) {
        throw "Frame $i la $($bmp.Width)x$($bmp.Height), ky vong ${expected}px"
    }
    $bmp.Dispose()
    $ms.Dispose()
}

Write-Host "OK: $OutPath ($($bytes.Length) byte, $count frame: $($Sizes -join ', ')px)"
