using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace Edge.Core
{
	/// <summary>
	/// A collection of string key/values that can be serialized to- or deserialized from
	/// a CSS-like declaration format.
	/// </summary>
	/// <remarks>
	/// The <parmref name="Definition"/> property is used to get or set the string
	/// format of the settings collection. The format expected is
	/// <c>name: value; other-name: value; </c> etc. Whitespace is flexible.
	/// Comments enclosed in <c>/*</c> and <c>*/</c> are allowed anywhere in the string.
	/// </remarks>
	[Obsolete]
	public class SettingsCollection : Dictionary<string, string>
	{
		/// <summary>
		/// The regular expression that parses a definition string to retrieve settings.
		/// </summary>
		/// <remarks>
		private static Regex _settingParser = new Regex(@"\b([A-Za-z]+[A-Za-z0-9-_/=\+]*)\s*:\s*([^;]*)");

		/// <summary>
		/// The regular expression that extracts comments from the entire definition string.
		/// </summary>
		private static Regex _commentFinder = new Regex(@"/\*.*\*/");

		/// <summary>
		/// Called when the contents of the collection have changed.
		/// </summary>
		public event EventHandler Changed;

		public SettingsCollection()
			: base(StringComparer.InvariantCultureIgnoreCase)
		{
		}

		public SettingsCollection(string definition)
			: this()
		{
			this.Definition = definition;
		}

		/// <summary>
		/// Gets or sets the text-based definition of the settings collection.
		/// </summary>
		public string Definition
		{
			get
			{
				// Construct an output string from the array
				string output = String.Empty;
				int i = 0;
				foreach (string key in this.Keys)
				{
					output += String.Format("{0}: {1}", key, this[key]);

					// Add a semi-color separator unless it's the last setting
					if (i < this.Count - 1)
						output += "; ";

					i++;
				}

				return output;
			}

			set
			{
				string input = value;

				// Remove all comments first
				if (input != null)
					input = _commentFinder.Replace(input, String.Empty);

				// Clear all values
				this.Clear();

				// Don't continue if input is null
				if (input == null)
					return;

				// Iterate the key/value pairs found by the regular expression
				foreach (Match setting in _settingParser.Matches(input))
				{
					if (setting.Groups.Count != 3)
						continue;

					string key, val;
					key = setting.Groups[1].Value;
					val = setting.Groups[2].Value;

					// Add the setting to the collection
					this.Add(key, val);
				}

				if (Changed != null)
					Changed(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Returns the string definition of the settings collection.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Definition;
		}

		/// <summary>
		/// Re-implements the indexer of the Dictionary class to avoid KeyNotFoundException.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public new string this[string key]
		{
			get
			{
				string val;
				if (TryGetValue(key, out val))
					return val;
				else
					return null;
			}
			set
			{
				base[key] = value;
			}
		}

		public void Merge(SettingsCollection otherCollection)
		{
			foreach (KeyValuePair<string, string> entry in otherCollection)
			{
				this[entry.Key] = (string)entry.Value;
			}
		}

		public Dictionary<string, string> ToDictionary()
		{
			Dictionary<string, string> dict = new Dictionary<string, string>();

			foreach (KeyValuePair<string, string> entry in this)
			{
				dict[entry.Key] = (string)entry.Value;
			}

			return dict;
		}
	}
}
