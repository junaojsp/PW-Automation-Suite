internal interface IMeditationRuntime
{
	bool Sitting(CharacterRef c);

	MeditationResult Set(CharacterRef c, bool sitting);
}
