using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace RotaAvalon {
    // 0.47.1 (owner): routes recorded by another program, used only to load and run (nothing is copied into FarmZilla):
    //   Mat Name:            one material name per line (names may be cut at 25 letters: "Pedreira de Pedra de Amol")
    //   Coordenadas:         "cordx = 1699,899, cordy = -1643,908" — the game's internal X and Z (comma decimals), no height
    // Checked against the embedded material map (05/11/2026): the points lie 8–78 from spots of the listed materials.
    // Without height the run keeps the height it is flying at (Y = NaN on the point).
    public sealed partial class TrailRoute {
        public bool Imported;public List<string> UnknownNames=new List<string>();
        static readonly Regex OtherPoint=new Regex(@"cordx\s*=\s*(-?\d+(?:[.,]\d+)?)\s*,\s*cordy\s*=\s*(-?\d+(?:[.,]\d+)?)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        public static bool IsOtherFormat(string path){try{foreach(var line in File.ReadLines(path).Take(400)){var t=line.Trim();if(t.StartsWith("Mat Name:",StringComparison.OrdinalIgnoreCase)||OtherPoint.IsMatch(t))return true;}}catch{}return false;}
        static string Fold(string s){var n=(s??"").Trim().Normalize(NormalizationForm.FormD);var b=new StringBuilder();foreach(char c in n)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));return Regex.Replace(b.ToString(),@"\s+"," ");}
        // Material name → catalog ids (ground materials only): same name, or a cut name (≥ 12 letters) that starts a catalog name.
        public static List<int> MaterialIds(string name,ResourceCatalog catalog){var key=Fold(name);if(key.Length==0||catalog==null)return new List<int>();
            var ground=catalog.Definitions.Values.Where(d=>catalog.Ground(d.id)).ToList();
            var exact=ground.Where(d=>Fold(d.nome)==key).Select(d=>d.id).ToList();if(exact.Count>0)return exact;
            if(key.Length<12)return exact;return ground.Where(d=>Fold(d.nome).StartsWith(key)).Select(d=>d.id).ToList();}
        static double Decimal(string s){return Double.Parse(s.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture);}
        public static TrailRoute ReadOther(string path,ResourceCatalog catalog){
            var info=new FileInfo(path);if(info.Length>4*1024*1024)throw new InvalidDataException("Arquivo grande demais para um trajeto.");
            var r=new TrailRoute{Name=System.IO.Path.GetFileNameWithoutExtension(path),Imported=true,Reach=50};bool names=false;
            foreach(var raw in File.ReadAllLines(path,Encoding.UTF8)){var line=raw.Trim();if(line.Length==0)continue;
                if(line.StartsWith("Mat Name",StringComparison.OrdinalIgnoreCase)){names=true;continue;}
                if(line.StartsWith("Coordenadas",StringComparison.OrdinalIgnoreCase)){names=false;continue;}
                var m=OtherPoint.Match(line);
                if(m.Success){names=false;double x=Decimal(m.Groups[1].Value),z=Decimal(m.Groups[2].Value);if(!GameClient.ValidCoordinate(x)||!GameClient.ValidCoordinate(z))throw new InvalidDataException("Coordenada fora do mapa: "+line);
                    var last=r.Path.Count==0?null:r.Path[r.Path.Count-1];if(last!=null&&Math.Abs(last.X-x)<0.01&&Math.Abs(last.Z-z)<0.01)continue;
                    r.Path.Add(new TrailPoint{X=x,Y=Double.NaN,Z=z,Flying=true});if(r.Path.Count>20000)throw new InvalidDataException("Trajeto com mais de 20 000 pontos.");continue;}
                if(names){var ids=MaterialIds(line,catalog);if(ids.Count==0)r.UnknownNames.Add(line);foreach(var id in ids)if(!r.Materials.Contains(id))r.Materials.Add(id);}}
            if(r.Path.Count<2)throw new InvalidDataException("A rota precisa de pelo menos 2 coordenadas (cordx/cordy).");
            return r;}
        // Same format back, for the other program: names from the catalog (one per name), "cordx = x, cordy = z" with comma decimals.
        public void WriteOther(string path,ResourceCatalog catalog){var pt=CultureInfo.GetCultureInfo("pt-BR");
            var lines=new List<string>{"Mat Name:"};ResourceDefinition d;
            foreach(var name in Materials.Select(id=>catalog!=null&&catalog.Definitions.TryGetValue(id,out d)?d.nome:null).Where(n=>!String.IsNullOrWhiteSpace(n)).Distinct())lines.Add(name);
            lines.Add("");lines.Add("Coordenadas:");
            foreach(var p in Path)lines.Add("cordx = "+Math.Round(p.X,3).ToString("0.###",pt)+", cordy = "+Math.Round(p.Z,3).ToString("0.###",pt));
            File.WriteAllLines(path,lines,new UTF8Encoding(true));}
        public static TrailRoute Read(string path,ResourceCatalog catalog){return IsOtherFormat(path)?ReadOther(path,catalog):Read(path);}
    }
}
