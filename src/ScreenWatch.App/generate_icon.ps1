Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$teal = [System.Drawing.Color]::FromArgb(255, 13, 115, 119)
$darkTeal = [System.Drawing.Color]::FromArgb(255, 7, 79, 82)
$white = [System.Drawing.Color]::White

# 圆角方形背景
$radius = 50
$rectSize = [int]($size - 17)
$rect = New-Object System.Drawing.Rectangle(8, 8, $rectSize, $rectSize)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
$path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
$path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
$path.CloseFigure()
$brush = New-Object System.Drawing.SolidBrush($teal)
$g.FillPath($brush, $path)

# 白色时钟圆
$clockR = 72
$cx = $size / 2
$cy = $size / 2
$whiteBrush = New-Object System.Drawing.SolidBrush($white)
$g.FillEllipse($whiteBrush, $cx - $clockR, $cy - $clockR, $clockR * 2, $clockR * 2)

# 时针（指向10点 = -60度）
$pen = New-Object System.Drawing.Pen($darkTeal, 9.0)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$hourAng = -60.0 * [Math]::PI / 180.0
$hourLen = $clockR * 0.55
$g.DrawLine($pen, $cx, $cy, $cx + $hourLen * [Math]::Sin($hourAng), $cy - $hourLen * [Math]::Cos($hourAng))

# 分针（指向2点 = 60度）
$minAng = 60.0 * [Math]::PI / 180.0
$minLen = $clockR * 0.75
$g.DrawLine($pen, $cx, $cy, $cx + $minLen * [Math]::Sin($minAng), $cy - $minLen * [Math]::Cos($minAng))

# 中心圆点
$dotR = 9
$dotBrush = New-Object System.Drawing.SolidBrush($darkTeal)
$g.FillEllipse($dotBrush, $cx - $dotR, $cy - $dotR, $dotR * 2, $dotR * 2)

# 保存为 ICO
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Create("d:\ai\Qodercn\app\src\ScreenWatch.App\app.ico")
$icon.Save($fs)
$fs.Close()

Write-Output "Icon saved to app.ico"
