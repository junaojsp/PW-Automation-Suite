using System;using System.Collections.Generic;using System.Threading;
// 0.50.5 (registro 07/11 12:26:53–12:27:02: o ataque abriu a ponte de suporte na Limeira enquanto o "EP ressuscita" abria
// outra no mesmo cliente; a restauração falhou e o jogo da Limeira fechou): uma ponte por cliente de cada vez. Toda ponte
// espera a vez no construtor (até 10 s) e libera no Dispose.
static class ProcessGate {
 public sealed class Token {internal SemaphoreSlim Gate;}
 static readonly Dictionary<int,SemaphoreSlim> gates=new Dictionary<int,SemaphoreSlim>();
 public static Token Enter(int pid){SemaphoreSlim g;lock(gates){if(!gates.TryGetValue(pid,out g))gates[pid]=g=new SemaphoreSlim(1,1);}
  if(!g.Wait(10000))throw new Exception("Cliente ocupado com outro comando do FarmZilla; tente de novo.");return new Token{Gate=g};}
 public static void Exit(ref Token token){var t=token;token=null;if(t!=null&&t.Gate!=null){var g=t.Gate;t.Gate=null;g.Release();}}
}
