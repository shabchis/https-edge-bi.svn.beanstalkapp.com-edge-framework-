using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace Eggplant.Persistence
{
	public enum PersistenceCommandType
	{
		Nothing,
		Value,
		Array,
		Reader,
		ResultSet
	}
	/*
	public abstract class PersistenceCommand
	{
		// Eggplant-TODO:
		//public readonly PersistenceCommandConfiguration Configuration;
		public Action OnComplete;
		public Action<Thing> OnThingReceived;

		public PersistenceCommand()//PersistenceCommandConfiguration configuration)
		{
			//Configuration = configuration;
			Init();
		}

		protected virtual void Init()
		{
		}

		protected void Execute(Thing thing, List<Thing> resultsContainer)
		{
			MapValues(thing, thing.PropertyDefinitions);			
			OnExecute(PersistenceProvider.Current);
		}

		protected virtual void MapValues(Thing thing, IThingProperty[] properties)
		{
		}

		protected virtual void OnExecute(PersistenceProvider provider)
		{
		}
	}

	public class SqlServerPersistenceCommand: PersistenceCommand
	{
		SqlCommand _cmd;

		protected override void Init()
		{
			//_cmd = new SqlCommand(Configuration.CommandText);
		}

		protected override void OnExecute(PersistenceProvider provider)
		{
			//if (!(provider is SqlServerPersistenceProvider))
			//	throw new NotSupportedException("This command can only be executed using a SqlServerPersistenceProvider.");

		}
	}
	*/
}
