using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace RotaAvalon {
    public static class DdsImage {
        public static Bitmap Region(byte[] bytes,Rectangle region){
            if(bytes.Length<128||BitConverter.ToUInt32(bytes,0)!=0x20534444)throw new InvalidDataException("Atlas DDS inválido.");
            int height=BitConverter.ToInt32(bytes,12),width=BitConverter.ToInt32(bytes,16);uint format=BitConverter.ToUInt32(bytes,84);int blockSize=format==0x31545844?8:format==0x33545844||format==0x35545844?16:0;
            if(blockSize==0||region.X<0||region.Y<0||region.Width<1||region.Height<1||region.Right>width||region.Bottom>height||((region.X|region.Y|region.Width|region.Height)&3)!=0)throw new InvalidDataException("Região DDS inválida.");
            int rows=region.Height/4,columns=region.Width/4,sourceColumns=(width+3)/4;byte[] small=new byte[128+rows*columns*blockSize];Array.Copy(bytes,small,128);Array.Copy(BitConverter.GetBytes(region.Height),0,small,12,4);Array.Copy(BitConverter.GetBytes(region.Width),0,small,16,4);
            for(int row=0;row<rows;row++){int source=checked(128+((region.Y/4+row)*sourceColumns+region.X/4)*blockSize);if(source+columns*blockSize>bytes.Length)throw new InvalidDataException("Atlas DDS truncado.");Array.Copy(bytes,source,small,128+row*columns*blockSize,columns*blockSize);}return Decode(small);
        }
        static int[] Color(ushort c){return new[]{((c>>11)&31)*255/31,((c>>5)&63)*255/63,(c&31)*255/31,255};}
        public static Bitmap Decode(byte[] b){
            if(b.Length<128||BitConverter.ToUInt32(b,0)!=0x20534444)throw new InvalidDataException("DDS inválido.");
            int h=BitConverter.ToInt32(b,12),w=BitConverter.ToInt32(b,16);uint format=BitConverter.ToUInt32(b,84);int blockSize=format==0x31545844?8:format==0x33545844||format==0x35545844?16:0;
            if(w<1||h<1||w>8192||h>8192||blockSize==0)throw new InvalidDataException("Formato DDS não suportado.");
            int bw=(w+3)/4,bh=(h+3)/4;if(128L+(long)bw*bh*blockSize>b.Length)throw new InvalidDataException("DDS truncado.");byte[] pixels=new byte[checked(w*h*4)];int at=128;
            for(int by=0;by<bh;by++)for(int bx=0;bx<bw;bx++,at+=blockSize){
                int co=at+(blockSize==8?0:8);ushort c0=BitConverter.ToUInt16(b,co),c1=BitConverter.ToUInt16(b,co+2);int[][] colors={Color(c0),Color(c1),new int[4],new int[4]};
                for(int k=0;k<3;k++){if(c0>c1||blockSize!=8){colors[2][k]=(2*colors[0][k]+colors[1][k])/3;colors[3][k]=(colors[0][k]+2*colors[1][k])/3;}else colors[2][k]=(colors[0][k]+colors[1][k])/2;}colors[2][3]=255;colors[3][3]=c0>c1||blockSize!=8?255:0;
                uint indices=BitConverter.ToUInt32(b,co+4);int[] alphas=new int[8];ulong alphaIndices=0;
                if(format==0x35545844){alphas[0]=b[at];alphas[1]=b[at+1];if(alphas[0]>alphas[1])for(int i=2;i<8;i++)alphas[i]=((8-i)*alphas[0]+(i-1)*alphas[1])/7;else{for(int i=2;i<6;i++)alphas[i]=((6-i)*alphas[0]+(i-1)*alphas[1])/5;alphas[6]=0;alphas[7]=255;}for(int i=0;i<6;i++)alphaIndices|=(ulong)b[at+2+i]<<(8*i);}
                for(int i=0;i<16;i++){int x=bx*4+i%4,y=by*4+i/4;if(x>=w||y>=h)continue;int[] c=colors[(indices>>(2*i))&3];int alpha=c[3];if(format==0x33545844)alpha=((b[at+i/2]>>((i%2)*4))&15)*17;else if(format==0x35545844)alpha=alphas[(alphaIndices>>(3*i))&7];int p=(y*w+x)*4;pixels[p]=(byte)c[2];pixels[p+1]=(byte)c[1];pixels[p+2]=(byte)c[0];pixels[p+3]=(byte)alpha;}
            }
            var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);var locked=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*4,IntPtr.Add(locked.Scan0,y*locked.Stride),w*4);}finally{bitmap.UnlockBits(locked);}return bitmap;
        }
    }
}
