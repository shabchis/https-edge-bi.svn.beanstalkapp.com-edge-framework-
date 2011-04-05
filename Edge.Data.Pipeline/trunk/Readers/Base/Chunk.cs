using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;

namespace Edge.Data.Pipeline.Readers
{
	/// <summary>
	/// Contains the data of a chunk found by an XmlChunkReader.
	/// </summary>
	public class Chunk : IEnumerable<KeyValuePair<string, string>>
	{
		Dictionary<string, string> _dict;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="options"></param>
		internal Chunk(Dictionary<string,string> values)
		{
			_dict = values;
		}

		/// <summary>
		/// Gets the value of an attribute or element in the chunk.
		/// </summary>
		/// <param name="key">The attribute or element name.</param>
		/// <returns></returns>
		public string this[string key]
		{
			get
			{
				string val;
				return _dict.TryGetValue(key, out val) ? val : null;
			}
		}

		#region IEnumerable<KeyValuePair<string,string>> Members

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return _dict.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion
	}

}
