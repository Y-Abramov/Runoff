# Генератор иконок модуля «Схема стока».
# Рисует каждую иконку из одного векторного описания во все требуемые размеры:
#   {name}_{16|32}dp_{1x|1.5x|2x|2.5x|3x}.png
# Палитра — акцентные цвета ABR, заданные пользователем.

Add-Type -AssemblyName System.Drawing

$Out = Join-Path $PSScriptRoot '..\icons'   # относительно скрипта - не зависит от пути чекаута

# ── палитра ───────────────────────────────────────────────────────────────
$BLUE   = '#1064AF'   # основной синий
$BLUE_L = '#3089D9'   # светлый синий
$GREEN  = '#10AF6A'   # выпуск, норма
$AMBER  = '#AF8F10'   # правка/параметры
$RED    = '#AF1010'   # ошибка
$GRAY   = '#939393'   # вспомогательное
$DARK   = '#373636'   # контур
$WHITE  = '#FFFFFF'

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# ── примитивы (координаты нормированы 0..1, Y вниз) ───────────────────────
function Line($g, $S, $x1, $y1, $x2, $y2, $hex, $w) {
  $pen = New-Object System.Drawing.Pen((C $hex), [float]($w * $S))
  $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
  $g.DrawLine($pen, [float]($x1*$S), [float]($y1*$S), [float]($x2*$S), [float]($y2*$S))
  $pen.Dispose()
}

function Poly($g, $S, $pts, $hex, $w, [bool]$fill) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $arr = @()
  for ($i = 0; $i -lt $pts.Count; $i += 2) {
    $arr += New-Object System.Drawing.PointF([float]($pts[$i]*$S), [float]($pts[$i+1]*$S))
  }
  if ($fill) {
    $p.AddPolygon($arr)
    $b = New-Object System.Drawing.SolidBrush((C $hex)); $g.FillPath($b, $p); $b.Dispose()
  } else {
    $p.AddLines($arr)
    $pen = New-Object System.Drawing.Pen((C $hex), [float]($w*$S))
    $pen.StartCap='Round'; $pen.EndCap='Round'; $pen.LineJoin='Round'
    $g.DrawPath($pen, $p); $pen.Dispose()
  }
  $p.Dispose()
}

function Disc($g, $S, $cx, $cy, $r, $hex) {
  $b = New-Object System.Drawing.SolidBrush((C $hex))
  $g.FillEllipse($b, [float](($cx-$r)*$S), [float](($cy-$r)*$S), [float](2*$r*$S), [float](2*$r*$S))
  $b.Dispose()
}

function Ring($g, $S, $cx, $cy, $r, $hex, $w) {
  $pen = New-Object System.Drawing.Pen((C $hex), [float]($w*$S))
  $g.DrawEllipse($pen, [float](($cx-$r)*$S), [float](($cy-$r)*$S), [float](2*$r*$S), [float](2*$r*$S))
  $pen.Dispose()
}

function Box($g, $S, $x, $y, $w, $h, $hex) {
  $b = New-Object System.Drawing.SolidBrush((C $hex))
  $g.FillRectangle($b, [float]($x*$S), [float]($y*$S), [float]($w*$S), [float]($h*$S))
  $b.Dispose()
}

function BoxOutline($g, $S, $x, $y, $w, $h, $hex, $t) {
  $pen = New-Object System.Drawing.Pen((C $hex), [float]($t*$S))
  $g.DrawRectangle($pen, [float]($x*$S), [float]($y*$S), [float]($w*$S), [float]($h*$S))
  $pen.Dispose()
}

# Стрелка потока: линия + залитый наконечник в конце.
function Arrow($g, $S, $x1, $y1, $x2, $y2, $hex, $w) {
  $dx = $x2-$x1; $dy = $y2-$y1
  $len = [Math]::Sqrt($dx*$dx + $dy*$dy)
  if ($len -lt 1e-6) { return }
  $ux = $dx/$len; $uy = $dy/$len
  $head = $w * 2.6
  $bx = $x2 - $ux*$head; $by = $y2 - $uy*$head
  Line $g $S $x1 $y1 $bx $by $hex $w
  $nx = -$uy; $ny = $ux
  Poly $g $S @($x2,$y2, ($bx+$nx*$head*0.45),($by+$ny*$head*0.45), ($bx-$nx*$head*0.45),($by-$ny*$head*0.45)) $hex 0 $true
}

# Ползунок «параметры»: две дорожки с ручками.
# Расстояние между дорожками задаётся явно (gap), а не выводится из ширины -
# иначе широкий ползунок уезжает за нижний край иконки.
function Sliders($g, $S, $x, $y, $wd, $gap, $hex, $t) {
  Line $g $S $x  $y        ($x+$wd)  $y        $hex $t
  Line $g $S $x ($y+$gap)  ($x+$wd) ($y+$gap)  $hex $t
  Disc $g $S ($x+$wd*0.30)  $y        ($t*1.6) $hex
  Disc $g $S ($x+$wd*0.70) ($y+$gap)  ($t*1.6) $hex
}

# Капля воды: круг снизу + треугольная вершина сверху, одним цветом.
# Многоугольник из четырёх точек давал ромб, а не каплю.
function Drop($g, $S, $cx, $cy, $r, $apexY, $hex) {
  Disc $g $S $cx $cy $r $hex
  $dy = $cy - $apexY
  $k  = $r * $r / $dy                      # точка касания образующих к окружности
  $hx = $r * [Math]::Sqrt([Math]::Max(0.0, 1.0 - ($r*$r)/($dy*$dy)))
  Poly $g $S @($cx,$apexY, ($cx+$hx),($cy-$k), ($cx-$hx),($cy-$k)) $hex 0 $true
}

# ── описания иконок ───────────────────────────────────────────────────────
# Каждая — скриптблок, рисующий в квадрате 0..1. Толщины заданы долями,
# поэтому 16px и 96px получаются из одного кода без ручной подгонки.
$Icons = @{

  # Модуль: капля + волна под ней.
  'rf_module' = {
    param($g,$S)
    Drop $g $S 0.50 0.46 0.22 0.10 $BLUE_L
    Line $g $S 0.18 0.82 0.38 0.82 $BLUE 0.080
    Line $g $S 0.44 0.82 0.60 0.82 $BLUE 0.080
    Line $g $S 0.66 0.82 0.84 0.82 $BLUE 0.080
  }

  # Схема стока: дно кювета + стрелка течения + плюс (создать).
  'rf_add' = {
    param($g,$S)
    Poly $g $S @(0.10,0.30, 0.32,0.62, 0.66,0.62, 0.88,0.30) $GRAY 0.075 $false
    Arrow $g $S 0.24 0.50 0.74 0.50 $BLUE 0.085
    Disc $g $S 0.79 0.79 0.19 $WHITE
    Line $g $S 0.79 0.70 0.79 0.88 $GREEN 0.085
    Line $g $S 0.70 0.79 0.88 0.79 $GREEN 0.085
  }

  # Параметры схемы: дно кювета + ползунки.
  'rf_edit' = {
    param($g,$S)
    Poly $g $S @(0.10,0.24, 0.32,0.52, 0.66,0.52, 0.88,0.24) $GRAY 0.075 $false
    Sliders $g $S 0.18 0.72 0.64 0.17 $AMBER 0.070
  }

  # Взорвать: целая линия схемы разорвана на две половины, осколки в стороны.
  # Прежний вариант (четыре разлетающихся штриха) на 16px читался как шум.
  'rf_explode' = {
    param($g,$S)
    Line $g $S 0.10 0.50 0.38 0.50 $BLUE 0.130
    Line $g $S 0.62 0.50 0.90 0.50 $BLUE 0.130
    Line $g $S 0.46 0.24 0.54 0.14 $GRAY 0.095
    Line $g $S 0.46 0.76 0.54 0.86 $GRAY 0.095
  }

  # Сеть водоотвода: граф из узлов, нижний - выпуск (зелёный).
  'rf_network' = {
    param($g,$S)
    Line $g $S 0.22 0.24 0.52 0.50 $BLUE 0.080
    Line $g $S 0.82 0.24 0.52 0.50 $BLUE 0.080
    Line $g $S 0.52 0.50 0.52 0.80 $BLUE 0.080
    Disc $g $S 0.22 0.24 0.115 $BLUE_L
    Disc $g $S 0.82 0.24 0.115 $BLUE_L
    Disc $g $S 0.52 0.50 0.105 $BLUE
    Disc $g $S 0.52 0.80 0.135 $GREEN
  }

  # Параметры сети: тот же граф приглушённо + ползунки.
  'rf_network_edit' = {
    param($g,$S)
    Line $g $S 0.20 0.20 0.46 0.44 $GRAY 0.070
    Line $g $S 0.72 0.20 0.46 0.44 $GRAY 0.070
    Disc $g $S 0.20 0.20 0.095 $GRAY
    Disc $g $S 0.72 0.20 0.095 $GRAY
    Disc $g $S 0.46 0.44 0.095 $GRAY
    Sliders $g $S 0.18 0.72 0.64 0.17 $AMBER 0.070
  }

  # Проследить сток: подсвеченный путь по сети со стрелкой.
  'rf_trace' = {
    param($g,$S)
    Line $g $S 0.18 0.22 0.44 0.46 $GRAY 0.060
    Disc $g $S 0.18 0.22 0.085 $GRAY
    Poly $g $S @(0.44,0.46, 0.44,0.66, 0.72,0.66) $GREEN 0.105 $false
    Arrow $g $S 0.72 0.66 0.90 0.66 $GREEN 0.095
    Disc $g $S 0.44 0.46 0.105 $GREEN
  }

  # Ведомость участков: таблица с синей шапкой.
  'rf_report' = {
    param($g,$S)
    Box $g $S 0.16 0.14 0.68 0.16 $BLUE
    BoxOutline $g $S 0.16 0.14 0.68 0.72 $DARK 0.060
    Line $g $S 0.24 0.46 0.76 0.46 $GRAY 0.060
    Line $g $S 0.24 0.62 0.76 0.62 $GRAY 0.060
    Line $g $S 0.24 0.78 0.60 0.78 $GRAY 0.060
  }

  # Ведомость бассейнов: таблица + цветные маркеры бассейнов слева.
  'rf_network_report' = {
    param($g,$S)
    BoxOutline $g $S 0.16 0.14 0.68 0.72 $DARK 0.060
    Box $g $S 0.16 0.14 0.68 0.14 $BLUE
    Box $g $S 0.23 0.42 0.11 0.11 $BLUE_L
    Box $g $S 0.23 0.60 0.11 0.11 $GREEN
    Box $g $S 0.23 0.78 0.11 0.11 $AMBER
    Line $g $S 0.42 0.475 0.76 0.475 $GRAY 0.055
    Line $g $S 0.42 0.655 0.76 0.655 $GRAY 0.055
    Line $g $S 0.42 0.835 0.66 0.835 $GRAY 0.055
  }

  # Условные обозначения: образец знака + расшифровка.
  'rf_legend' = {
    param($g,$S)
    Disc $g $S 0.26 0.28 0.12 $BLUE
    Line $g $S 0.46 0.28 0.86 0.28 $GRAY 0.070
    Poly $g $S @(0.26,0.44, 0.38,0.62, 0.14,0.62) $GREEN 0 $true
    Line $g $S 0.46 0.55 0.86 0.55 $GRAY 0.070
    Ring $g $S 0.26 0.80 0.11 $AMBER 0.070
    Line $g $S 0.46 0.80 0.72 0.80 $GRAY 0.070
  }

  # Водосбор: контур водораздела + главный лог + выпуск.
  'ws_build' = {
    param($g,$S)
    Poly $g $S @(0.18,0.32, 0.34,0.14, 0.62,0.12, 0.84,0.30, 0.82,0.60, 0.60,0.84, 0.32,0.82, 0.14,0.56, 0.18,0.32) $BLUE_L 0.055 $false
    Line $g $S 0.50 0.24 0.50 0.68 $BLUE 0.095
    Disc $g $S 0.50 0.78 0.14 $GREEN
  }

  # Пересчитать водосборы: тот же контур, приглушённый (устарел) + две встречные стрелки.
  'ws_update' = {
    param($g,$S)
    Poly $g $S @(0.18,0.32, 0.34,0.14, 0.62,0.12, 0.84,0.30, 0.82,0.60, 0.60,0.84, 0.32,0.82, 0.14,0.56, 0.18,0.32) $GRAY 0.055 $false
    Arrow $g $S 0.28 0.50 0.50 0.30 $AMBER 0.085
    Arrow $g $S 0.50 0.70 0.72 0.50 $AMBER 0.085
  }

  # Ведомость водосборов: та же таблица, что rf_report, + маркер выпуска -
  # чтобы две ведомости не путались в списке команд.
  'ws_report' = {
    param($g,$S)
    BoxOutline $g $S 0.16 0.14 0.68 0.72 $DARK 0.060
    Box $g $S 0.16 0.14 0.68 0.16 $BLUE
    Line $g $S 0.24 0.46 0.76 0.46 $GRAY 0.060
    Line $g $S 0.24 0.62 0.76 0.62 $GRAY 0.060
    Line $g $S 0.24 0.78 0.60 0.78 $GRAY 0.060
    Disc $g $S 0.78 0.78 0.12 $GREEN
  }
}

# ── отрисовка во все размеры ──────────────────────────────────────────────
$Variants = @(
  @{ dp = 16; scale = '1x';   px = 16 }, @{ dp = 16; scale = '1.5x'; px = 24 }
  @{ dp = 16; scale = '2x';   px = 32 }, @{ dp = 16; scale = '2.5x'; px = 40 }
  @{ dp = 16; scale = '3x';   px = 48 }
  @{ dp = 32; scale = '1x';   px = 32 }, @{ dp = 32; scale = '1.5x'; px = 48 }
  @{ dp = 32; scale = '2x';   px = 64 }, @{ dp = 32; scale = '2.5x'; px = 80 }
  @{ dp = 32; scale = '3x';   px = 96 }
)

$made = 0
foreach ($name in $Icons.Keys) {
  foreach ($v in $Variants) {
    $S  = $v.px
    $bm = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g  = [System.Drawing.Graphics]::FromImage($bm)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    & $Icons[$name] $g $S

    $g.Dispose()
    $file = Join-Path $Out ("{0}_{1}dp_{2}.png" -f $name, $v.dp, $v.scale)
    $bm.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bm.Dispose()
    $made++
  }
}
Write-Output ("создано файлов: {0} ({1} иконок)" -f $made, $Icons.Count)
