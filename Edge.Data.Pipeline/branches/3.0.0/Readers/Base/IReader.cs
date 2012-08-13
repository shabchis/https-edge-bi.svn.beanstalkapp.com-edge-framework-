using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public interface IReader: IDisposable
	{
		object Current { get; }

		bool Read();
	}

	public interface IReader<T> : IReader
	{
		new T Current { get; }
	}
}
