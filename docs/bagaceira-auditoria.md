# Auditoria inicial — Bagaceira(3).exe

## Evidências verificadas
- PE32 Windows x86, assembly Mono/.NET.
- Strings e identificadores internos presentes: `CharacterMemory`, `CharacterRef`, `OpenProcess`, `ReadProcessMemory`, `GetWindowThreadProcessId`, `InternalBridge`, `PartyBridge`, `FollowIntent`, `BuffBridge`, `AttackBridge`, `PartyService`, `BuffService`, `AttackController`, `InitializeCharacterLists`, `PopulateFollowers`, `MaintainFollowing`, `BuffParty`, `RequestAttack`.
- Mensagens de interface mencionam nome do personagem, processo x86, ponte, Montar PT, Remontar PT, Seguir, Bufar e Atacar.

## Limitações
Esta inspeção é de formato e strings, **não** descompilação integral dos corpos de métodos. Os nomes sugerem componentes, mas não comprovam offsets, algoritmos, compatibilidade ou funcionamento no EclipseBot. Nenhuma função foi portada ou testada.

## Plano de integração segura
1. Recuperar e auditar os métodos .NET com descompilador adequado (ILSpy/dnSpy) e mapear dependências.
2. Priorizar leitura/associação de nome de personagem a PID/HWND, com validação de versão do cliente e tratamento de erros.
3. Criar interfaces independentes para descoberta de personagem e operações de grupo/seguimento, sem alterar a UI existente.
4. Integrar uma função por vez e testar com logs; não copiar módulos de licença, autenticação ou atualizador.
5. Preservar o projeto WPF v0.9 e manter ações que dependem da ponte desativadas até validação.

## Estado
**Somente auditoria inicial.** Código não compilado, não executado no jogo, sem integração funcional.
