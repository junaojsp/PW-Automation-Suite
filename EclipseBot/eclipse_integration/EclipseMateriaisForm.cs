using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// Visual route editor only. Does not issue game commands or change the original bot engine.
internal sealed class EclipseMateriaisForm : Form
{
    private readonly Color bg = Color.FromArgb(12, 19, 31);
    private readonly Color panel = Color.FromArgb(20, 35, 54);
    private readonly Color cyan = Color.FromArgb(63, 216, 239);
    private readonly Color ink = Color.FromArgb(223, 238, 248);
    private readonly ListBox routes = new ListBox();
    private readonly DataGridView points = new DataGridView();
    private readonly TextBox name = new TextBox();
    private readonly Label info = new Label();
    private readonly string store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EclipseBot", "materiais-rotas.txt");
    private bool loading;
    private readonly Func<string[]> characterSource;
    private readonly ListView characters = new ListView();
    private readonly Label diagnostic = new Label();
    private readonly ComboBox assignment = new ComboBox();
    private readonly string assignmentStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EclipseBot", "materiais-atribuicoes.tsv");
    private readonly Dictionary<string,string> assignedRoutes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);


    public EclipseMateriaisForm(Func<string[]> source = null)
    {
        characterSource = source;
        Text = "EclipseBot | Eclipse Materiais";
        ClientSize = new Size(940, 620);
        MinimumSize = new Size(790, 540);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = bg;
        ForeColor = ink;
        Font = new Font("Segoe UI", 9f);
        Icon = SystemIcons.Application;
        LoadAssignments();
        Build();
        LoadRoutes();
        RefreshCharacters();
    }

    private Label Label(string text, int x, int y, int w, int h, float size, bool bold = false)
    {
        var c = new Label { Text = text, Bounds = new Rectangle(x,y,w,h), ForeColor = ink,
            Font = new Font("Segoe UI",size,bold ? FontStyle.Bold : FontStyle.Regular), AutoEllipsis = true };
        Controls.Add(c); return c;
    }
    private Button Button(string text, int x, int y, int w, EventHandler click)
    {
        var b = new Button { Text=text, Bounds=new Rectangle(x,y,w,32), BackColor=panel, ForeColor=ink,
            FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(51,94,120);
        b.Click += click; Controls.Add(b); return b;
    }
    private void Build()
    {
        Label("ECLIPSE MATERIAIS", 22,18,420,36,18,true).ForeColor=cyan;
        Label("Gerenciador visual de rotas de coleta",22,55,450,24,9);
        Label("PERSONAGENS DETECTADOS",22,91,270,24,10,true);
        diagnostic.Bounds = new Rectangle(22, 73, 860, 19);
        diagnostic.ForeColor = Color.FromArgb(149,179,196);
        diagnostic.Text = "Diagnóstico: aguardando atualização de personagens. Materiais próximos: não integrado.";
        Controls.Add(diagnostic);
        characters.Bounds = new Rectangle(22,119,620,66);
        characters.View = View.Details; characters.FullRowSelect = true;
        characters.MultiSelect = false; characters.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        characters.Columns.Add("Personagem", 235); characters.Columns.Add("Rota atribuída", 350);
        characters.BackColor = panel; characters.ForeColor = ink;
        characters.BorderStyle = BorderStyle.FixedSingle;
        characters.SelectedIndexChanged += (s,e) => UpdateAssignmentSelection();
        Controls.Add(characters);
        assignment.Bounds = new Rectangle(662,120,250,26);
        assignment.DropDownStyle = ComboBoxStyle.DropDownList;
        assignment.BackColor = panel; assignment.ForeColor = ink;
        Controls.Add(assignment);
        Button("Atribuir rota",662,151,120,(s,e)=>AssignRoute());
        Button("Atualizar lista",792,151,120,(s,e)=>RefreshCharacters());
        Label("ROTAS SALVAS",22,196,220,25,10,true);
        routes.Bounds=new Rectangle(22,225,235,340);
        routes.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Bottom;
        routes.BackColor=panel; routes.ForeColor=ink; routes.BorderStyle=BorderStyle.FixedSingle;
        routes.SelectedIndexChanged += (s,e)=>ShowRoute(); Controls.Add(routes);
        Button("+ Nova rota",22,580,111,(s,e)=>NewRoute()).Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
        Button("Excluir",145,580,112,(s,e)=>DeleteRoute()).Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
        Label("NOME DA ROTA",285,196,280,25,10,true);
        name.Bounds=new Rectangle(285,225,390,30); name.BackColor=panel; name.ForeColor=ink;
        name.BorderStyle=BorderStyle.FixedSingle; name.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        Controls.Add(name);
        Label("PONTOS DE PASSAGEM",285,266,400,24,10,true);
        points.Bounds=new Rectangle(285,296,630,269);
        points.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom;
        points.BackgroundColor=panel; points.GridColor=Color.FromArgb(44,67,86);
        points.EnableHeadersVisualStyles=false; points.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(28,53,76);
        points.ColumnHeadersDefaultCellStyle.ForeColor=ink;
        points.DefaultCellStyle.BackColor=panel; points.DefaultCellStyle.ForeColor=ink;
        points.DefaultCellStyle.SelectionBackColor=Color.FromArgb(33,94,118);
        points.DefaultCellStyle.SelectionForeColor=Color.White;
        points.RowHeadersVisible=false; points.AllowUserToAddRows=true;
        points.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        points.Columns.Add("ordem","Ordem"); points.Columns.Add("x","X");
        points.Columns.Add("y","Y"); points.Columns.Add("z","Z / Altura");
        points.Columns.Add("obs","Observação");
        Controls.Add(points);
        Button("+ Ponto",285,580,110,(s,e)=>points.Rows.Add()).Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
        Button("Remover ponto",407,580,130,(s,e)=>RemovePoint()).Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
        var save=Button("Salvar rota",787,580,128,(s,e)=>SaveRoute());
        save.BackColor=Color.FromArgb(24,113,136); save.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;
        info.Bounds=new Rectangle(285,623,630,42); info.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
        info.ForeColor=Color.FromArgb(149,179,196); info.Text="Editor visual. Nenhum comando será enviado ao jogo.";
        Controls.Add(info);
        Resize += (s,e)=> { characters.Width=Math.Max(300,ClientSize.Width-320); assignment.Left=ClientSize.Width-278; save.Top=ClientSize.Height-100; info.Top=ClientSize.Height-56; points.Width=Math.Max(350,ClientSize.Width-310); name.Width=Math.Max(220,ClientSize.Width-550); save.Left=ClientSize.Width-153; info.Width=ClientSize.Width-310; };
    }
    private void NewRoute()
    {
        routes.Items.Add("Nova rota " + (routes.Items.Count+1));
        routes.SelectedIndex=routes.Items.Count-1;
        name.Text=routes.SelectedItem.ToString(); points.Rows.Clear();
        info.Text="Nova rota criada. Adicione pontos e clique em Salvar rota.";
    }
    private void ShowRoute()
    {
        if (loading || routes.SelectedItem==null) return;
        name.Text=routes.SelectedItem.ToString(); points.Rows.Clear();
        var file=RouteFile(name.Text);
        if (!File.Exists(file)) return;
        foreach(var line in File.ReadAllLines(file,Encoding.UTF8))
        {
            var values=line.Split('\t');
            if(values.Length==5) points.Rows.Add(values);
        }
    }
    private string RouteFile(string route)
    {
        var safe = new StringBuilder();
        foreach(char c in route) safe.Append(char.IsLetterOrDigit(c)||c=='-'||c=='_' ? c : '_');
        return Path.Combine(Path.GetDirectoryName(store),"route_"+safe.ToString()+".tsv");
    }
    private void LoadRoutes()
    {
        loading=true;
        try { if(File.Exists(store)) foreach(var item in File.ReadAllLines(store,Encoding.UTF8))
            if(!string.IsNullOrWhiteSpace(item)) routes.Items.Add(item); }
        catch(Exception ex) { info.Text="Não foi possível carregar rotas: "+ex.Message; }
        finally { loading=false; }
        if(routes.Items.Count>0) routes.SelectedIndex=0;
    }
    private void SaveRoute()
    {
        if(routes.SelectedIndex<0) { info.Text="Crie ou selecione uma rota primeiro."; return; }
        var route=name.Text.Trim();
        if(route.Length==0 || route.IndexOfAny(Path.GetInvalidFileNameChars())>=0) { info.Text="Informe um nome de rota válido."; return; }
        try
        {
            points.EndEdit(); Directory.CreateDirectory(Path.GetDirectoryName(store));
            var previous=routes.SelectedItem.ToString();
            var oldFile=RouteFile(previous);
            var newFile=RouteFile(route);
            if(!string.Equals(oldFile,newFile,StringComparison.OrdinalIgnoreCase) && File.Exists(oldFile) && !File.Exists(newFile)) File.Move(oldFile,newFile);
            var lines=new System.Collections.Generic.List<string>();
            foreach(DataGridViewRow row in points.Rows)
            {
                if(row.IsNewRow) continue;
                var cols=new string[5]; bool any=false;
                for(int i=0;i<5;i++) { cols[i]=Convert.ToString(row.Cells[i].Value)??"";
                    cols[i]=cols[i].Replace("\t"," ").Replace("\r"," ").Replace("\n"," ");
                    if(cols[i].Length>0) any=true; }
                if(any) lines.Add(string.Join("\t",cols));
            }
            File.WriteAllLines(newFile,lines.ToArray(),Encoding.UTF8);
            int index=routes.SelectedIndex; routes.Items[index]=route;
            File.WriteAllLines(store,GetRouteNames(),Encoding.UTF8);
            info.Text="Rota salva localmente: "+lines.Count+" ponto(s). Nenhuma automação ativada.";
            RefreshCharacters();
        }
        catch(Exception ex) { info.Text="Erro ao salvar: "+ex.Message; }
    }
    private string[] GetRouteNames()
    {
        var names=new string[routes.Items.Count];
        for(int i=0;i<names.Length;i++) names[i]=routes.Items[i].ToString();
        return names;
    }
    private void DeleteRoute()
    {
        if(routes.SelectedIndex<0) return;
        if(MessageBox.Show(this,"Excluir esta rota?","Eclipse Materiais",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
        var selected=routes.SelectedItem.ToString();
        try { var file=RouteFile(selected); if(File.Exists(file)) File.Delete(file);
            routes.Items.RemoveAt(routes.SelectedIndex); name.Text=""; points.Rows.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(store)); File.WriteAllLines(store,GetRouteNames(),Encoding.UTF8);
            info.Text="Rota excluída.";
        } catch(Exception ex) { info.Text="Erro ao excluir: "+ex.Message; }
    }
    private void LoadAssignments()
    {
        try { if (File.Exists(assignmentStore)) foreach(var line in File.ReadAllLines(assignmentStore, Encoding.UTF8))
        { var pair=line.Split(new char[]{'\t'},2); if(pair.Length==2) assignedRoutes[pair[0]]=pair[1]; } }
        catch { /* Route assignments are optional. */ }
    }
    public void RefreshCharacters()
    {
        string selected = characters.SelectedItems.Count>0 ? characters.SelectedItems[0].Text : null;
        characters.BeginUpdate();
        try {
            characters.Items.Clear();
            foreach(string character in (characterSource==null ? new string[0] : characterSource()))
            {
                if(string.IsNullOrWhiteSpace(character)) continue;
                var item = new ListViewItem(character);
                item.SubItems.Add(assignedRoutes.ContainsKey(character) ? assignedRoutes[character] : "—");
                characters.Items.Add(item);
                if(character==selected) item.Selected=true;
            }
        } finally { characters.EndUpdate(); }
        diagnostic.Text = "Diagnóstico: " + characters.Items.Count + " personagem(ns) identificado(s). Detecção de materiais: aguardando integração.";
        assignment.Items.Clear(); assignment.Items.Add("— Sem rota —");
        foreach(var route in routes.Items) assignment.Items.Add(route.ToString());
        assignment.SelectedIndex=0;
        UpdateAssignmentSelection();
    }
    private void UpdateAssignmentSelection()
    {
        if(characters.SelectedItems.Count==0) return;
        string key=characters.SelectedItems[0].Text;
        diagnostic.Text = "Selecionado: " + key + " | Detecção de materiais: não disponível nesta versão.";
        string route;
        if(assignedRoutes.TryGetValue(key,out route) && assignment.Items.Contains(route))
            assignment.SelectedItem=route;
        else assignment.SelectedIndex=0;
    }
    private void AssignRoute()
    {
        if(characters.SelectedItems.Count==0) { info.Text="Selecione um personagem na lista."; return; }
        var character=characters.SelectedItems[0].Text;
        var route=assignment.SelectedIndex<=0 ? null : assignment.SelectedItem.ToString();
        if(route==null) assignedRoutes.Remove(character); else assignedRoutes[character]=route;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(assignmentStore));
            File.WriteAllLines(assignmentStore,assignedRoutes.Select(p=>p.Key+"\t"+p.Value).ToArray(),Encoding.UTF8);
            characters.SelectedItems[0].SubItems[1].Text=route??"—";
            info.Text="Atribuição salva. Nenhuma automação de coleta foi iniciada.";
        } catch(Exception ex) { info.Text="Falha ao salvar atribuição: "+ex.Message; }
    }
    private void RemovePoint()
    {
        if(points.CurrentRow!=null && !points.CurrentRow.IsNewRow) points.Rows.Remove(points.CurrentRow);
    }
}
