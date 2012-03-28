using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Reflection;
using System.ComponentModel;
using System.Xml;
using System.IO;

namespace Edge.Core.Configuration
{
	#region Enums
	/*=========================*/
	public enum ServiceType
	{
		Executable=0,
		Class=1,
		Workflow=2
	};

	public enum FailureOutcome
	{
		Unspecified=0,
		Terminate=1,
		Continue=2,
		Handler=3
	}

	public enum CalendarUnit
	{
		Month=0,
		Week=1,
		Day=2
	}

	/*=========================*/
	#endregion

	#region Classes
	/*=========================*/

	public class ElementReference<ElementT> where ElementT: NamedConfigurationElement
	{
		internal string Value;
		ElementT _elem;

		/// <summary>
		/// Used as empty reference
		/// </summary>
		public ElementReference()
		{
			Value = null;
			_elem = null;
		}

		/// <summary>
		/// This type of reference is creating during deserialization, the reference is resolved
		/// during post-deserialization
		/// </summary>
		internal ElementReference(string value)
		{
			Value = value;
			_elem = null;
		}

		// This type of reference is for runtime setting of a reference
		public ElementReference(ElementT element)
		{
			Value = element == null ? null : element.Name;
			_elem = element;
		}

		// The actual reference
		public ElementT Element
		{
			get { return _elem; }
			internal set { _elem = value; }
		}

		// Compares the reference
		public override bool Equals(object obj)
		{
			if (!(obj is ElementReference<ElementT>))
				return false;

			ElementReference<ElementT> otherRef = (ElementReference<ElementT>) obj;
			return otherRef.Element == this.Element;
		}

		public override int GetHashCode()
		{
			return Value == null ? 0 : Value.GetHashCode();
		}
	}
	
	///// <summary>
	///// 
	///// </summary>
	//public abstract class ConfigurationExtender
	//{
	//    public virtual ConfigurationElement CreateElement(ServiceElement service, string name, XmlReader reader)
	//    {
	//        return null;
	//    }
	//}

	public interface ISerializableConfigurationElement
	{
		void Deserialize(XmlReader reader);
		void Serialize(XmlWriter writer, string elementName);
	}

	public interface IServiceReferencingConfigurationElement
	{
		void ResolveReferences(ServiceElementCollection services, ServiceElement service);
	}

    /* Configuration File Changes Monitor */
    public delegate void ConfigurationChangedEvent(object sender, ConfigurationChangedEventArgs e);

    /// <summary>
    /// Provides the means to monitor changes in configuration files (.config files).
    /// </summary>
    ///
    /// <remarks>
    /// The configuration watcher class is a class which allows the monitoring of configuration files. When
    /// a configuration file changes, the configuration watcher will issue an event to the consumer with the relevant
    /// information. A user can monitor multiple files (i.e. all configuration files in a specific directory), or a
    /// separate watcher can be created for a specific file.
    /// </remarks>
    /// <example>
    /// The following example shows how to initialize the ConfigurationWatcher for a specific file.
    /// <code>
    /// try
    /// {
    ///     //Create the ConfigurationWatcher, and give it a specific file name.
    ///     string _configFileName = @"c:\tests\bin\services.exe.config
    ///     
    ///     //Configuration watcher is enabled by default.
    ///     ConfigurationWatcher _configWatcher = new ConfigurationWatcher(_configFileName);
    ///     _configWatcher.ConfigurationChanged += new ConfigurationChangedEvent(_ConfigurationChanged);
    /// }
    /// catch (Exception ex)
    /// {
    /// ...
    /// }
    /// </code>
    /// </example>
    public class ConfigurationWatcher
    {

        #region Events
        /// <summary>
        /// Event to send out when the configuration file has changed.
        /// </summary>
        public event ConfigurationChangedEvent ConfigurationChanged;
        #endregion

        #region Members
        private FileSystemWatcher _fsw = null;
        private string _fileName = string.Empty;
        #endregion

        #region Constructors
        /// <summary>
        /// Basic constructor
        /// </summary>
        public ConfigurationWatcher()
        {
        }

        /// <summary>
        /// Constructor - receives the path to monitor. This would monitor multiple configuration
        /// files in a specific path.
        /// </summary>
        /// <param name="path">The path to monitor</param>
        /// <param name="subdir">Include subdirectories</param>
        public ConfigurationWatcher(string path, bool subdir)
        {
            _fsw = new FileSystemWatcher(path, "*.config");
            _fsw.NotifyFilter = NotifyFilters.LastWrite;
            _fsw.IncludeSubdirectories = subdir;
            _fsw.Changed += new FileSystemEventHandler(_fsw_Changed);
            _fsw.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Constructor - will monitor a specific configuration file.
        /// </summary>
        /// <param name="fileName"></param>
        public ConfigurationWatcher(string fileName)
        {
            if (fileName != null)
            {
                _fileName = fileName;
                _fsw = new FileSystemWatcher();
                _fsw.Filter = fileName;
                _fsw.NotifyFilter = NotifyFilters.LastWrite;
                _fsw.Changed += new FileSystemEventHandler(_fsw_Changed);
                _fsw.EnableRaisingEvents = true;
            }
        }
        #endregion

        #region File System Watcher Event
        /// <summary>
        /// Called when the file system watcher detects a change in the relevant monitoring element.
        /// </summary>
        /// <param name="sender">The file system watcher sending the event</param>
        /// <param name="e">The event arguments</param>
        void _fsw_Changed(object sender, FileSystemEventArgs e)
        {
            ConfigurationChangedEventArgs args = new ConfigurationChangedEventArgs(e.Name,e.FullPath);
            OnConfigurationChanged(args);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enables the configuration watcher.
        /// </summary>
        public void Enable()
        {
            if (_fsw != null)
                _fsw.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Disables the configuration watcher.
        /// </summary>
        public void Disable()
        {
            if (_fsw != null)
                _fsw.EnableRaisingEvents = false;
        }
        #endregion

        #region Event Handling
        /// <summary>
        /// The event handler function which would activate the delegate that sends out the event when the
        /// configuration file has changed.
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected virtual void OnConfigurationChanged(ConfigurationChangedEventArgs e)
        {
            if (ConfigurationChanged != null)
                ConfigurationChanged(this, e);
        }
        #endregion

    }

    /// <summary>
    /// Event arguments for ConfigurationWatcher.
    /// </summary>
    /// <remarks>
    /// Contains the event arguments for the ConfigurationWatcher class. Currently contains the file name and the full path,
    /// future versions should be able to monitor specific elements in specific files.
    /// </remarks>
    public class ConfigurationChangedEventArgs : System.EventArgs
    {

        #region Members
        //TODO: Potentially add the specific element that's changed. Would require the user to give us
        //      what to monitor at a specific configuration file.
        private string _fileName = string.Empty;
        private string _fullPath = string.Empty;
        #endregion

        #region Constructors
        public ConfigurationChangedEventArgs()
        {
        }

        public ConfigurationChangedEventArgs(string fileName, string fullPath)
        {
            _fileName = fileName;
            _fullPath = fullPath;
        }
        #endregion

        #region Public Properties
        public string FileName
        {
            set
            {
                _fileName = value;
            }
            get
            {
                return _fileName;
            }
        }

        public string FullPath
        {
            set
            {
                _fullPath = value;
            }
            get
            {
                return _fullPath;
            }
        }
        #endregion

    }
	/*=========================*/
	#endregion

}