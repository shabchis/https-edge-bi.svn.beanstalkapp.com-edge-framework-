using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using System.Collections;

namespace Eggplant.Entities.Cache
{
	interface IEntityCache
	{
		object Get(IdentityDefinition def, Identity id);
		object Get(IdentityDefinition def, params object[] idParts);
		IEnumerable Get();
		void Add(IEnumerable objects, IEntityProperty[] activeProperties);
	}
}
