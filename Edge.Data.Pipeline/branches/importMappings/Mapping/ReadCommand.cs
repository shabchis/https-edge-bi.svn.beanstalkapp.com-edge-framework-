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
		
		public Regex Regex;
	}
}
