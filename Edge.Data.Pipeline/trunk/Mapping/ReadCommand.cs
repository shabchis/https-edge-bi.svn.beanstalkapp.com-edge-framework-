using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Dynamic;
using Edge.Core.Utilities;
using System.ComponentModel;

namespace Edge.Data.Pipeline.Mapping
{
	/// <summary>
	/// 
	/// </summary>
	public class ReadCommand
	{

		string _name;
		string _field;
		string _regexString;
		Regex _regex = null;
		string[] _fragments = null;
		internal string[] RawGroupNames = null;

		static Regex _fixRegex = new Regex(@"\(\?\{(\w+)\}");
		static string _fixReplace = @"(?<$1>";
		static Regex _varName = new Regex("^[A-Za-z_][A-Za-z0-9_]*$");

		public string VarName
		{
			get { return _name; }
			set
			{
				if (!_varName.IsMatch(value))
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid read command name. Use C# variable naming rules.", value));

				_name = value;
			}
		}

		public string Field
		{
			get { return _field; }
			set { _field = value; }
		}

		/// <summary>
		/// Indicates whether this command is optional, i.e. will not throw an exception if it fails.
		/// </summary>
		public bool IsRequired { get; internal set; }

        /// <summary>
        /// Indicates whether this command is optional, i.e. will not throw an exception if it fails but will write error to log.
        /// </summary>
        public bool IsRequiredAlert { get; internal set; }

		/// <summary>
		/// Indicates whether this read command is implicit (part of a &lt;Map&gt; command).
		/// </summary>
		public bool IsImplicit { get; internal set; }

		public string RegexPattern
		{
			get { return _regexString; }
			set
			{
				_regexString = value;
				_regex = null;
				_fragments = null;
				RawGroupNames = null;
				CreateRegex();
			}
		}

		public Regex Regex
		{
			get { return _regex; }
		}

		public string[] RegexFragments
		{
			get { return _fragments; }
		}

		public override string ToString()
		{
			return this.VarName;
		}

		void CreateRegex()
		{
			if (!String.IsNullOrWhiteSpace(this.RegexPattern))
			{
				_regex = new Regex(_fixRegex.Replace(this.RegexPattern, _fixReplace), RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

				// skip the '0' group which is always first, the asshole
				string[] groupNames = this.Regex.GetGroupNames();
				this.RawGroupNames = groupNames.Length > 0 ? groupNames.Skip(1).ToArray() : groupNames;

				List<string> frags = new List<string>();
				foreach (string frag in this.RawGroupNames)
				{
					if (_varName.IsMatch(frag))
						frags.Add(frag);
					else
						throw new MappingConfigurationException(String.Format("'{0}' is not a valid read command fragment name. Use C# variable naming rules.", frag));
				}
				_fragments = frags.ToArray();
			}
		}

		internal static bool IsValidVarName(string name)
		{
			return _varName.IsMatch(name);
		}

		internal void Read(MappingContext context)
		{
			// Read from source if necessary
			object rawValue;
			if (!context.FieldValues.TryGetValue(this.Field, out rawValue))
			{
				if (context.Root.OnFieldRequired == null)
					throw new MappingException("MappingConfiguration.OnFieldRequired is not set - you must supply a function that will return the required field value.");
				
				try { rawValue = context.Root.OnFieldRequired(this.Field); }
				catch (Exception ex)
				{
                    if (this.IsRequired)
                        throw new MappingException(String.Format("Failed to read field '{0}'. See inner exception for details.", this.Field), ex);
                    else if (this.IsRequiredAlert)
                    {
                        Log.Write(String.Format("Failed to read field '{0}'.", this.Field), ex);
                        return;
                    }
                    else return;
				}
				context.FieldValues.Add(this.Field, rawValue);
			}

			ReadResult result;
			if (!context.ReadResults.TryGetValue(this, out result))
			{
				// Process regular expressions
				result = new ReadResult() { FieldValue = rawValue == null ? null : rawValue.ToString() };
				bool add = true;
				
				if (_regex != null && rawValue != null)
				{
					Match m = _regex.Match(result.FieldValue);
					if (m.Success)
					{
						foreach (string fragment in this.RegexFragments)
						{
							Group g = m.Groups[fragment];
							if (g.Success)
								((dynamic)result)[fragment] = g.Value;
						}
					}
					else
					{
						if (this.IsRequired)
							throw new MappingException(String.Format("The regular expression '{0}' for required read command '{1}' failed.", this.RegexPattern, this.VarName));
						else
							add = false;
					}
				}
				
				if (add)
					context.ReadResults.Add(this, result);
			}
		}
	}
	
	[TypeConverter(typeof(ReadResultConverter))]
	public class ReadResult : DynamicDictionaryObject	
	{
		public string FieldValue;

		public override string ToString()
		{
			//if(this.Values.Count == 1)
			//{
			//    object val = this.Values.First().Value;
			//    return val == null ? null : val.ToString();
			//}
			//else
				return this.FieldValue;
		}

		public static implicit operator string(ReadResult result)
		{
			return result == null ? null : result.ToString();
		}
	}

	public class ReadResultConverter : TypeConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(string))
				return true;
			else
				return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
				return ((ReadResult)value).ToString();
			else
				return base.ConvertTo(context, culture, value, destinationType);
		}
	}
}
