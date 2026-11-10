using System;using System.Collections.Generic;using System.Diagnostics;
// 0.37.0 (owner): RegisterHotKey takes a key away from every other window, so a shortcut like "1" could not be typed in a
// browser, WhatsApp or anywhere else. The shortcuts are now registered only while a game window is in front and released
// as soon as another program is; switching back to the game registers them again. Nothing changes inside the game.
partial class AvalonInterno {
 bool gameInFront,quietBind;readonly Dictionary<int,bool> gamePids=new Dictionary<int,bool>();
 bool IsGamePid(int pid){bool game;if(gamePids.TryGetValue(pid,out game))return game;
  try{using(var p=Process.GetProcessById(pid))game=String.Equals(p.ProcessName,"ElementClient",StringComparison.OrdinalIgnoreCase);}catch{game=false;}
  if(gamePids.Count>256)gamePids.Clear();gamePids[pid]=game;return game;}
 // Called every 300 ms with the active-window tracking; re-binds only when the game gains or loses the front.
 void TrackShortcutFocus(){uint pid;Win.GetWindowThreadProcessId(Win.GetForegroundWindow(),out pid);bool now=pid!=0&&IsGamePid((int)pid);
  if(now==gameInFront)return;gameInFront=now;if(!now)gamePids.Clear();quietBind=true;try{Bind();}finally{quietBind=false;}}
 // While re-binding for a focus change, the "shortcut registered" lines are not repeated in the log (failures still are).
 bool QuietNote(string t){return quietBind&&(t.StartsWith("Atalho registrado")||t.Contains(": atalho "));}
}
