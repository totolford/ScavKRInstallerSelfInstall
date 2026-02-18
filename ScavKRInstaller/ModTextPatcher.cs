using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ScavKRInstaller;

public static class ModTextPatcher
{
    private const string HeaderOriginal = "This is a mod";
    private const string BodyOriginalPrefix = "<alpha=#11><i>...meaning it's a shitty mod for an unfinished game.";

    private const string HeaderReplacement = "Salut / Hello";
    private const string BodyReplacement = "Salut, ceci est un fork du mod https://github.com/danxnader/ScavKRInstaller permettant de lancer l'installation sans GUI avec une connexion automatique a un serveur au choix.\n\nHello, this is a fork of the mod https://github.com/danxnader/ScavKRInstaller that allows launching installation without GUI and with automatic connection to a server of your choice.";
    private const string ConsoleDisabledSvCheatsOriginal = "Console commands disabled. (sv_cheats is False)";
    private const string ConsoleDisabledByRulesOriginal = "Console commands disabled. (by the server rules)";
    private const string UnknownCommandOriginal = "Unknown command (btw co-op mod patch is active)";
    private const string ModdedCommandsHeaderMarker = "NOW MODDED COMMANDS";
    private const string ChatHelpOriginalPrefix = "Main commands:";

    private const string ConsoleDisabledSvCheatsReplacement = "Console disabled (sv_cheats=false). Host: enable cheats first, then use 'krok help'.";
    private const string ConsoleDisabledByRulesReplacement = "Console command blocked by server rules. Host can review rules with: krok rules";
    private const string UnknownCommandReplacement = "Unknown command. Try: krok help";
    private const string ModdedCommandsHeaderReplacement = "\n//  MULTIPLAYER COMMANDS / COMMANDES MP ==================";
    private const string ChatHelpReplacement = "Main commands / Commandes:\n/help // show this message\n/krok help // show all MP commands\n/krok rules // list current game rules\n/krok rule [rule] [value] // set a rule (host)\n/krok maxplayers [number] // set max players (host)\n/krok kick [player] // kick player (host)\n\nTip: open the console and run 'krok help' for full syntax.";

    public static bool TryPatchKrokoshaMod(string gameFolderPath, string ipPort, string playerName, string password, out string message)
    {
        try
        {
            string dllPath = Path.Combine(gameFolderPath, "BepInEx", "plugins", "KrokoshaCasualtiesMP.dll");
            if (!File.Exists(dllPath))
            {
                message = $"Target mod DLL not found: {dllPath}";
                return false;
            }

            string backupPath = dllPath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(dllPath, backupPath, overwrite: false);
            }

            using AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(dllPath);
            bool changed = false;

            foreach (TypeDefinition type in asm.MainModule.Types)
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (!method.HasBody)
                    {
                        continue;
                    }

                    foreach (Instruction instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode != OpCodes.Ldstr || instruction.Operand is not string s)
                        {
                            continue;
                        }

                        if (s == HeaderOriginal)
                        {
                            instruction.Operand = HeaderReplacement;
                            changed = true;
                            continue;
                        }

                        if (s.StartsWith(BodyOriginalPrefix, StringComparison.Ordinal))
                        {
                            instruction.Operand = BodyReplacement;
                            changed = true;
                            continue;
                        }

                        if (s == ConsoleDisabledSvCheatsOriginal)
                        {
                            instruction.Operand = ConsoleDisabledSvCheatsReplacement;
                            changed = true;
                            continue;
                        }

                        if (s == ConsoleDisabledByRulesOriginal)
                        {
                            instruction.Operand = ConsoleDisabledByRulesReplacement;
                            changed = true;
                            continue;
                        }

                        if (s == UnknownCommandOriginal)
                        {
                            instruction.Operand = UnknownCommandReplacement;
                            changed = true;
                            continue;
                        }

                        if (s.StartsWith(ChatHelpOriginalPrefix, StringComparison.Ordinal))
                        {
                            instruction.Operand = ChatHelpReplacement;
                            changed = true;
                            continue;
                        }

                        if (s.Contains(ModdedCommandsHeaderMarker, StringComparison.Ordinal))
                        {
                            instruction.Operand = ModdedCommandsHeaderReplacement;
                            changed = true;
                        }
                    }
                }
            }

            TypeDefinition? multiplayerType = asm.MainModule.Types.FirstOrDefault(t => t.Name == "KrokoshaScavMultiplayer");
            MethodDefinition? cctor = multiplayerType?.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic && m.Name == ".cctor");
            if (cctor != null && cctor.HasBody)
            {
                IList<Instruction> instructions = cctor.Body.Instructions;
                for (int i = 0; i < instructions.Count; i++)
                {
                    Instruction inst = instructions[i];
                    if (inst.OpCode != OpCodes.Stsfld || inst.Operand is not FieldReference field)
                    {
                        continue;
                    }

                    string? replacement = field.Name switch
                    {
                        "input_ipport_text" => ipPort,
                        "local_player_name" => playerName,
                        "password" => password,
                        _ => null
                    };

                    if (replacement == null)
                    {
                        continue;
                    }

                    int prev = i - 1;
                    while (prev >= 0 && instructions[prev].OpCode == OpCodes.Nop)
                    {
                        prev--;
                    }

                    if (prev >= 0 && instructions[prev].OpCode == OpCodes.Ldstr)
                    {
                        instructions[prev].Operand = replacement;
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                message = "No target menu/connection strings found to patch in KrokoshaCasualtiesMP.dll.";
                return false;
            }

            asm.Write(dllPath);
            message = "Patched Krokosha menu text and connection defaults successfully.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Exception while patching menu/connection text: {ex}";
            return false;
        }
    }
}
