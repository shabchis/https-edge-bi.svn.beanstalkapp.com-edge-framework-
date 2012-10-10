﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeObject : EdgeObjectBase
	{
		public ulong GK;
		public string Name;

		public Account Account;

		public Dictionary<MetaProperty, object> MetaProperties;
	}
	
	public abstract class EdgeObjectBase
	{
		public virtual bool HasChildsObjects
		{
			get { return false; }
		}

		public virtual IEnumerable<EdgeObject> GetChildObjects()
		{
			return null;
		}
	}


}
