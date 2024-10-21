using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;
using UnityEngine;

namespace uconsole
{
    public class ConsoleCommandsConsoleSystem
    {
        [ConsoleMethod("console.cmd.list", "help", "Print the list of all available commands", "Specify commands to print by a wildcard. Works with full names of commands"), UnityEngine.Scripting.Preserve]
		public static void PrintAllCommands(string wildcard = null)
        {
            int counter = 0;

            Debug.Log("Format: FullName<AliasName>(Parameters) : Description");

            for (int i = 0; i < ConsoleSystem.Instance.Methods.Count; i++)
            {
                if (!string.IsNullOrEmpty(wildcard))
                {
                    var regExpression = _wildCardToRegular(wildcard);
                    var pass = Regex.IsMatch(ConsoleSystem.Instance.Methods[i].FullName, regExpression);
                    if (!pass)
                        continue;
                }

                var method = "  - " + ConsoleSystem.Instance.Methods[i].Signature;
                if(!string.IsNullOrEmpty(ConsoleSystem.Instance.Methods[i].CmdDescription))
                    method += " : " + ConsoleSystem.Instance.Methods[i].CmdDescription;
                if (!ConsoleSystem.Instance.Methods[i].IsValid())
                    method += "[Invalid]";

                Debug.Log(method);
                ++counter;
            }

            Debug.Log("");
            Debug.Log($"Commands amount: {counter}");
        }

        [ConsoleMethod("console.cmd.help", "hlp", "Print help for specific command", "Full name or alias of command. Parameter could be a string or a direct address of the function. For example: hlp(hlp) or hlp('hlp') or hlp('console.cmd.hlp')"), UnityEngine.Scripting.Preserve]
        public static void PrintCommandHelp(DynValue command)
        {
            void PrintCommandHelpImpl(ConsoleSystem.ConsoleMethodInfo consoleMethodInfo)
            {
                StringBuilder stringBuilder = new StringBuilder(256);
                stringBuilder.Append(consoleMethodInfo.Signature);
                if (!string.IsNullOrEmpty(consoleMethodInfo.CmdDescription))
                    stringBuilder.Append(" : ").Append(consoleMethodInfo.CmdDescription);
                if (!consoleMethodInfo.IsValid())
                    stringBuilder.Append("[Invalid]");
                Debug.Log(stringBuilder.ToString());
                foreach (var desc in consoleMethodInfo.ParameterDescriptions)
                {
                    Debug.Log($"  - {desc}");
                }
            }

            if (command.Callback != null)
            {
                var reg = ConsoleSystem.Instance.Methods.FirstOrDefault(x => command.Callback.Name == x.Method.Name);
                if (reg == null)
                {
                    Debug.LogError($"Can't find Clr method {command.Callback.Name}");
                    return;
                }

                PrintCommandHelpImpl(reg);
                return;
            }

            if (command.String != null)
            {
                var commandName = command.String;
                commandName = commandName.ToLower().Trim();

                var methodInfo = ConsoleSystem.Instance.Methods.FirstOrDefault(x => x.AliasName == commandName);
                if (methodInfo == null)
                    methodInfo = ConsoleSystem.Instance.Methods.FirstOrDefault(x => x.FullName == commandName);
                if (methodInfo == null)
                {
                    Debug.LogError($"Can't find command with full name or alias '{commandName}'");
                    return;
                }

                PrintCommandHelpImpl(methodInfo);
            }
        }

        [ConsoleMethod("console.var.list", "lsv", "Print the list of all available variables", "Specify variables to print by a wildcard. Works with full names of variables"), UnityEngine.Scripting.Preserve]
        public static void PrintAllVariables(string wildcard = null)
        {
            StringBuilder stringBuilder = new StringBuilder(4096);
            int counter = 0;

            for (int i = 0; i < ConsoleSystem.Instance.Variables.Count; i++)
            {
                if (!string.IsNullOrEmpty(wildcard))
                {
                    var regExpression = _wildCardToRegular(wildcard);
                    var pass = Regex.IsMatch(ConsoleSystem.Instance.Variables[i].FullName, regExpression);
                    if (!pass)
                        continue;
                }
                stringBuilder.Append("  - ").Append(ConsoleSystem.Instance.Variables[i].Signature).AppendLine();
                ++counter;
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"Variables amount: {counter}");

            Debug.Log(stringBuilder.ToString());
        }

        [ConsoleMethod("console.printregisteredtypes", "printregisteredtypes", "Print the list of registered types"), UnityEngine.Scripting.Preserve]
        public static void PrintRegisteredTypes()
        {
            Debug.Log("Registered types:");
            var registeredTypes = UserData.GetRegisteredTypes();
            foreach (var registeredType in registeredTypes)
                Debug.Log(registeredType);
        }

        [ConsoleMethod("console.clear", "cls", "Clear the console"), UnityEngine.Scripting.Preserve]
        public static void ClearConsole()
        {
            throw new NotImplementedException();
            //WidgetQonsoleController.Instance.Clear();
        }

        private static string _wildCardToRegular(string wildcard)
        {
            return "^" + Regex.Escape(wildcard).Replace("\\*", ".*") + "$";
        }
    }
}