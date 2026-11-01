using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class QuestService
{
	private sealed class Request
	{
		public CharacterRef Source;

		public CharacterRef[] Recipients;

		public byte[] Npc;

		public byte[] Packet;

		public ushort Quest;

		public bool Finish;

		public string Key;

		public float X;

		public float Z;

		public DateTime Created;
	}

	private sealed class Result
	{
		public int Pid;

		public bool Blocked;

		public bool Confirmed;

		public string Message;
	}

	private readonly Action<string> note;

	private readonly HashSet<int> blocked;

	private readonly Queue<Request> queue = new Queue<Request>();

	private readonly HashSet<string> queued = new HashSet<string>();

	private readonly ConcurrentDictionary<int, byte> active = new ConcurrentDictionary<int, byte>();

	private readonly ConcurrentDictionary<int, byte> unsafeClients = new ConcurrentDictionary<int, byte>();

	private CharacterRef source;

	private CharacterRef[] recipients = new CharacterRef[0];

	private HashSet<ushort> known;

	private string selection = "";

	private byte[] npc;

	private float npcX;

	private float npcZ;

	private DateTime npcTime;

	private CancellationTokenSource cancel = new CancellationTokenSource();

	private Task job = Task.FromResult(0);

	private bool stopping;

	private int generation;

	public Action<ushort, int, int> ReportCheck;

	public bool Working => !job.IsCompleted || queue.Count > 0;

	public bool Running => !job.IsCompleted;

	public QuestService(Action<string> log, HashSet<int> blockedClients)
	{
		note = log;
		blocked = blockedClients;
	}

	public bool IsClientBusy(int pid)
	{
		return active.ContainsKey(pid) || unsafeClients.ContainsKey(pid);
	}

	public void CancelAll()
	{
		generation++;
		cancel.Cancel();
		queue.Clear();
		queued.Clear();
		npc = null;
	}

	public void UpdateSelection(CharacterRef main, CharacterRef[] selected)
	{
		string text = ((main == null) ? "" : (main.Key + "/" + string.Join(",", (from s in selected
			select s.Key into s
			orderby s
			select s).ToArray())));
		if (!(text == selection))
		{
			CancelAll();
			selection = text;
			source = main;
			recipients = selected;
			known = null;
			RefreshPrimarySnapshot();
		}
	}

	public void RefreshPrimarySnapshot()
	{
		if (source == null)
		{
			return;
		}
		try
		{
			using CharacterMemory characterMemory = Open(source);
			known = new HashSet<ushort>(characterMemory.ActiveQuestIds());
		}
		catch
		{
			known = null;
		}
	}

	private static CharacterMemory Open(CharacterRef character)
	{
		CharacterMemory characterMemory = new CharacterMemory(character.Pid);
		if (characterMemory.Id != character.Id)
		{
			characterMemory.Dispose();
			throw new InvalidOperationException("O personagem mudou. Atualize a lista.");
		}
		return characterMemory;
	}

	public void Observe(byte[] packet)
	{
		if (stopping || source == null || recipients.Length == 0)
		{
			return;
		}
		if (packet.Length == 6 && BitConverter.ToUInt16(packet, 0) == 35)
		{
			try
			{
				using CharacterMemory characterMemory = Open(source);
				npc = (byte[])packet.Clone();
				npcX = characterMemory.X;
				npcZ = characterMemory.Z;
				npcTime = DateTime.UtcNow;
				return;
			}
			catch
			{
				npc = null;
				return;
			}
		}
		bool flag = packet.Length == 18 && BitConverter.ToUInt16(packet, 0) == 37 && BitConverter.ToUInt32(packet, 2) == 6 && BitConverter.ToUInt32(packet, 6) == 8;
		bool flag2 = packet.Length == 22 && BitConverter.ToUInt16(packet, 0) == 37 && BitConverter.ToUInt32(packet, 2) == 7 && BitConverter.ToUInt32(packet, 6) == 12;
		if (!flag && !flag2)
		{
			return;
		}
		if (flag && BitConverter.ToUInt32(packet, 14) != 0)
		{
			note("Entrega com escolha de recompensa ainda não validada; confira os secundários.");
			return;
		}
		uint num = BitConverter.ToUInt32(packet, 10);
		if (num == 0 || num > 65535)
		{
			note("Missão: identificador fora do perfil validado.");
		}
		else if (known == null)
		{
			note("Missão " + num + ": leitura do principal indisponível; não espelhada.");
		}
		else
		{
			if (flag ? (!known.Contains((ushort)num)) : known.Contains((ushort)num))
			{
				return;
			}
			if (npc == null || (DateTime.UtcNow - npcTime).TotalMinutes > 5.0)
			{
				note("Missão " + num + ": reabra o NPC no principal com o app ativo antes de aceitar ou entregar.");
				return;
			}
			string text = selection + ":" + generation + ":" + BitConverter.ToUInt32(npc, 2) + ":" + num + ":" + flag;
			if (!queued.Contains(text))
			{
				if (queue.Count >= 16)
				{
					note("Fila de missões cheia; missão " + num + " não adicionada.");
					return;
				}
				queued.Add(text);
				queue.Enqueue(new Request
				{
					Source = source,
					Recipients = recipients.ToArray(),
					Npc = (byte[])npc.Clone(),
					Packet = (byte[])packet.Clone(),
					Quest = (ushort)num,
					Finish = flag,
					Key = text,
					X = npcX,
					Z = npcZ,
					Created = DateTime.UtcNow
				});
				note("Missão " + num + (flag ? ": entrega capturada, aguardando confirmação do principal." : ": aceitação capturada, aguardando confirmação do principal."));
			}
		}
	}

	public void Pump(bool otherBusy)
	{
		if (!stopping && !otherBusy && job.IsCompleted && queue.Count != 0)
		{
			Request request = queue.Dequeue();
			if ((DateTime.UtcNow - request.Created).TotalSeconds > 120.0)
			{
				queued.Remove(request.Key);
				note("Missão " + request.Quest + ": solicitação expirada; confira os secundários.");
			}
			else
			{
				cancel.Dispose();
				cancel = new CancellationTokenSource();
				job = Run(request, cancel.Token);
			}
		}
	}

	public async Task CancelAndWait()
	{
		CancelAll();
		await job;
	}

	private static bool WaitQuest(CharacterRef character, ushort quest, int timeout, CancellationToken token, bool present = true)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeout)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				using CharacterMemory characterMemory = Open(character);
				if (characterMemory.ActiveQuestIds().Contains(quest) == present)
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
			if (token.WaitHandle.WaitOne(50))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		return false;
	}

	private async Task Run(Request request, CancellationToken token)
	{
		CharacterRef[] array = request.Recipients;
		foreach (CharacterRef characterRef in array)
		{
			active[characterRef.Pid] = 0;
		}
		if (true)
		{
		}
		try
		{
			if (!(await Task.Run(() => WaitQuest(request.Source, request.Quest, 8000, token, !request.Finish))))
			{
				note("Missão " + request.Quest + (request.Finish ? ": entrega não confirmada no principal; nenhum comando enviado aos secundários." : ": não entrou na lista ativa do principal; nenhum comando enviado aos secundários."));
				return;
			}
			Progress<string> progress = new Progress<string>((string s) =>
			{
				note(s);
			});
			CharacterRef[] selected = request.Recipients.Where((CharacterRef s) => !blocked.Contains(s.Pid) && !unsafeClients.ContainsKey(s.Pid)).ToArray();
			Result[] results = await Task.WhenAll(selected.Select((CharacterRef character) => Task.Run(() =>
			{
				try
				{
					Result result2 = Mirror(character, request, token, progress);
					if (result2.Blocked)
					{
						unsafeClients[character.Pid] = 0;
					}
					return result2;
				}
				finally
				{
					active.TryRemove(character.Pid, out var _);
				}
			})));
			Result[] array2 = results;
			foreach (Result result in array2)
			{
				if (result.Blocked)
				{
					blocked.Add(result.Pid);
				}
				note(result.Message);
			}
			if (true)
			{
			}
			int confirmedCount = results.Count((Result r) => r.Confirmed);
			note("CHECK missão " + request.Quest + ": " + confirmedCount + "/" + request.Recipients.Length + " secundário(s) confirmados.");
			if (ReportCheck != null)
			{
				ReportCheck(request.Quest, confirmedCount, request.Recipients.Length);
			}
		}
		catch (OperationCanceledException)
		{
			note("Missão " + request.Quest + ": tentativas pendentes canceladas.");
		}
		catch (Exception ex2)
		{
			note("Missão " + request.Quest + ": " + ex2.Message);
		}
		finally
		{
			queued.Remove(request.Key);
			array = request.Recipients;
			foreach (CharacterRef characterRef in array)
			{
				active.TryRemove(characterRef.Pid, out var _);
			}
			if (1 == 0)
			{
			}
		}
	}

	private static void Send(CharacterRef character, byte[] packet, Result result)
	{
		NpcBridge npcBridge = null;
		try
		{
			npcBridge = new NpcBridge(character.Pid);
			if (npcBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou.");
			}
			npcBridge.SendNpc(packet);
			if (npcBridge.LastSendResult != 1)
			{
				throw new Exception("Envio não aceito pelo cliente.");
			}
		}
		finally
		{
			if (npcBridge != null)
			{
				try
				{
					npcBridge.Dispose();
				}
				finally
				{
					if (!npcBridge.RestoreSucceeded)
					{
						result.Blocked = true;
					}
				}
			}
			if (result.Blocked)
			{
				throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
			}
		}
	}

	public static bool PopupOpen(CharacterRef character)
	{
		CharacterMemory state = Open(character);
		try
		{
			return state.NpcDialogs(CancellationToken.None).Any((uint d) => state.NpcVisible(d) && !state.NpcInteractionActive(d));
		}
		finally
		{
			if (state != null)
			{
				((IDisposable)state).Dispose();
			}
		}
	}

	public static bool ClosePopup(CharacterRef character, CancellationToken token, out bool blocked)
	{
		blocked = false;
		CharacterMemory state = Open(character);
		uint[] array;
		try
		{
			array = (from d in state.NpcDialogs(token)
				where state.NpcVisible(d) && !state.NpcInteractionActive(d)
				select d).Take(4).ToArray();
		}
		finally
		{
			if (state != null)
			{
				((IDisposable)state).Dispose();
			}
		}
		if (array.Length == 0)
		{
			return false;
		}
		NpcBridge npcBridge = null;
		try
		{
			npcBridge = new NpcBridge(character.Pid);
			if (npcBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou antes de fechar janela");
			}
			npcBridge.CloseDialogs(array);
		}
		finally
		{
			if (npcBridge != null)
			{
				try
				{
					npcBridge.Dispose();
				}
				finally
				{
					blocked = !npcBridge.RestoreSucceeded;
				}
			}
		}
		for (int num = 0; num < 20; num++)
		{
			CharacterMemory state2 = Open(character);
			try
			{
				if (array.All((uint d) => !state2.NpcVisible(d)))
				{
					return true;
				}
			}
			finally
			{
				if (state2 != null)
				{
					((IDisposable)state2).Dispose();
				}
			}
			Thread.Sleep(50);
		}
		throw new Exception("Janela ainda aberta no cliente");
	}

	public static string CloseWindows(CharacterRef character, CancellationToken token, out bool blocked)
	{
		Result result = new Result();
		result.Pid = character.Pid;
		Result result2 = result;
		try
		{
			CloseInteraction(character, 0, result2, token);
			blocked = result2.Blocked;
			return null;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			blocked = result2.Blocked;
			return ex2.Message;
		}
	}

	private static void CloseInteraction(CharacterRef character, ushort quest, Result result, CancellationToken token)
	{
		CharacterMemory state = Open(character);
		uint[] array;
		try
		{
			array = (from d in state.QuestWindows(token)
				where (!state.IsNpcDialog(d)) ? state.QuestWindowVisible(d) : (state.NpcVisible(d) || state.NpcInteractionActive(d))
				orderby state.IsNpcDialog(d) ? 1 : 0
				select d).Take(16).ToArray();
		}
		finally
		{
			if (state != null)
			{
				((IDisposable)state).Dispose();
			}
		}
		if (array.Length == 0)
		{
			return;
		}
		NpcBridge npcBridge = null;
		try
		{
			npcBridge = new NpcBridge(character.Pid);
			if (npcBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou antes de fechar janela");
			}
			npcBridge.CloseDialogs(array);
		}
		finally
		{
			if (npcBridge != null)
			{
				try
				{
					npcBridge.Dispose();
				}
				finally
				{
					if (!npcBridge.RestoreSucceeded)
					{
						result.Blocked = true;
					}
				}
			}
			if (result.Blocked)
			{
				throw new Exception("Ponte de fechamento não confirmou remoção; reinicie esse cliente");
			}
		}
		for (int num = 0; num < 20; num++)
		{
			CharacterMemory state2 = Open(character);
			try
			{
				if (array.All((uint d) => !state2.QuestWindowVisible(d) && (!state2.IsNpcDialog(d) || !state2.NpcInteractionActive(d))))
				{
					return;
				}
			}
			finally
			{
				if (state2 != null)
				{
					((IDisposable)state2).Dispose();
				}
			}
			Thread.Sleep(50);
		}
		throw new Exception("Janela ou interação com NPC ainda ativa no cliente");
	}

	private static Result Mirror(CharacterRef character, Request request, CancellationToken token, IProgress<string> progress)
	{
		Result result = new Result
		{
			Pid = character.Pid
		};
		try
		{
			using (CharacterMemory characterMemory = Open(character))
			{
				bool flag = characterMemory.ActiveQuestIds().Contains(request.Quest);
				if (request.Finish && !flag)
				{
					result.Message = character.Name + ": não possui a missão " + request.Quest + " ativa; entrega não enviada nem confirmada.";
					return result;
				}
				if (!request.Finish && flag)
				{
					result.Confirmed = true;
					result.Message = character.Name + ": já possui a missão " + request.Quest + "; não repetida.";
					return result;
				}
				if (Math.Abs(characterMemory.X - request.X) > 30f || Math.Abs(characterMemory.Z - request.Z) > 30f)
				{
					throw new Exception("aproxime o personagem do mesmo NPC antes de aceitar a missão no principal.");
				}
			}
			List<uint> dialogs;
			using (CharacterMemory characterMemory = Open(character))
			{
				dialogs = characterMemory.NpcDialogs(token);
			}
			if (dialogs.Count == 0)
			{
				throw new Exception("janela interna do NPC indisponível.");
			}
			int attempt = 0;
			QuestDelivery.Outcome outcome = QuestDelivery.Execute(() =>
			{
				using CharacterMemory characterMemory2 = Open(character);
				return request.Finish ? (!characterMemory2.ActiveQuestIds().Contains(request.Quest)) : characterMemory2.ActiveQuestIds().Contains(request.Quest);
			}, () =>
			{
				attempt++;
				progress.Report(character.Name + ": missão " + request.Quest + " · tentativa " + attempt + "/3.");
				Send(character, request.Npc, result);
				if (token.WaitHandle.WaitOne(50))
				{
					token.ThrowIfCancellationRequested();
				}
			}, () =>
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (stopwatch.ElapsedMilliseconds < 6000)
				{
					token.ThrowIfCancellationRequested();
					CharacterMemory state = Open(character);
					try
					{
						if (dialogs.Any((uint d) => state.NpcVisible(d)))
						{
							return true;
						}
					}
					finally
					{
						if (state != null)
						{
							((IDisposable)state).Dispose();
						}
					}
					if (token.WaitHandle.WaitOne(50))
					{
						token.ThrowIfCancellationRequested();
					}
				}
				return false;
			}, () =>
			{
				Send(character, request.Packet, result);
			}, () => WaitQuest(character, request.Quest, 8000, token, !request.Finish), () =>
			{
				if (!result.Blocked)
				{
					CloseInteraction(character, request.Quest, result, CancellationToken.None);
				}
			}, token, () => !result.Blocked);
			result.Confirmed = outcome.Confirmed;
			if (outcome.Confirmed)
			{
				result.Message = character.Name + ": missão " + request.Quest + (request.Finish ? " entregue e confirmada." : " recebida e confirmada.") + ((outcome.CloseError != null) ? (" Não foi possível fechar a janela: " + outcome.CloseError) : (outcome.AlreadyPresent ? "" : " Janelas fechadas."));
			}
			else
			{
				result.Message = character.Name + ": missão " + request.Quest + " não confirmada após 3 tentativas. Confira disponibilidade da missão e proximidade do NPC.";
			}
		}
		catch (OperationCanceledException)
		{
			result.Message = character.Name + ": missão pendente cancelada.";
		}
		catch (Exception ex2)
		{
			result.Message = character.Name + ": " + ex2.Message;
		}
		return result;
	}

	public async Task Stop()
	{
		stopping = true;
		CancelAll();
		await job;
		cancel.Dispose();
	}
}
