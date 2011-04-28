using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;

namespace Eggplant.Persistence
{
	/// <summary>
	/// 
	/// </summary>
	[System.Serializable]
	public class EntityException: Exception
	{
		public readonly IEntityProperty Property;
	
		public EntityException() { }
		public EntityException(string message) : base(message) { }
		public EntityException(string message, Exception inner) : base(message, inner) { }
		protected EntityException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}


	[System.Serializable]
	public class DependencyException: EntityException
	{
		public DependencyException() { }
		public DependencyException(string message) : base(message) { }
		public DependencyException(string message, Exception inner) : base(message, inner) { }
		protected DependencyException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	[System.Serializable]
	public class EntityIndexException: Exception
	{
		public EntityIndexException() { }
		public EntityIndexException(string message) : base(message) { }
		public EntityIndexException(string message, Exception inner) : base(message, inner) { }
		protected EntityIndexException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

}
