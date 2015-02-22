using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Threading;

namespace Eggplant.Persistence
{
	public class PersistenceProviderConfiguration: ConfigurationElement
	{
		public readonly Dictionary<string, string> CustomSettings;
	}

	[AttributeUsage(AttributeTargets.Class, Inherited=true, AllowMultiple=false)]
	sealed class RequiresCustomSettingsAttribute: Attribute
	{
		public readonly List<string> RequiredSettings;

		// This is a positional argument
		public RequiresCustomSettingsAttribute(params string[] configurationSettings)
		{
			RequiredSettings = new List<string>(configurationSettings);
		}
	}
	
	public partial class PersistenceProvider: IDisposable
	{
		public readonly PersistenceProviderConfiguration Configuration;

		public event EventHandler Opened;
		public event EventHandler Closed;

		private bool _open;

		public PersistenceProvider(PersistenceProvider provider, PersistenceProviderConfiguration configuration)
		{
			// Check required attribute
			object[] attr = this.GetType().GetCustomAttributes(typeof(RequiresCustomSettingsAttribute), true);
			if (attr.Length > 0)
			{
				RequiresCustomSettingsAttribute attribute = (RequiresCustomSettingsAttribute) attr[0];
				foreach (string setting in attribute.RequiredSettings)
				{
					// EXCEPTION:
					if (!configuration.CustomSettings.ContainsKey(setting))
						throw new ConfigurationErrorsException(String.Format("The custom configuration setting {0} is required.", setting));
				}
			}

			Configuration = configuration;
		}

		// Eggplant-TODO:
		//public PersistenceTransaction ActiveTransaction
		//{
		//}

		internal void Open()
		{
			InternalOpen();
			_open = true;

			if (Opened != null)
				Opened(this, EventArgs.Empty);
		}

		internal void Close()
		{
			InternalClose();
			_open = false;

			if (Closed != null)
				Closed(this, EventArgs.Empty);
		}

		protected virtual void InternalOpen()
		{
			throw new NotImplementedException();
		}

		protected virtual void InternalClose()
		{
			throw new NotImplementedException();
		}

		public bool IsOpen
		{
			get
			{
				return _open;
			}
		}

		void IDisposable.Dispose()
		{
			if (_open)
				this.Close();
		}

		protected void ExecuteCommand(IPersistable target, PersistenceCommand command)
		{
			//command.ApplyValues(target);
			//command.ExecuteAsQuery();
		}
	}

}
