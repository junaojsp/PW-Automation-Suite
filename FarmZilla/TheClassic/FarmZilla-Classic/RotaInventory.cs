using System;
using System.IO;
using System.Collections.Generic;
namespace RotaAvalon {
    public sealed class InventoryItem {public int Slot,Id,Quantity;public string NativeType;}
    public sealed class InventorySnapshot {
        public DateTime ReadAt;public int Slots;public List<InventoryItem> Items=new List<InventoryItem>();
        public int QuantityOf(int id){int total=0;foreach(var item in Items)if(item.Id==id)total=checked(total+item.Quantity);return total;}
    }
    public static class GameInventory {
        public static InventorySnapshot Read(ProcessMemory memory,uint player,uint imageBase,uint imageEnd){
            uint bag=memory.UInt32(checked(player+ClassicLayout.HostPack));
            if(NearbyResources.NativeType(memory,bag,imageBase,imageEnd)!=".?AVCECInventory@@")throw new IOException("Inventário incompatível com o perfil.");
            uint vector=memory.UInt32(bag+0xC),capacity=memory.UInt32(bag+0x10),count=memory.UInt32(bag+0x14);
            if(count<1||count>256||capacity<count||capacity>256||vector<0x10000||(vector&3)!=0)throw new IOException("Cabeçalho do inventário inconsistente.");
            byte[] pointers=memory.Read(vector,checked((int)count*4));if(pointers.Length!=count*4)throw new IOException("Inventário incompleto.");
            var result=new InventorySnapshot{Slots=(int)count,ReadAt=DateTime.Now};
            for(int slot=0;slot<count;slot++){uint item=BitConverter.ToUInt32(pointers,slot*4);if(item==0)continue;
                string type=NearbyResources.NativeType(memory,item,imageBase,imageEnd);if(type==null||!type.StartsWith(".?AVCECIvtr",StringComparison.Ordinal))throw new IOException("Tipo de item não reconhecido.");
                byte[] data=memory.Read(item+0xC,16);if(data.Length!=16)throw new IOException("Item incompleto.");
                int id=BitConverter.ToInt32(data,0),quantity=BitConverter.ToInt32(data,8),maximum=BitConverter.ToInt32(data,12);
                if(id<1||id>1000000||quantity<1||quantity>100000000||maximum<quantity||maximum>100000000)throw new IOException("Quantidade de item inconsistente.");
                if(memory.UInt32(vector+(uint)slot*4)!=item||memory.UInt32(item+0xC)!=(uint)id||memory.UInt32(item+0x14)!=(uint)quantity)throw new IOException("Inventário mudou durante a leitura; tente novamente.");
                result.Items.Add(new InventoryItem{Slot=slot,Id=id,Quantity=quantity,NativeType=type});
            }
            if(memory.UInt32(player+ClassicLayout.HostPack)!=bag||memory.UInt32(bag+0xC)!=vector||memory.UInt32(bag+0x14)!=count)throw new IOException("Inventário mudou durante a leitura.");
            return result;
        }
    }
}
