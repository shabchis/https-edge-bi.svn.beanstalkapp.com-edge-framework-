using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using DataStreams.Csv;

namespace Edge.Data.Pipeline.Readers
{
	public class CsvChunkReader : CsvObjectReader<Chunk>
	{
		public CsvChunkReader(string url, char delimeter = ',', Encoding encoding = null)
			: base(url, delimeter, encoding)
		{
			throw new NotImplementedException();
			//this.OnObjectRequired = GetChunk;
		}

		Chunk GetChunk(CsvReader reader)
		{
			throw new NotImplementedException();
		}
	}

}
