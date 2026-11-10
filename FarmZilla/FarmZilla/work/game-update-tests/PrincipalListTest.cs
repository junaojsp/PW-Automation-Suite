using System;using System.Linq;using System.Reflection;using System.Windows.Forms;
// Autoteste 0.19.14 fora da tela, sem jogo e sem timers: a janela ativa vai ao topo da lista Principal sem trocar o principal.
class PrincipalListForm : AvalonInterno { protected override void OnLoad(EventArgs e){} protected override void OnShown(EventArgs e){} protected override bool ShowWithoutActivation {get{return true;}} }
class PrincipalListTest {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Assert(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 [STAThread]static int Main(){try{using(var f=new PrincipalListForm()){
  var t=typeof(AvalonInterno);var main=(ComboBox)t.GetField("main",Flags).GetValue(f);var type=t.GetNestedType("Entry",BindingFlags.NonPublic);string[] names={"Lima","Limao","Limeira","Limago"};
  t.GetField("syncing",Flags).SetValue(f,true);for(int i=0;i<names.Length;i++){var e=Activator.CreateInstance(type,true);type.GetField("Pid").SetValue(e,i+1);type.GetField("Id").SetValue(e,(uint)i+100);type.GetField("Name").SetValue(e,names[i]);main.Items.Add(e);}main.SelectedIndex=0;t.GetField("syncing",Flags).SetValue(f,false);
  Func<string[]> order=()=>main.Items.Cast<object>().Select(o=>o.ToString()).ToArray();Action drop=()=>t.GetMethod("OrderPrincipalForDropDown",Flags).Invoke(f,null),close=()=>t.GetMethod("OrderPrincipalList",Flags).Invoke(f,null);
  drop();Assert(order().SequenceEqual(names)&&main.Text=="Lima","sem janela ativa conhecida a lista fica igual");
  t.GetField("lastActiveGamePid",Flags).SetValue(f,3);drop();Assert(order()[0]=="Limeira"&&order()[1]=="Lima"&&main.SelectedItem.ToString()=="Lima","janela ativa no topo, principal logo abaixo e ainda selecionado");
  var tag=((CyanCombo)main).ItemTag;Assert(tag(main.Items[0])=="● janela ativa"&&tag(main.Items[1])==null,"só a janela ativa recebe a marca");
  close();Assert(order()[0]=="Lima"&&main.SelectedItem.ToString()=="Lima","ao fechar sem escolher, volta a ordem normal");
  t.GetField("lastActiveGamePid",Flags).SetValue(f,1);drop();Assert(order()[0]=="Lima"&&order().Length==4,"principal que já é a janela ativa fica no topo sem duplicar");
  Console.WriteLine("Lista Principal aprovada.");return 0;}
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}
