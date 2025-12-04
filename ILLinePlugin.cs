using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
			.Insert(Transpilers.EmitDelegate(GetLineAndILOffset))
			.Start()
			.MatchEndForward([
				new(x => Ldloc(x, out frameIndex)),
				new(x => CallsMethod(x, "GetInternalMethodName"))
			])
			.Advance(1)
			.Insert([
				new(OpCodes.Ldloc, frameIndex),
				Transpilers.EmitDelegate(AppendLineAndILOffset)
			])
			.InstructionEnumeration();
	}

	static string GetLineAndILOffset(StackFrame frame) {
		int line = frame.GetFileLineNumber();
		int offset = frame.GetILOffset();
		return (line > 0)
			? $"{line} [IL_{offset:X4}]"
			: $"IL_{offset:X4}";
	}

	static string AppendLineAndILOffset(string currentText, StackFrame frame)
		=> $"{currentText} (at {GetLineAndILOffset(frame)})";

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
