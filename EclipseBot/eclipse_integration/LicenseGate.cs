using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class LicenseGate
{
	internal static string CurrentToken;

	internal static OnlineIdentity Identity;

	internal static string ServiceUrl;

	internal static bool Ensure()
	{
		string reason = null;
		try
		{
			Identity = OnlineIdentity.Load();
			ServiceUrl = OnlineApi.Service;
			if (!string.IsNullOrEmpty(ServiceUrl))
			{
				AccessResult accessResult = OnlineLicense.Evaluate(Identity, ServiceUrl, () => OnlineApi.Acquire(Identity, ServiceUrl));
				if (accessResult.Allowed)
				{
					CurrentToken = accessResult.Token;
					AuthorizationSession.Begin(accessResult.Token);
					return true;
				}
				reason = accessResult.Reason;
			}
		}
		catch (Exception ex)
		{
			reason = ex.Message;
		}
		Form dialog = new Form
		{
			Text = "FarmZilla · autorização do computador",
			ClientSize = new Size(590, 370),
			FormBorderStyle = FormBorderStyle.FixedDialog,
			StartPosition = FormStartPosition.CenterScreen,
			MaximizeBox = false,
			MinimizeBox = false,
			BackColor = Color.FromArgb(12, 16, 19),
			ForeColor = Color.White,
			Font = new Font("Segoe UI", 9f)
		};
		try
		{
			dialog.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			Label label = new Label();
			label.Text = "Liberação pelo gerenciador FarmZilla";
			label.Left = 20;
			label.Top = 17;
			label.Width = 550;
			label.Height = 28;
			label.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
			Label label2 = label;
			Label label3 = new Label();
			label3.Text = "Copie o código do PC e envie ao administrador.\nDepois de ele liberar, clique em Verificar liberação.";
			label3.Left = 20;
			label3.Top = 56;
			label3.Width = 550;
			label3.Height = 43;
			Label label4 = label3;
			TextBox code = new TextBox
			{
				Left = 20,
				Top = 108,
				Width = 420,
				Height = 48,
				ReadOnly = true,
				Multiline = true,
				Text = "Identificando hardware…",
				AccessibleName = "Código do PC"
			};
			Button copy = new Button
			{
				Text = "Copiar código",
				Left = 450,
				Top = 108,
				Width = 120,
				Height = 48,
				Enabled = false
			};
			copy.Click += (object s, EventArgs e) =>
			{
				try
				{
					Clipboard.SetText(code.Text);
				}
				catch
				{
				}
			};
			Label label5 = new Label();
			label5.Text = "Servidor de ativação fornecido pelo administrador";
			label5.Left = 20;
			label5.Top = 174;
			label5.Width = 550;
			label5.Height = 23;
			Label label6 = label5;
			TextBox service = new TextBox
			{
				Left = 20,
				Top = 199,
				Width = 550,
				Text = OnlineApi.Service,
				AccessibleName = "Servidor de ativação"
			};
			Label status = new Label
			{
				Left = 20,
				Top = 237,
				Width = 550,
				Height = 53,
				ForeColor = Color.Cyan,
				Text = (reason ?? "Aguarde a identificação do computador.")
			};
			Button check = new Button
			{
				Text = "Verificar liberação",
				Left = 380,
				Top = 316,
				Width = 190,
				Height = 34,
				Enabled = false
			};
			Label label7 = new Label();
			label7.Text = "Consulta online. Até 24 horas offline após a última autorização.";
			label7.Left = 20;
			label7.Top = 315;
			label7.Width = 340;
			label7.Height = 40;
			label7.Font = new Font("Segoe UI", 8f);
			Label label8 = label7;
			bool checking = false;
			Func<Task> verify = async () =>
			{
				if (checking || Identity == null)
				{
					return;
				}
				checking = true;
				check.Enabled = false;
				try
				{
					OnlineApi.Service = service.Text.Trim();
					ServiceUrl = OnlineApi.Service;
					status.Text = "Consultando autorização…";
					AccessResult access = await Task.Run(() => OnlineLicense.Evaluate(Identity, ServiceUrl, () => OnlineApi.Acquire(Identity, ServiceUrl)));
					if (!dialog.IsDisposed)
					{
						status.Text = access.Reason;
						if (access.Allowed)
						{
							CurrentToken = access.Token;
							AuthorizationSession.Begin(access.Token);
							dialog.DialogResult = DialogResult.OK;
						}
					}
				}
				catch (Exception ex2)
				{
					status.Text = ex2.Message;
				}
				finally
				{
					checking = false;
					if (!dialog.IsDisposed)
					{
						check.Enabled = true;
					}
				}
			};
			check.Click += async (object s, EventArgs e) =>
			{
				await verify();
			};
			dialog.Shown += async (object s, EventArgs e) =>
			{
				try
				{
					Identity = await Task.Run(() => OnlineIdentity.Load());
					if (!dialog.IsDisposed)
					{
						code.Text = Identity.Code;
						Button button = copy;
						bool enabled = (check.Enabled = true);
						button.Enabled = enabled;
						status.Text = reason ?? "Envie este código ao administrador para liberar este PC.";
					}
				}
				catch (Exception ex2)
				{
					status.Text = ex2.Message;
					code.Text = "Identificação indisponível";
				}
			};
			dialog.Controls.AddRange(new Control[9] { label2, label4, code, copy, label6, service, status, check, label8 });
			return dialog.ShowDialog() == DialogResult.OK;
		}
		finally
		{
			if (dialog != null)
			{
				((IDisposable)dialog).Dispose();
			}
		}
	}
}
