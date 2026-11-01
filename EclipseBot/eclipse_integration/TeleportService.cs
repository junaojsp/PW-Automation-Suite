using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class TeleportService
{
	private sealed class Result
	{
		public int Pid;

		public bool Blocked;

		public bool Confirmed;

		public string Message;
	}

	private readonly ConcurrentDictionary<int, byte> activeClients = new ConcurrentDictionary<int, byte>();

	private readonly ConcurrentDictionary<int, byte> unsafeClients = new ConcurrentDictionary<int, byte>();

	private readonly Action<string> note;

	private readonly Action<string> status;

	private readonly HashSet<int> blocked;

	private TeleportCapture capture;

	private string selection = "";

	private string fault = "";

	private int capturePid;

	private uint captureId;

	private bool pumping;

	private bool stopping;

	private int revision;

	private CancellationTokenSource cancel = new CancellationTokenSource();

	private Task job = Task.FromResult(0);

	private byte[] pending;

	public Action<byte[]> OnNpcCommand;

	public Func<int, bool> ShouldMirror;

	public Func<bool> QuestJobRunning;

	private readonly Queue<QuestChoice> questChoices = new Queue<QuestChoice>();

	private HashSet<ushort> questSnapshot;

	public Action OnTeleportCaptured;

	public Func<Task> BeforeTeleport;

	public Func<bool> QuestQueueBusy;

	private SpecialRequest pendingSpecial;

	private SpecialRequest specialUse;

	private DateTime specialUseTime;

	private readonly Dictionary<string, DateTime> lastCommandLog = new Dictionary<string, DateTime>();

	private DateTime commandMinute;

	private int commandLines;

	private readonly Dictionary<string, DateTime> lastItemLog = new Dictionary<string, DateTime>();

	public long CapturePollVersion { get; private set; }

	public bool Working => !job.IsCompleted || pending != null || pendingSpecial != null || pumping;

	public bool QuestChoicePending => questChoices.Count > 0;

	public bool IsClientBusy(int pid)
	{
		return pumping || pending != null || pendingSpecial != null || activeClients.ContainsKey(pid) || unsafeClients.ContainsKey(pid);
	}

	public TeleportService(Action<string> log, Action<string> label, HashSet<int> blockedClients)
	{
		note = log;
		status = label;
		blocked = blockedClients;
	}

	public void SelectionChanged()
	{
		revision++;
		cancel.Cancel();
		pending = null;
		pendingSpecial = (specialUse = null);
		questChoices.Clear();
		questSnapshot = null;
	}

	public async Task Pump(CharacterRef main, CharacterRef[] secondaries, bool followBusy)
	{
		if (pumping || stopping)
		{
			return;
		}
		pumping = true;
		int observedRevision = revision;
		try
		{
			string key = ((main == null) ? "" : (main.Key + "/" + string.Join(",", (from s in secondaries
				select s.Key into s
				orderby s
				select s).ToArray())));
			if (key != selection)
			{
				selection = key;
				fault = "";
				cancel.Cancel();
				pending = null;
				pendingSpecial = (specialUse = null);
				questChoices.Clear();
				questSnapshot = null;
				if (capture != null)
				{
					capture.Poll();
				}
			}
			bool wanted = main != null && secondaries.Length > 0;
			if (capture != null && (!wanted || capturePid != main.Pid || captureId != main.Id))
			{
				await job;
				TeleportCapture old = capture;
				await Task.Run(() =>
				{
					old.Dispose();
				});
				capture = null;
				note("Captura automática removida.");
			}
			if (wanted && fault == selection)
			{
				status("Teleporte indisponível\nConsulte o registro");
				return;
			}
			if (!wanted)
			{
				status("Teleporte e missões\nSelecione os secundários");
				return;
			}
			if (capture == null)
			{
				if (fault == selection)
				{
					status("Teleporte indisponível\nConsulte o registro");
					return;
				}
				if (followBusy)
				{
					return;
				}
				try
				{
					using (CharacterMemory characterMemory = new CharacterMemory(main.Pid))
					{
						if (characterMemory.Id != main.Id)
						{
							throw new Exception("O personagem principal mudou. Atualize a lista.");
						}
					}
					bool all = DiagnosticAll();
					capture = await Task.Run(() => new TeleportCapture(main.Pid, 0u, all));
					if (all)
					{
						note("Diagnóstico ampliado ativo: tipos de comando do principal serão registrados (arquivo diagnostico-comandos.txt).");
					}
					capturePid = main.Pid;
					captureId = main.Id;
					if (capture.RecoveredOrphan)
					{
						note("Captura deixada por uma execução anterior do FarmZilla foi removida do principal.");
					}
					capture.Poll();
					note("Teleporte e missões ativos: principal " + main.Name + ".");
				}
				catch (Exception ex)
				{
					fault = selection;
					note("Captura não iniciada: " + ex.Message);
					status("Teleporte indisponível\nConsulte o registro");
					return;
				}
			}
			if (observedRevision != revision)
			{
				return;
			}
			List<TeleportCapture.CapturedCommand> events = capture.Poll();
			CapturePollVersion++;
			foreach (TeleportCapture.CapturedCommand item in events)
			{
				if (item.FullLength != item.Packet.Length)
				{
					DiagnoseCommand(item);
				}
				else if (IsStone(item.Packet))
				{
					pending = item.Packet;
					cancel.Cancel();
					if (OnTeleportCaptured != null)
					{
						OnTeleportCaptured();
					}
				}
				else if (IsSpecialUse(item.Packet))
				{
					RememberSpecialUse(main, item.Packet);
				}
				else if (IsSpecialPoint(item.Packet))
				{
					if (specialUse != null && (DateTime.UtcNow - specialUseTime).TotalSeconds <= 120.0)
					{
						specialUse.Point = item.Packet;
						pendingSpecial = specialUse;
						specialUse = null;
						cancel.Cancel();
						if (OnTeleportCaptured != null)
						{
							OnTeleportCaptured();
						}
					}
					else
					{
						note("Teleporte Especial: ponto escolhido sem uso do item registrado; não espelhado.");
					}
				}
				else if (IsQuestChoice(item.Packet))
				{
					ushort num = BitConverter.ToUInt16(item.Packet, 7);
					if (questSnapshot == null)
					{
						note("Diagnóstico: escolha em janela (0x31) " + BitConverter.ToString(item.Packet) + " sem lista de missões do principal; não espelhada.");
					}
					else if (questChoices.Count < 4)
					{
						questChoices.Enqueue(new QuestChoice
						{
							Packet = item.Packet,
							Quest = num,
							Kind = item.Packet[6],
							WasActive = questSnapshot.Contains(num),
							Before = new HashSet<ushort>(questSnapshot)
						});
					}
				}
				else if (IsDialogChoice(item.Packet))
				{
					note("Diagnóstico: escolha em janela no principal (0x31): " + BitConverter.ToString(item.Packet) + ". Nada foi espelhado.");
				}
				else if (IsItemUse(item.Packet))
				{
					DiagnoseItemUse(main, item.Packet);
				}
				else if (IsNpcOrQuest(item.Packet))
				{
					if (OnNpcCommand != null)
					{
						OnNpcCommand(item.Packet);
					}
				}
				else
				{
					DiagnoseCommand(item);
				}
			}
			RefreshQuestSnapshot(main);
			status("Teleporte e missões\nAtivo · " + secondaries.Length + " secundário(s)");
			if (pending != null && !followBusy)
			{
				byte[] packet = pending;
				pending = null;
				CharacterRef[] recipients = Recipients(secondaries);
				if (recipients.Length != 0)
				{
					await job;
					if (BeforeTeleport != null)
					{
						await BeforeTeleport();
					}
					if (observedRevision == revision && !stopping)
					{
						cancel.Dispose();
						cancel = new CancellationTokenSource();
						job = RunBatch(token: cancel.Token, main: main, recipients: recipients, packet: packet);
					}
				}
			}
			else if (pendingSpecial != null && !followBusy)
			{
				SpecialRequest request = pendingSpecial;
				pendingSpecial = null;
				CharacterRef[] recipients2 = Recipients(secondaries);
				if (recipients2.Length != 0)
				{
					await job;
					if (BeforeTeleport != null)
					{
						await BeforeTeleport();
					}
					if (observedRevision == revision && !stopping)
					{
						cancel.Dispose();
						cancel = new CancellationTokenSource();
						job = RunSpecialBatch(token: cancel.Token, main: main, recipients: recipients2, request: request);
					}
				}
			}
			else
			{
				if (questChoices.Count <= 0 || followBusy || (QuestQueueBusy != null && QuestQueueBusy()))
				{
					return;
				}
				QuestChoice choice = questChoices.Dequeue();
				CharacterRef[] recipients3 = secondaries.Where((CharacterRef s) => !blocked.Contains(s.Pid)).ToArray();
				if (recipients3.Length != 0)
				{
					await job;
					if (observedRevision == revision && !stopping)
					{
						cancel.Dispose();
						cancel = new CancellationTokenSource();
						job = RunQuestChoice(token: cancel.Token, main: main, recipients: recipients3, choice: choice);
					}
				}
			}
		}
		catch (Exception ex)
		{
			fault = selection;
			note("Teleporte: " + ex.Message);
			status("Teleporte interrompido\nConsulte o registro");
			cancel.Cancel();
			if (capture != null)
			{
				TeleportCapture teleportCapture = capture;
				try
				{
					teleportCapture.Dispose();
					capture = null;
					return;
				}
				catch (Exception ex2)
				{
					note("Reinicie o cliente principal: restauração não confirmada. " + ex2.Message);
					return;
				}
			}
		}
		finally
		{
			pumping = false;
		}
	}

	private CharacterRef[] Recipients(CharacterRef[] secondaries)
	{
		CharacterRef[] array = secondaries.Where((CharacterRef s) => !blocked.Contains(s.Pid)).ToArray();
		if (ShouldMirror == null)
		{
			return array;
		}
		CharacterRef[] array2 = array.Where((CharacterRef s) => !ShouldMirror(s.Pid)).ToArray();
		CharacterRef[] array3 = array.Where((CharacterRef s) => ShouldMirror(s.Pid)).ToArray();
		if (array2.Length > 0)
		{
			note((array3.Length == 0) ? "Teleporte não espelhado: Seguir desativado nos secundários." : ("Teleporte: " + string.Join(", ", array2.Select((CharacterRef s) => s.Name).ToArray()) + " ficou (Seguir desativado)."));
		}
		return array3;
	}

	private static bool IsQuestChoice(byte[] p)
	{
		return p.Length == 9 && BitConverter.ToUInt16(p, 0) == 49 && BitConverter.ToUInt32(p, 2) == 3 && (p[6] == 3 || p[6] == 4);
	}

	private void RefreshQuestSnapshot(CharacterRef main)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(main.Pid);
			if (characterMemory.Id == main.Id)
			{
				questSnapshot = new HashSet<ushort>(characterMemory.ActiveQuestIds());
			}
		}
		catch
		{
		}
	}

	private static bool HasQuest(CharacterRef c, ushort quest)
	{
		using CharacterMemory characterMemory = new CharacterMemory(c.Pid);
		if (characterMemory.Id != c.Id)
		{
			throw new InvalidOperationException("O personagem mudou.");
		}
		return characterMemory.ActiveQuestIds().Contains(quest);
	}

	private static bool WaitQuest(CharacterRef c, ushort quest, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				if (HasQuest(c, quest))
				{
					return true;
				}
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return false;
	}

	private async Task RunQuestChoice(CharacterRef main, CharacterRef[] recipients, QuestChoice choice, CancellationToken token)
	{
		CharacterRef[] array = recipients;
		foreach (CharacterRef characterRef in array)
		{
			activeClients[characterRef.Pid] = 0;
		}
		if (true)
		{
		}
		try
		{
			string bytes = BitConverter.ToString(choice.Packet);
			if (!choice.WasActive)
			{
				if (!(await Task.Run(() => WaitQuest(main, choice.Quest, 4000, token))))
				{
					note("Missão " + choice.Quest + ": escolha na janela do principal (" + bytes + ") não virou missão ativa; não espelhada.");
					return;
				}
			}
			else
			{
				ushort next = await Task.Run(() => WaitAdvance(main, choice, 4000, token));
				if (next == 0)
				{
					note("Missão " + choice.Quest + ": escolha na janela do principal (" + bytes + ") não avançou para outra etapa; não espelhada (pode ser abandono).");
					return;
				}
				choice.Next = next;
			}
			Stopwatch clock = Stopwatch.StartNew();
			while (QuestJobRunning != null && QuestJobRunning() && clock.ElapsedMilliseconds < 20000)
			{
				await Task.Delay(250, token);
			}
			note(choice.WasActive ? ("Missão " + choice.Quest + ": etapa concluída pela janela no principal (seguinte " + choice.Next + ", " + bytes + "); espelhando nos secundários.") : ("Missão " + choice.Quest + ": aceita pela janela automática no principal (" + bytes + "); espelhando nos secundários."));
			Progress<string> progress = new Progress<string>((string s) =>
			{
				note(s);
			});
			Result[] results = await Task.WhenAll(recipients.Select((CharacterRef s) => Task.Run(() =>
			{
				try
				{
					Result result2 = MirrorQuestChoice(s, choice, token, progress);
					if (result2.Blocked)
					{
						unsafeClients[s.Pid] = 0;
					}
					return result2;
				}
				finally
				{
					activeClients.TryRemove(s.Pid, out var _);
				}
			})));
			int confirmed = 0;
			Result[] array2 = results;
			foreach (Result result in array2)
			{
				if (result.Blocked)
				{
					blocked.Add(result.Pid);
				}
				if (result.Confirmed)
				{
					confirmed++;
				}
				note(result.Message);
			}
			if (true)
			{
			}
			note("CHECK missão " + choice.Quest + " (janela automática): " + confirmed + "/" + results.Length + " secundário(s) confirmados.");
		}
		catch (OperationCanceledException)
		{
			note("Missão " + choice.Quest + ": espelhamento da janela automática cancelado.");
		}
		catch (Exception ex2)
		{
			note("Missão " + choice.Quest + ": " + ex2.Message);
		}
		finally
		{
			array = recipients;
			foreach (CharacterRef characterRef in array)
			{
				activeClients.TryRemove(characterRef.Pid, out var _);
			}
			if (1 == 0)
			{
			}
		}
	}

	private static ushort WaitAdvance(CharacterRef c, QuestChoice choice, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				using CharacterMemory characterMemory = new CharacterMemory(c.Pid);
				if (characterMemory.Id != c.Id)
				{
					throw new InvalidOperationException("O personagem mudou.");
				}
				ushort[] source = characterMemory.ActiveQuestIds();
				if (!source.Contains(choice.Quest))
				{
					ushort[] array = source.Where((ushort q) => !choice.Before.Contains(q)).ToArray();
					if (array.Length > 0)
					{
						return array[0];
					}
				}
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return 0;
	}

	private static bool WaitGone(CharacterRef c, ushort quest, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				if (!HasQuest(c, quest))
				{
					return true;
				}
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return false;
	}

	private static void CloseQuestWindow(CharacterRef character, Result result, CancellationToken token)
	{
		string text = QuestService.CloseWindows(character, token, out var flag);
		if (flag)
		{
			result.Blocked = true;
		}
		if (text != null)
		{
			result.Message = result.Message + " Não foi possível fechar a janela: " + text;
		}
		else
		{
			result.Message += " Janela fechada.";
		}
	}

	private static Result MirrorQuestAdvance(CharacterRef character, QuestChoice choice, CancellationToken token, IProgress<string> progress)
	{
		Result result = new Result();
		result.Pid = character.Pid;
		Result result2 = result;
		try
		{
			if (!HasQuest(character, choice.Quest))
			{
				result2.Confirmed = HasQuest(character, choice.Next);
				result2.Message = character.Name + (result2.Confirmed ? (": já está na etapa " + choice.Next + ".") : (": não tem a missão " + choice.Quest + "; nada enviado."));
				if (result2.Confirmed)
				{
					CloseQuestWindow(character, result2, token);
				}
				return result2;
			}
			for (int i = 1; i <= 3; i++)
			{
				token.ThrowIfCancellationRequested();
				progress.Report(character.Name + ": missão " + choice.Quest + " (janela automática, avançar etapa) · tentativa " + i + "/3.");
				TeleportBridge teleportBridge = null;
				try
				{
					teleportBridge = new TeleportBridge(character.Pid);
					if (teleportBridge.CharacterId != character.Id)
					{
						throw new Exception("Personagem mudou durante a operação.");
					}
					teleportBridge.SendQuestChoice(choice.Packet);
				}
				finally
				{
					if (teleportBridge != null)
					{
						try
						{
							teleportBridge.Dispose();
						}
						finally
						{
							if (!teleportBridge.RestoreSucceeded)
							{
								result2.Blocked = true;
							}
						}
					}
				}
				if (result2.Blocked)
				{
					throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
				}
				if (WaitGone(character, choice.Quest, 3000, token))
				{
					result2.Confirmed = true;
					result2.Message = character.Name + ": missão " + choice.Quest + " concluída pela janela" + (HasQuest(character, choice.Next) ? ("; etapa " + choice.Next + " ativa.") : ".");
					CloseQuestWindow(character, result2, token);
					return result2;
				}
				if (i < 3 && token.WaitHandle.WaitOne(1000))
				{
					token.ThrowIfCancellationRequested();
				}
			}
			result2.Message = character.Name + ": missão " + choice.Quest + " não avançou após 3 tentativas.";
		}
		catch (OperationCanceledException)
		{
			result2.Message = character.Name + ": tentativa pendente cancelada.";
		}
		catch (Exception ex2)
		{
			result2.Message = character.Name + ": " + ex2.Message;
		}
		return result2;
	}

	private static Result MirrorQuestChoice(CharacterRef character, QuestChoice choice, CancellationToken token, IProgress<string> progress)
	{
		Result result = new Result();
		result.Pid = character.Pid;
		Result result2 = result;
		try
		{
			if (choice.WasActive)
			{
				return MirrorQuestAdvance(character, choice, token, progress);
			}
			for (int i = 1; i <= 3; i++)
			{
				token.ThrowIfCancellationRequested();
				if (HasQuest(character, choice.Quest))
				{
					result2.Confirmed = true;
					result2.Message = character.Name + ": missão " + choice.Quest + ((i == 1) ? " já estava ativa." : " aceita e confirmada.");
					CloseQuestWindow(character, result2, token);
					return result2;
				}
				progress.Report(character.Name + ": missão " + choice.Quest + " (janela automática) · tentativa " + i + "/3.");
				TeleportBridge teleportBridge = null;
				try
				{
					teleportBridge = new TeleportBridge(character.Pid);
					if (teleportBridge.CharacterId != character.Id)
					{
						throw new Exception("Personagem mudou durante a operação.");
					}
					teleportBridge.SendQuestChoice(choice.Packet);
				}
				finally
				{
					if (teleportBridge != null)
					{
						try
						{
							teleportBridge.Dispose();
						}
						finally
						{
							if (!teleportBridge.RestoreSucceeded)
							{
								result2.Blocked = true;
							}
						}
					}
				}
				if (result2.Blocked)
				{
					throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
				}
				if (WaitQuest(character, choice.Quest, 3000, token))
				{
					result2.Confirmed = true;
					result2.Message = character.Name + ": missão " + choice.Quest + " aceita e confirmada.";
					CloseQuestWindow(character, result2, token);
					return result2;
				}
				if (i < 3 && token.WaitHandle.WaitOne(1000))
				{
					token.ThrowIfCancellationRequested();
				}
			}
			result2.Message = character.Name + ": missão " + choice.Quest + " não confirmada após 3 tentativas (pode faltar requisito ou a missão anterior).";
		}
		catch (OperationCanceledException)
		{
			result2.Message = character.Name + ": tentativa pendente cancelada.";
		}
		catch (Exception ex2)
		{
			result2.Message = character.Name + ": " + ex2.Message;
		}
		return result2;
	}

	private static bool IsSpecialUse(byte[] p)
	{
		return p.Length == 10 && BitConverter.ToUInt32(p, 0) == 16777256 && BitConverter.ToUInt32(p, 6) == 52256;
	}

	private static bool IsSpecialPoint(byte[] p)
	{
		return p.Length == 9 && BitConverter.ToUInt16(p, 0) == 49 && BitConverter.ToUInt32(p, 2) == 3 && p[6] == 1;
	}

	private static bool IsDialogChoice(byte[] p)
	{
		return p.Length >= 2 && BitConverter.ToUInt16(p, 0) == 49;
	}

	private void RememberSpecialUse(CharacterRef main, byte[] packet)
	{
		SpecialRequest specialRequest = new SpecialRequest();
		specialRequest.Use = packet;
		SpecialRequest specialRequest2 = specialRequest;
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(main.Pid);
			if (characterMemory.Id == main.Id)
			{
				specialRequest2.HasStart = true;
				specialRequest2.X = characterMemory.X;
				specialRequest2.Y = characterMemory.Y;
				specialRequest2.Z = characterMemory.Z;
			}
		}
		catch
		{
		}
		specialUse = specialRequest2;
		specialUseTime = DateTime.UtcNow;
	}

	private async Task RunSpecialBatch(CharacterRef main, CharacterRef[] recipients, SpecialRequest request, CancellationToken token)
	{
		CharacterRef[] array = recipients;
		foreach (CharacterRef characterRef in array)
		{
			activeClients[characterRef.Pid] = 0;
		}
		if (true)
		{
		}
		try
		{
			Destination destination = await Task.Run(() => WaitMoved(main, request, 12000, token));
			if (destination == null)
			{
				note("Teleporte Especial: principal não confirmou chegada ao ponto " + request.PointId + "; secundários não receberam comando.");
				return;
			}
			note("Teleporte Especial: principal chegou ao ponto " + request.PointId + ".");
			Progress<string> progress = new Progress<string>((string s) =>
			{
				note(s);
			});
			Result[] array2 = await Task.WhenAll(recipients.Select((CharacterRef s) => Task.Run(() =>
			{
				try
				{
					Result result2 = MirrorSpecial(s, destination, request, token, progress);
					if (result2.Blocked)
					{
						unsafeClients[s.Pid] = 0;
					}
					return result2;
				}
				finally
				{
					activeClients.TryRemove(s.Pid, out var _);
				}
			})));
			foreach (Result result in array2)
			{
				if (result.Blocked)
				{
					blocked.Add(result.Pid);
				}
				note(result.Message);
			}
			if (1 == 0)
			{
			}
		}
		catch (OperationCanceledException)
		{
			note("Teleporte Especial: tentativas anteriores canceladas por mudança de seleção ou destino.");
		}
		catch (Exception ex2)
		{
			note("Teleporte Especial: " + ex2.Message);
		}
		finally
		{
			array = recipients;
			foreach (CharacterRef characterRef in array)
			{
				activeClients.TryRemove(characterRef.Pid, out var _);
			}
			if (1 == 0)
			{
			}
		}
	}

	private static Destination WaitMoved(CharacterRef character, SpecialRequest request, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		float num = float.NaN;
		float num2 = 0f;
		float num3 = 0f;
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				using CharacterMemory characterMemory = new CharacterMemory(character.Pid);
				if (characterMemory.Id != character.Id)
				{
					throw new InvalidOperationException("O personagem mudou.");
				}
				bool flag = !request.HasStart || Math.Abs(characterMemory.X - request.X) > 30f || Math.Abs(characterMemory.Z - request.Z) > 30f;
				bool flag2 = !float.IsNaN(num) && Math.Abs(characterMemory.X - num) < 1f && Math.Abs(characterMemory.Z - num3) < 1f && Math.Abs(characterMemory.Y - num2) < 2f;
				if (flag && flag2)
				{
					Destination destination = new Destination();
					destination.Id = request.PointId;
					destination.Name = "ponto " + request.PointId;
					destination.X = characterMemory.X;
					destination.Y = characterMemory.Y;
					destination.Z = characterMemory.Z;
					return destination;
				}
				num = characterMemory.X;
				num2 = characterMemory.Y;
				num3 = characterMemory.Z;
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return null;
	}

	private static bool Near(CharacterMemory s, Destination d)
	{
		return Math.Abs(s.X - d.X) < 20f && Math.Abs(s.Z - d.Z) < 20f && Math.Abs(s.Y - d.Y) < 30f;
	}

	private static bool WaitNear(CharacterRef character, Destination destination, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				using CharacterMemory characterMemory = new CharacterMemory(character.Pid);
				if (characterMemory.Id != character.Id)
				{
					throw new InvalidOperationException("O personagem mudou.");
				}
				if (Near(characterMemory, destination))
				{
					return true;
				}
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return false;
	}

	private static Result MirrorSpecial(CharacterRef character, Destination destination, SpecialRequest request, CancellationToken token, IProgress<string> progress)
	{
		Result result = new Result();
		result.Pid = character.Pid;
		Result result2 = result;
		try
		{
			int num = -1;
			for (int i = 1; i <= 3; i++)
			{
				token.ThrowIfCancellationRequested();
				ushort num2 = 0;
				bool flag = false;
				string text = null;
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (true)
				{
					bool flag2 = true;
					using (CharacterMemory characterMemory = new CharacterMemory(character.Pid))
					{
						if (characterMemory.Id != character.Id)
						{
							throw new Exception("Personagem mudou; atualize a lista.");
						}
						if (Near(characterMemory, destination))
						{
							result2.Message = character.Name + ": chegada confirmada ao " + destination.Name + ".";
							return result2;
						}
						try
						{
							num2 = characterMemory.FindSpecialTeleport();
							flag = true;
						}
						catch (Exception)
						{
							if (num >= 0)
							{
								text = characterMemory.DescribeSlot(num);
							}
						}
					}
					if (flag)
					{
						break;
					}
					if (stopwatch.ElapsedMilliseconds >= 5000)
					{
						throw new Exception("Teleporte Especial indisponível no inventário" + ((text != null) ? (" (slot " + num + ": " + text + ")") : "") + ".");
					}
					if (token.WaitHandle.WaitOne(250))
					{
						token.ThrowIfCancellationRequested();
					}
				}
				num = num2;
				progress.Report(character.Name + ": Teleporte Especial para o " + destination.Name + " · tentativa " + i + "/3.");
				byte[] array = (byte[])request.Use.Clone();
				Buffer.BlockCopy(BitConverter.GetBytes(num2), 0, array, 4, 2);
				TeleportBridge teleportBridge = null;
				try
				{
					teleportBridge = new TeleportBridge(character.Pid);
					if (teleportBridge.CharacterId != character.Id)
					{
						throw new Exception("Personagem mudou durante a operação.");
					}
					teleportBridge.SendSpecial(array);
					if (token.WaitHandle.WaitOne(1500))
					{
						token.ThrowIfCancellationRequested();
					}
					teleportBridge.SendSpecial(request.Point);
				}
				finally
				{
					if (teleportBridge != null)
					{
						try
						{
							teleportBridge.Dispose();
						}
						finally
						{
							if (!teleportBridge.RestoreSucceeded)
							{
								result2.Blocked = true;
							}
						}
					}
				}
				if (result2.Blocked)
				{
					throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
				}
				if (WaitNear(character, destination, 5000, token))
				{
					result2.Message = character.Name + ": chegou ao " + destination.Name + ".";
					return result2;
				}
				if (i < 3 && token.WaitHandle.WaitOne(500))
				{
					token.ThrowIfCancellationRequested();
				}
			}
			result2.Message = character.Name + ": não chegou ao " + destination.Name + " após 3 tentativas; confira se o ponto está liberado para esse personagem.";
		}
		catch (OperationCanceledException)
		{
			result2.Message = character.Name + ": tentativa pendente cancelada.";
		}
		catch (Exception ex3)
		{
			result2.Message = character.Name + ": " + ex3.Message;
		}
		return result2;
	}

	private static bool IsNpcOrQuest(byte[] p)
	{
		return (p.Length == 6 && BitConverter.ToUInt16(p, 0) == 35) || ((p.Length == 18 || p.Length == 22) && BitConverter.ToUInt16(p, 0) == 37);
	}

	private static bool DiagnosticAll()
	{
		try
		{
			return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno", "diagnostico-comandos.txt"));
		}
		catch
		{
			return false;
		}
	}

	private void DiagnoseCommand(TeleportCapture.CapturedCommand c)
	{
		if (c.Packet.Length < 2)
		{
			return;
		}
		ushort num = BitConverter.ToUInt16(c.Packet, 0);
		string key = num + "/" + c.FullLength;
		if (!lastCommandLog.TryGetValue(key, out var value) || !((DateTime.UtcNow - value).TotalSeconds < 30.0))
		{
			if ((DateTime.UtcNow - commandMinute).TotalSeconds >= 60.0)
			{
				commandMinute = DateTime.UtcNow;
				commandLines = 0;
			}
			if (++commandLines <= 40)
			{
				lastCommandLog[key] = DateTime.UtcNow;
				byte[] array = new byte[Math.Min(c.Packet.Length, 40)];
				Buffer.BlockCopy(c.Packet, 0, array, 0, array.Length);
				note("Diagnóstico comando: tipo 0x" + num.ToString("X2") + ", " + c.FullLength + " bytes: " + BitConverter.ToString(array) + ((c.FullLength > array.Length) ? "…" : "") + ". Nada foi espelhado.");
			}
		}
	}

	private static bool IsItemUse(byte[] p)
	{
		return p.Length >= 10 && BitConverter.ToUInt32(p, 0) == 16777325;
	}

	private static bool IsStone(byte[] p)
	{
		return p.Length == 14 && IsItemUse(p) && BitConverter.ToUInt32(p, 6) == 36905;
	}

	private void DiagnoseItemUse(CharacterRef main, byte[] packet)
	{
		string text = BitConverter.ToString(packet);
		if (lastItemLog.TryGetValue(text, out var value) && (DateTime.UtcNow - value).TotalSeconds < 5.0)
		{
			return;
		}
		lastItemLog[text] = DateTime.UtcNow;
		string text2 = "";
		try
		{
			using CharacterMemory s = new CharacterMemory(main.Pid);
			text2 = " · posição " + Pos(s);
		}
		catch
		{
		}
		note("Diagnóstico: item " + BitConverter.ToUInt32(packet, 6) + " usado no principal, slot " + BitConverter.ToUInt16(packet, 4) + ", " + packet.Length + " bytes: " + text + text2 + ". Nada foi espelhado.");
		Progress<string> progress = new Progress<string>(note);
		CharacterRef who = main;
		Task.Run(() =>
		{
			Thread.Sleep(4000);
			try
			{
				using CharacterMemory characterMemory = new CharacterMemory(who.Pid);
				if (characterMemory.Id == who.Id)
				{
					((IProgress<string>)progress).Report("Diagnóstico: principal 4 s depois do item " + BitConverter.ToUInt32(packet, 6) + " · posição " + Pos(characterMemory) + ".");
				}
			}
			catch
			{
			}
		});
	}

	private static string Pos(CharacterMemory s)
	{
		return s.X.ToString("0.0", CultureInfo.InvariantCulture) + ", " + s.Y.ToString("0.0", CultureInfo.InvariantCulture) + ", " + s.Z.ToString("0.0", CultureInfo.InvariantCulture);
	}

	private async Task RunBatch(CharacterRef main, CharacterRef[] recipients, byte[] packet, CancellationToken token)
	{
		CharacterRef[] array = recipients;
		foreach (CharacterRef characterRef in array)
		{
			activeClients[characterRef.Pid] = 0;
		}
		if (true)
		{
		}
		try
		{
			Destination destination;
			using (CharacterMemory characterMemory = new CharacterMemory(main.Pid))
			{
				if (characterMemory.Id != main.Id)
				{
					throw new Exception("Principal mudou. Atualize a lista.");
				}
				destination = characterMemory.FindDestination(BitConverter.ToUInt32(packet, 10) & 0xFFFF);
			}
			if (!(await Task.Run(() => WaitArrival(main, destination, 12000, token))))
			{
				note("Principal não confirmou chegada a " + destination.Name + "; secundários não receberam comando.");
				return;
			}
			note("Destino do principal confirmado: " + destination.Name + ".");
			Progress<string> progress = new Progress<string>((string s) =>
			{
				note(s);
			});
			Result[] array2 = await Task.WhenAll(recipients.Select((CharacterRef s) => Task.Run(() =>
			{
				try
				{
					Result result2 = Mirror(s, destination, packet, token, progress);
					if (result2.Blocked)
					{
						unsafeClients[s.Pid] = 0;
					}
					return result2;
				}
				finally
				{
					activeClients.TryRemove(s.Pid, out var _);
				}
			})));
			foreach (Result result in array2)
			{
				if (result.Blocked)
				{
					blocked.Add(result.Pid);
				}
				note(result.Message);
			}
			if (1 == 0)
			{
			}
		}
		catch (OperationCanceledException)
		{
			note("Tentativas anteriores canceladas por mudança de seleção ou destino.");
		}
		catch (Exception ex2)
		{
			note("Teleporte: " + ex2.Message);
		}
		finally
		{
			array = recipients;
			foreach (CharacterRef characterRef in array)
			{
				activeClients.TryRemove(characterRef.Pid, out var _);
			}
			if (1 == 0)
			{
			}
		}
	}

	private static bool WaitArrival(CharacterRef character, Destination destination, int timeout, CancellationToken token)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				using CharacterMemory characterMemory = new CharacterMemory(character.Pid);
				if (characterMemory.Id != character.Id)
				{
					throw new InvalidOperationException("O personagem mudou.");
				}
				if (characterMemory.At(destination))
				{
					return true;
				}
			}
			catch (InvalidOperationException)
			{
				throw;
			}
			catch
			{
			}
			if (token.WaitHandle.WaitOne(250))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return false;
	}

	private static Result Mirror(CharacterRef character, Destination destination, byte[] original, CancellationToken token, IProgress<string> progress)
	{
		Result result = new Result();
		result.Pid = character.Pid;
		Result result2 = result;
		try
		{
			for (int i = 1; i <= 3; i++)
			{
				token.ThrowIfCancellationRequested();
				Stone stone;
				using (CharacterMemory characterMemory = new CharacterMemory(character.Pid))
				{
					if (characterMemory.Id != character.Id)
					{
						throw new Exception("Personagem mudou; atualize a lista.");
					}
					if (characterMemory.At(destination))
					{
						result2.Message = character.Name + ": chegada confirmada a " + destination.Name + ".";
						return result2;
					}
					stone = characterMemory.FindStone();
				}
				progress.Report(character.Name + ": teleporte para " + destination.Name + " · tentativa " + i + "/3.");
				byte[] array = (byte[])original.Clone();
				Buffer.BlockCopy(BitConverter.GetBytes(stone.Slot), 0, array, 4, 2);
				token.ThrowIfCancellationRequested();
				TeleportBridge teleportBridge = null;
				try
				{
					teleportBridge = new TeleportBridge(character.Pid);
					if (teleportBridge.CharacterId != character.Id)
					{
						throw new Exception("Personagem mudou durante a operação.");
					}
					teleportBridge.SendTeleport(array);
				}
				finally
				{
					if (teleportBridge != null)
					{
						try
						{
							teleportBridge.Dispose();
						}
						finally
						{
							if (!teleportBridge.RestoreSucceeded)
							{
								result2.Blocked = true;
							}
						}
					}
				}
				if (result2.Blocked)
				{
					throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
				}
				if (WaitArrival(character, destination, 8000, token))
				{
					result2.Message = character.Name + ": chegou a " + destination.Name + ".";
					return result2;
				}
				using (CharacterMemory characterMemory = new CharacterMemory(character.Pid))
				{
					if (characterMemory.Id != character.Id)
					{
						throw new Exception("Personagem mudou.");
					}
					Stone stone2 = characterMemory.FindStone();
					if (stone2.Count < stone.Count)
					{
						if (WaitArrival(character, destination, 10000, token))
						{
							result2.Message = character.Name + ": chegou a " + destination.Name + ".";
							return result2;
						}
						throw new Exception("Pedra consumida, mas chegada não confirmada. Nova tentativa automática suspensa.");
					}
				}
				if (i < 3 && token.WaitHandle.WaitOne(1500))
				{
					token.ThrowIfCancellationRequested();
				}
			}
			result2.Message = character.Name + ": não chegou após 3 tentativas; confira se o ponto está liberado e se pode teleportar.";
		}
		catch (OperationCanceledException)
		{
			result2.Message = character.Name + ": tentativa pendente cancelada.";
		}
		catch (Exception ex2)
		{
			result2.Message = character.Name + ": " + ex2.Message;
		}
		return result2;
	}

	public async Task Stop()
	{
		stopping = true;
		cancel.Cancel();
		pending = null;
		pendingSpecial = (specialUse = null);
		questChoices.Clear();
		while (pumping)
		{
			await Task.Delay(50);
		}
		await job;
		if (capture != null)
		{
			TeleportCapture old = capture;
			await Task.Run(() =>
			{
				old.Dispose();
			});
			capture = null;
			note("Captura automática removida ao fechar o aplicativo.");
		}
		cancel.Dispose();
	}
}
