using System;
using GameLib.Log;
using MoonSharp.Interpreter;
using MoonSharp.UnityWrapper;
using UnityEngine;

namespace uconsole
{
    public class Executor
    {
        private Script Script;
        private ConsoleSystem _consoleSystem;

        public Executor(ConsoleSystem consoleSystem)
        {
            _consoleSystem = consoleSystem;
            // Redefine print to print using Unity Debug.Log
            Script.DefaultOptions.DebugPrint = s => Debug.Log(s);

            Script = new Script();
            Script.DoFile("uconsole-core/LuaImplHelpers");

            _consoleSystem.SortMethodsTable();
            _consoleSystem.PrepareSearchTable();

            RegisterLuaWrapperTypes();
            RegisterParameterTypes();
            AddFunctionsToRegistryTable();
            AddVariablesToRegistryTable();
            RegisterCommandsAndVariable();

            // if (RunAutoexec)
            //      Script.DoFile("Autoexec");
        }

        private void RegisterLuaWrapperTypes()
        {
            LuaVector3.RegisterWrapperType(Script);
        }

        private void RegisterParameterTypes()
        {
            foreach (var consoleMethodInfo in _consoleSystem.Methods)
            {
                foreach (var parType in consoleMethodInfo.ParameterTypes)
                {
                    if (!UserData.IsTypeRegistered(parType) && parType.IsEnum)
                    {
                        UserData.RegisterType(parType);
                        if (Script.Globals.Get(parType.Name).Type != DataType.Nil)
                        {
                            Debug.LogError($"Can't register lua type '{parType.Name}'. The name already exists globally");
                            break;
                        }
                        Script.Globals[parType.Name] = parType;
                    }
                }
            }
        }

        private void AddFunctionsToRegistryTable()
        {
            foreach (var consoleMethodInfo in _consoleSystem.Methods)
            {
                var regTable = new Table(Script);
                regTable["alias"] = consoleMethodInfo.AliasName;
                regTable["fullName"] = consoleMethodInfo.FullName;
                regTable["func"] = consoleMethodInfo.Method;
                Script.Globals["__tmpRegItem"] = regTable; // Put method's parameters (alias,fullName,CS static method) needed for AddToCommandRegister function on Lua side
                Script.DoString("AddToCommandRegister(__tmpRegItem.alias, __tmpRegItem.fullName, __tmpRegItem.func)");
            }

            Script.DoString("__tmpRegItem = nil"); // Keep global namespace clean
            LogHelpers.Log.Print(LogChecker.Level.Normal, $"Registered {_consoleSystem.Methods.Count} console commands");
        }

        private void AddVariablesToRegistryTable()
        {
            foreach (var consoleVarInfo in _consoleSystem.Variables)
            {
                var regTable = new Table(Script);
                regTable["alias"] = consoleVarInfo.AliasName;
                regTable["fullName"] = consoleVarInfo.FullName;
                regTable["getter"] = consoleVarInfo.Property.GetGetMethod();
                regTable["setter"] = consoleVarInfo.Property.GetSetMethod();

                Script.Globals["__tmpRegItem"] = regTable; // Put method's parameters (alias,fullName,getter, setter) needed for AddToCommandRegister function on Lua side
                Script.DoString("AddToVariableRegister(__tmpRegItem.alias, __tmpRegItem.fullName, __tmpRegItem.getter,__tmpRegItem.setter)");
            }

            Script.DoString("__tmpRegItem = nil"); // Keep global namespace clean
            LogHelpers.Log.Print(LogChecker.Level.Normal, $"Registered {_consoleSystem.Variables.Count} console variables");
        }

        private void RegisterCommandsAndVariable()
        {
            Script.DoString("Register()");
        }

        public void ExecuteString(string luaCode)
        {
            try
            {
                if (string.IsNullOrEmpty(luaCode))
                    return;
                if( CountLines(luaCode) > 1)
                    Debug.Log($">>\n{luaCode}");
                else
                    Debug.Log($">>{luaCode}");

                var dVal = Script.DoString(luaCode);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static int CountLines(string inputString)
        {
            if (string.IsNullOrEmpty(inputString))
                return 0;

            int lineCount = 1; // If the inputString is not empty, there is at least one line.

            for (int i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] == '\n')
                    lineCount++;
            }

            return lineCount;
        }
    }
}