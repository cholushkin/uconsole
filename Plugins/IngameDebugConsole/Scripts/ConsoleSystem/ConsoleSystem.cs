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
    public static class ConsoleSystem
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

            //public bool IsValid()
            //{
            //    return Property.GetSetMethod().IsStatic s || (Instance != null && !Instance.Equals(null));
            //}
        }

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


        public static List<ConsoleMethodInfo> Methods { get; } = new(128);
        public static List<(string, ConsoleMethodInfo)> MethodSearchTable { get; } = new(128);
        public static List<ConsoleVariableInfo> Variables { get; } = new();


        // CompareInfo used for case-insensitive command name comparison
        private static readonly CompareInfo caseInsensitiveComparer = new CultureInfo("en-US").CompareInfo;


        static ConsoleSystem()
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

            UnityWrapperTypes.RegisterUnityWrapperTypes();
            UnityCustomConvertors.RegisterCustomConvertors();
        }


        private static void AddCommand(string commandFullName, string aliasName, string description, MethodInfo method, object instance, string[] parameterDescription)
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

        private static void AddVariable(string varFullName, string aliasName, string description, PropertyInfo prop, object instance)
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



        public static void SortMethodsTable()
        {
            Methods.Sort(
                (e1, e2) => String.Compare(e1.FullName, e2.FullName, StringComparison.Ordinal)
            );
        }

        public static void PrepareSearchTable()
        {
            MethodSearchTable.Sort(
                (e1, e2) => String.Compare(e1.Item1, e2.Item1, StringComparison.Ordinal)
            );
        }

        // Find command's index in the list of registered commands using binary search
        private static int FindCommandIndex(string command)
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



        //{
        //    public:

        //        /*!
        //         * \brief Initialize system object
        //         */
        //        System();

        ///*!
        // * \brief
        // *      Move constructor
        // * \param rhs
        // *      System to be copied.
        // */
        //System(System && rhs) = default;

        ///*!
        // * \brief
        // *      Copy constructor
        // * \param rhs
        // *      System to be copied.
        // */
        //System(const System &rhs);

        ///*!
        // * \brief
        // *      Move assignment operator
        // * \param rhs
        // *      System to be copied.
        // */
        //System &operator=(System &&rhs) = default;

        ///*!
        // * \brief
        // *      Copy assigment operator.
        // * \param rhs
        // *      System to be copied.
        // */
        //System &operator=(const System &rhs);

        ///*!
        // * \brief
        // *      Parse given command line input and run it
        // * \param line
        // *      Command line string
        // */
        //void RunCommand(const std::string &line);

        ///*!
        // * \brief
        // *      Get console registered command autocomplete tree
        // * \return
        // *      Autocomplete Ternary Search Tree
        // */
        //AutoComplete & CmdAutocomplete();

        ///*!
        // * \brief
        // *      Get console registered variables autocomplete tree
        // * \return
        // *      Autocomplete Ternary Search Tree
        // */
        //AutoComplete & VarAutocomplete();

        ///*!
        // * \brief
        // *      Get command history container
        // * \return
        // *      Command history vector
        // */
        //CommandHistory & History();

        ///*!
        // * \brief
        // *      Get console items
        // * \return
        // *      Console items container
        // */
        //std::vector<Item> & Items();



        ///*!
        // * \brief
        // *      Creates a new item entry to log information
        // * \param type
        // *      Log type (COMMAND, LOG, WARNING, CSYS_ERROR)
        // * \return
        // *      Reference to console items obj
        // */
        //ItemLog & Log(ItemType type = ItemType::LOG);

        ///*!
        // * \brief
        // *      Run the given script
        // * \param script_name
        // *      Script to be executed
        // *
        // *  \note
        // *      If script exists but its not loaded, this methods will load the script and proceed to run it.
        // */
        //void RunScript(const std::string &script_name);

        ///*!
        // * \brief
        // *      Get registered command container
        // * \return
        // *      Commands container
        // */
        //std::unordered_map < std::string, std::unique_ptr < CommandBase >> &Commands();

        ///*!
        // * \brief
        // *      Get registered scripts container
        // * \return
        // *      Scripts container
        // */
        //std::unordered_map < std::string, std::unique_ptr < Script >> &Scripts();

        ///*!
        // * \brief
        // *      Registers a command within the system to be invokable
        // * \tparam Fn
        // *      Decltype of the function to invoke when command is ran
        // * \tparam Args
        // *      List of arguments that match that of the argument list within the function Fn of type csys::Arg<T>
        // * \param name
        // *      Non-whitespace separating name of the command. Whitespace will be dropped
        // * \param description
        // *      Description describing what the command does
        // * \param function
        // *      A non-member function to run when command is called
        // * \param args
        // *      List of csys::Arg<T>s that matches that of the argument list of 'function'
        // */
        //template < typename Fn, typename...Args >
        // void RegisterCommand(const String &name, const String &description, Fn function, Args... args)
        //        {
        //    // Check if function can be called with the given arguments and is not part of a class
        //    static_assert(std::is_invocable_v < Fn, typename Args::ValueType...>, "Arguments specified do not match that of the function");
        //    static_assert(!std::is_member_function_pointer_v<Fn>, "Non-static member functions are not allowed");

        //    // Move to command
        //    size_t name_index = 0;
        //    auto range = name.NextPoi(name_index);

        //    // Command already registered
        //    if (m_Commands.find(name.m_String) != m_Commands.end())
        //        throw csys::Exception("ERROR: Command already exists");

        //    // Check if command has a name
        //    else if (range.first == name.End())
        //    {
        //        Log(CSYS_ERROR) << "Empty command name given" << csys::endl;
        //        return;
        //    }

        //    // Get command name
        //    std::string command_name = name.m_String.substr(range.first, range.second - range.first);

        //    // Command contains more than one word
        //    if (name.NextPoi(name_index).first != name.End())
        //        throw csys::Exception("ERROR: Whitespace separated command names are forbidden");

        //    // Register for autocomplete.
        //    if (m_RegisterCommandSuggestion)
        //    {
        //        m_CommandSuggestionTree.Insert(command_name);
        //        m_VariableSuggestionTree.Insert(command_name);
        //    }

        //    // Add commands to system
        //    m_Commands[name.m_String] = std::make_unique < Command < Fn, Args...>> (name, description, function, args...);

        //    // Make help command for command just added
        //    auto help = [this, command_name]() {
        //        Log(LOG) << m_Commands[command_name]->Help() << csys::endl;
        //    };

        //    m_Commands["help " + command_name] = std::make_unique < Command < decltype(help) >> ("help " + command_name,
        //                                                                                   "Displays help info about command " +
        //                                                                                   command_name, help);
        //}

        ///*!
        // * \brief
        // *      Register's a variable within the system
        // * \tparam T
        // *      Type of the variable
        // * \tparam Types
        // *      Type of arguments that type T can be constructed with
        // * \param name
        // *      Name of the variable
        // * \param var
        // *      The variable to register
        // * \param args
        // *      List of csys::Arg to be used for the construction of type T
        // * \note
        // *      Type T requires an assignment operator, and constructor that takes type 'Types...'
        // *      Param 'var' is assumed to have a valid life-time up until it is unregistered or the program ends
        // */
        //template < typename T, typename...Types >
        // void RegisterVariable(const String &name, T &var, Arg<Types>... args)
        //        {
        //    static_assert(std::is_constructible_v < T, Types...>, "Type of var 'T' can not be constructed with types of 'Types'");
        //    static_assert(sizeof... (Types) != 0, "Empty variadic list");

        //    // Register get command
        //    auto var_name = RegisterVariableAux(name, var);

        //    // Register set command
        //    auto setter = [&var](Types... params){ var = T(params...); };
        //    m_Commands["set " + var_name] = std::make_unique < Command < decltype(setter), Arg < Types > ...>> ("set " + var_name,
        //                                                                                        "Sets the variable " + var_name,
        //                                                                                        setter, args...);
        //}

        ///*!
        // * \brief
        // *      Register's a variable within the system
        // * \tparam T
        // *      Type of the variable
        // * \tparam Types
        // *      Type of arguments that type T can be constructed with
        // * \param name
        // *      Name of the variable
        // * \param var
        // *      The variable to register
        // * \param setter
        // *      Custom setter that runs when command set 'name' is invoked
        // * \note
        // *      The setter must have dceltype of void(decltype(var)&, Types...)
        // *      Param 'var' is assumed to have a valid life-time up until it is unregistered or the program ends
        // */
        //template < typename T, typename...Types >
        // void RegisterVariable(const String &name, T &var, void(*setter)(T &, Types...))
        //        {
        //    // Register get command
        //    auto var_name = RegisterVariableAux(name, var);

        //    // Register set command
        //    auto setter_l = [&var, setter](Types... args){ setter(var, args...); };
        //    m_Commands["set " + var_name] = std::make_unique < Command < decltype(setter_l), Arg < Types > ...>> ("set " + var_name,
        //                                                                                        "Sets the variable " + var_name,
        //                                                                                         setter_l, Arg<Types>("")...);
        //}

        ///*!
        // * \brief
        // *      Register script into console system
        // * \param name
        // *      Script name
        // * \param path
        // *      Scrip path
        // */
        //void RegisterScript(const std::string &name, const std::string &path);

        ///*!
        // * \brief
        // *      Unregister command from console system
        // * \param cmd_name
        // *      Command to unregister
        // */
        //void UnregisterCommand(const std::string &cmd_name);

        ///*!
        // * \brief
        // *      Unregister variable from console system
        // * \param var_name
        // *      Variable to unregister
        // */
        //void UnregisterVariable(const std::string &var_name);

        ///*!
        // * \brief
        // *      Unregister script from console system
        // * \param script_name
        // *      Script to unregister
        // */
        //void UnregisterScript(const std::string &script_name);

        //protected:
        //        template < typename T >
        //          std::string RegisterVariableAux(const String &name, T &var)
        //{
        //    // Disable.
        //    m_RegisterCommandSuggestion = false;

        //    // Make sure only one word was passed in
        //    size_t name_index = 0;
        //    auto range = name.NextPoi(name_index);
        //    if (name.NextPoi(name_index).first != name.End())
        //        throw csys::Exception("ERROR: Whitespace separated variable names are forbidden");

        //    // Get variable name
        //    std::string var_name = name.m_String.substr(range.first, range.second - range.first);

        //    // Get Command
        //    const auto GetFunction = [this, &var]() {
        //        m_ItemLog.log(LOG) << var << endl;
        //    };

        //    // Register get command
        //    m_Commands["get " + var_name] = std::make_unique < Command < decltype(GetFunction) >> ("get " + var_name,
        //                                                                                     "Gets the variable " +
        //                                                                                     var_name, GetFunction);

        //    // Enable again.
        //    m_RegisterCommandSuggestion = true;

        //    // Register variable
        //    m_VariableSuggestionTree.Insert(var_name);

        //    return var_name;
        //}

        //void ParseCommandLine(const String &line);                                   //!< Parse command line and execute command

        //std::unordered_map < std::string, std::unique_ptr < CommandBase >> m_Commands;    //!< Registered command container
        //AutoComplete m_CommandSuggestionTree;                                        //!< Autocomplete Ternary Search Tree for commands
        //AutoComplete m_VariableSuggestionTree;                                       //!< Autocomplete Ternary Search Tree for registered variables
        //CommandHistory m_CommandHistory;                                             //!< History of executed commands
        //ItemLog m_ItemLog;                                                           //!< Console Items (Logging)
        //std::unordered_map < std::string, std::unique_ptr < Script >> m_Scripts;          //!< Scripts
        //bool m_RegisterCommandSuggestion = true;                                     //!< Flag that determines if commands will be registered for autocomplete.
    }
}