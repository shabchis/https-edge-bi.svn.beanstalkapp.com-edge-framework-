﻿using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositePartField
	{
		public static EntityDefinition<CompositePartField> Definition = new EntityDefinition<CompositePartField>(baseDefinition: EdgeField.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<CompositePartField, Channel> Channel = new EntityProperty<CompositePartField, Channel>("Channel");
		}
	}
}