# Diagnóstico estático — ElementClient 17/11/2026

Executável fornecido: `ELEMENTCLIENT(2).EXE`, SHA-256 `2987ee326956e694391c35e11f94e674f38cf6395cd7188359ec57b32daa26f8`, 13.287.888 bytes, PE32 x86, ImageBase `0x400000`, SizeOfImage `0xD66000` sem relocações.

## Comparação de RVAs existentes no GameSignatures.Data.cs

| Função | RVA anterior | 12 bytes no Element atualizado | Avaliação preliminar |
|---|---|---|---|
| FollowPost | 0x425090 | 01 c7 46 0c 00 00 00 00 c7 46 08 00 | Não é prólogo confiável |
| AttackSelect | 0x10AF70 | 51 53 55 56 57 8b f9 f6 87 04 0b 00 | Prólogo plausível |
| AttackNormal | 0x110590 | 51 53 56 57 6a 12 8b f9 32 db e8 61 | Prólogo plausível |
| MacroStart | 0x10DCB0 | 51 56 8b f1 8b 8e 18 14 00 00 85 c9 | Prólogo plausível |
| MacroStop | 0x10DDA0 | 56 8b f1 8b 8e 18 14 00 00 85 c9 | Prólogo plausível |
| SendData | 0x4C0C90 | 24 0c 74 20 8b 44 24 08 8b 00 8b 00 | Não é prólogo confiável |
| NpcClose | 0x75B0B0 | 00 00 c7 45 dc 00 00 00 00 c7 45 d0 | Não é prólogo confiável |
| NpcCancel | 0x281F80 | 08 8b 8b 54 02 00 00 56 e8 f3 2f 4d | Não é prólogo confiável |
| TraceMatter | 0x10AC20 | 51 53 55 56 57 8b f9 8b 87 64 07 | Prólogo plausível |

**Atenção:** mesmo um prólogo plausível não valida semântica, convenção de chamada nem estruturas de memória. A atualização não pode ser habilitada simplesmente trocando o SHA-256. Os RVAs obsoletos precisam ser remapeados e testados em processo antes de ativar farm/ataque/seguir.

Status: inspeção estática parcial, correção funcional e EXE compatível **não concluídos**.
