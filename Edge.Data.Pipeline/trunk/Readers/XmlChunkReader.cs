using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;

namespace Edge.Data.Pipeline.Readers
{
	public class XmlChunkReader : XmlObjectReader<XmlChunk>
	{
		public readonly XmlChunkReaderOptions Options;

		public XmlChunkReader(string url, string xpath, XmlChunkReaderOptions options = XmlChunkReaderOptions.ElementsAsValues | XmlChunkReaderOptions.AttributesAsValues):
			base(url, xpath)
		{
			Options = options;
			this.OnObjectRequired = GetChunk;
		}

		XmlChunk GetChunk(XmlReader reader)
		{
			return new XmlChunk(reader, this.Options);
		}
	}

	/// <summary>
	/// Contains the data of a chunk found by an XmlChunkReader.
	/// </summary>
	public class XmlChunk: IEnumerable<KeyValuePair<string,string>>
	{
		Dictionary<string, string> _dict = new Dictionary<string, string>();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="options"></param>
		internal XmlChunk(XmlReader reader, XmlChunkReaderOptions options = XmlChunkReaderOptions.ElementsAsValues | XmlChunkReaderOptions.AttributesAsValues)
		{
			// Read attributes
			if (reader.HasAttributes && (int)(options & XmlChunkReaderOptions.AttributesAsValues) != 0)
			{
				while (reader.MoveToNextAttribute())
					_dict[reader.Name] = reader.Value;
			}

			// Read value elements
			if ((int)(options & XmlChunkReaderOptions.ElementsAsValues) != 0)
			{
				using (var r = reader.ReadSubtree())
				{
					while (r.Read())
					{
						if (r.NodeType == XmlNodeType.Element)
							_dict[r.Name] = r.ReadElementContentAsString();
					}
				}
			}
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

	[Flags]
	public enum XmlChunkReaderOptions
	{
		ElementsAsValues = 0x1,
		AttributesAsValues = 0x2
	}
}
