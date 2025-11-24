using BepInEx;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ILLine;

[BepInAutoPlugin(id: "io.github.kaycodes13.illine")]
public partial class ILLinePlugin : BaseUnityPlugin {
	static Harmony Harmony { get; } = new(Id);
	void Awake() {
		Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
		Harmony.PatchAll();
	}
}

[HarmonyPatch(typeof(StackTrace), "AddFrames")]
file class ReplaceLineNumber {
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen) {
		int frameIndex = -1;
		return new CodeMatcher(instructions, ilGen)
			.Start()
			.MatchStartForward([
				new(x => CallsMethod(x, "GetFileLineNumber"))
			])
			.RemoveInstructions(2)
			.Insert(Transpilers.EmitDelegate(GetLineOrILOffset))
			.Start()
			.MatchEndForward([
				new(x => Ldloc(x, out frameIndex)),
				new(x => CallsMethod(x, "GetInternalMethodName"))
			])
			.Advance(1)
			.Insert([
				new(OpCodes.Ldloc, frameIndex),
				Transpilers.EmitDelegate(AppendLineOrILOffset)
			])
			.InstructionEnumeration();
	}

	static string GetLineOrILOffset(StackFrame frame) {
		int line = frame.GetFileLineNumber();
		return (line > 0)
			? line.ToString()
			: $"IL_{frame.GetILOffset():X4}";
	}

	static string AppendLineOrILOffset(string currentText, StackFrame frame)
		=> $"{currentText} (at {GetLineOrILOffset(frame)})";

	static bool CallsMethod(CodeInstruction x, string name) =>
		(x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt)
		&& x.operand is MethodInfo m && m.Name == name;

	static bool Ldloc(CodeInstruction ci, out int index) {
		index = -1;
		if (ci.opcode == OpCodes.Ldloc || ci.opcode == OpCodes.Ldloc_S)
			index = (int)ci.operand;
		else if (ci.opcode == OpCodes.Ldloc_0)
			index = 0;
		else if (ci.opcode == OpCodes.Ldloc_1)
			index = 1;
		else if (ci.opcode == OpCodes.Ldloc_2)
			index = 2;
		else if (ci.opcode == OpCodes.Ldloc_3)
			index = 3;

		return index >= 0;
	}
}
