using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
		static Regex _fragmentNameInvalid = new Regex(@"^\d+$");

		public string Name
		{
			get { return _name; }
			set
			{
				if (!Regex.IsMatch(value, "[A-Za-z_][A-Za-z0-9_]*"))
					throw new MappingException(String.Format("The read command name '{0}' is not valid because it includes illegal characters.", value));

				_name = value;
			}
		}

		public string Field
		{
			get { return _field; }
			set { _field = value; }
		}

		public string RegexPattern
		{
			get { return _regexString; }
			set
			{
				_regexString = value;
				_regex = null;
				CreateRegex();
			}
		}

		public Regex Regex
		{
			get { CreateRegex(); return _regex; }
		}

		public string[] RegexFragments
		{
			get { CreateRegex(); return _fragments; }
		}

		void CreateRegex()
		{
			if (_regex == null && !String.IsNullOrWhiteSpace(this.RegexPattern))
			{
				_regex = this.RegexPattern == null ? null : new Regex(_fixRegex.Replace(this.RegexPattern, _fixReplace), RegexOptions.ExplicitCapture);
				this.RawGroupNames = this.RegexPattern == null ? null : this.Regex.GetGroupNames();
				if (this.RawGroupNames != null)
				{
					List<string> frags = new List<string>();
					foreach (string frag in this.RawGroupNames)
						if (IsValidFragmentName(frag))
							frags.Add(frag);
					_fragments = frags.ToArray();
				}
				else
					_fragments = null;
			}
		}

		static bool IsValidFragmentName(string name)
		{
			return !_fragmentNameInvalid.IsMatch(name);
		}
	}
}
