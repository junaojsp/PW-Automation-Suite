using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

namespace RotaAvalon {
    // Original Rody's images matched by the user to the 49 ground material
    // definitions in RotaAvalon.Catalogo. These numbers are Rody icon indexes,
    // NOT the item ID from elements.data.
    public static class OriginalMaterialIcons {
        static readonly Dictionary<int,int> Index=new Dictionary<int,int>{
            {3074,795},
            {3075,796},
            {3076,797},
            {3077,798},
            {3078,799},
            {3079,800},
            {3080,801},
            {3081,802},
            {3082,803},
            {3083,804},
            {3084,805},
            {3085,806},
            {3086,807},
            {3087,808},
            {3088,809},
            {3089,815},
            {3090,816},
            {3091,817},
            {3092,818},
            {3093,819},
            {3094,1848},
            {3095,1830},
            {3096,1831},
            {3097,1832},
            {3098,1844},
            {3099,1845},
            {3100,1846},
            {3101,1857},
            {3102,1820},
            {3103,1823},
            {3535,1834},
            {3536,1837},
            {3537,1850},
            {3538,1854},
            {3539,1821},
            {3540,1824},
            {3541,1835},
            {3542,1838},
            {3543,1851},
            {3544,1855},
            {3545,1825},
            {3546,1839},
            {3547,1852},
            {3548,1826},
            {3549,1840},
            {3550,1827},
            {3551,1841},
            {3552,1828},
            {3553,1842}
        };
        static string Root {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MatFarmIcons");}}
        public static Image Load(int template){
            int icon;if(!Index.TryGetValue(template,out icon))return null;
            string file=Path.Combine(Root,icon.ToString()+".jpg");
            if(!File.Exists(file))return null;
            try{
                // FileStream must not remain locked while the UI shows the icon.
                using(var source=Image.FromFile(file))return new Bitmap(source);
            }catch{return null;}
        }
        public static int PresentCount(){
            int count=0;foreach(var icon in Index.Values)
                if(File.Exists(Path.Combine(Root,icon.ToString()+".jpg")))count++;
            return count;
        }
    }
}
