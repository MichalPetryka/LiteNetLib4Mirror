using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mirror
{
	static class DefineAdder
	{
		/// <summary>
		/// Add define symbols as soon as Unity gets done compiling.
		/// </summary>
		[InitializeOnLoadMethod]
		static void AddDefineSymbols()
		{
			List<string> defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';').ToList();
			if (!defines.Contains("LITENETLIB4MIRROR"))
				defines.Add("LITENETLIB4MIRROR");
			PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", defines));
		}
	}
}
