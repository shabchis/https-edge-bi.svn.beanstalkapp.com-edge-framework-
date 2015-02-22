using System;
using System.Configuration;
using System.Reflection;
using System.Diagnostics;

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
	[DebuggerNonUserCode]
	public class AppSettings
	{
		#region Static
		/*=========================*/

		/// <summary>
		///	Gets a configuration setting using the full type name of the caller as a prefix.
		/// </summary>	
		public static string Get(object caller, string setting)
		{
			return Get(caller, setting, true);
		}

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
		///	int minLength = Int32.Parse(AppSettings.Get(typeof(String), "MinLength"));
		///	
		///	// Retrieves the setting "System.string.DefaultValue"
		///	string defaultValue = AppSettings.Get(typeof(String), "DefaultValue");
		///	</code>
		/// </example>
		/// <exception cref="PT.Data.ConfigurationException">
		///	Thrown when the specified setting could not be found for any class up the hierarchy.
		/// </exception>
		public static string Get(object caller, string setting, bool throwException = true, bool isConnectionString = false, System.Configuration.Configuration configFile = null)
		{
			string prefix;
			Type targetType = null;

			// Get the type to start the search with - either the passed Type object or the type of the object passed
			if (caller is string)
				prefix = (string)caller;
			else
			{
				targetType = caller is Type ? (Type)caller :
					caller.GetType();
				prefix = targetType.FullName;
			}

			string originalKey = prefix + "." + setting;

			string settingKey = null;
			string val = null;

			// Apply default configuration if necessary
			configFile = configFile ?? (EdgeServicesConfiguration.Current != null ? EdgeServicesConfiguration.Current.ConfigurationFile : null);

			// Go up the class hierarchy searching for requested config var
			while (val == null && prefix != null)
			{
				settingKey = prefix + "." + setting;

				// Try getting app setting/conn string from custom config file
				bool useExeConfig = true; 
					 
				if (configFile != null)
				{
					if (isConnectionString)
					{
						ConnectionStringSettings csEntry = configFile.ConnectionStrings.ConnectionStrings[settingKey];
						if (csEntry != null)
							val = csEntry.ConnectionString;
					}
					else
					{
						KeyValueConfigurationElement elem = configFile.AppSettings.Settings[settingKey];
						val = elem == null ? null : elem.Value;
					}

					if (val != null)
						useExeConfig = false;
				}

				// Fallback to exe config if nothing found in custom config file
				if (useExeConfig)
				{
					if (isConnectionString)
					{
						ConnectionStringSettings csEntry = ConfigurationManager.ConnectionStrings[settingKey];
						if (csEntry != null)
							val = csEntry.ConnectionString;
					}
					else
						val = ConfigurationManager.AppSettings[settingKey];
				}

				// Nothing found, get the base class
				if (val == null && targetType != null)
				{
					targetType = targetType.BaseType == typeof(object) ? null : targetType.BaseType;
					prefix = targetType == null ? null : targetType.FullName;
				}
			}

			// Reached System.Object and nothing was found, throw an exception
			if (val == null && throwException)
				throw new ConfigurationErrorsException(String.Format("Undefined {0}: {1}", isConnectionString ? "connection string" : "app setting", originalKey));
			else
				return val;
		}

		public static string GetConnectionString(object caller, string name, bool throwException = true, System.Configuration.Configuration configFile = null)
		{
			return Get(caller, name, throwException, true, configFile);
		}

		public static string GetConnectionString(object caller, string name)
		{
			return GetConnectionString(caller, name, true);
		}


		/*=========================*/
		#endregion

		#region Instance
		/*=========================*/

		private object _caller;
		private System.Configuration.Configuration _configFile;

		/// <summary>
		///	Creates a new configuration accessor for the type of the specified object.
		/// </summary>
		/// 
		/// <param name="caller">
		///	If caller is a System.Type, the name of the type it references is used; if a string, the string is used;
		///	otherwise, the type of the object is used.
		/// </param>
		/// 
		/// <remarks>
		///	The full type name (including namespace) is used as the prefix of settings
		///	retrieved using Get.
		/// </remarks>
		public AppSettings(object caller, System.Configuration.Configuration configFile)
		{
			_caller = caller;
			_configFile = configFile;
		}


		/// <summary>
		/// Retrieves a setting with the current prefix.
		/// </summary>
		public string Get(string setting)
		{
			return Get(_caller, setting, configFile: _configFile);
		}

		/// <summary>
		/// Retrieves a connection string with the current prefix.
		/// </summary>
		public string GetConnectionString(string name)
		{
			return Get(_caller, name, isConnectionString: true, configFile: _configFile);
		}

		/*=========================*/
		#endregion
	}
}
