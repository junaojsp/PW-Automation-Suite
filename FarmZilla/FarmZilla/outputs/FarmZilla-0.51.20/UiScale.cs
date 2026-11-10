using System;using System.Drawing;
// 0.51.20 (dono: no notebook, com zoom do Windows, textos cortados no Mat Farm): o app é DPI-aware e as caixas têm tamanho fixo em
// pixels, mas as fontes em pontos cresciam com o zoom (125% = 25% maiores) e não cabiam. Agora as fontes são reduzidas na mesma
// proporção: o FarmZilla fica igual ao de 100% (mesmos pixels) em qualquer zoom.
static class UiScale {
 public static readonly float F=Compute();
 static float Compute(){try{using(var g=Graphics.FromHwnd(IntPtr.Zero)){float dpi=g.DpiX;return dpi>0?96f/dpi:1f;}}catch{return 1f;}}
}
