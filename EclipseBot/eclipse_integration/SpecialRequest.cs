using System;

internal sealed class SpecialRequest
{
	public byte[] Use;

	public byte[] Point;

	public bool HasStart;

	public float X;

	public float Y;

	public float Z;

	public ushort PointId => BitConverter.ToUInt16(Point, 7);
}
