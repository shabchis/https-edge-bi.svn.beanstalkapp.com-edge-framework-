using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public interface IRowReader: IDisposable
	{
		object CurrentRow { get; }

		bool Read();
	}

	public interface IRowReader<RowT> : IRowReader
	{
		new RowT CurrentRow { get; }
	}
}
