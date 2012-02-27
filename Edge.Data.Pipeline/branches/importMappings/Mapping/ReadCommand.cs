using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Dynamic;

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

		public string Name
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

		void CreateRegex()
		{
			if (!String.IsNullOrWhiteSpace(this.RegexPattern))
			{
				_regex = new Regex(_fixRegex.Replace(this.RegexPattern, _fixReplace), RegexOptions.ExplicitCapture);

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
				if (context.Root.OnFieldRead == null)
					throw new MappingException("MappingConfiguration.OnFieldRead is not set - you must supply a function that will return the requested field value.");

				rawValue = context.Root.OnFieldRead(this.Field);
				context.FieldValues.Add(this.Field, rawValue);
			}

			ReadResult result;
			if (!context.ReadResults.TryGetValue(this, out result))
			{
				// Process regular expressions
				result = new ReadResult() { FieldValue = rawValue.ToString() };
				Match m = _regex.Match(result.FieldValue);
				if (_regex != null && rawValue != null)
				{
					if (m.Success)
					{
						foreach (string fragment in this.RegexFragments)
						{
							Group g = m.Groups[fragment];
							if (g.Success)
								((dynamic)result)[fragment] = g.Value;
						}
					}
				}
				context.ReadResults.Add(this, result);
			}
		}
	}

	public class ReadResult : DynamicDictionaryObject	
	{
		public string FieldValue;

		public override string ToString()
		{
			return FieldValue;
		}
	}
}
