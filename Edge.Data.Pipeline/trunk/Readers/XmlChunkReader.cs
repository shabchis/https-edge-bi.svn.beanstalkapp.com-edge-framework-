using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;

namespace Edge.Data.Pipeline.Readers
{
	public class XmlChunkReader : XmlObjectReader<Chunk>
	{
		public readonly XmlChunkReaderOptions Options;

		public XmlChunkReader(string url, string xpath, XmlChunkReaderOptions options = XmlChunkReaderOptions.ElementsAsValues | XmlChunkReaderOptions.AttributesAsValues):
			base(url, xpath)
		{
			Options = options;
			this.OnObjectRequired = GetChunk;
		}

		Chunk GetChunk(XmlReader reader)
		{
			Dictionary<string, string> dict = new Dictionary<string, string>();

			// Read attributes
			if (reader.HasAttributes && (int)(Options & XmlChunkReaderOptions.AttributesAsValues) != 0)
			{
				while (reader.MoveToNextAttribute())
					dict[reader.Name] = reader.Value;
			}

			// Read value elements
			if ((int)(Options & XmlChunkReaderOptions.ElementsAsValues) != 0)
			{
				using (var r = reader.ReadSubtree())
				{
					while (r.Read())
					{
						if (r.NodeType == XmlNodeType.Element)
							dict[r.Name] = r.ReadElementContentAsString();
					}
				}
			}

			return new Chunk(dict);
		}
	}

	[Flags]
	public enum XmlChunkReaderOptions
	{
		ElementsAsValues = 0x1,
		AttributesAsValues = 0x2
	}
}
