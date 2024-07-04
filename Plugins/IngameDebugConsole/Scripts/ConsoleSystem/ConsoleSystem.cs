using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using MoonSharp;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;


namespace uconsole
{
    public class ConsoleSystem
    {
        public class ConsoleMethodInfo
        {
            public readonly MethodInfo Method;
            public readonly Type[] ParameterTypes;
            public readonly object Instance;
            public readonly string FullName;
            public readonly string AliasName;
            public readonly string Signature;
            public readonly string CmdDescription;
            public readonly string[] ParameterDescriptions;

            public ConsoleMethodInfo(MethodInfo method, Type[] parameterTypes, object instance, string fullName, string aliasName, string signature, string cmdDescription, string[] parameterDescriptions)
            {
                Method = method;
                ParameterTypes = parameterTypes;
                Instance = instance;
                FullName = fullName;
                AliasName = aliasName;
                Signature = signature;
                CmdDescription = cmdDescription;
                ParameterDescriptions = parameterDescriptions;
            }

            public bool IsValid()
            {
                return Method.IsStatic || (Instance != null && !Instance.Equals(null));
            }
        }

        public class ConsoleVariableInfo
        {
            public readonly PropertyInfo Property;
            public readonly object Instance;
            public readonly string FullName;
            public readonly string AliasName;
            public readonly string Signature;
            public readonly string Description;

            public ConsoleVariableInfo(PropertyInfo prop, object instance, string fullName, string aliasName, string signature, string description)
            {
                Property = prop;
                Instance = instance;
                FullName = fullName;
                AliasName = aliasName;
                Signature = signature;
                Description = description;
            }

            // public bool IsValid()
            // {
            //     return Property.GetSetMethod().IsStatic s || (Instance != null && !Instance.Equals(null));
            // }
        }

        public static ConsoleSystem Instance;
        public Executor Executor;

        // All the readable names of accepted types
        private static readonly Dictionary<Type, string> typeReadableNames = new Dictionary<Type, string>()
        {
            { typeof( string ), "String" },
            { typeof( bool ), "Boolean" },
            { typeof( int ), "Integer" },
            { typeof( uint ), "Unsigned Integer" },
            { typeof( long ), "Long" },
            { typeof( ulong ), "Unsigned Long" },
            { typeof( byte ), "Byte" },
            { typeof( sbyte ), "Short Byte" },
            { typeof( short ), "Short" },
            { typeof( ushort ), "Unsigned Short" },
            { typeof( char ), "Char" },
            { typeof( float ), "Float" },
            { typeof( double ), "Double" },
            { typeof( decimal ), "Decimal" }
        };


        public List<ConsoleMethodInfo> Methods { get; } = new(128);
        public List<(string, ConsoleMethodInfo)> MethodSearchTable { get; } = new(128);
        public List<ConsoleVariableInfo> Variables { get; } = new();

        // CompareInfo used for case-insensitive command name comparison
        internal static readonly CompareInfo caseInsensitiveComparer = new CultureInfo("en-US").CompareInfo;


        public ConsoleSystem()
        {
            AddCommandsAndVariables();
            UnityWrapperTypes.RegisterUnityWrapperTypes();
            UnityCustomConvertors.RegisterCustomConvertors();
            Executor = new Executor(this);
        }

        private void AddCommandsAndVariables()
        {
            #if UNITY_EDITOR || !NETFX_CORE
            // Find all [ConsoleMethod] functions
            // Don't search built-in assemblies for console methods since they can't have any
            string[] ignoredAssemblies = {
                "Unity",
                "System",
                "Mono.",
                "mscorlib",
                "netstandard",
                "TextMeshPro",
                "Microsoft.GeneratedCode",
                "I18N",
                "Boo.",
                "UnityScript.",
                "ICSharpCode.",
                "ExCSS.Unity",
#if UNITY_EDITOR
				"Assembly-CSharp-Editor",
                "Assembly-UnityScript-Editor",
                "nunit.",
                "SyntaxTree.",
                "AssetStoreTools",
#endif
			};
#endif

#if UNITY_EDITOR || !NETFX_CORE
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
#else
			foreach( Assembly assembly in new Assembly[] { typeof( DebugLogConsole ).Assembly } ) // On UWP, at least search this plugin's Assembly for console methods
#endif
            {
#if (NET_4_6 || NET_STANDARD_2_0) && (UNITY_EDITOR || !NETFX_CORE)
                if (assembly.IsDynamic)
                    continue;
#endif

                string assemblyName = assembly.GetName().Name;

#if UNITY_EDITOR || !NETFX_CORE
                bool ignoreAssembly = false;
                for (int i = 0; i < ignoredAssemblies.Length; i++)
                {
                    if (caseInsensitiveComparer.IsPrefix(assemblyName, ignoredAssemblies[i], CompareOptions.IgnoreCase))
                    {
                        ignoreAssembly = true;
                        break;
                    }
                }

                if (ignoreAssembly)
                    continue;
#endif

                try
                {
                    foreach (Type type in assembly.GetExportedTypes())
                    {
                        // Parse methods
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        {
                            foreach (object attribute in method.GetCustomAttributes(typeof(ConsoleMethodAttribute), false))
                            {
                                if (attribute is ConsoleMethodAttribute consoleMethod)
                                    AddCommand(consoleMethod.FullName, consoleMethod.AliasName, consoleMethod.Description, method, null, consoleMethod.ParameterDescriptions);
                            }
                        }

                        // Parse properties
                        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        {
                            foreach (object attribute in prop.GetCustomAttributes(typeof(ConsoleVariableAttribute), false))
                            {
                                if (attribute is ConsoleVariableAttribute consoleVariable)
                                    AddVariable(consoleVariable.FullName, consoleVariable.AliasName, consoleVariable.Description, prop, null);
                            }
                        }
                    }
                }
                catch (NotSupportedException) { }
                catch (System.IO.FileNotFoundException) { }
                catch (Exception e)
                {
                    Debug.LogError($"Couldn't search assembly for [ConsoleMethod] attributes: {assemblyName}\n{e}");
                }
            }

        }


        private void AddCommand(string commandFullName, string aliasName, string description, MethodInfo method, object instance, string[] parameterDescription)
        {
            commandFullName = commandFullName.ToLower().Trim();
            aliasName = aliasName.ToLower().Trim();

            // todo: also add to alias search table

            if (string.IsNullOrEmpty(commandFullName) || string.IsNullOrEmpty(aliasName))
            {
                Debug.LogError("Command name can't be empty!");
                return;
            }

            commandFullName = commandFullName.Trim();
            if (!IsValidAliasIdentifier(aliasName) ||  !IsValidFullNameIdentifier(commandFullName))
            {
                Debug.LogError("Command name must be a valid identifier name!");
                return;
            }

            // Fetch the parameters of the method
            ParameterInfo[] parameters = method.GetParameters();
            Assert.IsNotNull(parameters);

            // Store the parameter types in an array
            Type[] parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    Debug.LogError("Command can't have 'out' or 'ref' parameters");
                    return;
                }

                Type parameterType = parameters[i].ParameterType;
                if (IsParseableType(parameterType) || typeof(Component).IsAssignableFrom(parameterType) || parameterType.IsEnum || IsSupportedArrayType(parameterType))
                    parameterTypes[i] = parameterType;
                else
                {
                    Debug.LogError(string.Concat("Parameter ", parameters[i].Name, "'s Type ", parameterType, " isn't supported"));
                    return;
                }
            }

            var existingMethod = Methods.FirstOrDefault(x => (x.AliasName == aliasName || x.FullName == commandFullName));
            if (existingMethod != null)
            {
                Debug.LogError($"Method with such alias or command name is already registered: {commandFullName} {aliasName} ");
                return;
            }

            // Create the command
            var methodSignature = CreateMethodSignature(aliasName, commandFullName, parameters);
            var parameterDescriptions = CreateParameterDescriptions(parameters, parameterDescription);

            var methodInfo = new ConsoleMethodInfo(method, parameterTypes, instance, commandFullName, aliasName, methodSignature, description, parameterDescriptions);
            Methods.Add(methodInfo);
            MethodSearchTable.Add((commandFullName, methodInfo));
            MethodSearchTable.Add((aliasName, methodInfo));
        }

        private void AddVariable(string varFullName, string aliasName, string description, PropertyInfo prop, object instance)
        {
            varFullName = varFullName.ToLower().Trim();
            aliasName = aliasName.ToLower().Trim();


            if (string.IsNullOrEmpty(varFullName) || string.IsNullOrEmpty(aliasName))
            {
                Debug.LogError("Command name can't be empty!");
                return;
            }

            if (!IsValidAliasIdentifier(aliasName) || !IsValidFullNameIdentifier(varFullName))
            {
                Debug.LogError("Command name must be a valid identifier name!");
                return;
            }

            // Create the command
            string variableSignature = CreatePropSignature(aliasName, varFullName, description, prop);

            Variables.Add(new ConsoleVariableInfo(prop, instance, varFullName, aliasName, variableSignature, description));
        }

        private static string[] CreateParameterDescriptions(ParameterInfo[] parameters, string[] parameterDescription)
        {
            int index = 0;
            string[] res = new string[parameters.Length];
            foreach (var parameterInfo in parameters)
            {
                var description = index < parameterDescription.Length ? parameterDescription[index] : "No description;";
                var parameterName = $"{parameterInfo.Name}";
                res[index] = $"{GetTypeReadableName(parameterInfo.ParameterType)} {parameterName} - {description}";
                ++index;
            }

            return res;
        }

        private static string CreateMethodSignature(string aliasName, string commandFullName, ParameterInfo[] parameters)
        {
            StringBuilder sb = new StringBuilder(256);
            var index = 0;
            foreach (var parameterInfo in parameters)
            {
                var parameterName = parameterInfo.HasDefaultValue ? $"{parameterInfo.Name} = {parameterInfo.DefaultValue ?? "null"}" : $"{parameterInfo.Name}";
                sb.Append($"{GetTypeReadableName(parameterInfo.ParameterType)} {parameterName}");
                if (index != parameters.Length-1)
                    sb.Append(", ");
                ++index;
            }

            return $"{commandFullName}<{aliasName}>({sb.ToString()})";
        }


        private static string CreatePropSignature(string aliasName, string fullName, string description, PropertyInfo propInfo)
        {
            StringBuilder sb = new StringBuilder(256);
            
            sb.Append(fullName);
            sb.Append($"<{aliasName}> ");
            sb.Append(GetTypeReadableName(propInfo.PropertyType));
            sb.Append($" = {(propInfo.GetValue(null) ?? "null")}");


            if (propInfo.GetSetMethod() == null)
                sb.Append(" [readonly]");
            sb.Append(" - ");
            sb.Append(description);

            return sb.ToString();
        }



        public void SortMethodsTable()
        {
            Methods.Sort(
                (e1, e2) => String.Compare(e1.FullName, e2.FullName, StringComparison.Ordinal)
            );
        }

        public void PrepareSearchTable()
        {
            MethodSearchTable.Sort(
                (e1, e2) => String.Compare(e1.Item1, e2.Item1, StringComparison.Ordinal)
            );
        }

        // Find command's index in the list of registered commands using binary search
        private int FindCommandIndex(string command)
        {
            int min = 0;
            int max = Methods.Count - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                int comparison = caseInsensitiveComparer.Compare(command, Methods[mid].FullName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                if (comparison == 0)
                    return mid;
                else if (comparison < 0)
                    max = mid - 1;
                else
                    min = mid + 1;
            }

            return ~min;
        }

        public static bool IsSupportedArrayType(Type type)
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    return false;

                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() != typeof(List<>))
                    return false;

                type = type.GetGenericArguments()[0];
            }
            else
                return false;

            return IsParseableType(type) || typeof(Component).IsAssignableFrom(type) || type.IsEnum;
        }

        public static string GetTypeReadableName(Type type)
        {
            string result;
            if (typeReadableNames.TryGetValue(type, out result))
                return result;

            if (IsSupportedArrayType(type))
            {
                Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                if (typeReadableNames.TryGetValue(elementType, out result))
                    return result + "[]";
                else
                    return elementType.Name + "[]";
            }

            return type.Name;
        }

        public static bool IsParseableType(Type type)
        {
            return true;
        }

        public static bool IsValidAliasIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return char.IsLetter(name[0]) && name.All(chr => char.IsLetterOrDigit(chr) || chr == '_');
        }

        public static bool IsValidFullNameIdentifier(string name)
        {
            // todo:
            if (string.IsNullOrEmpty(name))
                return false;

            return true;
        }

    }
}