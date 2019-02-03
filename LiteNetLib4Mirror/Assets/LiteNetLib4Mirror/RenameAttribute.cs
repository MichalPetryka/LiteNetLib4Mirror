#if UNITY_EDITOR
using UnityEngine;

public class RenameAttribute : PropertyAttribute
{
	public RenameAttribute(string name)
	{
		NewName = name;
	}

	public string NewName { get; }
}

public class ArrayRenameAttribute : PropertyAttribute
{
	public ArrayRenameAttribute(string name)
	{
		NewName = name;
	}

	public string NewName { get; }
}
#endif