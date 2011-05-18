using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant
{
	public interface IReader: IDisposable
	{
		object Current { get; }
		bool Read();
	}
}
