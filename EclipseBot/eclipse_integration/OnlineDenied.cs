using System;

internal sealed class OnlineDenied : Exception
{
	public OnlineDenied(string message)
		: base(message)
	{
	}
}
