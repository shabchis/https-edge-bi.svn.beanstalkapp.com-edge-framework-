using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Eggplant.Persistence
{
	/// <summary>
	/// Query
	/// </summary>
	public class EntityQuery
	{
		List<EntityQueryParam> Parameters;
	}

	public class EntityQueryParam
	{
		public IEntityProperty Property;
		public object[] PossibleValues; // IN (..)
		public object MinValue; // BETWEEN ..
		public object MaxValue; // 
		public string Pattern; // LIKE
	}
}
