using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;

namespace RotaAvalon {
    public sealed class PckEntry {public string Name;public long Offset;public int Size,Compressed;}
    // Read-only reader for the client's old/new single-file Angelica packages.
    public sealed class PckArchive : IDisposable {
        readonly FileStream stream;public readonly List<PckEntry> Entries=new List<PckEntry>();
        static byte[] Inflate(byte[] compressed,int expected){using(var input=new MemoryStream(compressed,2,compressed.Length-6))using(var z=new DeflateStream(input,CompressionMode.Decompress))using(var output=new MemoryStream()){byte[] buffer=new byte[16384];int n;while((n=z.Read(buffer,0,buffer.Length))>0){if(output.Length+n>expected)throw new InvalidDataException("PCK: conteúdo excedeu tamanho declarado.");output.Write(buffer,0,n);}if(output.Length!=expected)throw new InvalidDataException("PCK: tamanho descomprimido inconsistente.");return output.ToArray();}}
        byte[] Bytes(int count){byte[] result=new byte[count];int at=0;while(at<count){int n=stream.Read(result,at,count-at);if(n==0)throw new EndOfStreamException();at+=n;}return result;}
        public PckArchive(string path){stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);try{
            stream.Position=stream.Length-8;byte[] tail=Bytes(8);uint count=BitConverter.ToUInt32(tail,0),version=BitConverter.ToUInt32(tail,4);bool newer=version==0x20003;if(!newer&&version!=0x20002&&version!=0x20001)throw new InvalidDataException("PCK: versão desconhecida.");if(count>500000)throw new InvalidDataException("PCK: índice excessivo.");
            stream.Position=stream.Length-(newer?280:272);byte[] offset=Bytes(newer?8:4);long table=newer?unchecked((long)(BitConverter.ToUInt64(offset,0)^0xffffffffa8937462UL)):(long)(BitConverter.ToUInt32(offset,0)^0xa8937462U);if(table<0||table>=stream.Length)throw new InvalidDataException("PCK: índice inválido.");stream.Position=table;
            for(uint i=0;i<count;i++){byte[] header=Bytes(8);uint size=BitConverter.ToUInt32(header,0)^0xa8937462U,check=BitConverter.ToUInt32(header,4)^0xa8937462U^0x59374231U;if(size!=check||size>4096||size<6)throw new InvalidDataException("PCK: entrada inválida.");byte[] entry=Bytes((int)size);int expected=newer?288:276;if(entry.Length!=expected)entry=Inflate(entry,expected);string name=Encoding.GetEncoding(936).GetString(entry,0,260).Split('\0')[0].Replace('\\','/');int o=newer?264:260;long position=newer?(long)BitConverter.ToUInt64(entry,o):BitConverter.ToUInt32(entry,o);int length=BitConverter.ToInt32(entry,o+(newer?8:4)),packed=BitConverter.ToInt32(entry,o+(newer?12:8));if(position<0||length<0||packed<0||position+packed>stream.Length)throw new InvalidDataException("PCK: limites inválidos.");Entries.Add(new PckEntry{Name=name,Offset=position,Size=length,Compressed=packed});}
        }catch{stream.Dispose();throw;}}
        public byte[] Read(PckEntry e){if(e.Size>128*1024*1024)throw new InvalidDataException("PCK: arquivo muito grande.");stream.Position=e.Offset;byte[] data=Bytes(e.Compressed);return e.Compressed==e.Size?data:Inflate(data,e.Size);}
        public void Dispose(){stream.Dispose();}
    }
}
