using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Edge.Core.Services.Configuration;
using System.Configuration;

namespace Edge.Core.Services.Configuration.Converters
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class BaseStringConverter: TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class TimeSpanArrayConverter: BaseStringConverter
	{
		internal static TimeSpanArrayConverter Instance = new TimeSpanArrayConverter();

		/// <summary>
		/// 
		/// </summary>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(TimeSpan[]);
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			// Null string converts to an empty array
			if (value == null)
				return new TimeSpan[0];

			// Split the input string
			string[] elements = ((string)value).Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries);
			
			// Attempt to parse each commma-delimited value as a TimeSpan
			List<TimeSpan> list = new List<TimeSpan>(); 
			foreach (string elem in elements)
			{
				TimeSpan val;
				if (TimeSpan.TryParse(elem, out val))
					list.Add(val);
			}

			return list.ToArray();
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;

			TimeSpan[] vals = (TimeSpan[]) value;
			StringBuilder s = new StringBuilder();
			for(int i = 0; i < vals.Length; i++)
			{
				s.Append(vals[i].ToString());
				if (i < vals.Length-1)
					s.Append(',');
			}
			
			return s.ToString();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class StringArrayConverter: BaseStringConverter
	{
		internal static StringArrayConverter Instance = new StringArrayConverter();

		/// <summary>
		/// 
		/// </summary>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string[]);
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			// Null string converts to an empty array
			if (value == null)
				return new string[0];

			// Split the input string
			string[] elements = ((string) value).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			// Attempt to parse each commma-delimited value as a string
			List<string> list = new List<string>();
			foreach (string elem in elements)
			{
				list.Add(elem);
			}

			return list.ToArray();
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;

			string[] vals = (string[]) value;
			StringBuilder s = new StringBuilder();
			for (int i = 0; i < vals.Length; i++)
			{
				s.Append(vals[i].ToString());
				if (i < vals.Length-1)
					s.Append(',');
			}

			return s.ToString();
		}
	}
	
	/// <summary>
	/// 
	/// </summary>
	public class Int32ArrayConverter: BaseStringConverter
	{
		internal static Int32ArrayConverter Instance = new Int32ArrayConverter();

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(int[]);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			// Null string converts to an empty array
			if (value == null)
				return new int[0];

			// Split the input string
			string[] elements = ((string) value).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			// Attempt to parse each commma-delimited value as a TimeSpan
			List<int> list = new List<int>();
			foreach (string elem in elements)
			{
				int val;
				if (int.TryParse(elem, out val))
					list.Add(val);
			}

			return list.ToArray();
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;

			int[] vals = (int[]) value;
			StringBuilder s = new StringBuilder();
			for (int i = 0; i < vals.Length; i++)
			{
				s.Append(vals[i].ToString());
				if (i < vals.Length-1)
					s.Append(',');
			}

			return s.ToString();
		}

	}

	#region Obsolete
	///// <summary>
	///// 
	///// </summary>
	//public class ConfigurationExtenderConverter: TypeConverter
	//{
	//    internal static ConfigurationExtenderConverter Instance = new ConfigurationExtenderConverter();

	//    /// <summary>
	//    /// 
	//    /// </summary>
	//    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
	//    {
	//        return destinationType == typeof(ConfigurationExtender);
	//    }

	//    /// <summary>
	//    /// 
	//    /// </summary>
	//    public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
	//    {
	//        // Null string converts to an empty array
	//        if (value == null)
	//            return null;

	//        Type t = Type.GetType(value.ToString(), false, true);
	//        if (t == null)
	//            throw new TypeLoadException("Extender class could not be loaded. Make sure the path is fully qualified type with assembly name.");
	//        else if (!t.IsSubclassOf(typeof(ConfigurationExtender)))
	//            throw new TypeLoadException("Extender class must inherit from " + typeof(ConfigurationExtender).ToString());

	//        return t.GetConstructor(System.Type.EmptyTypes).Invoke(new object[]{});
	//    }

	//    /// <summary>
	//    /// 
	//    /// </summary>
	//    public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
	//    {
	//        if (value == null)
	//            return null;

	//        return value.GetType().AssemblyQualifiedName;
	//    }
	//}

	/*
	/// <summary>
	/// 
	/// </summary>
	public class SettingsCollectionConverter: TypeConverter
	{
		internal static SettingsCollectionConverter Instance = new SettingsCollectionConverter();

		/// <summary>
		/// 
		/// </summary>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(SettingsCollection);
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			SettingsCollection col = new SettingsCollection();

			if (value == null)
				return col;

			col.Definition = value.ToString();

			return col;
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null || ((SettingsCollection)value).Count < 1)
				return null;

			return value.ToString();
		}
	}
	*/
	#endregion

	/// <summary>
	/// 
	/// </summary>
	/// 
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
	public class ElementReferenceConverter<ElementT>: BaseStringConverter where ElementT: NamedConfigurationElement
	{
		ReferencingConfigurationElement _element;
		string _property;

		public ElementReferenceConverter(ReferencingConfigurationElement element, string propertyName)
		{
			_property = propertyName;
			_element = element;
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(ElementT);
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			// Return a reference with only the name
			return new ElementReference<ElementT>(value != null && value.ToString().Trim().Length > 0 ?
				value.ToString().Trim():
				null
			);
		}

		/// <summary>
		/// 
		/// </summary>
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;

			// Return the referenced element's name, or null
			ElementReference<ElementT> reference = (ElementReference<ElementT>) value;
			return reference.Element != null ? 
				reference.Element.Name : 
				reference.Value;
		}
	}
}
