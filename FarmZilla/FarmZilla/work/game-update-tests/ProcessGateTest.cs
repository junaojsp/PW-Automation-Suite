using System;using System.Diagnostics;using System.Threading;using System.Threading.Tasks;
// Autoteste 0.50.5 sem jogo: uma ponte por cliente de cada vez; outro cliente não espera; liberar duas vezes não quebra.
class ProcessGateTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static int Main(){try{
  var a=ProcessGate.Enter(111);var clock=Stopwatch.StartNew();var other=Task.Run(()=>{var t=ProcessGate.Enter(111);var waited=clock.ElapsedMilliseconds;ProcessGate.Exit(ref t);return waited;});
  var b=ProcessGate.Enter(222);Check(clock.ElapsedMilliseconds<200,"outro cliente entra sem esperar");ProcessGate.Exit(ref b);
  Thread.Sleep(400);Check(!other.IsCompleted,"mesmo cliente espera a ponte anterior terminar");ProcessGate.Exit(ref a);
  Check(other.Wait(2000)&&other.Result>=350,"depois de liberar, a próxima entra ("+other.Result+" ms)");ProcessGate.Exit(ref a);Check(a==null,"liberar duas vezes é seguro");
  Console.WriteLine("Trava de ponte aprovada.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
