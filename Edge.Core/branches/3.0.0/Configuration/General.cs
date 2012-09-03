using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Reflection;
using System.ComponentModel;
using System.Xml;
using System.IO;
using System;

namespace Edge.Core.Services.Configuration
{
	#region Enums
	/*=========================*/
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
	public enum ServiceType
	{
		Executable=0,
		Class=1,
		Workflow=2
	};
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
	public enum FailureOutcome
	{
		Unspecified=0,
		Terminate=1,
		Continue=2,
		Handler=3
	}

	/*=========================*/
	#endregion

	#region Classes
	/*=========================*/
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
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
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
	public interface ISerializableConfigurationElement
	{
		void Deserialize(XmlReader reader);
		void Serialize(XmlWriter writer, string elementName);
	}
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
	public interface IServiceReferencingConfigurationElement
	{
		void ResolveReferences(ServiceElementCollection services, ServiceElement service);
	}

	/*=========================*/
	#endregion

}