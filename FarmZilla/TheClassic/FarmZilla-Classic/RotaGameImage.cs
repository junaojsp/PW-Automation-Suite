using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace RotaAvalon {
    public static class GameImage {
        public static Bitmap Decode(byte[] bytes){if(bytes.Length<18)throw new InvalidDataException("Imagem truncada.");if(BitConverter.ToUInt32(bytes,0)==0x20534444)return DdsImage.Decode(bytes);
            if(bytes[0]==0x89&&bytes[1]==0x50||bytes[0]==0xff&&bytes[1]==0xd8||bytes[0]==0x42&&bytes[1]==0x4d){using(var input=new MemoryStream(bytes))using(var image=System.Drawing.Image.FromStream(input))return new Bitmap(image);}
            int type=bytes[2],w=BitConverter.ToUInt16(bytes,12),h=BitConverter.ToUInt16(bytes,14),bpp=bytes[16]/8;if(bytes[1]!=0||w<1||h<1||w>4096||h>4096||(type!=2&&type!=10&&type!=3&&type!=11)||(bpp!=1&&bpp!=3&&bpp!=4))throw new InvalidDataException("Imagem não suportada.");
            byte[] pixels=new byte[checked(w*h*4)];int offset=18+bytes[0],position=0;bool rle=type==10||type==11;
            while(position<w*h){int count=1;bool repeat=false;if(rle){if(offset>=bytes.Length)throw new InvalidDataException("TGA truncado.");int header=bytes[offset++];count=(header&127)+1;repeat=(header&128)!=0;}if(position+count>w*h)throw new InvalidDataException("TGA inválido.");int saved=offset;
                for(int i=0;i<count;i++){if(repeat)offset=saved;if(offset+bpp>bytes.Length)throw new InvalidDataException("TGA truncado.");int x=position%w,y=position/w;if((bytes[17]&32)==0)y=h-1-y;if((bytes[17]&16)!=0)x=w-1-x;int dest=(y*w+x)*4;pixels[dest]=bytes[offset];pixels[dest+1]=bpp==1?bytes[offset]:bytes[offset+1];pixels[dest+2]=bpp==1?bytes[offset]:bytes[offset+2];pixels[dest+3]=bpp==4?bytes[offset+3]:(byte)255;offset+=bpp;position++;}
            }
            var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);var locked=result.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*4,IntPtr.Add(locked.Scan0,y*locked.Stride),w*4);}finally{result.UnlockBits(locked);}return result;
        }
    }
}
