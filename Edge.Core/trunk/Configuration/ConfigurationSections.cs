using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Edge.Core.Configuration
{

	/// <summary>
    /// Represents a service section. The service section contains all of the services defined in the system.
    /// </summary>
    internal class ServicesSection : ConfigurationSection
    {
        #region Fields
        private static ConfigurationPropertyCollection s_properties;
        private static ConfigurationProperty s_services;
        private static ConfigurationProperty s_accounts;
		private static ConfigurationProperty s_extensions;
        #endregion

        #region Constructor
		static ServicesSection()
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
			   new ExtensionElementCollection());
			
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
			get { return (ExtensionElementCollection) base[s_extensions]; }
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
		}
		
		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class ServicesConfiguration
	{
		const string SectionName = "edge.services";
		static ServicesSection _section;
		static bool _loading = false;
		static ExtensionElementCollection _extensions;

		static void Load()
		{
			if (_section == null)
			{
				_loading = true;
				_section = (ServicesSection) ConfigurationManager.GetSection(SectionName);
				_loading = false;
			}
		}

		public static bool IsLoading
		{
			get { return _loading; }
		}

		public static AccountElement SystemAccount
		{
			get
			{
				return Accounts.GetAccount(-1);
			}
		}

		public static AccountElementCollection Accounts
		{
			get
			{
				if (_loading)
					return null;

				Load();
				return _section.Accounts;
			}
		}

		public static ServiceElementCollection Services
		{
			get
			{
				if (_loading)
					return null;

				Load();
				return _section.Services;
			}
		}

		public static ExtensionElementCollection Extensions
		{
			get
			{
				if (_loading)
				{
					return _extensions;
				}
				else
				{
					Load();
					return _section.Extensions;
				}
			}

			internal set
			{
				if (_loading)
				{
					_extensions = value;
				}
				else
				{
					throw new InvalidOperationException();
				}
			}
		}
	}

}
