internal interface IAttackRuntime
{
	uint Target(CharacterRef c);

	bool ValidTarget(CharacterRef c, uint target);

	bool Running(CharacterRef c, uint combo, uint target);

	AttackResult Start(CharacterRef c, uint target);

	AttackResult Stop(CharacterRef c, uint combo, uint target);
}
