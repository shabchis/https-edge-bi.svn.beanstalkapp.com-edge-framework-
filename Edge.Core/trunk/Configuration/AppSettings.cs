using System;
using System.Configuration;
using System.Reflection;

namespace Edge.Core.Configuration
{
	/// <summary>
	///	Provides easy access to solution configuration file (.config) settings.
	/// </summary>
	/// 
	/// <remarks>
	///	Configuration settings are defined in the appSettings section of Web.config or
	///	App.config. The standard format used in the solution is (full name of class) + (setting name).
	///	This allows grouping of settings based on the class that uses them, and simple access notation.
	/// </remarks>
	public class AppSettings
	{
		#region Fields
		/*=========================*/

		/// <summary>
		/// Stores the object that determines the
		/// prefix of the settings retrieved by the instance of the class.
		/// </summary>
		private object _caller;
		
		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/
		
		/// <summary>
		///	Creates a new configuration accessor for the type of the specified object.
		/// </summary>
		/// 
		/// <param name="caller">
		///	If caller is a System.Type, the name of the type it references is used;
		///	otherwise, the type of the object is used.
		/// </param>
		/// 
		/// <remarks>
		///	The full type name (including namespace) is used as the prefix of settings
		///	retrieved using Get.
		/// </remarks>
		/// 
		/// <example>
		///	The following code retrieves the settings with the prefix "System.String.".
		///	<code>
		///	// Create a dedicated configuration accessor
		///	Config strConfig = new Config(typeof(String));
		///	
		///	// Retrieves the setting "System.String.MinLength" and converts it to an integer value.
		///	int minLength = Int32.Parse(strConfig.Get("MinLength"));
		///	
		///	// Retrieves the setting "System.string.DefaultValue"
		///	string defaultValue = strConfig.Get("DefaultValue");
		///	</code>
		/// </example>
		public AppSettings(object caller)
		{
			_caller = caller;
		}


		/*=========================*/
		#endregion

		#region Static Methods
		/*=========================*/

		/// <summary>
		///	Gets a configuration setting using the full type name of the caller as a prefix.
		/// </summary>
		/// 
		/// <param name="caller">
		///	If caller is a System.Type, the name of the type it references is used;
		///	otherwise, the type of the object is used.
		/// </param>
		/// 
		/// <param name="setting">
		///	The name of the setting to retrieve, not including the prefix (which is the class name).
		///	</param>
		/// 
		/// <returns>
		///	The setting value.
		///	</returns>
		/// 
		/// <remarks>
		///	The method uses class hierarchy to find the requested setting. If a setting is not found
		///	using the specified type prefix, a setting with the base type name as a prefix is looked up.
		///	For example, if System.String.MySetting is not found, System.Object.MySetting will be looked up. 
		///	This allows derived classes to override their base class's configuration values without additional
		///	code.
		/// </remarks>
		/// 
		/// <example>
		///	The following code retrieves the settings with the prefix "System.String.".
		///	<code>
		///	// Retrieves the setting "System.String.MinLength" and converts it to an integer value.
		///	int minLength = Int32.Parse(Config.Get(typeof(String), "MinLength"));
		///	
		///	// Retrieves the setting "System.string.DefaultValue"
		///	string defaultValue = Config.Get(typeof(String), "DefaultValue");
		///	</code>
		/// </example>
		/// <exception cref="PT.Data.ConfigurationException">
		///	Thrown when the specified setting could not be found for any class up the hierarchy.
		/// </exception>
		public static string Get(object caller, string setting)
		{
			return Get(caller, setting, true);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="caller"></param>
		/// <param name="setting"></param>
		/// <param name="throwWhenNotFound"></param>
		/// <returns></returns>
		public static string Get(object caller, string setting, bool throwException)
		{
			// Get the type to start the search with - either the passed Type object or the type of the object passed
			Type targetType = caller is Type ? (Type) caller : caller.GetType();

			string originalKey = targetType.FullName + "." + setting;

			string settingKey = null;
			string val = null;

			// Go up the class hierarchy searching for requested config var
			while (val == null && targetType != null)
			{
				settingKey = targetType.FullName + "." + setting;
				val = ConfigurationManager.AppSettings[settingKey];

				// Nothing found, get the base class
				if (val == null)
					targetType = targetType.BaseType;
			}

			// Reached System.Object and nothing was found, throw an exception
			if (val == null && throwException)
				throw new ConfigurationErrorsException("Undefined configuration setting: " + originalKey);
			else
				return val;
		}

		
		/// <summary>
		/// Gets the setting with the exact specified name.
		/// </summary>
		/// 
		/// <param name="setting">
		/// The full name of the setting to retrieve. Does not need to contain a namespace prefix.
		/// </param>
		/// 
		/// <remarks>
		/// This method is equivalent to using the .NET Framework's ConfigurationSettings.AppSettings[setting].
		/// </remarks>
		/// 
		/// <returns>
		/// The setting value.
		/// </returns>
		public static string GetAbsolute(string setting)
		{
			return ConfigurationManager.AppSettings[setting];
		}

		
		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		/// <summary>
		/// Retrieves a setting with the current prefix.
		/// </summary>
		/// 
		/// <param name="setting">
		/// The name of the setting to retrieve, not including the prefix (which is the class name).
		/// </param>
		/// 
		/// <returns>
		///	The setting value.
		/// </returns>
		/// 
		/// <example>
		///	The following code retrieves the settings with the prefix "System.String.".
		///	<code>
		///	// Create a dedicated configuration accessor
		///	Config strConfig = new Config(typeof(String));
		///	
		///	// Retrieves the setting "System.String.MinLength" and converts it to an integer value.
		///	int minLength = Int32.Parse(strConfig.Get("MinLength"));
		///	
		///	// Retrieves the setting "System.string.DefaultValue"
		///	string defaultValue = strConfig.Get("DefaultValue");
		///	</code>
		/// </example>
		public string Get(string setting)
		{
			return Get(_caller, setting);
		}

		/*=========================*/
		#endregion
	}
}
