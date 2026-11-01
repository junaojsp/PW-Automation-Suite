using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal class AvalonInterno : Form, IParentPainter
{
	private class Entry
	{
		public int Pid;

		public uint Id;

		public string Name;

		public int Class = -1;

		public override string ToString()
		{
			return Name;
		}

		public CharacterRef Ref()
		{
			CharacterRef characterRef = new CharacterRef();
			characterRef.Pid = Pid;
			characterRef.Id = Id;
			characterRef.Name = Name;
			return characterRef;
		}
	}

	private QuestService missions;

	private TeleportService teleport;

	private System.Windows.Forms.Timer teleportTimer;

	private Label teleportStatus = new Label();

	private Label missionCheck = new Label();

	private bool closeReady;

	private bool closeInProgress;

	private ComboBox main = new CyanCombo();

	private ComboBox[] rows = new ComboBox[9];

	private bool syncing;

	private bool shortcutActive = true;

	private ToolTip hints = new ToolTip();

	private CheckedListBox followers = new CheckedListBox();

	private TextBox key = new TextBox();

	private TextBox log = new TextBox();

	private CheckBox ctrl = new CheckBox();

	private CheckBox alt = new CheckBox();

	private CheckBox shift = new CheckBox();

	private Label status = new Label();

	private Button action;

	private Keys choice = Keys.F8;

	private bool busy;

	private HashSet<int> blocked = new HashSet<int>();

	private string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno", "config.txt");

	private bool teleportOnlyFollowing = true;

	private readonly CheckBox[] attackRows = new CheckBox[9];

	private readonly ComboBox attackMode = new CyanCombo();

	private TextBox attackKey = new TextBox();

	private Keys attackChoice = Keys.D1;

	private bool attackCtrl;

	private bool attackAlt;

	private bool attackShift;

	private bool attackRequested;

	private bool attackStopRequested;

	private bool attackStopping;

	private int savedAttackMode = 1;

	private AttackController attacks;

	private Task attackJob = Task.FromResult(0);

	private TextBox attackModeKey = new TextBox();

	private Keys attackModeChoice = Keys.None;

	private bool attackModeCtrl;

	private bool attackModeAlt;

	private bool attackModeShift;

	private bool authorizationDenied;

	private bool authorizationChecking;

	private bool logSending;

	private string authorizationFailureReason;

	private System.Windows.Forms.Timer authorizationTimer;

	private System.Windows.Forms.Timer logTimer;

	private DateTime nextAuthorizationCheck;

	private TextBox buffKey = new TextBox();

	private Keys buffChoice = Keys.D5;

	private bool buffCtrl;

	private bool buffAlt;

	private bool buffShift;

	private CancellationTokenSource buffCancel;

	private bool buffing;

	private int lastActiveGamePid;

	private readonly Dictionary<int, FollowIntent> followIntents = new Dictionary<int, FollowIntent>();

	private bool repairingFollow;

	private readonly CheckBox meditationMirror = new CheckBox();

	private MeditationController meditation;

	private IMeditationRuntime meditationRuntime = new NativeMeditationRuntime();

	private bool meditationPrimarySitting;

	private TextBox partyKey = new TextBox();

	private Keys partyChoice = Keys.F6;

	private bool partyCtrl;

	private bool partyAlt;

	private bool partyShift;

	private CancellationTokenSource partyCancel;

	private bool popupWasOpen;

	private bool popupChecking;

	private bool popupClosing;

	private TextBox remountKey = new TextBox();

	private Keys remountChoice = Keys.F7;

	private bool remountCtrl;

	private bool remountAlt;

	private bool remountShift;

	private string updateSummary = "Atualizar aplicativo";

	private Button updateSettingsButton;

	private System.Windows.Forms.Timer updateTimer;

	private bool updateChecking;

	private string pendingUpdate;

	private bool MeditationPause => shortcutActive && meditationMirror.Checked && meditationPrimarySitting;

	public AvalonInterno()
	{
		Text = "EclipseBot";
		Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		ClientSize = new Size(432, 665);
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(7, 7, 7);
		ForeColor = Color.White;
		Font = new Font("Segoe UI", 9f);
		DoubleBuffered = true;
		Label label = new Label
		{
			Text = "",
			Left = 5,
			Top = 5,
			Width = 420,
			Height = 26,
			BackColor = Color.FromArgb(14, 15, 15)
		};
		MouseEventHandler value = (object o, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				DragNative.ReleaseCapture();
				DragNative.SendMessage(Handle, 161, new IntPtr(2), IntPtr.Zero);
			}
		};
		label.MouseDown += value;
		Controls.Add(label);
		PictureBox value2 = new PictureBox
		{
			Image = Icon.ToBitmap(),
			Left = 13,
			Top = 1,
			Width = 32,
			Height = 32,
			SizeMode = PictureBoxSizeMode.Zoom
		};
		Controls.Add(value2);
		AddLabel("v0.19.19 · x86", 50, 10, 90, 18, new Font("Segoe UI", 8f), Color.Cyan);
		AddLabel("FARMZILLA", 150, 10, 167, 18, new Font("Segoe UI", 8f, FontStyle.Bold), Color.Cyan);
		Button button = ButtonAt("—", 358, 5, 30, () =>
		{
			WindowState = FormWindowState.Minimized;
		});
		button.Height = 23;
		Button button2 = ButtonAt("×", 393, 5, 30, Close);
		button2.Height = 23;
		AddLabel("Principal", 71, 51, 95, 20, new Font("Segoe UI", 10f, FontStyle.Bold), Color.White);
		main.SetBounds(29, 80, 153, 36);
		main.DropDownStyle = ComboBoxStyle.DropDownList;
		main.SelectedIndexChanged += (object o, EventArgs e) =>
		{
			CheckFollowers();
		};
		Controls.Add(main);
		hints.SetToolTip(main, "Escolha o personagem principal. A lista mostra o nome do personagem.");
		AddLabel("Secundários", 61, 134, 130, 21, new Font("Segoe UI", 10f, FontStyle.Bold), Color.White);
		for (int num = 0; num < 9; num++)
		{
			CyanCombo row = new CyanCombo();
			row.SetBounds(29, 157 + num * 47, 153, 36);
			row.DropDownStyle = ComboBoxStyle.DropDownList;
			row.SelectedIndexChanged += (object o, EventArgs e) =>
			{
				SyncFollowers(row);
			};
			Controls.Add(row);
			rows[num] = row;
			CheckBox checkBox = new CheckBox
			{
				Text = "",
				Left = 188,
				Top = 167 + num * 47,
				Width = 18,
				Height = 20,
				Enabled = false,
				Font = new Font("Segoe UI", 8f),
				ForeColor = Color.Cyan
			};
			attackRows[num] = checkBox;
			Controls.Add(checkBox);
			AddLabel("Atacar", 208, 169 + num * 47, 48, 17, new Font("Segoe UI", 8f), Color.White);
			hints.SetToolTip(checkBox, "Marque para este secundário usar a macro 0 no alvo do principal.");
		}
		string[] array = new string[13]
		{
			"Montar PT", "Remont. PT", "Bufar", "Seguir", "Atacar", "Ress MS", "Auto F12", "Skill Daimon", "Extra F4", "Extra F5",
			"Extra F6", "Extra F7", "Extra F8"
		};
		string[] array2 = new string[13]
		{
			"D3", "D4", "D5", "F8", "D1", "D0", "Oem5", "D9", "Oem5", "Oem5",
			"Oem5", "Oem5", "Oem5"
		};
		for (int num = 0; num < array.Length; num++)
		{
			if (num == 5 || num == 6 || num == 7)
			{
				continue;
			}
			int num2 = 48 + num * 28;
			AddLabel(array[num], 268, num2 + 3, 71, 19, new Font("Segoe UI", 8f), (num <= 4) ? Color.White : Color.FromArgb(125, 132, 132));
			switch (num)
			{
			case 3:
				key.SetBounds(352, num2 + 3, 58, 16);
				key.ReadOnly = true;
				key.BorderStyle = BorderStyle.None;
				key.Text = "F8";
				key.TextAlign = HorizontalAlignment.Center;
				key.BackColor = Color.Black;
				key.ForeColor = Color.Cyan;
				key.Font = new Font("Segoe UI", 8f);
				key.KeyDown += (object o, KeyEventArgs e) =>
				{
					e.SuppressKeyPress = true;
					if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
					{
						choice = e.KeyCode;
						ctrl.Checked = e.Control;
						alt.Checked = e.Alt;
						shift.Checked = e.Shift;
						key.Text = KeyText();
						Save();
					}
				};
				Controls.Add(key);
				hints.SetToolTip(key, "Clique e pressione a tecla desejada. A alteração é salva automaticamente. Ctrl, Alt e Shift são aceitos.");
				break;
			case 0:
				PartyKeyEditor(352, num2 + 3);
				break;
			case 1:
				RemountKeyEditor(352, num2 + 3);
				break;
			case 2:
				BuffKeyEditor(352, num2 + 3);
				break;
			case 4:
				AttackKeyEditor(352, num2 + 3);
				break;
			default:
			{
				Label label2 = new Label
				{
					Text = "—",
					Left = 352,
					Top = num2 + 3,
					Width = 58,
					Height = 16,
					TextAlign = ContentAlignment.MiddleCenter,
					ForeColor = Color.FromArgb(85, 101, 101),
					BackColor = Color.Black,
					Font = new Font("Segoe UI", 8f)
				};
				Controls.Add(label2);
				hints.SetToolTip(label2, array[num] + ": ainda não disponível.");
				break;
			}
			}
		}
		PartyButton();
		RemountButton();
		InitAttack();
		InitMeditation();
		// EclipseBot fork: do not start the original automatic updater.
		// InitUpdates();
		InitAuthorization();
		status.SetBounds(29, 585, 382, 23);
		status.ForeColor = Color.FromArgb(142, 204, 204);
		status.Font = new Font("Segoe UI", 8f);
		status.Text = "Aguardando clientes";
		status.AutoEllipsis = true;
		status.Click += (object o, EventArgs e) =>
		{
			ShowLog();
		};
		Controls.Add(status);
		hints.SetToolTip(status, "Clique para ver o registro completo.");
		hints.SetToolTip(teleportStatus, "Teleporte e missões funcionam com o app minimizado. Selecione os secundários e reabra o NPC no principal antes de aceitar a missão.");
		action = ButtonAt("Desativar", 29, 618, 153, () =>
		{
			if (!busy)
			{
				shortcutActive = !shortcutActive;
				RequestAttackStop();
				Bind();
				action.Text = (shortcutActive ? "Desativar" : "Ativar");
				status.Text = (shortcutActive ? ("Atalhos ativos · Seguir " + KeyText() + " · PT " + PartyKeyText()) : "Atalhos desativados");
			}
		});
		action.Height = 30;
		hints.SetToolTip(action, "Ativa ou desativa os atalhos de Seguir, Montar PT, Remontar PT, Bufar e Atacar. O automático também pausa ao desativar. Não cancela seguimento já ativo.");
		Button button3 = ButtonAt("Atualizar", 190, 618, 70, Refresh);
		button3.Height = 30;
		hints.SetToolTip(button3, "Atualiza a lista de personagens abertos.");
		Button button4 = ButtonAt("Configurações", 272, 618, 140, Configure);
		button4.Height = 30;
		updateSettingsButton = button4;
		teleportStatus.SetBounds(270, 490, 143, 60);
		teleportStatus.ForeColor = Color.Cyan;
		teleportStatus.Font = new Font("Segoe UI", 8f);
		Controls.Add(teleportStatus);
		missionCheck.SetBounds(270, 557, 143, 24);
		missionCheck.Font = new Font("Segoe UI", 8f);
		missionCheck.ForeColor = Color.LightGreen;
		missionCheck.AutoEllipsis = true;
		missionCheck.Click += (object a, EventArgs b) =>
		{
			ShowLog();
		};
		Controls.Add(missionCheck);
		missions = new QuestService(Note, blocked);
		missions.ReportCheck = (ushort id, int ok, int total) =>
		{
			missionCheck.Text = ((ok == total) ? "✓ " : "") + ok + "/" + total + " · missão " + id;
			missionCheck.ForeColor = ((ok == total) ? Color.LightGreen : Color.Orange);
		};
		teleport = new TeleportService(Note, (string t) =>
		{
			teleportStatus.Text = t;
		}, blocked);
		teleport.OnNpcCommand = missions.Observe;
		teleport.QuestJobRunning = () => missions.Running;
		teleport.QuestQueueBusy = () => missions.Working;
		teleport.ShouldMirror = (int pid) => !teleportOnlyFollowing || (followIntents.TryGetValue(pid, out var value3) && value3.Enabled);
		teleport.OnTeleportCaptured = () =>
		{
			missions.CancelAll();
			RequestAttackStop();
		};
		teleport.BeforeTeleport = async () =>
		{
			await StopAttacks();
			await missions.CancelAndWait();
		};
		teleportTimer = new System.Windows.Forms.Timer
		{
			Interval = 300
		};
		teleportTimer.Tick += async (object o, EventArgs e) =>
		{
			if (!authorizationDenied)
			{
				TrackActiveGame();
				Entry primary = main.SelectedItem as Entry;
				CharacterRef[] selected = (from Entry x in followers.CheckedItems
					where primary != null && x.Pid != primary.Pid
					select x.Ref()).ToArray();
				SyncFollowIntent();
				missions.UpdateSelection((primary == null) ? null : primary.Ref(), selected);
				long pollVersion = teleport.CapturePollVersion;
				await teleport.Pump((primary == null) ? null : primary.Ref(), selected, busy);
				if (teleport.CapturePollVersion != pollVersion)
				{
					missions.RefreshPrimarySnapshot();
				}
				if (missions.Working && !busy)
				{
					await StopAttacks();
				}
				missions.Pump(busy || teleport.Working);
				await MeditationTick();
				await AttackTick();
				await MaintainFollowing();
				await PopupTick();
			}
		};
		InitializeCharacterLists();
		ApplyRubiCompact();
		ApplyEclipseShell();
		Load += (object o, EventArgs e) =>
		{
			LoadSettings();
			syncing = true;
			attackMode.SelectedIndex = savedAttackMode;
			syncing = false;
			Refresh();
			Bind();
			teleportTimer.Start();
		};
		FormClosing += async (object o, FormClosingEventArgs e) =>
		{
			if (!closeReady)
			{
				e.Cancel = true;
				if (!closeInProgress)
				{
					closeInProgress = true;
					if (busy)
					{
						CancelParty();
						Note("Encerrando: aguardando a operação atual terminar.");
						while (busy)
						{
							await Task.Delay(100);
						}
					}
					missions.CancelAll();
					Enabled = false;
					teleportTimer.Stop();
					try
					{
						await StopAttacks();
						await teleport.Stop();
						await missions.Stop();
						closeReady = true;
						Close();
					}
					catch (Exception ex)
					{
						closeInProgress = false;
						Enabled = !authorizationDenied;
						Note("Falha removendo captura: " + ex.Message);
						MessageBox.Show(this, "Não foi possível confirmar a remoção da captura. Feche o cliente principal e tente fechar o aplicativo novamente.", "Captura de teleporte", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					}
				}
			}
		};
		FormClosed += (object o, FormClosedEventArgs e) =>
		{
			Win.UnregisterHotKey(Handle, 1);
			Win.UnregisterHotKey(Handle, 2);
			Win.UnregisterHotKey(Handle, 3);
			Win.UnregisterHotKey(Handle, 4);
			Win.UnregisterHotKey(Handle, 5);
			teleportTimer.Dispose();
			hints.Dispose();
			if (Icon != null)
			{
				Icon.Dispose();
			}
		};
	}

	private string KeyText()
	{
		return (ctrl.Checked ? "Ctrl+" : "") + (alt.Checked ? "Alt+" : "") + (shift.Checked ? "Shift+" : "") + choice;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		PaintRubi(e.Graphics);
	}

	private void ShowLog()
	{
		Form form = new Form();
		form.Text = "Registro · FarmZilla";
		form.Size = new Size(700, 360);
		form.StartPosition = FormStartPosition.CenterParent;
		form.BackColor = BackColor;
		form.ForeColor = ForeColor;
		Form form2 = form;
		TextBox textBox = new TextBox();
		textBox.Dock = DockStyle.Fill;
		textBox.Multiline = true;
		textBox.ReadOnly = true;
		textBox.ScrollBars = ScrollBars.Vertical;
		textBox.Text = log.Text;
		textBox.BackColor = Color.FromArgb(15, 20, 20);
		textBox.ForeColor = Color.LightGray;
		textBox.Font = new Font("Segoe UI", 9f);
		TextBox value = textBox;
		form2.Controls.Add(value);
		form2.ShowDialog(this);
		form2.Dispose();
	}

	private void Configure()
	{
		if (busy)
		{
			return;
		}
		Form d = new Form
		{
			Text = "Configurações · FarmZilla",
			ClientSize = new Size(330, 330),
			FormBorderStyle = FormBorderStyle.FixedDialog,
			MaximizeBox = false,
			MinimizeBox = false,
			StartPosition = FormStartPosition.CenterParent,
			BackColor = BackColor,
			ForeColor = ForeColor,
			Font = Font
		};
		try
		{
			Label label = new Label();
			label.Text = "Seguir: " + KeyText() + " · Montar PT: " + PartyKeyText() + "\nTroque nos campos da tela principal.";
			label.Left = 18;
			label.Top = 16;
			label.Width = 300;
			label.Height = 43;
			Label label2 = label;
			d.Controls.Add(label2);
			Action<string, int, Action> action = (string t, int y, Action cb) =>
			{
				NeonButton neonButton = new NeonButton
				{
					Text = t,
					Left = 18,
					Top = y,
					Width = 292,
					Height = 32
				};
				neonButton.Click += (object o, EventArgs e) =>
				{
					d.Close();
					cb();
				};
				d.Controls.Add(neonButton);
			};
			action("Identificar principal (3 segundos)", 73, Identify);
			action("Conferir estado dos personagens", 116, Inspect);
			action("Testar seguir / parar", 159, () =>
			{
				Task task = Toggle();
			});
			action("Ver registro", 202, ShowLog);
			hints.SetToolTip(label2, updateSummary);
			action("Atualizar aplicativo", 245, () =>
			{
				Task task = CheckUpdate(interactive: true);
			});
			CheckBox onlyFollowing = new CheckBox
			{
				Text = "Teleportar só quem está seguindo",
				Left = 20,
				Top = 290,
				Width = 292,
				Height = 24,
				Checked = teleportOnlyFollowing,
				ForeColor = Rubi.Text,
				FlatStyle = FlatStyle.Flat
			};
			onlyFollowing.CheckedChanged += (object o, EventArgs e) =>
			{
				teleportOnlyFollowing = onlyFollowing.Checked;
				Save();
				Note(teleportOnlyFollowing ? "Teleporte: só secundários com Seguir ativo acompanham." : "Teleporte: todos os secundários selecionados acompanham.");
			};
			hints.SetToolTip(onlyFollowing, "Ligado: com Seguir desativado, o secundário não acompanha a Pedra nem o Teleporte Especial.");
			d.Controls.Add(onlyFollowing);
			d.ShowDialog(this);
		}
		finally
		{
			if (d != null)
			{
				((IDisposable)d).Dispose();
			}
		}
	}

	private void AddLabel(string t, int x, int y, int w, int h, Font font, Color color)
	{
		Controls.Add(new Label
		{
			Text = t,
			Left = x,
			Top = y,
			Width = w,
			Height = h,
			Font = (font ?? Font),
			ForeColor = color
		});
	}

	private Button ButtonAt(string t, int x, int y, int w, Action cb)
	{
		NeonButton neonButton = new NeonButton();
		neonButton.Text = t;
		neonButton.Left = x;
		neonButton.Top = y;
		neonButton.Width = w;
		neonButton.Height = 35;
		NeonButton neonButton2 = neonButton;
		neonButton2.Click += (object o, EventArgs e) =>
		{
			if (!authorizationDenied)
			{
				cb();
			}
		};
		Controls.Add(neonButton2);
		return neonButton2;
	}

	private void Note(string t)
	{
		DiagnosticLog.Write(t);
		log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + t + Environment.NewLine);
	}

	private new void Refresh()
	{
		if (busy)
		{
			return;
		}
		RequestAttackStop();
		teleport.SelectionChanged();
		CancelParty();
		missions.CancelAll();
		int num = ((main.SelectedItem is Entry) ? ((Entry)main.SelectedItem).Pid : 0);
		int[] chosen = (from r in rows
			select r.SelectedItem as Entry into x
			select x?.Pid ?? 0).ToArray();
		syncing = true;
		try
		{
			main.Items.Clear();
			followers.Items.Clear();
			ClientCandidate[] array = ClientDiscovery.Find(Note);
			ClientCandidate[] array2 = array;
			foreach (ClientCandidate clientCandidate in array2)
			{
				try
				{
					if (ClientDiscovery.Compatible(clientCandidate.Pid, Note))
					{
						using CharacterMemory characterMemory = new CharacterMemory(clientCandidate.Pid);
						Entry entry = new Entry();
						entry.Pid = clientCandidate.Pid;
						entry.Id = characterMemory.Id;
						entry.Name = characterMemory.Name;
						entry.Class = characterMemory.Profession;
						Entry item = entry;
						main.Items.Add(item);
						followers.Items.Add(item);
					}
				}
				catch (Exception ex)
				{
					Note("PID " + clientCandidate.Pid + " · " + (string.IsNullOrEmpty(clientCandidate.WindowTitle) ? "cliente encontrado" : clientCandidate.WindowTitle) + ": " + ex.Message + " [" + ex.GetType().Name + "]");
				}
			}
			for (int num3 = 0; num3 < main.Items.Count; num3++)
			{
				if (((Entry)main.Items[num3]).Pid == num)
				{
					main.SelectedIndex = num3;
				}
			}
			if (main.SelectedIndex < 0 && main.Items.Count > 0)
			{
				main.SelectedIndex = 0;
			}
			if (main.Items.Count > 0)
			{
				LoadClassIcons(((Entry)main.Items[0]).Pid);
			}
			status.Text = "Personagens: " + main.Items.Count + " · Clientes encontrados: " + array.Length;
		}
		finally
		{
			syncing = false;
			PopulateFollowers(chosen);
		}
	}

	private void CheckFollowers()
	{
		if (main.SelectedItem != null && !syncing)
		{
			RequestAttackStop();
			teleport.SelectionChanged();
			CancelParty();
			missions.CancelAll();
			PopulateFollowers(new int[9]);
		}
	}

	private async void Identify()
	{
		if (busy)
		{
			return;
		}
		busy = true;
		try
		{
			status.Text = "Ative a janela do principal em até 3 segundos...";
			await Task.Delay(3000);
			Win.GetWindowThreadProcessId(Win.GetForegroundWindow(), out var pid);
			for (int i = 0; i < main.Items.Count; i++)
			{
				if (((Entry)main.Items[i]).Pid == pid)
				{
					main.SelectedIndex = i;
					status.Text = "Principal definido";
					Note("Principal: PID " + pid);
					return;
				}
			}
			Note("A janela ativa não é um dos clientes conectados.");
		}
		finally
		{
			busy = false;
		}
	}

	private void Bind()
	{
		BindAttack();
		BindParty();
		key.Text = KeyText();
		Win.UnregisterHotKey(Handle, 1);
		if (shortcutActive)
		{
			uint mods = (uint)(0x4000 | (ctrl.Checked ? 2 : 0) | (alt.Checked ? 1 : 0) | (shift.Checked ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 1, mods, (uint)choice) ? ("Atalho registrado: " + (ctrl.Checked ? "Ctrl+" : "") + (alt.Checked ? "Alt+" : "") + (shift.Checked ? "Shift+" : "") + choice) : "Tecla ocupada; escolha outra e salve.");
		}
	}

	private void Save()
	{
		if (busy)
		{
			return;
		}
		Bind();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(settings));
			string path = settings;
			string[] array = new string[27];
			int num = (int)choice;
			array[0] = num.ToString();
			array[1] = ctrl.Checked.ToString();
			array[2] = alt.Checked.ToString();
			array[3] = shift.Checked.ToString();
			num = (int)partyChoice;
			array[4] = num.ToString();
			array[5] = partyCtrl.ToString();
			array[6] = partyAlt.ToString();
			array[7] = partyShift.ToString();
			num = (int)remountChoice;
			array[8] = num.ToString();
			array[9] = remountCtrl.ToString();
			array[10] = remountAlt.ToString();
			array[11] = remountShift.ToString();
			num = (int)buffChoice;
			array[12] = num.ToString();
			array[13] = buffCtrl.ToString();
			array[14] = buffAlt.ToString();
			array[15] = buffShift.ToString();
			num = (int)attackChoice;
			array[16] = num.ToString();
			array[17] = attackCtrl.ToString();
			array[18] = attackAlt.ToString();
			array[19] = attackShift.ToString();
			array[20] = savedAttackMode.ToString();
			array[21] = meditationMirror.Checked.ToString();
			array[22] = teleportOnlyFollowing.ToString();
			num = (int)attackModeChoice;
			array[23] = num.ToString();
			array[24] = attackModeCtrl.ToString();
			array[25] = attackModeAlt.ToString();
			array[26] = attackModeShift.ToString();
			File.WriteAllLines(path, array);
		}
		catch (Exception ex)
		{
			Note(ex.Message);
		}
	}

	private void LoadSettings()
	{
		try
		{
			if (!File.Exists(settings))
			{
				return;
			}
			string[] array = File.ReadAllLines(settings);
			choice = (Keys)int.Parse(array[0]);
			key.Text = choice.ToString();
			ctrl.Checked = bool.Parse(array[1]);
			alt.Checked = bool.Parse(array[2]);
			shift.Checked = bool.Parse(array[3]);
			if (array.Length >= 8)
			{
				partyChoice = (Keys)int.Parse(array[4]);
				partyCtrl = bool.Parse(array[5]);
				partyAlt = bool.Parse(array[6]);
				partyShift = bool.Parse(array[7]);
			}
			if (array.Length >= 12)
			{
				remountChoice = (Keys)int.Parse(array[8]);
				remountCtrl = bool.Parse(array[9]);
				remountAlt = bool.Parse(array[10]);
				remountShift = bool.Parse(array[11]);
			}
			if (array.Length >= 16)
			{
				buffChoice = (Keys)int.Parse(array[12]);
				buffCtrl = bool.Parse(array[13]);
				buffAlt = bool.Parse(array[14]);
				buffShift = bool.Parse(array[15]);
			}
			if (array.Length >= 21)
			{
				attackChoice = (Keys)int.Parse(array[16]);
				attackCtrl = bool.Parse(array[17]);
				attackAlt = bool.Parse(array[18]);
				attackShift = bool.Parse(array[19]);
				savedAttackMode = ((int.Parse(array[20]) != 0) ? 1 : 0);
			}
			if (array.Length >= 22)
			{
				bool flag = syncing;
				syncing = true;
				try
				{
					meditationMirror.Checked = bool.Parse(array[21]);
				}
				finally
				{
					syncing = flag;
				}
			}
			if (array.Length >= 23)
			{
				teleportOnlyFollowing = bool.Parse(array[22]);
			}
			if (array.Length >= 27)
			{
				attackModeChoice = (Keys)int.Parse(array[23]);
				attackModeCtrl = bool.Parse(array[24]);
				attackModeAlt = bool.Parse(array[25]);
				attackModeShift = bool.Parse(array[26]);
			}
		}
		catch
		{
			Note("Configuração padrão carregada.");
		}
	}

	private void Inspect()
	{
		if (busy)
		{
			return;
		}
		foreach (Entry item in followers.Items)
		{
			try
			{
				using State state = new State(item.Pid);
				Note("PID " + item.Pid + ": " + (state.Following ? ("seguindo ID " + state.Target) : "sem seguimento ativo"));
			}
			catch (Exception ex)
			{
				Note(ex.Message);
			}
		}
	}

	private string Command(int pid, uint target, bool stop)
	{
		if (blocked.Contains(pid))
		{
			throw new Exception("Ponte anterior não confirmou remoção no PID " + pid + ". Reinicie esse cliente antes de tentar novamente.");
		}
		InternalBridge internalBridge = new InternalBridge(pid);
		uint num = 0u;
		try
		{
			internalBridge.Follow(stop ? 1u : target);
			num = internalBridge.ExecutedThread;
		}
		finally
		{
			internalBridge.Dispose();
			if (!internalBridge.RestoreSucceeded)
			{
				blocked.Add(pid);
			}
		}
		if (!internalBridge.RestoreSucceeded)
		{
			throw new Exception("Não foi possível confirmar a remoção da ponte. Novos comandos para esse cliente foram bloqueados.");
		}
		for (int i = 0; i < 15; i++)
		{
			Thread.Sleep(100);
			using State state = new State(pid);
			bool num2;
			if (!stop)
			{
				if (state.Following)
				{
					num2 = state.Target == target;
					goto IL_00ca;
				}
				continue;
			}
			num2 = !state.Following;
			goto IL_00ca;
			IL_00ca:
			if (num2)
			{
				return "PID " + pid + ": " + (stop ? "parou de seguir" : ("seguindo ID " + target)) + " • interno, thread " + num;
			}
		}
		throw new Exception("Comando enviado, mas o estado de seguimento não foi confirmado no PID " + pid);
	}

	protected override void WndProc(ref Message m)
	{
		if (!authorizationDenied && shortcutActive && m.Msg == 786 && (m.WParam.ToInt32() == 1 || m.WParam.ToInt32() == 2 || m.WParam.ToInt32() == 3 || m.WParam.ToInt32() == 4 || m.WParam.ToInt32() == 5 || m.WParam.ToInt32() == 6))
		{
			Win.GetWindowThreadProcessId(Win.GetForegroundWindow(), out var pid);
			if (pid == Process.GetCurrentProcess().Id || main.Items.Cast<Entry>().Any((Entry e) => e.Pid == pid))
			{
				Task task;
				if (m.WParam.ToInt32() == 2)
				{
					task = MakeParty();
				}
				else if (m.WParam.ToInt32() == 3)
				{
					task = RemountParty();
				}
				else if (m.WParam.ToInt32() == 4)
				{
					task = BuffParty();
				}
				else if (m.WParam.ToInt32() == 5)
				{
					task = RequestAttack();
				}
				else
				{
					task = ((m.WParam.ToInt32() == 6) ? ToggleAttackMode() : Toggle());
				}
			}
		}
		base.WndProc(ref m);
	}

	[STAThread]
	private static void Main(string[] args)
	{
		if (!AppUpdate.RunInstaller(args))
		{
			Win.SetProcessDPIAware();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			if (LicenseGate.Ensure())
			{
				AppUpdate.ClientAuthorization = LicenseGate.CurrentToken;
				Application.Run(new AvalonInterno());
			}
		}
	}

	private string AttackKeyText()
	{
		return (attackCtrl ? "Ctrl+" : "") + (attackAlt ? "Alt+" : "") + (attackShift ? "Shift+" : "") + attackChoice;
	}

	private void AttackKeyEditor(int x, int y)
	{
		attackKey.SetBounds(x, y, 58, 16);
		attackKey.ReadOnly = true;
		attackKey.BorderStyle = BorderStyle.None;
		attackKey.Text = AttackKeyText();
		attackKey.TextAlign = HorizontalAlignment.Center;
		attackKey.BackColor = Color.Black;
		attackKey.ForeColor = Color.Cyan;
		attackKey.Font = new Font("Segoe UI", 8f);
		attackKey.KeyDown += (object o, KeyEventArgs e) =>
		{
			e.SuppressKeyPress = true;
			if (!busy && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
			{
				attackChoice = e.KeyCode;
				attackCtrl = e.Control;
				attackAlt = e.Alt;
				attackShift = e.Shift;
				attackKey.Text = AttackKeyText();
				Save();
			}
		};
		Controls.Add(attackKey);
		hints.SetToolTip(attackKey, "Tecla para iniciar a macro 0 no modo Manual. Padrão: 1.");
	}

	private void InitAttack()
	{
		AddLabel("Modo de ataque", 270, 191, 143, 18, new Font("Segoe UI", 8f), Color.White);
		attackMode.SetBounds(270, 212, 143, 28);
		attackMode.DropDownStyle = ComboBoxStyle.DropDownList;
		attackMode.Items.AddRange(new object[2] { "Automático", "Manual por tecla" });
		attackMode.SelectedIndex = 1;
		attackMode.SelectedIndexChanged += (object o, EventArgs e) =>
		{
			RequestAttackStop();
			if (!syncing)
			{
				savedAttackMode = attackMode.SelectedIndex;
				Save();
			}
		};
		Controls.Add(attackMode);
		hints.SetToolTip(attackMode, "Automático: enquanto o principal tiver um monstro selecionado, os secundários repetem a macro 0; selecione você mesmo para parar. Manual: uma vez por tecla Atacar. Participam os secundários marcados.");
		AttackModeKeyEditor();
		attacks = new AttackController(new NativeAttackRuntime(), blocked, (string s) =>
		{
			if (!IsDisposed)
			{
				BeginInvoke((Action)(() =>
				{
					Note(s);
				}));
			}
		});
	}

	private string AttackModeKeyText()
	{
		return (attackModeChoice == Keys.None) ? "—" : ((attackModeCtrl ? "Ctrl+" : "") + (attackModeAlt ? "Alt+" : "") + (attackModeShift ? "Shift+" : "") + attackModeChoice);
	}

	private void AttackModeKeyEditor()
	{
		attackModeKey.ReadOnly = true;
		attackModeKey.TextAlign = HorizontalAlignment.Center;
		attackModeKey.Text = AttackModeKeyText();
		attackModeKey.KeyDown += (object o, KeyEventArgs e) =>
		{
			e.SuppressKeyPress = true;
			if (!busy && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
			{
				if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
				{
					attackModeChoice = Keys.None;
					attackModeCtrl = (attackModeAlt = (attackModeShift = false));
				}
				else
				{
					attackModeChoice = e.KeyCode;
					attackModeCtrl = e.Control;
					attackModeAlt = e.Alt;
					attackModeShift = e.Shift;
				}
				attackModeKey.Text = AttackModeKeyText();
				Save();
			}
		};
		Controls.Add(attackModeKey);
		hints.SetToolTip(attackModeKey, "Tecla que alterna o modo de ataque entre Automático e Manual por tecla. Delete apaga.");
	}

	private void BindAttackMode()
	{
		attackModeKey.Text = AttackModeKeyText();
		Win.UnregisterHotKey(Handle, 6);
		if (shortcutActive && attackModeChoice != Keys.None)
		{
			uint mods = (uint)(0x4000 | (attackModeCtrl ? 2 : 0) | (attackModeAlt ? 1 : 0) | (attackModeShift ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 6, mods, (uint)attackModeChoice) ? ("Modo de ataque: atalho " + AttackModeKeyText()) : "Tecla do modo de ataque ocupada; escolha outra.");
		}
	}

	private Task ToggleAttackMode()
	{
		if (closeInProgress)
		{
			return Task.FromResult(0);
		}
		CancelBuffWait();
		attackMode.SelectedIndex = ((attackMode.SelectedIndex == 0) ? 1 : 0);
		string text = ((attackMode.SelectedIndex == 0) ? "Automático" : "Manual por tecla");
		Note("Modo de ataque: " + text + ".");
		status.Text = "Modo de ataque · " + text;
		return Task.FromResult(0);
	}

	private void BindAttack()
	{
		BindAttackMode();
		attackKey.Text = AttackKeyText();
		Win.UnregisterHotKey(Handle, 5);
		if (shortcutActive)
		{
			uint mods = (uint)(0x4000 | (attackCtrl ? 2 : 0) | (attackAlt ? 1 : 0) | (attackShift ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 5, mods, (uint)attackChoice) ? ("Atacar: atalho " + AttackKeyText()) : "Tecla de Atacar ocupada; escolha outra.");
		}
	}

	private void UpdateAttackRows()
	{
		for (int i = 0; i < 9; i++)
		{
			bool flag = rows[i].SelectedItem is Entry;
			attackRows[i].Enabled = flag;
			if (!flag)
			{
				attackRows[i].Checked = false;
			}
		}
	}

	private void RequestAttackStop()
	{
		attackStopRequested = true;
		attackRequested = false;
	}

	private Task RequestAttack()
	{
		if (closeInProgress)
		{
			return Task.FromResult(0);
		}
		CancelBuffWait();
		if (MeditationPause)
		{
			Note("Levante o principal antes de atacar.");
			return Task.FromResult(0);
		}
		if (!shortcutActive || attackMode.SelectedIndex != 1)
		{
			Note("A tecla Atacar é usada no modo Manual por tecla.");
			return Task.FromResult(0);
		}
		if (busy || teleport.Working || missions.Working)
		{
			Note("Aguarde a operação atual para atacar.");
			return Task.FromResult(0);
		}
		if (!attackRows.Any((CheckBox c) => c.Enabled && c.Checked))
		{
			Note("Marque Atacar nos secundários desejados.");
			return Task.FromResult(0);
		}
		if (!(main.SelectedItem is Entry entry))
		{
			Note("Escolha o principal.");
			return Task.FromResult(0);
		}
		try
		{
			if (!AttackController.MonsterId(attacks.ReadTarget(entry.Ref())))
			{
				Note("Selecione um monstro no principal antes de apertar Atacar.");
				return Task.FromResult(0);
			}
		}
		catch (Exception ex)
		{
			Note("Ataque: " + ex.Message);
			return Task.FromResult(0);
		}
		attackRequested = true;
		return AttackTick();
	}

	private async Task AttackTick()
	{
		if (attacks == null || closeInProgress || busy || buffing || MeditationPause || attackStopping || !attackJob.IsCompleted || teleport.Working || missions.Working)
		{
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		CharacterRef[] selected = (from row in rows.Where((ComboBox row, int i) => attackRows[i].Enabled && attackRows[i].Checked)
			select row.SelectedItem as Entry into e
			where e != null && primary != null && e.Pid != primary.Pid
			select e.Ref()).ToArray();
		bool stop = attackStopRequested;
		bool request = attackRequested;
		bool automatic = attackMode.SelectedIndex == 0;
		bool enabled = shortcutActive;
		attackStopRequested = (attackRequested = false);
		busy = true;
		try
		{
			attackJob = Task.Run(() =>
			{
				if (stop)
				{
					attacks.StopAll();
				}
				attacks.Tick((primary == null) ? null : primary.Ref(), selected, enabled, automatic, request);
			});
			await attackJob;
		}
		catch (Exception ex)
		{
			attackStopRequested = true;
			Note("Ataque: " + ex.Message);
		}
		finally
		{
			busy = false;
		}
	}

	private async Task StopAttacks()
	{
		RequestAttackStop();
		if (attacks == null)
		{
			return;
		}
		bool ownsBusy = !busy;
		busy = true;
		attackStopping = true;
		try
		{
			try
			{
				await attackJob;
			}
			catch (Exception ex)
			{
				Note("Ataque anterior: " + ex.Message);
			}
			await Task.Run(() =>
			{
				attacks.StopAll();
			});
			attackStopRequested = false;
		}
		finally
		{
			attackStopping = false;
			if (ownsBusy)
			{
				busy = false;
			}
		}
	}

	private bool AttackEngaged(int pid)
	{
		return attackStopping || (attacks != null && attacks.Engaged(pid));
	}

	private void InitAuthorization()
	{
		nextAuthorizationCheck = DateTime.UtcNow.AddMinutes(30.0);
		authorizationTimer = new System.Windows.Forms.Timer
		{
			Interval = 15000
		};
		authorizationTimer.Tick += async (object s, EventArgs e) =>
		{
			if (authorizationDenied)
			{
				if (!closeInProgress)
				{
					Close();
				}
			}
			else if (!authorizationChecking && !closeInProgress)
			{
				if (!AuthorizationSession.Valid())
				{
					DenyAuthorization("A autorização offline expirou ou o relógio mudou. Abra novamente com internet.");
				}
				else if (!(DateTime.UtcNow < nextAuthorizationCheck))
				{
					authorizationChecking = true;
					try
					{
						AccessResult result = await Task.Run(() => OnlineLicense.Evaluate(LicenseGate.Identity, LicenseGate.ServiceUrl, () => OnlineApi.Acquire(LicenseGate.Identity, LicenseGate.ServiceUrl)));
						if (!IsDisposed && !closeInProgress)
						{
							if (!result.Allowed)
							{
								DenyAuthorization(result.Reason);
							}
							else
							{
								AuthorizationSession.Begin(result.Token);
								AppUpdate.ClientAuthorization = result.Token;
								nextAuthorizationCheck = DateTime.UtcNow.AddMinutes(30.0);
							}
						}
					}
					catch
					{
						DenyAuthorization("Não foi possível validar a autorização local com segurança.");
					}
					finally
					{
						authorizationChecking = false;
					}
				}
			}
		};
		logTimer = new System.Windows.Forms.Timer
		{
			Interval = 60000
		};
		logTimer.Tick += async (object s, EventArgs e) =>
		{
			if (logSending || authorizationDenied)
			{
				return;
			}
			string token = AuthorizationSession.Token;
			string service = LicenseGate.ServiceUrl;
			logSending = true;
			try
			{
				await Task.Run(() =>
				{
					RemoteLog.Upload(service, token);
				});
			}
			catch
			{
			}
			finally
			{
				logSending = false;
			}
		};
		Shown += (object s, EventArgs e) =>
		{
			authorizationTimer.Start();
			logTimer.Start();
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			authorizationTimer.Dispose();
			logTimer.Dispose();
			if (authorizationFailureReason != null)
			{
				MessageBox.Show(authorizationFailureReason + "\nAs operações do FarmZilla foram encerradas. O jogo continua aberto.", "Autorização do FarmZilla", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		};
	}

	private void DenyAuthorization(string reason)
	{
		if (!authorizationDenied)
		{
			authorizationDenied = true;
			authorizationFailureReason = reason;
			Enabled = false;
			shortcutActive = false;
			Bind();
			RequestAttackStop();
			missions.CancelAll();
			CancelParty();
			Note("Autorização encerrada: " + reason);
			Close();
		}
	}

	private string BuffKeyText()
	{
		return (buffCtrl ? "Ctrl+" : "") + (buffAlt ? "Alt+" : "") + (buffShift ? "Shift+" : "") + buffChoice;
	}

	private void BuffKeyEditor(int x, int y)
	{
		buffKey.SetBounds(x, y, 58, 16);
		buffKey.ReadOnly = true;
		buffKey.BorderStyle = BorderStyle.None;
		buffKey.Text = BuffKeyText();
		buffKey.TextAlign = HorizontalAlignment.Center;
		buffKey.BackColor = Color.Black;
		buffKey.ForeColor = Color.Cyan;
		buffKey.Font = new Font("Segoe UI", 8f);
		buffKey.KeyDown += (object o, KeyEventArgs e) =>
		{
			e.SuppressKeyPress = true;
			if (!busy && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
			{
				buffChoice = e.KeyCode;
				buffCtrl = e.Control;
				buffAlt = e.Alt;
				buffShift = e.Shift;
				buffKey.Text = BuffKeyText();
				Save();
			}
		};
		Controls.Add(buffKey);
		hints.SetToolTip(buffKey, "Escolha a tecla de Bufar. Executa a macro 1 (segundo slot da janela R) uma vez no principal e nos secundários selecionados. Padrão: 5.");
	}

	private void BindBuff()
	{
		buffKey.Text = BuffKeyText();
		Win.UnregisterHotKey(Handle, 4);
		if (shortcutActive)
		{
			uint mods = (uint)(0x4000 | (buffCtrl ? 2 : 0) | (buffAlt ? 1 : 0) | (buffShift ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 4, mods, (uint)buffChoice) ? ("Bufar: atalho " + BuffKeyText() + " · macro 1, uma vez") : "Tecla de Bufar ocupada; escolha outra.");
		}
	}

	private void CancelBuffWait()
	{
		CancellationTokenSource cancellationTokenSource = buffCancel;
		if (cancellationTokenSource == null)
		{
			return;
		}
		buffCancel = null;
		buffing = false;
		try
		{
			cancellationTokenSource.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private async Task BuffParty()
	{
		if (closeInProgress)
		{
			return;
		}
		CancelBuffWait();
		if (busy || teleport.Working || missions.Working)
		{
			Note("Aguarde a operação atual para bufar.");
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			Note("Escolha o principal.");
			return;
		}
		Entry[] selected = (from e in new Entry[1] { primary }.Concat(from Entry e in followers.CheckedItems
				where e.Pid != primary.Pid
				select e)
			group e by e.Pid into g
			select g.First()).ToArray();
		busy = true;
		action.Enabled = false;
		Entry[] started;
		try
		{
			await StopAttacks();
			BuffResult[] results = await Task.WhenAll(selected.Select((Entry e) => Task.Run(() => blocked.Contains(e.Pid) ? new BuffResult
			{
				Message = e.Name + ": reinicie esse cliente antes de bufar."
			} : BuffService.Start(e.Ref()))));
			for (int num = 0; num < results.Length; num++)
			{
				if (results[num].Blocked)
				{
					blocked.Add(selected[num].Pid);
				}
				Note(results[num].Message);
			}
			started = selected.Where((Entry e, int i) => results[i].Started && !results[i].Blocked).ToArray();
			status.Text = "Bufar · macro 1 acionada em " + started.Length + "/" + selected.Length;
		}
		catch (Exception ex)
		{
			Note("Bufar: " + ex.Message);
			return;
		}
		finally
		{
			busy = false;
			action.Enabled = true;
		}
		CancellationTokenSource cts = (buffCancel = new CancellationTokenSource());
		buffing = true;
		CancellationToken token = cts.Token;
		try
		{
			await Task.Delay(400, token);
			while (started.Length > 0)
			{
				token.ThrowIfCancellationRequested();
				started = await Task.Run(() => started.Where((Entry e) =>
				{
					try
					{
						return BuffService.Running(e.Ref());
					}
					catch
					{
						return false;
					}
				}).ToArray());
				if (started.Length > 0)
				{
					await Task.Delay(300, token);
				}
			}
			Note("Bufar: execução encerrada. Cada habilidade depende de alcance, alvo e recursos do personagem.");
		}
		catch (OperationCanceledException)
		{
			Note("Bufar: espera encerrada; o novo comando assumiu.");
		}
		finally
		{
			if (buffCancel == cts)
			{
				buffCancel = null;
				buffing = false;
			}
			cts.Dispose();
		}
	}

	private void InitializeCharacterLists()
	{
		main.DropDown += (object s, EventArgs e) =>
		{
			OrderPrincipalForDropDown();
		};
		main.DropDownClosed += (object s, EventArgs e) =>
		{
			OrderPrincipalList();
		};
		Func<object, int> itemClass = (object item) => (!(item is Entry entry)) ? (-1) : entry.Class;
		foreach (ComboBox item in new ComboBox[1] { main }.Concat(rows))
		{
			if (item is CyanCombo)
			{
				((CyanCombo)item).ItemClass = itemClass;
			}
		}
		if (main is CyanCombo cyanCombo)
		{
			cyanCombo.ItemTag = (object item) => (item is Entry entry && entry.Pid == lastActiveGamePid) ? "● janela ativa" : null;
		}
		hints.SetToolTip(main, "Ao abrir, a janela do jogo usada por último aparece no topo (● janela ativa). O principal só muda quando você escolhe.");
	}

	private void TrackActiveGame()
	{
		Win.GetWindowThreadProcessId(Win.GetForegroundWindow(), out var pid);
		if (pid != 0 && main.Items.Cast<Entry>().Any((Entry e) => e.Pid == pid))
		{
			lastActiveGamePid = (int)pid;
		}
	}

	private void OrderPrincipalForDropDown()
	{
		if (syncing || main.Items.Count < 2)
		{
			return;
		}
		Entry[] array = main.Items.Cast<Entry>().ToArray();
		Entry entry = main.SelectedItem as Entry;
		Entry entry2 = array.FirstOrDefault((Entry e) => e.Pid == lastActiveGamePid);
		List<Entry> list = new List<Entry>();
		if (entry2 != null)
		{
			list.Add(entry2);
		}
		if (entry != null && entry != entry2)
		{
			list.Add(entry);
		}
		Entry[] array2 = array;
		foreach (Entry item in array2)
		{
			if (!list.Contains(item))
			{
				list.Add(item);
			}
		}
		if (list.SequenceEqual(array))
		{
			return;
		}
		object selectedItem = main.SelectedItem;
		bool flag = syncing;
		syncing = true;
		main.BeginUpdate();
		try
		{
			main.Items.Clear();
			foreach (Entry item2 in list)
			{
				main.Items.Add(item2);
			}
			if (selectedItem != null)
			{
				main.SelectedItem = selectedItem;
			}
		}
		finally
		{
			main.EndUpdate();
			syncing = flag;
		}
	}

	private void LoadClassIcons(int pid)
	{
		Task.Run(() =>
		{
			string fileName;
			try
			{
				using Process process = Process.GetProcessById(pid);
				fileName = process.MainModule.FileName;
			}
			catch
			{
				return;
			}
			string note = ClassIcons.Load(fileName);
			try
			{
				if (!IsDisposed)
				{
					BeginInvoke((Action)(() =>
					{
						if (note != null)
						{
							Note(note);
						}
						main.Invalidate();
						ComboBox[] array = rows;
						foreach (ComboBox comboBox in array)
						{
							comboBox.Invalidate();
						}
					}));
				}
			}
			catch (InvalidOperationException)
			{
			}
		});
	}

	private void OrderPrincipalList()
	{
		if (syncing || main.SelectedItem == null || main.Items.Count < 2)
		{
			return;
		}
		Entry[] array = main.Items.Cast<Entry>().ToArray();
		if (!(main.SelectedItem is Entry entry) || array[0] == entry)
		{
			return;
		}
		object selectedItem = main.SelectedItem;
		bool flag = syncing;
		syncing = true;
		main.BeginUpdate();
		try
		{
			main.Items.Clear();
			main.Items.Add(entry);
			Entry[] array2 = array;
			foreach (Entry entry2 in array2)
			{
				if (entry2 != entry)
				{
					main.Items.Add(entry2);
				}
			}
			if (selectedItem != null)
			{
				main.SelectedItem = selectedItem;
			}
		}
		finally
		{
			main.EndUpdate();
			syncing = flag;
		}
	}

	private void PopulateFollowers(int[] chosen)
	{
		bool flag = syncing;
		syncing = true;
		try
		{
			int pid = (main.SelectedItem as Entry)?.Pid ?? 0;
			Entry[] array = (from Entry v in followers.Items
				where v.Pid != pid
				select v).ToArray();
			HashSet<int> hashSet = new HashSet<int>();
			Entry[] array2 = new Entry[rows.Length];
			for (int num = 0; num < rows.Length; num++)
			{
				int wanted = ((num < chosen.Length) ? chosen[num] : 0);
				Entry entry = array.FirstOrDefault((Entry x) => x.Pid == wanted);
				if (entry != null && hashSet.Add(entry.Pid))
				{
					array2[num] = entry;
				}
			}
			for (int num = 0; num < rows.Length; num++)
			{
				ComboBox comboBox = rows[num];
				comboBox.BeginUpdate();
				try
				{
					comboBox.Items.Clear();
					comboBox.Items.Add("—");
					Entry[] array3 = array;
					foreach (Entry entry in array3)
					{
						if (!hashSet.Contains(entry.Pid) || entry == array2[num])
						{
							comboBox.Items.Add(entry);
						}
					}
					if (array2[num] == null)
					{
						comboBox.SelectedIndex = 0;
					}
					else
					{
						comboBox.SelectedItem = array2[num];
					}
				}
				finally
				{
					comboBox.EndUpdate();
				}
			}
			for (int num = 0; num < followers.Items.Count; num++)
			{
				followers.SetItemChecked(num, hashSet.Contains(((Entry)followers.Items[num]).Pid));
			}
		}
		finally
		{
			syncing = flag;
			UpdateAttackRows();
		}
	}

	private void SyncFollowers(ComboBox changed)
	{
		if (syncing)
		{
			return;
		}
		RequestAttackStop();
		int num = Array.IndexOf(rows, changed);
		if (num >= 0)
		{
			attackRows[num].Checked = false;
		}
		teleport.SelectionChanged();
		CancelParty();
		missions.CancelAll();
		Entry entry = changed.SelectedItem as Entry;
		int[] array = (from r in rows
			select r.SelectedItem as Entry into x
			select x?.Pid ?? 0).ToArray();
		if (entry != null)
		{
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				if (num2 != num && array[num2] == entry.Pid)
				{
					array[num2] = 0;
				}
			}
		}
		PopulateFollowers(array);
	}

	private void SyncFollowIntent()
	{
		Entry primary = main.SelectedItem as Entry;
		Entry[] array = (from Entry e in followers.CheckedItems
			where primary != null && e.Pid != primary.Pid
			select e).ToArray();
		HashSet<int> keep = new HashSet<int>(array.Select((Entry e) => e.Pid));
		int[] array2 = followIntents.Keys.Where((int pid) => !keep.Contains(pid)).ToArray();
		foreach (int num2 in array2)
		{
			followIntents.Remove(num2);
		}
		if (primary == null)
		{
			return;
		}
		Entry[] array3 = array;
		foreach (Entry entry in array3)
		{
			if (followIntents.TryGetValue(entry.Pid, out var value) && value.Id == entry.Id && value.Target == primary.Id)
			{
				continue;
			}
			try
			{
				using State state = new State(entry.Pid);
				if (state.Id == entry.Id)
				{
					followIntents[entry.Pid] = new FollowIntent(entry.Pid, entry.Id, primary.Id, state.Following && state.Target == primary.Id);
				}
			}
			catch
			{
			}
		}
	}

	private Task Toggle()
	{
		if (closeInProgress)
		{
			return Task.FromResult(0);
		}
		CancelBuffWait();
		if (busy && !repairingFollow && partyCancel == null)
		{
			Note("Aguarde a operação de configuração terminar.");
			return Task.FromResult(0);
		}
		SyncFollowIntent();
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			Note("Escolha o principal.");
			return Task.FromResult(0);
		}
		Entry[] array = (from Entry e in followers.CheckedItems
			where e.Pid != primary.Pid
			select e).ToArray();
		if (array.Length == 0)
		{
			Note("Marque um secundário.");
			return Task.FromResult(0);
		}
		if (array.Any((Entry e) => !followIntents.ContainsKey(e.Pid)))
		{
			Note("Personagem carregando; tente a tecla novamente após entrar no mundo.");
			return Task.FromResult(0);
		}
		bool flag = !array.Any((Entry e) => followIntents[e.Pid].Enabled);
		Entry[] array2 = array;
		foreach (Entry entry in array2)
		{
			followIntents[entry.Pid].ToggleTo(flag);
		}
		status.Text = (flag ? "SEGUIR ATIVO · mantido após teleporte" : "SEGUIR DESATIVADO · pela tecla");
		Note(flag ? "Seguir ativado pela tecla; retomada automática após teleporte." : "Seguir desativado pela tecla; retomada automática cancelada.");
		return MaintainFollowing();
	}

	private async Task MaintainFollowing()
	{
		if (closeInProgress || busy || buffing)
		{
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			return;
		}
		Entry[] selected = (from Entry entry in followers.CheckedItems
			where entry.Pid != primary.Pid
			select entry).ToArray();
		busy = true;
		repairingFollow = true;
		action.Enabled = false;
		try
		{
			Entry[] array = selected;
			foreach (Entry e in array)
			{
				if (closeInProgress)
				{
					return;
				}
				if (teleport.IsClientBusy(e.Pid) || missions.IsClientBusy(e.Pid) || AttackEngaged(e.Pid) || MeditationEngaged(e.Pid) || !followIntents.TryGetValue(e.Pid, out var intent) || intent.Target != primary.Id || intent.Id != e.Id || blocked.Contains(e.Pid) || DateTime.UtcNow < intent.NextAttempt)
				{
					continue;
				}
				try
				{
					bool following;
					using (State state = new State(e.Pid))
					{
						if (state.Id != intent.Id)
						{
							continue;
						}
						following = state.Following && state.Target == intent.Target;
						goto IL_03eb;
					}
					IL_03eb:
					if (intent.Enabled)
					{
						if (!following)
						{
							intent.NextAttempt = DateTime.UtcNow.AddSeconds(3.0);
							string message = await Task.Run(() => Command(e.Pid, intent.Target, stop: false));
							intent.NextAttempt = DateTime.MinValue;
							Note(e.Name + ": seguimento retomado. " + message);
						}
					}
					else
					{
						if (!intent.StopPending)
						{
							continue;
						}
						if (!following)
						{
							intent.StopPending = false;
							continue;
						}
						intent.NextAttempt = DateTime.UtcNow.AddSeconds(3.0);
						string message2 = await Task.Run(() => Command(e.Pid, intent.Target, stop: true));
						intent.NextAttempt = DateTime.MinValue;
						Note(e.Name + ": seguimento parado pela tecla. " + message2);
						if (!intent.Enabled)
						{
							intent.StopPending = false;
						}
						continue;
					}
				}
				catch (Exception ex)
				{
					intent.NextAttempt = DateTime.UtcNow.AddSeconds(3.0);
					Note(e.Name + ": não foi possível confirmar seguimento. " + ex.Message);
				}
			}
			if (1 == 0)
			{
			}
		}
		finally
		{
			repairingFollow = false;
			busy = false;
			action.Enabled = true;
		}
	}

	private bool MeditationEngaged(int pid)
	{
		return meditation != null && meditation.Holds(pid);
	}

	private void InitMeditation()
	{
		meditationMirror.SetBounds(270, 247, 145, 24);
		meditationMirror.Text = "Espelhar meditação";
		meditationMirror.Font = new Font("Segoe UI", 8f);
		meditationMirror.ForeColor = Color.Cyan;
		meditationMirror.Checked = true;
		meditationMirror.CheckedChanged += (object o, EventArgs e) =>
		{
			if (!syncing)
			{
				Save();
			}
		};
		Controls.Add(meditationMirror);
		hints.SetToolTip(meditationMirror, "Quando o principal medita ou levanta, os secundários selecionados acompanham. Funciona minimizado. Seguir aguarda o fim da meditação.");
		meditation = new MeditationController(meditationRuntime, blocked, (string s) =>
		{
			if (!IsDisposed)
			{
				BeginInvoke((Action)(() =>
				{
					Note(s);
				}));
			}
		});
	}

	private async Task MeditationTick()
	{
		if (meditation == null || closeInProgress || busy || buffing || teleport.Working || missions.Working)
		{
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		CharacterRef[] selected = (from Entry e in followers.CheckedItems
			where primary != null && e.Pid != primary.Pid
			select e.Ref()).ToArray();
		try
		{
			meditationPrimarySitting = primary != null && meditationRuntime.Sitting(primary.Ref());
		}
		catch
		{
			return;
		}
		bool enabled = shortcutActive && meditationMirror.Checked;
		busy = true;
		try
		{
			if (enabled && meditationPrimarySitting && selected.Any((CharacterRef e) => AttackEngaged(e.Pid)))
			{
				await StopAttacks();
			}
			await Task.Run(() =>
			{
				meditation.Tick((primary == null) ? null : primary.Ref(), selected, enabled);
			});
		}
		catch (Exception ex)
		{
			Note("Meditação: " + ex.Message);
		}
		finally
		{
			busy = false;
		}
	}

	private string PartyKeyText()
	{
		return (partyCtrl ? "Ctrl+" : "") + (partyAlt ? "Alt+" : "") + (partyShift ? "Shift+" : "") + partyChoice;
	}

	private void PartyKeyEditor(int x, int y)
	{
		partyKey.SetBounds(x, y, 58, 16);
		partyKey.ReadOnly = true;
		partyKey.BorderStyle = BorderStyle.None;
		partyKey.Text = PartyKeyText();
		partyKey.TextAlign = HorizontalAlignment.Center;
		partyKey.BackColor = Color.Black;
		partyKey.ForeColor = Color.Cyan;
		partyKey.Font = new Font("Segoe UI", 8f);
		partyKey.KeyDown += (object o, KeyEventArgs e) =>
		{
			e.SuppressKeyPress = true;
			if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
			{
				partyChoice = e.KeyCode;
				partyCtrl = e.Control;
				partyAlt = e.Alt;
				partyShift = e.Shift;
				partyKey.Text = PartyKeyText();
				Save();
			}
		};
		Controls.Add(partyKey);
		hints.SetToolTip(partyKey, "Clique e pressione a tecla para Montar PT. Padrão: F6.");
	}

	private void PartyButton()
	{
		Button button = ButtonAt("Montar PT", 270, 408, 143, () =>
		{
			Task task = MakeParty();
		});
		button.Height = 29;
		hints.SetToolTip(button, "O principal convida os secundários selecionados; o app confirma a entrada no grupo.");
	}

	private void BindParty()
	{
		BindBuff();
		BindRemount();
		partyKey.Text = PartyKeyText();
		Win.UnregisterHotKey(Handle, 2);
		if (shortcutActive)
		{
			uint mods = (uint)(0x4000 | (partyCtrl ? 2 : 0) | (partyAlt ? 1 : 0) | (partyShift ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 2, mods, (uint)partyChoice) ? ("Montar PT: atalho " + PartyKeyText()) : "Tecla de Montar PT ocupada; escolha outra.");
		}
	}

	private void CancelParty()
	{
		if (partyCancel != null)
		{
			partyCancel.Cancel();
		}
		CancelBuffWait();
	}

	private async Task MakeParty()
	{
		if (closeInProgress)
		{
			return;
		}
		CancelBuffWait();
		if (busy || teleport.Working || missions.Working)
		{
			Note("Aguarde a operação atual para montar PT.");
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			Note("Escolha o principal.");
			return;
		}
		Entry[] selected = (from Entry e in followers.CheckedItems
			where e.Pid != primary.Pid
			select e).ToArray();
		if (selected.Length == 0)
		{
			Note("Selecione os secundários para montar PT.");
			return;
		}
		if (blocked.Contains(primary.Pid))
		{
			Note("Reinicie o cliente principal: ponte anterior não confirmou remoção.");
			return;
		}
		busy = true;
		action.Enabled = false;
		partyCancel = new CancellationTokenSource();
		CancellationToken token = partyCancel.Token;
		Progress<string> progress = new Progress<string>((string s) =>
		{
			Note(s);
		});
		try
		{
			await StopAttacks();
			foreach (Entry secondary in selected)
			{
				if (token.IsCancellationRequested) break;
				if (blocked.Contains(secondary.Pid))
				{
					Note(secondary.Name + ": reinicie esse cliente antes de tentar novamente.");
					continue;
				}
				PartyService.Outcome result = await Task.Run(() => PartyService.Join(primary.Ref(), secondary.Ref(), token, progress));
				if (result.Blocked)
				{
					blocked.Add(result.Pid);
					Note(result.Message);
					break;
				}
				Note(result.Message);
			}
			status.Text = (token.IsCancellationRequested ? "Montagem da PT cancelada" : "Montar PT concluído · consulte o registro");
		}
		catch (Exception ex)
		{
			Note("Montar PT: " + ex.Message);
		}
		finally
		{
			partyCancel.Dispose();
			partyCancel = null;
			busy = false;
			action.Enabled = true;
		}
	}

	private async Task PopupTick()
	{
		if (popupChecking || popupClosing || closeInProgress || authorizationDenied || !shortcutActive)
		{
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			return;
		}
		popupChecking = true;
		bool open;
		try
		{
			CharacterRef who = primary.Ref();
			open = await Task.Run(() => QuestService.PopupOpen(who));
		}
		catch
		{
			return;
		}
		finally
		{
			popupChecking = false;
		}
		if (open)
		{
			popupWasOpen = true;
		}
		else
		{
			if (!popupWasOpen)
			{
				return;
			}
			popupWasOpen = false;
			popupClosing = true;
			try
			{
				await Task.Delay(1500);
				if (closeInProgress || teleport.Working || teleport.QuestChoicePending || missions.Working || !(main.SelectedItem is Entry current) || current.Pid != primary.Pid)
				{
					return;
				}
				CharacterRef[] targets = (from Entry e in followers.CheckedItems
					where e.Pid != primary.Pid && !blocked.Contains(e.Pid) && !teleport.IsClientBusy(e.Pid) && !missions.IsClientBusy(e.Pid)
					select e.Ref()).ToArray();
				if (targets.Length == 0)
				{
					return;
				}
				Tuple<CharacterRef, bool, bool, string>[] results = await Task.WhenAll(targets.Select((CharacterRef t) => Task.Run(() =>
				{
					bool item = false;
					string item2 = null;
					bool item3 = false;
					try
					{
						item3 = QuestService.ClosePopup(t, CancellationToken.None, out item);
					}
					catch (Exception ex)
					{
						item2 = ex.Message;
					}
					return Tuple.Create(t, item3, item, item2);
				})));
				string[] closedNames = (from r in results
					where r.Item2
					select r.Item1.Name).ToArray();
				if (closedNames.Length > 0)
				{
					Note("Janela automática fechada no principal; fechada também em " + string.Join(", ", closedNames) + ".");
				}
				Tuple<CharacterRef, bool, bool, string>[] array = results;
				foreach (Tuple<CharacterRef, bool, bool, string> tuple in array)
				{
					if (tuple.Item3)
					{
						blocked.Add(tuple.Item1.Pid);
						Note(tuple.Item1.Name + ": ponte de fechamento sem restauração confirmada; reinicie esse cliente.");
					}
					else if (tuple.Item4 != null)
					{
						Note(tuple.Item1.Name + ": não foi possível fechar a janela automática: " + tuple.Item4);
					}
				}
				if (1 == 0)
				{
				}
			}
			finally
			{
				popupClosing = false;
			}
		}
	}

	private string RemountKeyText()
	{
		return (remountCtrl ? "Ctrl+" : "") + (remountAlt ? "Alt+" : "") + (remountShift ? "Shift+" : "") + remountChoice;
	}

	private void RemountKeyEditor(int x, int y)
	{
		remountKey.SetBounds(x, y, 58, 16);
		remountKey.ReadOnly = true;
		remountKey.BorderStyle = BorderStyle.None;
		remountKey.Text = RemountKeyText();
		remountKey.TextAlign = HorizontalAlignment.Center;
		remountKey.BackColor = Color.Black;
		remountKey.ForeColor = Color.Cyan;
		remountKey.Font = new Font("Segoe UI", 8f);
		remountKey.KeyDown += (object o, KeyEventArgs e) =>
		{
			e.SuppressKeyPress = true;
			if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
			{
				remountChoice = e.KeyCode;
				remountCtrl = e.Control;
				remountAlt = e.Alt;
				remountShift = e.Shift;
				remountKey.Text = RemountKeyText();
				Save();
			}
		};
		Controls.Add(remountKey);
		hints.SetToolTip(remountKey, "Clique e pressione a tecla para Remontar PT. Padrão: F7.");
	}

	private void BindRemount()
	{
		remountKey.Text = RemountKeyText();
		Win.UnregisterHotKey(Handle, 3);
		if (shortcutActive)
		{
			uint mods = (uint)(0x4000 | (remountCtrl ? 2 : 0) | (remountAlt ? 1 : 0) | (remountShift ? 4 : 0));
			Note(Win.RegisterHotKey(Handle, 3, mods, (uint)remountChoice) ? ("Remontar PT: atalho " + RemountKeyText()) : "Tecla de Remontar PT ocupada; escolha outra.");
		}
	}

	private void RemountButton()
	{
		Button button = ButtonAt("Remontar PT", 270, 445, 143, () =>
		{
			Task task = RemountParty();
		});
		button.Height = 29;
		hints.SetToolTip(button, "Transfere liderança temporariamente, encerra a PT anterior e reúne novamente os selecionados com o principal como líder.");
	}

	private async Task RemountParty()
	{
		if (closeInProgress)
		{
			return;
		}
		CancelBuffWait();
		if (busy || teleport.Working || missions.Working)
		{
			Note("Aguarde a operação atual para remontar PT.");
			return;
		}
		Entry primary = main.SelectedItem as Entry;
		if (primary == null)
		{
			Note("Escolha o principal.");
			return;
		}
		Entry[] selected = (from Entry e in followers.CheckedItems
			where e.Pid != primary.Pid
			select e).ToArray();
		if (selected.Length == 0)
		{
			Note("Selecione todos os secundários da PT antes de remontar.");
			return;
		}
		if (blocked.Contains(primary.Pid) || selected.Any((Entry s) => blocked.Contains(s.Pid)))
		{
			Note("Reinicie o cliente cuja ponte anterior não confirmou remoção.");
			return;
		}
		busy = true;
		action.Enabled = false;
		partyCancel = new CancellationTokenSource();
		CancellationToken token = partyCancel.Token;
		Progress<string> progress = new Progress<string>((string s) =>
		{
			Note(s);
		});
		try
		{
			await StopAttacks();
			PartyService.Outcome result = await Task.Run(() => PartyService.Remount(primary.Ref(), selected.Select((Entry s) => s.Ref()).ToArray(), token, progress));
			if (result.Blocked)
			{
				blocked.Add(result.Pid);
			}
			Note(result.Message);
			status.Text = result.Message;
		}
		catch (Exception ex)
		{
			Note("Remontar PT: " + ex.Message);
		}
		finally
		{
			partyCancel.Dispose();
			partyCancel = null;
			busy = false;
			action.Enabled = true;
		}
	}

	private void ApplyRubiCompact()
	{
		SuspendLayout();
		try
		{
			Button button = Controls.OfType<Button>().Single((Button b) => b.Text == "Montar PT");
			Button button2 = Controls.OfType<Button>().Single((Button b) => b.Text == "Remontar PT");
			Button button3 = Controls.OfType<Button>().Single((Button b) => b.Text == "Atualizar");
			Button button4 = Controls.OfType<Button>().Single((Button b) => b.Text == "—");
			Button button5 = Controls.OfType<Button>().Single((Button b) => b.Text == "×");
			HashSet<Control> hashSet = new HashSet<Control>();
			hashSet.Add(main);
			hashSet.Add(key);
			hashSet.Add(partyKey);
			hashSet.Add(remountKey);
			hashSet.Add(buffKey);
			hashSet.Add(attackKey);
			hashSet.Add(attackModeKey);
			hashSet.Add(attackMode);
			hashSet.Add(meditationMirror);
			hashSet.Add(status);
			hashSet.Add(missionCheck);
			hashSet.Add(teleportStatus);
			hashSet.Add(action);
			hashSet.Add(updateSettingsButton);
			hashSet.Add(button);
			hashSet.Add(button2);
			hashSet.Add(button3);
			hashSet.Add(button4);
			hashSet.Add(button5);
			HashSet<Control> hashSet2 = hashSet;
			ComboBox[] array = rows;
			foreach (ComboBox item in array)
			{
				hashSet2.Add(item);
			}
			CheckBox[] array2 = attackRows;
			foreach (CheckBox item2 in array2)
			{
				hashSet2.Add(item2);
			}
			Control[] array3 = Controls.Cast<Control>().ToArray();
			foreach (Control control in array3)
			{
				if (!hashSet2.Contains(control))
				{
					Controls.Remove(control);
					control.Dispose();
				}
			}
			Text = "EclipseBot";
			AutoScaleMode = AutoScaleMode.None;
			ClientSize = new Size(390, 606);
			BackColor = Rubi.Background;
			ForeColor = Rubi.Text;
			Font = new Font("Segoe UI", 9f);
			Padding = Padding.Empty;
			MouseDown += (object s, MouseEventArgs e) =>
			{
				if (e.Y < 48 && e.Button == MouseButtons.Left)
				{
					DragNative.ReleaseCapture();
					DragNative.SendMessage(Handle, 161, new IntPtr(2), IntPtr.Zero);
				}
			};
			Label label = RubiLabel("E", 14, 12, 28, 25, 9f, FontStyle.Bold, Rubi.OnAccent);
			label.TextAlign = ContentAlignment.MiddleCenter;
			EnableRubiDrag(label);
			Label label2 = RubiLabel("EclipseBot", 51, 8, 180, 32, 13f, FontStyle.Bold, Rubi.Accent);
			label2.Font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
			label2.AutoEllipsis = false;
			label2.TextAlign = ContentAlignment.MiddleLeft;
			EnableRubiDrag(label2);
			Label control2 = RubiLabel("v0.19.19", 232, 18, 79, 18, 8f, FontStyle.Regular, Rubi.Muted);
			EnableRubiDrag(control2);
			button4.SetBounds(321, 10, 25, 28);
			button5.SetBounds(351, 10, 25, 28);
			RubiLabel("PRINCIPAL", 27, 66, 140, 17, 8f, FontStyle.Regular, Rubi.Muted);
			main.SetBounds(27, 88, 336, 29);
			main.AccessibleName = "Principal";
			RubiLabel("Secundários", 14, 143, 163, 18, 8f, FontStyle.Bold, Rubi.Muted);
			RubiLabel("Atk", 201, 143, 28, 18, 8f, FontStyle.Regular, Rubi.Muted);
			RubiLabel("Comandos", 238, 143, 138, 18, 8f, FontStyle.Bold, Rubi.Muted);
			for (int num2 = 0; num2 < rows.Length; num2++)
			{
				rows[num2].SetBounds(14, 168 + num2 * 32, 178, 27);
				rows[num2].AccessibleName = "Secundário " + (num2 + 1);
				attackRows[num2].SetBounds(202, 171 + num2 * 32, 19, 21);
				attackRows[num2].AccessibleName = "Atacar com secundário " + (num2 + 1);
				attackRows[num2].ForeColor = Rubi.Accent;
				attackRows[num2].FlatStyle = FlatStyle.Flat;
				attackRows[num2].BackColor = CardTone(137, 322, 171 + num2 * 32 + 10);
			}
			string[] array4 = new string[5] { "Montar PT", "Remont. PT", "Bufar", "Seguir", "Atacar" };
			TextBox[] array5 = new TextBox[5] { partyKey, remountKey, buffKey, key, attackKey };
			for (int num2 = 0; num2 < array5.Length; num2++)
			{
				int num3 = 170 + num2 * 29;
				RubiLabel(array4[num2], 238, num3, 78, 20, 8f, FontStyle.Regular, Rubi.Text);
				array5[num2].SetBounds(321, num3, 52, 20);
				array5[num2].BorderStyle = BorderStyle.FixedSingle;
				array5[num2].BackColor = Rubi.Field;
				array5[num2].ForeColor = Rubi.Accent;
				array5[num2].Font = new Font("Segoe UI", 8f);
				array5[num2].AccessibleName = "Tecla " + array4[num2];
			}
			RubiLabel("Modo ataque", 238, 317, 80, 18, 8f, FontStyle.Regular, Rubi.Muted);
			attackModeKey.SetBounds(321, 315, 52, 20);
			attackModeKey.BorderStyle = BorderStyle.FixedSingle;
			attackModeKey.BackColor = Rubi.Field;
			attackModeKey.ForeColor = Rubi.Accent;
			attackModeKey.Font = new Font("Segoe UI", 8f);
			attackModeKey.AccessibleName = "Tecla Modo de ataque";
			attackMode.SetBounds(238, 341, 138, 28);
			attackMode.AccessibleName = "Modo de ataque";
			meditationMirror.SetBounds(238, 377, 138, 22);
			meditationMirror.Text = "Meditação";
			meditationMirror.ForeColor = Rubi.Muted;
			meditationMirror.FlatStyle = FlatStyle.Flat;
			meditationMirror.BackColor = CardTone(137, 268, 388);
			button.SetBounds(238, 411, 138, 28);
			button2.SetBounds(238, 445, 138, 28);
			Button button6 = ButtonAt("Bufar", 238, 479, 138, () =>
			{
				Task task = BuffParty();
			});
			button6.Height = 28;
			hints.SetToolTip(button6, "Executa a macro 1 uma única vez no principal e nos secundários selecionados.");
			teleportStatus.SetBounds(14, 465, 205, 35);
			teleportStatus.ForeColor = Rubi.Muted;
			teleportStatus.Font = new Font("Segoe UI", 8f);
			teleportStatus.AutoEllipsis = true;
			missionCheck.SetBounds(14, 506, 362, 19);
			missionCheck.Font = new Font("Segoe UI", 8f);
			status.SetBounds(14, 530, 362, 19);
			status.ForeColor = Rubi.Muted;
			status.Font = new Font("Segoe UI", 8f);
			Label label3 = teleportStatus;
			Label label4 = missionCheck;
			Color color = (status.BackColor = Color.Transparent);
			color = (label4.BackColor = color);
			label3.BackColor = color;
			action.SetBounds(14, 562, 179, 30);
			button3.SetBounds(201, 562, 76, 30);
			updateSettingsButton.SetBounds(285, 562, 91, 30);
			updateSettingsButton.Text = "Ajustes";
			foreach (NeonButton item3 in Controls.OfType<NeonButton>())
			{
				item3.BackColor = Rubi.Panel;
				item3.ForeColor = Rubi.Text;
				item3.Primary = item3 == action;
			}
			foreach (ComboBox item4 in Controls.OfType<ComboBox>())
			{
				item4.BackColor = Rubi.Field;
				item4.ForeColor = Rubi.Text;
				item4.Font = new Font("Segoe UI", 8f);
				item4.ItemHeight = 21;
				item4.DropDownHeight = 220;
			}
			hints.SetToolTip(updateSettingsButton, "Configurações e registro. Atualização automática original desativada nesta edição.");
			Invalidate();
		}
		finally
		{
			ResumeLayout(performLayout: false);
		}
	}

	private EclipseMateriaisForm materialsPage;
	private Panel materialsHeader;
	private Panel eclipseSidebar;
	private Size eclipseNormalSize;
	private bool materialsVisible;

	private string[] EclipseCharacterNames()
	{
		return new ComboBox[] { main }
			.SelectMany(c => c.Items.Cast<object>())
			.Select(item => item == null ? "" : item.ToString())
			.Where(label => !string.IsNullOrWhiteSpace(label) && label != "—" && label != "-")
			.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private void ToggleMaterialsPage(bool show)
	{
		if (materialsVisible == show) return;
		SuspendLayout();
		try
		{
			if (show)
			{
				eclipseNormalSize = ClientSize;
				foreach (Control c in Controls.Cast<Control>().ToArray())
					if (c != eclipseSidebar && c != materialsPage && c != materialsHeader) c.Visible = false;
				ClientSize = new Size(1120, 680);
				if (materialsPage == null || materialsPage.IsDisposed)
				{
					materialsPage = new EclipseMateriaisForm(EclipseCharacterNames);
					materialsPage.TopLevel = false;
					materialsPage.FormBorderStyle = FormBorderStyle.None;
					materialsPage.Dock = DockStyle.None;
					Controls.Add(materialsPage);
				}
				materialsPage.Bounds = new Rectangle(184, 40, ClientSize.Width - 184, ClientSize.Height - 40);
				if (materialsHeader == null)
				{
					materialsHeader = new Panel { BackColor = Color.FromArgb(15, 26, 43) };
					Controls.Add(materialsHeader);
					Label headerTitle = new Label { Text = "ECLIPSEBOT  /  MATERIAIS", ForeColor = Color.FromArgb(63,216,239), Font = new Font("Segoe UI", 10f, FontStyle.Bold), Bounds = new Rectangle(15, 9, 370, 24) };
					materialsHeader.Controls.Add(headerTitle);
					EnableRubiDrag(materialsHeader);
					EnableRubiDrag(headerTitle);
					Button minimize = new Button { Text = "—", Bounds = new Rectangle(0, 5, 30, 29), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(24,41,62) };
					minimize.Click += (s,e) => WindowState = FormWindowState.Minimized;
					Button close = new Button { Text = "×", Bounds = new Rectangle(0, 5, 30, 29), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(24,41,62) };
					close.Click += (s,e) => Close();
					materialsHeader.Controls.Add(minimize);
					materialsHeader.Controls.Add(close);
					materialsHeader.Resize += (s,e) => { minimize.Left = materialsHeader.Width - 75; close.Left = materialsHeader.Width - 40; };
				}
				materialsHeader.Bounds = new Rectangle(184, 0, ClientSize.Width - 184, 40);
				materialsHeader.Show();
				materialsHeader.BringToFront();
				materialsPage.RefreshCharacters();
				materialsPage.Show();
				materialsPage.BringToFront();
				}
			else
			{
				if (materialsPage != null) materialsPage.Hide();
				if (materialsHeader != null) materialsHeader.Hide();
				ClientSize = eclipseNormalSize;
				foreach (Control c in Controls.Cast<Control>().ToArray())
						if (c != eclipseSidebar && c != materialsPage && c != materialsHeader) c.Visible = true;
			}
			materialsVisible = show;
			if (eclipseSidebar != null) { eclipseSidebar.Height = ClientSize.Height; eclipseSidebar.BringToFront(); }
			if (show && materialsHeader != null) materialsHeader.BringToFront();
		}
		finally { ResumeLayout(true); }
	}

	// Presentation-only shell. Existing control instances and event handlers are preserved.
	private void ApplyEclipseShell()
	{
		const int sidebarWidth = 184;
		SuspendLayout();
		try
		{
			ClientSize = new Size(390 + sidebarWidth, 606);
			BackColor = Color.FromArgb(12, 18, 30);
			foreach (Control control in Controls.Cast<Control>().ToArray())
			{
				// Shift the complete existing operational UI; retain all handlers.
				control.Left += sidebarWidth;
			}
			Panel sidebar = new Panel
			{
				Bounds = new Rectangle(0, 0, sidebarWidth, ClientSize.Height),
				BackColor = Color.FromArgb(15, 26, 43)
			};
			Controls.Add(sidebar);
			eclipseSidebar = sidebar;
			Label brand = new Label
			{
				Text = "◉  ECLIPSEBOT",
				Bounds = new Rectangle(17, 20, 158, 35),
				Font = new Font("Segoe UI", 13f, FontStyle.Bold),
				ForeColor = Color.FromArgb(65, 214, 246),
				BackColor = Color.Transparent
			};
			sidebar.Controls.Add(brand);
			Label subtitle = new Label
			{
				Text = "ECLIPSE ENGINE  ·  x86",
				Bounds = new Rectangle(19, 57, 158, 21),
				Font = new Font("Segoe UI", 7.5f),
				ForeColor = Color.FromArgb(143, 165, 187)
			};
			sidebar.Controls.Add(subtitle);
			AddEclipseNav(sidebar, "Personagens", 103, () => { ToggleMaterialsPage(false); main.Focus(); });
			AddEclipseNav(sidebar, "Atalhos", 148, () => { ToggleMaterialsPage(false); partyKey.Focus(); });
			AddEclipseNav(sidebar, "Funções", 193, () => { ToggleMaterialsPage(false); attackMode.Focus(); });
			AddEclipseNav(sidebar, "Macro Skill", 238, () => { ToggleMaterialsPage(false); buffKey.Focus(); });
			AddEclipseNav(sidebar, "Configurações", 283, () => { ToggleMaterialsPage(false); updateSettingsButton.PerformClick(); });
			Label future = new Label
			{
				Text = "EM DESENVOLVIMENTO",
				Bounds = new Rectangle(19, 363, 155, 22),
				Font = new Font("Segoe UI", 7f, FontStyle.Bold),
				ForeColor = Color.FromArgb(130, 151, 173)
			};
			sidebar.Controls.Add(future);
			AddEclipseNav(sidebar, "Eclipse Materiais", 394, () => ToggleMaterialsPage(true));
			AddEclipseNav(sidebar, "Macro Rota VM", 439, () => MessageBox.Show(this, "Módulo ainda não integrado.", "Macro Rota VM"));
			Label footer = new Label
			{
				Text = "EclipseBot  ·  beta\nMotor original preservado",
				Bounds = new Rectangle(19, 553, 160, 42),
				Font = new Font("Segoe UI", 7.5f),
				ForeColor = Color.FromArgb(143, 165, 187)
			};
			sidebar.Controls.Add(footer);
			sidebar.BringToFront();
		}
		finally { ResumeLayout(false); }
	}

	private static void AddEclipseNav(Panel parent, string title, int y, Action click)
	{
		Button nav = new Button
		{
			Text = title,
			Bounds = new Rectangle(12, y, 161, 36),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(24, 41, 62),
			ForeColor = Color.FromArgb(215, 231, 245),
			Font = new Font("Segoe UI", 9f, FontStyle.Regular),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(10, 0, 0, 0),
			Cursor = Cursors.Hand
		};
		nav.FlatAppearance.BorderColor = Color.FromArgb(39, 74, 105);
		nav.Click += (sender, args) => click();
		parent.Controls.Add(nav);
	}

	private Label RubiLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
	{
		Label label = new Label();
		label.Text = text;
		label.Bounds = new Rectangle(x, y, w, h);
		label.Font = new Font("Segoe UI", size, style);
		label.ForeColor = color;
		label.BackColor = Color.Transparent;
		label.AutoEllipsis = true;
		Label label2 = label;
		Controls.Add(label2);
		return label2;
	}

	private void EnableRubiDrag(Control control)
	{
		control.MouseDown += (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				DragNative.ReleaseCapture();
				DragNative.SendMessage(Handle, 161, new IntPtr(2), IntPtr.Zero);
			}
		};
	}

	public void PaintBehind(PaintEventArgs e)
	{
		using (SolidBrush brush = new SolidBrush(BackColor))
		{
			e.Graphics.FillRectangle(brush, e.ClipRectangle);
		}
		PaintRubi(e.Graphics);
	}

	private void PaintRubi(Graphics g)
	{
		// Paint the original operational panel in its translated area.
		// Without this, cards are painted under the sidebar and over controls.
		GraphicsState eclipsePaintState = g.Save();
		g.TranslateTransform(184f, 0f);
		g.SetClip(new Rectangle(0, 0, 390, ClientSize.Height));
		g.SmoothingMode = SmoothingMode.AntiAlias;
		int num = 390;
		int num2 = ClientSize.Height;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, num, num2), Rubi.Shade(Rubi.Background, 6), Rubi.Shade(Rubi.Background, -8), LinearGradientMode.Vertical))
		{
			g.FillRectangle(brush, 0, 0, num, num2);
		}
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, num, 49), Rubi.Shade(Rubi.Panel, 14), Rubi.Shade(Rubi.Panel, -6), LinearGradientMode.Vertical))
		{
			g.FillRectangle(brush, 1, 1, num - 2, 47);
		}
		using (Pen pen = new Pen(Color.FromArgb(90, 0, 0, 0)))
		{
			g.DrawLine(pen, 1, 48, num - 2, 48);
		}
		using (Pen pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
		{
			g.DrawLine(pen, 1, 49, num - 2, 49);
		}
		Rectangle r = new Rectangle(14, 12, 28, 25);
		Depth.Shadow(g, r, 5, 2);
		Depth.Raised(g, r, 5, Rubi.Shade(Rubi.Accent, 30), Rubi.Shade(Rubi.Accent, -35), Rubi.Shade(Rubi.Accent, -50));
		Card(g, new Rectangle(14, 59, 362, 70));
		using (Pen pen = new Pen(Rubi.Accent, 3f))
		{
			g.DrawLine(pen, 16, 67, 16, 121);
		}
		Card(g, new Rectangle(8, 137, 222, 322));
		Card(g, new Rectangle(233, 137, 149, 268));
		using (Pen pen = new Pen(Color.FromArgb(80, 0, 0, 0)))
		{
			g.DrawLine(pen, 14, 520, 376, 520);
		}
		using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255)))
		{
			g.DrawLine(pen, 14, 521, 376, 521);
		}
		using (Pen pen2 = new Pen(Rubi.Line))
		{
			using GraphicsPath path = Shapes.Round(new Rectangle(0, 0, num - 1, num2 - 1), 12);
			g.DrawPath(pen2, path);
		}
		using Pen topHighlightPen = new Pen(Color.FromArgb(30, 255, 255, 255));
		g.DrawLine(topHighlightPen, 12, 1, num - 13, 1);
		g.Restore(eclipsePaintState);
	}

	private static Color CardTone(int top, int height, int y)
	{
		Color color = Rubi.Shade(Rubi.Panel, 8);
		Color color2 = Rubi.Shade(Rubi.Panel, -5);
		float num = Math.Max(0f, Math.Min(1f, (float)(y - top) / (float)height));
		return Color.FromArgb((int)((float)(int)color.R + (float)(color2.R - color.R) * num), (int)((float)(int)color.G + (float)(color2.G - color.G) * num), (int)((float)(int)color.B + (float)(color2.B - color.B) * num));
	}

	private static void Card(Graphics g, Rectangle r)
	{
		Depth.Shadow(g, r, 7, 3);
		Depth.Raised(g, r, 7, Rubi.Shade(Rubi.Panel, 8), Rubi.Shade(Rubi.Panel, -5), Rubi.Line);
	}

	private void InitUpdates()
	{
		updateTimer = new System.Windows.Forms.Timer
		{
			Interval = 1800000
		};
		updateTimer.Tick += (object s, EventArgs e) =>
		{
			Task task = CheckUpdate(interactive: false);
		};
		Shown += async (object s, EventArgs e) =>
		{
			updateTimer.Start();
			await Task.Delay(3000);
			if (!IsDisposed)
			{
				await CheckUpdate(interactive: false);
			}
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			updateTimer.Dispose();
			if (pendingUpdate != null)
			{
				try
				{
					AppUpdate.LaunchInstaller(pendingUpdate, Application.ExecutablePath);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Atualização não iniciada: " + ex.Message + "\nAbra o aplicativo e tente novamente.", "FarmZilla");
				}
			}
		};
	}

	private bool ConfigureRepository()
	{
		Form dialog = new Form
		{
			Text = "Atualizações do aplicativo",
			ClientSize = new Size(350, 160),
			FormBorderStyle = FormBorderStyle.FixedDialog,
			StartPosition = FormStartPosition.CenterParent,
			MaximizeBox = false,
			MinimizeBox = false
		};
		try
		{
			Label label = new Label();
			label.Text = "Endereço HTTPS do servidor privado de atualizações";
			label.Left = 12;
			label.Top = 12;
			label.Width = 325;
			label.Height = 35;
			Label label2 = label;
			TextBox input = new TextBox
			{
				Left = 12,
				Top = 51,
				Width = 325,
				Text = AppUpdate.Repository
			};
			Button button = new Button();
			button.Text = "Salvar";
			button.Left = 240;
			button.Top = 99;
			button.Width = 97;
			Button button2 = button;
			button2.Click += (object s, EventArgs e) =>
			{
				try
				{
					AppUpdate.Repository = input.Text.Trim();
					dialog.DialogResult = DialogResult.OK;
				}
				catch (Exception ex)
				{
					MessageBox.Show(dialog, ex.Message);
				}
			};
			dialog.Controls.AddRange(new Control[3] { label2, input, button2 });
			dialog.AcceptButton = button2;
			return dialog.ShowDialog(this) == DialogResult.OK;
		}
		finally
		{
			if (dialog != null)
			{
				((IDisposable)dialog).Dispose();
			}
		}
	}

	private async Task CheckUpdate(bool interactive)
	{
		if (updateChecking || closeInProgress)
		{
			return;
		}
		string repository = AppUpdate.Repository;
		if (repository == "")
		{
			updateSummary = "Atualizações privadas ainda não configuradas";
			if (!interactive || !ConfigureRepository())
			{
				return;
			}
			repository = AppUpdate.Repository;
		}
		updateChecking = true;
		try
		{
			updateSummary = "Consultando versão…";
			string envelope = await Task.Run(() => Encoding.UTF8.GetString(AppUpdate.Download(AppUpdate.ManifestUrl(repository), 65536)));
			UpdateManifest manifest = AppUpdate.Verify(envelope, repository, "<RSAKeyValue><Modulus>wsBKXnprkMV7G0sTgnnrnOj3zWnCPpoVmq20PyrVZOlpfvM/N3wDMM/ym/RiLJv4xhVn5BPvUv6khxv1n6MfM56REJQA+GW7HDAZPThfHWV9qeHvYHOwmBiPHSIw5kBtEq6cU0eNT3buJfYHadVtCKmvKnt6Mb1AwTxxT2VfJ91gT23RbxwlnunKTf/F7Ot9KAFUt17sW++c9wttLJBhNWNhC7qQPKHOiIO4/2t9F6oG8rGa0JLz6kcfwKC+jW+acXmQp/gr36RmRGyHaPhoUZutlqnOc0Optu3ZGfpUcp63OCBJSGTIIWO7m5bi6x5J/oZWRwaICJAhWHykKO93KQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
			if (IsDisposed || closeInProgress)
			{
				return;
			}
			if (new Version(manifest.version) <= new Version("0.19.19"))
			{
				updateSummary = "App atualizado · 0.19.19";
				if (interactive)
				{
					MessageBox.Show(this, "Você já está usando a versão mais recente.", "FarmZilla");
				}
				return;
			}
			updateSummary = "Disponível: " + manifest.version;
			updateSettingsButton.Text = "Atualização disponível";
			Note("Nova versão disponível: " + manifest.version + ". Abra Configurações > Atualizar aplicativo.");
			if (!interactive && (busy || teleport.Working || missions.Working || followers.CheckedItems.Count > 0))
			{
				return;
			}
			if (busy || teleport.Working || missions.Working)
			{
				MessageBox.Show(this, "Aguarde a operação atual terminar e clique em Atualizar aplicativo novamente.", "FarmZilla");
			}
			else if (!interactive || MessageBox.Show(this, "Instalar a versão " + manifest.version + " e reiniciar o aplicativo?\nAs teclas salvas serão mantidas. Selecione novamente os personagens após reiniciar.", "Atualizar FarmZilla", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
			{
				updateSummary = "Baixando e conferindo…";
				string request = await Task.Run(() => AppUpdate.Stage(repository, envelope, manifest, Application.ExecutablePath));
				if (!IsDisposed && !closeInProgress && (interactive || (!busy && !teleport.Working && !missions.Working && followers.CheckedItems.Count <= 0)))
				{
					pendingUpdate = request;
					Note("Atualização verificada. Encerrando capturas antes de reiniciar.");
					Close();
				}
			}
		}
		catch (Exception ex)
		{
			if (!IsDisposed)
			{
				updateSummary = "Não foi possível consultar";
				Note("Atualizações: " + ex.Message);
				if (interactive)
				{
					MessageBox.Show(this, "Atualização não concluída: " + ex.Message, "FarmZilla");
				}
			}
		}
		finally
		{
			updateChecking = false;
		}
	}
}
