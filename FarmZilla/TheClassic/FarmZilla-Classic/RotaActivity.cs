using System;
using System.IO;
using System.Collections.Generic;
namespace RotaAvalon {
    public enum ActivityKind { Idle, Moving, FlightTransition, Approaching, Collecting, Other }
    public sealed class NativeActivity {public int Queue,ResourceId;public string NativeType;public ActivityKind Kind;}
    public sealed class ActivitySnapshot {
        public DateTime ReadAt;public List<NativeActivity> Actions=new List<NativeActivity>();
        public ActivityKind Kind {get{ActivityKind result=ActivityKind.Idle;foreach(var action in Actions){if(action.Kind==ActivityKind.Collecting)return action.Kind;if(action.Kind==ActivityKind.Approaching)result=action.Kind;else if(result!=ActivityKind.Approaching&&action.Kind==ActivityKind.FlightTransition)result=action.Kind;else if(result!=ActivityKind.Approaching&&result!=ActivityKind.FlightTransition&&action.Kind==ActivityKind.Moving)result=action.Kind;else if(result==ActivityKind.Idle&&action.Kind==ActivityKind.Other)result=action.Kind;}return result;}}
    }
    public static class GameActivity {
        public static ActivityKind Classify(string type,int resourceId){
            switch(type){case ".?AVCECHPWorkStand@@":return ActivityKind.Idle;
                case ".?AVCECHPWorkMove@@":case ".?AVCECHPWorkFMove@@":return ActivityKind.Moving;
                case ".?AVCECHPWorkFly@@":case ".?AVCECHPWorkFall@@":return ActivityKind.FlightTransition;
                case ".?AVCECHPWorkTrace@@":return ActivityKind.Approaching;
                case ".?AVCECHPWorkPick@@":return resourceId>0?ActivityKind.Collecting:ActivityKind.Other;
                default:return ActivityKind.Other;}
        }
        public static ActivitySnapshot Read(ProcessMemory memory,uint actor,uint image,uint end){
            uint manager=memory.UInt32(actor+0x1D1C);
            if(NearbyResources.NativeType(memory,manager,image,end)!=".?AVCECHPWorkMan@@")throw new IOException("Gerenciador de ações incompatível.");
            var result=new ActivitySnapshot{ReadAt=DateTime.Now};
            for(int queue=0;queue<4;queue++){uint offset=(uint)(8+queue*16),begin=memory.UInt32(manager+offset),last=memory.UInt32(manager+offset+4),capacity=memory.UInt32(manager+offset+8),count=memory.UInt32(manager+offset+12);
                if(count>64||capacity>128||count>capacity||last<begin||last-begin!=count*4||(count>0&&(begin<0x10000||(begin&3)!=0)))throw new IOException("Fila de ações inconsistente.");
                for(uint at=begin;at<last;at+=4){uint item=memory.UInt32(at);string type=NearbyResources.NativeType(memory,item,image,end);
                    if(type==null||!type.StartsWith(".?AVCECHPWork",StringComparison.Ordinal))throw new IOException("Ação nativa desconhecida.");
                    if(memory.UInt32(item+0x18)!=manager||memory.UInt32(item+0x1C)!=actor)throw new IOException("Ação pertence a outro personagem.");
                    int resourceId=type==".?AVCECHPWorkPick@@"?unchecked((int)memory.UInt32(item+0x2C)):0;
                    if(resourceId<0||resourceId>1000000)throw new IOException("Identificação de coleta inconsistente.");
                    if(memory.UInt32(at)!=item)throw new IOException("Ação mudou durante a leitura.");
                    result.Actions.Add(new NativeActivity{Queue=queue,NativeType=type,ResourceId=resourceId,Kind=Classify(type,resourceId)});
                }
                if(memory.UInt32(manager+offset)!=begin||memory.UInt32(manager+offset+4)!=last||memory.UInt32(manager+offset+12)!=count)throw new IOException("Fila mudou durante a leitura.");
            }
            if(memory.UInt32(actor+0x1D1C)!=manager)throw new IOException("Gerenciador mudou durante a leitura.");return result;
        }
    }
    public sealed class ResourceReward {
        public int ResourceId,TemporaryItemId,FinalItemId;public string Name;
        public ResourceReward(int resource,int temporary,int final,string name){ResourceId=resource;TemporaryItemId=temporary;FinalItemId=final;Name=name;}
    }
    public static class VerifiedRewards {
        // Confirmed by controlled manual collection and inventory changes.
        static readonly ResourceReward[] Known={new ResourceReward(3091,62504,817,"Recompensa do Núcleo de Mina de Carvão"),new ResourceReward(3090,62503,816,"Carvão"),new ResourceReward(3076,62488,797,"Tábua Polida"),new ResourceReward(3089,62502,815,"Pó de Carvão"),new ResourceReward(3539,62509,1821,"Ginseng Azul"),new ResourceReward(3537,62550,1850,"Ameixeira de Cálice Verde")};
        public static ResourceReward Find(int resource){foreach(var reward in Known)if(reward.ResourceId==resource)return reward;return null;}
        public static string ItemName(int id){foreach(var reward in Known){if(reward.FinalItemId==id)return reward.Name+" (definitivo)";if(reward.TemporaryItemId==id)return reward.Name+" (temporário)";}return "";}
    }
}

