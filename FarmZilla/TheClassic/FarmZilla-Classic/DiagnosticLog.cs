using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Web.Script.Serialization;
static class DiagnosticLog {
 static readonly object gate=new object();
 // Lines waiting to be sent to the administrator's panel; bounded so an offline PC never grows memory.
 static readonly LinkedList<string> pending=new LinkedList<string>();const int MaxPending=3000,MaxLine=480;
 internal static bool ToFile=true; // tests turn this off so they never touch the user's log file
 public static void Write(string message){try{lock(gate){string line=DateTime.Now.ToString("HH:mm:ss.fff")+" "+message;pending.AddLast(line.Length>MaxLine?line.Substring(0,MaxLine)+"…":line);while(pending.Count>MaxPending)pending.RemoveFirst();if(!ToFile)return;string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZillaClassic","logs");Directory.CreateDirectory(folder);string path=Path.Combine(folder,DateTime.Now.ToString("yyyy-MM-dd")+".txt");if(!File.Exists(path)||new FileInfo(path).Length<2097152)File.AppendAllText(path,line+Environment.NewLine,Encoding.UTF8);}}catch{}}
 // Oldest lines first, limited by count and by their size once serialized (the service accepts 16 KB per request).
 public static List<string> Take(int maxLines,int maxBytes){var batch=new List<string>();var json=new JavaScriptSerializer();int bytes=0;lock(gate){while(pending.Count>0&&batch.Count<maxLines){int cost=Encoding.UTF8.GetByteCount(json.Serialize(pending.First.Value))+1;if(batch.Count>0&&bytes+cost>maxBytes)break;bytes+=cost;batch.Add(pending.First.Value);pending.RemoveFirst();}}return batch;}
 public static void PutBack(List<string> batch){lock(gate){for(int i=batch.Count-1;i>=0;i--)pending.AddFirst(batch[i]);while(pending.Count>MaxPending)pending.RemoveFirst();}}
 public static int Pending {get{lock(gate)return pending.Count;}}
}
// Sends the log to the service with this PC's signed authorization; failures keep the lines for the next attempt.
static class RemoteLog {
 public static void Upload(string service,string token){if(String.IsNullOrEmpty(service)||String.IsNullOrEmpty(token))return;for(int round=0;round<5;round++){var batch=DiagnosticLog.Take(200,12000);if(batch.Count==0)return;try{OnlineApi.Post<Dictionary<string,object>>(service,"/v1/device/log",new{version=ReleaseInfo.Version,lines=batch},token);}catch{DiagnosticLog.PutBack(batch);return;}}}
}
