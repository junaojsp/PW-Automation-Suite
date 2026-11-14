using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace RotaAvalon {
    // Mat Farm "Manual" (0.47.0): recording the path while the player flies it, the .txt file and the run.
    public partial class RotaForm {
        TrailRoute trail;TrailRecorder recorder;bool trailRecording;
        string TrailFile{get{return Path.Combine(RotaPaths.Data,SlotFile("trajeto.txt"));}}
        string OtherPointer{get{return Path.Combine(RotaPaths.Data,SlotFile("trajeto-externo.txt"));}}
        void RememberOther(string path){try{if(path==null){if(File.Exists(OtherPointer))File.Delete(OtherPointer);}else File.WriteAllText(OtherPointer,path);}catch{}}
        string TrailHeightFile{get{return Path.Combine(RotaPaths.Data,SlotFile("altura_trajeto_v2.txt"));}}
        string ModeFile{get{return Path.Combine(RotaPaths.Data,SlotFile("modo_farm.txt"));}}
        void WireTrail(){
            // A route from another program is not copied: only its path is remembered and it is read again from there.
            try{string other=File.Exists(OtherPointer)?File.ReadAllText(OtherPointer).Trim():"";if(other.Length>0&&File.Exists(other))trail=TrailRoute.Read(other,catalog);else if(File.Exists(TrailFile))trail=TrailRoute.Read(TrailFile);if(trail!=null)farm.Reach=trail.Reach;}catch(Exception e){trail=null;Log("Trajeto salvo: "+e.Message,false);}
            try{int h;if(File.Exists(TrailHeightFile)&&Int32.TryParse(File.ReadAllText(TrailHeightFile).Trim(),out h))farm.TrailHeight=h;}catch{}
            farm.TrailHeightChanged+=()=>{try{File.WriteAllText(TrailHeightFile,((int)farm.TrailHeight).ToString());}catch{}};
            ShowTrailInfo();
            farm.RecordRequested+=ToggleTrailRecording;farm.TrailStartRequested+=StartTrail;farm.TrailSaveRequested+=SaveTrail;farm.TrailLoadRequested+=LoadTrail;
            farm.ReachChanged+=()=>{if(trail!=null){trail.Reach=farm.Reach;AutoSaveTrail();}};
            farm.ModeChanged+=()=>{try{File.WriteAllText(ModeFile,farm.Manual?"manual":"automatico");}catch{}if(farm.Manual)ShowTrailMap();else Replan();};
            bool manual=false;try{manual=File.Exists(ModeFile)&&File.ReadAllText(ModeFile).Trim()=="manual";}catch{}farm.SetMode(manual);}
        void ShowTrailInfo(){if(trail==null){farm.SetTrail(null);return;}
            farm.SetTrail((trailRecording?"Gravando: ":"Trajeto: ")+trail.Name+" · "+trail.Path.Count+" pontos · "+trail.Length().ToString("F0")+" de caminho · "+trail.Seen.Count+" materiais vistos · "+(trail.Closed?"circular":"ida e volta"));}
        void ShowTrailMap(){if(!farm.Manual)return;farm.ShowTrail(trail==null?null:trail.ToMap());SyncOverlay();}
        void AutoSaveTrail(){if(trail==null||trail.Path.Count<2||trail.Imported)return;try{Directory.CreateDirectory(RotaPaths.Data);trail.Write(TrailFile);}catch(Exception e){Log("Trajeto: "+e.Message,false);}}
        void ToggleTrailRecording(){
            if(trailRecording){StopTrailRecording("Gravação do trajeto finalizada.");return;}
            if(execution.Running){Status("Pare o farm antes de gravar.");return;}if(!HaveSnapshot())return;
            trail=new TrailRoute{Name=snapshot.Name+"-trajeto",Character=snapshot.Name,Reach=farm.Reach,Materials=route.ResourceIds.ToList()};recorder=new TrailRecorder(trail);trailRecording=true;RememberOther(null);
            recorder.Offer(snapshot.X,snapshot.Y,snapshot.Z,snapshot.FlightStateKnown&&snapshot.IsFlying);farm.SetRecording(true);ShowTrailInfo();ShowTrailMap();
            Status("Gravando o trajeto: voe o caminho no jogo e clique Parar gravação no fim.");Log("Gravação do trajeto iniciada com "+snapshot.Name+".");}
        void StopTrailRecording(string text){if(!trailRecording)return;trailRecording=false;farm.SetRecording(false);
            if(trail!=null&&trail.Path.Count<2){trail=null;ShowTrailInfo();ShowTrailMap();Status("Trajeto curto demais (menos de 2 pontos); nada foi guardado.");return;}if(trail!=null)trail.Imported=false;
            if(trail!=null){if(route.ResourceIds.Count>0)trail.Materials=route.ResourceIds.ToList();else trail.Materials=trail.Seen.Select(w=>w.ResourceId).Distinct().ToList();AutoSaveTrail();}
            ShowTrailInfo();ShowTrailMap();Status(text+" Clique Salvar trajeto para guardar em .txt ou Rodar trajeto.");Log(text+(trail==null?"":" "+trail.Path.Count+" pontos, "+trail.Seen.Count+" materiais vistos."));}
        // Called by Poll (every 350 ms) while recording.
        void TrailSample(){if(!trailRecording||snapshot==null)return;if(trail.Character!=snapshot.Name){StopTrailRecording("O personagem mudou; gravação do trajeto interrompida.");return;}
            if(recorder.Offer(snapshot.X,snapshot.Y,snapshot.Z,snapshot.FlightStateKnown&&snapshot.IsFlying)){if(trail.Path.Count%3==0){ShowTrailInfo();ShowTrailMap();}}
            if(trail.Path.Count>=20000)StopTrailRecording("Limite de 20 mil pontos atingido.");}
        // Called by ReadResources while recording: ground materials within reach of the path are written as "coleta=".
        void TrailSeen(ResourceFrame frame){if(!trailRecording||frame==null)return;double reach=farm.Reach;foreach(var r in frame.Resources)if(catalog.Ground(r.TemplateId)&&r.HorizontalDistance<=reach)trail.AddSeen(r);}
        void StartTrail(){if(!HaveSnapshot()||execution.Running)return;
            if(trailRecording)StopTrailRecording("Gravação do trajeto finalizada.");
            if(trail==null||trail.Path.Count<2){Status("Grave ou carregue um trajeto primeiro.");return;}
            if(route.ResourceIds.Count==0){Status("Marque na lista os materiais que ele deve coletar no caminho.");return;}
            try{if(!RotaAllowed())return;trail.Reach=farm.Reach;execution.StartAttack=StartAttack;execution.AttackRunning=AttackRunning;execution.ReviveInTown=ReviveInTown;
                execution.StartTrail(client.Pid,trail,route.ResourceIds.ToList(),catalog,RotaPaths.Bridge(),farm.TrailHeight);LockCharacter();farm.SetRunning(true);
                Log("Trajeto iniciado: "+trail.Name+" ("+trail.Path.Count+" pontos"+(trail.Character.Length>0&&trail.Character!=snapshot.Name?", gravado por "+trail.Character:"")+"). Log: "+execution.LogPath);}
            catch(Exception e){Error(e);}}
        void SaveTrail(){if(trailRecording)StopTrailRecording("Gravação do trajeto finalizada.");if(trail==null||trail.Path.Count<2){Status("Nada para salvar: grave um trajeto primeiro.");return;}
            using(var dialog=new SaveFileDialog{Filter="Trajeto do FarmZilla (*.txt)|*.txt|Formato Mat Name / cordx-cordy (*.txt)|*.txt",DefaultExt="txt",AddExtension=true,FileName=trail.Name+".txt",Title="Salvar trajeto manual",InitialDirectory=RotaPaths.Routes}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            try{if(route.ResourceIds.Count>0)trail.Materials=route.ResourceIds.ToList();if(dialog.FilterIndex==2){trail.WriteOther(dialog.FileName,catalog);Status("Trajeto salvo no formato Mat Name/cordx: "+Path.GetFileName(dialog.FileName)+" ("+trail.Path.Count+" pontos, sem altura).");Log("Trajeto salvo (formato cordx) em "+dialog.FileName);return;}
                trail.Name=Path.GetFileNameWithoutExtension(dialog.FileName);trail.Reach=farm.Reach;trail.Write(dialog.FileName);AutoSaveTrail();ShowTrailInfo();Status("Trajeto salvo: "+Path.GetFileName(dialog.FileName)+" ("+trail.Path.Count+" pontos).");Log("Trajeto salvo em "+dialog.FileName);}catch(Exception e){Error(e);}}}
        void LoadTrail(){if(execution.Running||trailRecording){Status("Pare antes de carregar outro trajeto.");return;}
            using(var dialog=new OpenFileDialog{Filter="Trajeto do Mat Farm (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",Title="Carregar trajeto manual",InitialDirectory=RotaPaths.Routes}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            try{var loaded=TrailRoute.Read(dialog.FileName,catalog);trail=loaded;farm.Reach=loaded.Reach;RememberOther(loaded.Imported?dialog.FileName:null);
                if(loaded.Materials.Count>0){route.ResourceIds.Clear();route.ResourceIds.AddRange(loaded.Materials);SaveChoices();worldPanel.SelectionChanged();UpdateFarm(lastObserved);}
                AutoSaveTrail();ShowTrailInfo();ShowTrailMap();Status("Trajeto carregado: "+loaded.Name+" · "+loaded.Path.Count+" pontos"+(loaded.Imported?" · sem altura no arquivo: voa na altura em que estiver":"")+(loaded.UnknownNames.Count>0?" · materiais não reconhecidos: "+String.Join(", ",loaded.UnknownNames):"")+". Clique Rodar trajeto.");Log("Trajeto carregado de "+dialog.FileName);}catch(Exception e){Error(e);}}}
    }
}
