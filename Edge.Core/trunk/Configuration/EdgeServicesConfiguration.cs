﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Edge.Core.Configuration
{

	/// <summary>
    /// Represents the configuration section of the Edge services framework.
    /// </summary>
    public class EdgeServicesConfiguration : ConfigurationSection
    {
		#region Wrapper
		//===================

		public static string DefaultFileName = "Edge.Services.config";
 		public static string DefaultSectionName = "edge.services";

		private static EdgeServicesConfiguration _current = null;

		public static EdgeServicesConfiguration Current
		{
			get
			{
				/*
				if (_current == null)
				{
					// Auto load default config file
					Load(DefaultFileName, DefaultSectionName, true);
				}
				*/
				return _current;
			}
			private set { _current = value; }
		}

		public static string CurrentFileName
		{
			get;
			private set;
		}

		/// <summary>
		/// Loads the services configuration 'edge.services' from the current application's app.config file.
		/// </summary>
		public static void Load()
		{
			Load(null);
		}

		/// <summary>
		/// Loads the services configuration from the specified file.
		/// </summary>
		/// <param name="configFileName">Path to configuration file, relative to current working directory. If null, uses current application's app.config file.</param>
		/// <param name="sectionName">The name of the section to load, default (if null) is 'edge.services'.</param>
		public static void Load(string configFileName, string sectionName = null, bool readOnly = true)
		{
			if (sectionName == null)
				sectionName = DefaultSectionName;

			string loadErrorMsg = String.Format("Could not find configuration section '{0}'.", sectionName);
			if (configFileName == null)
			{
				Current = (EdgeServicesConfiguration)ConfigurationManager.GetSection(sectionName);
				if (Current == null)
					throw new ConfigurationErrorsException(loadErrorMsg);
				CurrentFileName = null;
			}
			else
			{
				var fileMap = new ExeConfigurationFileMap() { ExeConfigFilename = configFileName };
				System.Configuration.Configuration configFile = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

				Current = (EdgeServicesConfiguration)configFile.GetSection(sectionName);
				if (Current == null)
					throw new ConfigurationErrorsException(loadErrorMsg);

				Current.ConfigurationFile = configFile;
				CurrentFileName = configFileName;

				if (readOnly)
					Current.SetReadOnly();
			}
		}


		//==================
		#endregion

		#region Section
		//===================

		#region Fields

        private static ConfigurationPropertyCollection s_properties;
        private static ConfigurationProperty s_services;
        private static ConfigurationProperty s_accounts;
		private static ConfigurationProperty s_extensions;

		bool _loading = true;
		ExtensionElementCollection _extensions;
		
		#endregion

        #region Constructor
		static EdgeServicesConfiguration()
        {
            s_services = new ConfigurationProperty(
                "Services",
                typeof(ServiceElementCollection),
                null,
                ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsDefaultCollection);

            s_accounts = new ConfigurationProperty(
                "Accounts",
                typeof(AccountElementCollection),
                null,
                ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsDefaultCollection);

			s_extensions = new ConfigurationProperty(
			   "Extensions",
			   typeof(ExtensionElementCollection),
			   null);
			
			s_properties = new ConfigurationPropertyCollection();
            s_properties.Add(s_services);
			s_properties.Add(s_accounts);
			s_properties.Add(s_extensions);
       }
        #endregion

        #region Properties
        public ServiceElementCollection Services
        {
            get { return (ServiceElementCollection)base[s_services]; }
        }

        public AccountElementCollection Accounts
        {
            get { return (AccountElementCollection)base[s_accounts]; }
        }

		public ExtensionElementCollection Extensions
		{
			get
			{
				if (_loading)
					return _extensions;
				else
					return (ExtensionElementCollection) base[s_extensions];
			}
			internal set
			{
				if (!_loading)
					throw new InvalidOperationException();
				_extensions = value;
			}
		}
		
		public AccountElement SystemAccount
		{
			get { return Accounts.GetAccount(-1); }
		}

		public bool IsLoading
		{
			get { return _loading; }
		}

		public System.Configuration.Configuration ConfigurationFile
		{
			get;
			private set;
		}

		protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return s_properties;
            }
        }

        #endregion

		#region Internal Methods

		protected override void PostDeserialize()
		{
			base.PostDeserialize();
			foreach (ServiceElement service in this.Services)
			{
				service.ResolveReferences(this.Services, null);
			}
			foreach (AccountElement account in this.Accounts)
			{
				account.ResolveReferences(this.Services, null);
			}

			// Done loading
			_loading = false;
		}
		
		#endregion

		//==================
		#endregion
	}

}
