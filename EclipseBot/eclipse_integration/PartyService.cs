using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

internal static class PartyService
{
	public sealed class Outcome
	{
		public bool Blocked;

		public int Pid;

		public string Message;
	}

	private static byte[] GroupCommand(byte op, uint id)
	{
		byte[] array = new byte[(op == 30) ? 2 : 6];
		array[0] = op;
		if (array.Length == 6)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(id), 0, array, 2, 4);
		}
		return array;
	}

	private static PartySnapshot Group(CharacterRef character)
	{
		using CharacterMemory characterMemory = Open(character);
		return characterMemory.ReadParty();
	}

	private static void WaitGroup(Func<bool> condition, CancellationToken token, string failure)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < 6000)
		{
			token.ThrowIfCancellationRequested();
			if (condition())
			{
				return;
			}
			if (token.WaitHandle.WaitOne(75))
			{
				token.ThrowIfCancellationRequested();
			}
		}
		throw new Exception(failure);
	}

	private static void RequireLeaderAndMembers(CharacterRef leader, IEnumerable<uint> allowed)
	{
		PartySnapshot partySnapshot = Group(leader);
		HashSet<uint> ids = new HashSet<uint>(allowed);
		if (partySnapshot == null || partySnapshot.Leader != leader.Id || partySnapshot.Members.Any((uint id) => !ids.Contains(id)))
		{
			throw new Exception("A composição ou a liderança da PT mudou. Operação interrompida.");
		}
	}

	public static Outcome Remount(CharacterRef main, CharacterRef[] selected, CancellationToken token, IProgress<string> progress)
	{
		Outcome outcome = new Outcome();
		string text = "verificação inicial";
		try
		{
			token.ThrowIfCancellationRequested();
			if (selected.Length == 0)
			{
				throw new Exception("Selecione os secundários.");
			}
			HashSet<uint> all = new HashSet<uint>(selected.Select((CharacterRef s) => s.Id));
			if (all.Count != selected.Length || all.Contains(main.Id))
			{
				throw new Exception("Seleção de personagens inválida.");
			}
			all.Add(main.Id);
			PartySnapshot original = Group(main);
			if (original == null)
			{
				throw new Exception("O principal está sem PT. Use Montar PT.");
			}
			if (original.Leader != main.Id)
			{
				throw new Exception("O principal precisa ser o líder para remontar PT.");
			}
			if (original.Members.Any((uint id) => !all.Contains(id)))
			{
				throw new Exception("Há membros da PT fora da seleção. Selecione todos antes de remontar.");
			}
			CharacterRef[] array = selected;
			foreach (CharacterRef characterRef in array)
			{
				PartySnapshot partySnapshot = Group(characterRef);
				if (partySnapshot != null && (partySnapshot.Leader != main.Id || !original.Members.Contains(characterRef.Id)))
				{
					throw new Exception(characterRef.Name + " está em outra PT ou o grupo mudou.");
				}
			}
			CharacterRef anchor = selected.FirstOrDefault((CharacterRef s) => original.Members.Contains(s.Id));
			if (anchor == null)
			{
				throw new Exception("É necessário um secundário na PT para receber a liderança temporária.");
			}
			progress.Report("Remontar PT: liderança temporária ficará com " + anchor.Name + ".");
			text = "retirada dos demais secundários";
			foreach (CharacterRef secondary in selected.Where((CharacterRef s) => s.Id != anchor.Id && original.Members.Contains(s.Id)))
			{
				token.ThrowIfCancellationRequested();
				RequireLeaderAndMembers(main, all);
				progress.Report("Retirando " + secondary.Name + " da PT antes de remontar.");
				Send(main, GroupCommand(31, secondary.Id), outcome);
				Func<bool> condition = () =>
				{
					PartySnapshot partySnapshot5 = Group(main);
					return partySnapshot5 != null && partySnapshot5.Leader == main.Id && !partySnapshot5.Members.Contains(secondary.Id) && Group(secondary) == null;
				};
				WaitGroup(condition, token, secondary.Name + ": saída da PT não confirmada.");
			}
			text = "transferência de liderança";
			token.ThrowIfCancellationRequested();
			RequireLeaderAndMembers(main, new uint[2] { main.Id, anchor.Id });
			PartySnapshot partySnapshot2 = Group(main);
			if (partySnapshot2.Members.Count != 2 || !partySnapshot2.Members.Contains(anchor.Id))
			{
				throw new Exception("O secundário temporário não está mais na PT.");
			}
			progress.Report("Passando liderança para " + anchor.Name + ".");
			Send(main, GroupCommand(72, anchor.Id), outcome);
			WaitGroup(() =>
			{
				PartySnapshot partySnapshot5 = Group(main);
				PartySnapshot partySnapshot6 = Group(anchor);
				return partySnapshot5 != null && partySnapshot6 != null && partySnapshot5.Leader == anchor.Id && partySnapshot6.Leader == anchor.Id;
			}, token, "Transferência de liderança não confirmada. O principal não foi retirado.");
			text = "saída do principal";
			token.ThrowIfCancellationRequested();
			RequireLeaderAndMembers(anchor, new uint[2] { main.Id, anchor.Id });
			progress.Report(main.Name + ": saindo da PT após confirmar a liderança temporária.");
			Send(main, GroupCommand(30, 0u), outcome);
			WaitGroup(() => Group(main) == null, token, "Saída do principal não confirmada.");
			text = "liberação do secundário temporário";
			token.ThrowIfCancellationRequested();
			WaitGroup(() =>
			{
				PartySnapshot partySnapshot5 = Group(anchor);
				return partySnapshot5 == null || (partySnapshot5.Leader == anchor.Id && partySnapshot5.Members.Count == 1 && partySnapshot5.Members.Contains(anchor.Id));
			}, token, "O secundário temporário não confirmou a saída dos demais membros.");
			PartySnapshot partySnapshot3 = Group(anchor);
			if (partySnapshot3 != null)
			{
				if (partySnapshot3.Leader != anchor.Id || partySnapshot3.Members.Count != 1 || !partySnapshot3.Members.Contains(anchor.Id))
				{
					throw new Exception("A PT temporária recebeu outro membro; não foi desfeita.");
				}
				Send(anchor, GroupCommand(30, 0u), outcome);
				WaitGroup(() => Group(anchor) == null, token, "O secundário temporário ainda está em grupo.");
			}
			text = "novos convites";
			progress.Report("PT anterior encerrada. Convidando novamente os secundários.");
			array = selected;
			foreach (CharacterRef characterRef in array)
			{
				token.ThrowIfCancellationRequested();
				Outcome outcome2 = Join(main, characterRef, token, progress);
				if (outcome2.Blocked)
				{
					outcome.Blocked = true;
					outcome.Pid = outcome2.Pid;
					throw new Exception(outcome2.Message);
				}
				progress.Report(outcome2.Message);
				if (!Together(main, characterRef))
				{
					throw new Exception(characterRef.Name + ": entrada na nova PT não confirmada.");
				}
			}
			text = "verificação final";
			PartySnapshot partySnapshot4 = Group(main);
			if (partySnapshot4 == null || partySnapshot4.Leader != main.Id || selected.Any((CharacterRef s) => !Together(main, s)))
			{
				throw new Exception("A nova PT não foi confirmada em todos os clientes.");
			}
			outcome.Message = "PT remontada: " + main.Name + " como líder e todos os secundários selecionados confirmados.";
		}
		catch (OperationCanceledException)
		{
			outcome.Message = "Remontagem cancelada na etapa: " + text + ". Confira o estado atual da PT.";
		}
		catch (Exception ex2)
		{
			outcome.Message = "Remontagem interrompida na etapa " + text + ": " + ex2.Message;
		}
		return outcome;
	}

	private static void Send(CharacterRef character, byte[] packet, Outcome result)
	{
		PartyBridge partyBridge = null;
		try
		{
			partyBridge = new PartyBridge(character.Pid);
			if (partyBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou. Atualize a lista.");
			}
			partyBridge.SendParty(packet);
			if (partyBridge.LastSendResult != 1)
			{
				throw new Exception("Envio da PT não foi aceito pelo cliente.");
			}
		}
		finally
		{
			if (partyBridge != null)
			{
				try
				{
					partyBridge.Dispose();
				}
				finally
				{
					if (!partyBridge.RestoreSucceeded)
					{
						result.Blocked = true;
						result.Pid = character.Pid;
					}
				}
			}
			if (result.Blocked)
			{
				throw new Exception("Remoção da ponte não confirmada. Reinicie esse cliente.");
			}
		}
	}

	private static CharacterMemory Open(CharacterRef character)
	{
		CharacterMemory characterMemory = new CharacterMemory(character.Pid);
		if (characterMemory.Id != character.Id)
		{
			characterMemory.Dispose();
			throw new Exception("Personagem mudou. Atualize a lista.");
		}
		return characterMemory;
	}

	private static bool Together(CharacterRef main, CharacterRef secondary)
	{
		using CharacterMemory characterMemory = Open(main);
		using CharacterMemory characterMemory2 = Open(secondary);
		PartySnapshot partySnapshot = characterMemory.ReadParty();
		PartySnapshot partySnapshot2 = characterMemory2.ReadParty();
		return partySnapshot != null && partySnapshot2 != null && partySnapshot.Leader == main.Id && partySnapshot2.Leader == main.Id && partySnapshot.Members.Contains(secondary.Id) && partySnapshot2.Members.Contains(main.Id);
	}

	public static Outcome Join(CharacterRef main, CharacterRef secondary, CancellationToken token, IProgress<string> progress)
	{
		Outcome outcome = new Outcome();
		try
		{
			for (int i = 1; i <= 2; i++)
			{
				token.ThrowIfCancellationRequested();
				if (Together(main, secondary))
				{
					outcome.Message = secondary.Name + ": já está na PT do principal.";
					return outcome;
				}
				using (CharacterMemory characterMemory = Open(main))
				{
					using CharacterMemory characterMemory2 = Open(secondary);
					PartySnapshot partySnapshot = characterMemory.ReadParty();
					PartySnapshot partySnapshot2 = characterMemory2.ReadParty();
					if (partySnapshot != null && partySnapshot.Leader != main.Id)
					{
						throw new Exception("O principal precisa ser líder para convidar.");
					}
					if (partySnapshot2 != null)
					{
						throw new Exception("O secundário já está em outra PT; não foi removido.");
					}
				}
				byte[] array = new byte[6] { 27, 0, 0, 0, 0, 0 };
				Buffer.BlockCopy(BitConverter.GetBytes(secondary.Id), 0, array, 2, 4);
				progress.Report("Convidando " + secondary.Name + " · tentativa " + i + "/2.");
				token.ThrowIfCancellationRequested();
				Send(main, array, outcome);
				bool flag = false;
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (stopwatch.ElapsedMilliseconds < 12000)
				{
					token.ThrowIfCancellationRequested();
					if (Together(main, secondary))
					{
						outcome.Message = secondary.Name + ": entrada na PT confirmada.";
						return outcome;
					}
					if (!flag)
					{
						using CharacterMemory characterMemory3 = Open(secondary);
						PartyInvitation partyInvitation = characterMemory3.FindPartyInvitation(secondary.Pid, main.Id);
						if (partyInvitation != null)
						{
							token.ThrowIfCancellationRequested();
							byte[] array2 = new byte[10] { 28, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
							Buffer.BlockCopy(BitConverter.GetBytes(main.Id), 0, array2, 2, 4);
							Buffer.BlockCopy(BitConverter.GetBytes(partyInvitation.Ticket), 0, array2, 6, 4);
							Send(secondary, array2, outcome);
							flag = true;
							progress.Report(secondary.Name + ": convite do principal aceito pelo app.");
						}
					}
					if (token.WaitHandle.WaitOne(50))
					{
						token.ThrowIfCancellationRequested();
					}
				}
			}
			outcome.Message = secondary.Name + ": não entrou na PT. Confira distância, limite do grupo e restrições de convite.";
		}
		catch (OperationCanceledException)
		{
			outcome.Message = secondary.Name + ": montagem da PT cancelada pela mudança de seleção.";
		}
		catch (Exception ex2)
		{
			outcome.Message = secondary.Name + ": " + ex2.Message;
		}
		return outcome;
	}
}
