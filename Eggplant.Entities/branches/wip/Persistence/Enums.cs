using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public enum MappingFlags
	{
		MapIn = 0x01,
		MapOut = 0x02,
		MapToParameters = 0x10,
		MapToStream = 0x20
	}

	public enum MappingDirection
	{
		Inbound = 0x1,
		Outbound = 0x2,
		Both = 0x3
	}
}
