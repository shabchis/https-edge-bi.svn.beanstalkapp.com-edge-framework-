using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline
{
	public abstract class ReaderAdapter: IDisposable
	{
		public IReader Reader { get; protected set; }

		public abstract void Init(Stream stream, ServiceElement configuration);
		public abstract object GetField(string field);

		public void Dispose()
		{
			if (this.Reader != null)
				this.Reader.Dispose();
		}
	}
}
