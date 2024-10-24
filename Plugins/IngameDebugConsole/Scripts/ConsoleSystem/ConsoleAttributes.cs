using System;

namespace uconsole
{
	[AttributeUsage( AttributeTargets.Method, Inherited = false, AllowMultiple = true )]
	public class ConsoleMethodAttribute : Attribute
	{
		public string FullName { get; } // Full name including path stored in nested tables in Lua
		public string AliasName { get; } // Short global alias name 
		public string Description { get; }
		public string[] ParameterDescriptions { get; }

		
		// todo: support null aliasName
		public ConsoleMethodAttribute( string fullName, string aliasName, string description, params string[] parameterDescriptions)
		{
			FullName = fullName;
			AliasName = aliasName;
			Description = description;
			ParameterDescriptions = parameterDescriptions;
		}
	}


	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	public class ConsoleVariableAttribute : Attribute
	{
		public string FullName { get; } // Full name including path stored in nested tables in Lua
		public string AliasName { get; } // Short global alias name 
		public string Description { get; }

		public ConsoleVariableAttribute(string fullName, string aliasName, string description)
		{
			FullName = fullName;
			AliasName = aliasName;
			Description = description;
		}
	}
}