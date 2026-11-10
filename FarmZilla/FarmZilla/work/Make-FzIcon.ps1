param([string]$Ico,[string]$Png,[string]$Sheet)
Add-Type -AssemblyName System.Drawing
function Shade($c,$a){[Drawing.Color]::FromArgb($c.A,[Math]::Max(0,[Math]::Min(255,$c.R+$a)),[Math]::Max(0,[Math]::Min(255,$c.G+$a)),[Math]::Max(0,[Math]::Min(255,$c.B+$a)))}
function Round($x,$y,$w,$h,$r){$p=New-Object Drawing.Drawing2D.GraphicsPath;$d=2*$r;$p.AddArc($x,$y,$d,$d,180,90);$p.AddArc($x+$w-$d,$y,$d,$d,270,90);$p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$p.AddArc($x,$y+$h-$d,$d,$d,90,90);$p.CloseFigure();$p}
$accent=[Drawing.Color]::FromArgb(241,100,118);$on=[Drawing.Color]::FromArgb(35,9,15)
function Draw($s){
 $b=New-Object Drawing.Bitmap $s,$s,([Drawing.Imaging.PixelFormat]::Format32bppArgb);$g=[Drawing.Graphics]::FromImage($b);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint=$(if($s -le 24){'SingleBitPerPixelGridFit'}else{'AntiAliasGridFit'});$g.Clear([Drawing.Color]::Transparent)
 $m=if($s -le 24){0}else{[Math]::Max(0.5,$s*0.04)};$sh=if($s -ge 32){[Math]::Max(1,[int]($s*0.035))}else{0};$w=$s-2*$m;$h=$s-2*$m-$sh;$r=[Math]::Max(2,$s*0.2)
 for($i=$sh;$i -ge 1;$i--){$p=Round $m ($m+$i) $w $h $r;$br=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70,0,0,0));$g.FillPath($br,$p)}
 $p=Round $m $m $w $h $r;$lg=New-Object Drawing.Drawing2D.LinearGradientBrush ((New-Object Drawing.RectangleF $m,($m-1),$w,($h+2))),(Shade $accent 30),(Shade $accent -35),([Drawing.Drawing2D.LinearGradientMode]::Vertical);$g.FillPath($lg,$p)
 $pen=New-Object Drawing.Pen (Shade $accent -55),([Math]::Max(1,$s/48));$g.DrawPath($pen,$p)
 if($s -ge 24){$hl=New-Object Drawing.Pen ([Drawing.Color]::FromArgb(80,255,255,255)),([Math]::Max(1,$s/64));$g.DrawLine($hl,$m+$r,$m+[Math]::Max(1,$s/40),$m+$w-$r,$m+[Math]::Max(1,$s/40))}
 $font=New-Object Drawing.Font 'Segoe UI',($s*$(if($s -le 24){0.56}else{0.40})),([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
 $sf=New-Object Drawing.StringFormat;$sf.Alignment='Center';$sf.LineAlignment='Center'
 $rect=New-Object Drawing.RectangleF $m,($m+$s*0.01),$w,$h
 $g.DrawString('FZ',$font,(New-Object Drawing.SolidBrush $on),$rect,$sf);$g.Dispose();$b}
$sizes=16,24,32,48,64,128,256;$entries=@()
foreach($s in $sizes){$bmp=Draw $s
 if($s -eq 256){$ms=New-Object IO.MemoryStream;$bmp.Save($ms,[Drawing.Imaging.ImageFormat]::Png);$data=$ms.ToArray();if($Png){$bmp.Save($Png,[Drawing.Imaging.ImageFormat]::Png)}}
 else{$ms=New-Object IO.MemoryStream;$bw=New-Object IO.BinaryWriter $ms;$bw.Write([int]40);$bw.Write([int]$s);$bw.Write([int]($s*2));$bw.Write([int16]1);$bw.Write([int16]32);$bw.Write([int]0);$bw.Write([int]0);$bw.Write([int]0);$bw.Write([int]0);$bw.Write([int]0);$bw.Write([int]0)
  for($y=$s-1;$y -ge 0;$y--){for($x=0;$x -lt $s;$x++){$c=$bmp.GetPixel($x,$y);$bw.Write([byte]$c.B);$bw.Write([byte]$c.G);$bw.Write([byte]$c.R);$bw.Write([byte]$c.A)}}
  $stride=[int]([Math]::Ceiling($s/32.0)*4);for($y=$s-1;$y -ge 0;$y--){$row=New-Object byte[] $stride;for($x=0;$x -lt $s;$x++){if($bmp.GetPixel($x,$y).A -eq 0){$row[[int][Math]::Floor($x/8)]=$row[[int][Math]::Floor($x/8)] -bor (0x80 -shr ($x%8))}};$bw.Write($row)};$bw.Flush();$data=$ms.ToArray()}
 $entries+=,@($s,$data);$bmp.Dispose()}
$out=New-Object IO.MemoryStream;$w=New-Object IO.BinaryWriter $out;$w.Write([int16]0);$w.Write([int16]1);$w.Write([int16]$entries.Count);$off=6+16*$entries.Count
foreach($e in $entries){$s=$e[0];$d=$e[1];$w.Write([byte]($s%256));$w.Write([byte]($s%256));$w.Write([byte]0);$w.Write([byte]0);$w.Write([int16]1);$w.Write([int16]32);$w.Write([int]$d.Length);$w.Write([int]$off);$off+=$d.Length}
foreach($e in $entries){$w.Write($e[1])};$w.Flush();[IO.File]::WriteAllBytes($Ico,$out.ToArray())
if($Sheet){$canvas=New-Object Drawing.Bitmap 700,330;$g=[Drawing.Graphics]::FromImage($canvas);$g.Clear([Drawing.Color]::FromArgb(32,33,39));$g.FillRectangle([Drawing.Brushes]::WhiteSmoke,420,0,280,330)
 $g.DrawIcon((New-Object Drawing.Icon $Ico,256,256),(New-Object Drawing.Rectangle 10,10,256,256))
 $x=280;foreach($s in 16,24,32,48,64){$g.DrawIconUnstretched((New-Object Drawing.Icon $Ico,$s,$s),(New-Object Drawing.Rectangle $x,10,$s,$s));$g.DrawIconUnstretched((New-Object Drawing.Icon $Ico,$s,$s),(New-Object Drawing.Rectangle ($x+150),10,$s,$s));$x+=$s/2+4}
 $g.DrawIconUnstretched((New-Object Drawing.Icon $Ico,48,48),(New-Object Drawing.Rectangle 280,100,48,48));$g.DrawIconUnstretched((New-Object Drawing.Icon $Ico,48,48),(New-Object Drawing.Rectangle 440,100,48,48))
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.DrawImage((New-Object Drawing.Icon $Ico,16,16).ToBitmap(),(New-Object Drawing.Rectangle 280,170,128,128));$g.DrawImage((New-Object Drawing.Icon $Ico,32,32).ToBitmap(),(New-Object Drawing.Rectangle 440,170,128,128));$canvas.Save($Sheet)}
