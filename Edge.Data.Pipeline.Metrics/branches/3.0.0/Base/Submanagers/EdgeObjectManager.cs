using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Services;

namespace Edge.Data.Pipeline.Metrics.Base.Submanagers
{
	/// <summary>
	/// Contains collection of edge objects
	/// performs Db operations on objects: create object tables,insert objects into DB
	/// </summary>
	internal class EdgeObjectsManager
	{
		#region Data Members

		private readonly SqlConnection _deliverySqlConnection;

		private readonly Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		private readonly Dictionary<int, List<object>> _objectsByPass = new Dictionary<int, List<object>>();
		private readonly Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>();

		// dictionary of objects by TK (temporary key)
		private readonly Dictionary<string, EdgeObject> _objectsCache = new Dictionary<string, EdgeObject>();

		#endregion

		#region Ctor

		public EdgeObjectsManager(SqlConnection deliveryConnection)
		{
			_deliverySqlConnection = deliveryConnection;
		}

		#endregion

		#region Indexer

		public List<object> this[int index]
		{
			get { return _objectsByPass[index]; }
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Call DB stored procedure to create all delivery object tables by table prefix
		/// currently all delivery objects tables are created for all accounts even if they are not in use
		/// </summary>
		/// <param name="tablePrefix"></param>
		public void CreateDeliveryObjectTables(string tablePrefix)
		{
			using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
			{
				var cmd = SqlUtility.CreateCommand("MD_ObjectTables_Create", CommandType.StoredProcedure);
				cmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", tablePrefix));
				cmd.Connection = connection;
				connection.Open();

				cmd.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				cmd.ExecuteNonQuery();
			}
		}

		public void Add(object obj, int pass)
		{
			if (!_objectsByPass.ContainsKey(pass))
				_objectsByPass.Add(pass, new List<object>());

			_objectsByPass[pass].Add(obj);
		}

		public void Add(EdgeObject obj)
		{
			if (_allObjects.ContainsKey(obj))
				throw new ArgumentException(string.Format("element {0} of type {1}  already exists in _allObjects dictionary", obj.Account.Name, obj.GetType().Name));

			_otherObjects.Add(obj, obj);
			_allObjects.Add(obj, obj);
		}

		public bool ContainsKey(EdgeObject obj)
		{
			return _allObjects.ContainsKey(obj);
		}

		public bool ContainsKey(int pass)
		{
			return _objectsByPass.ContainsKey(pass);
		}

		public Dictionary<int, List<object>>.ValueCollection ObjectsByPassValues()
		{
			return _objectsByPass.Values;
		}

		public Dictionary<EdgeObject, EdgeObject>.ValueCollection GetOtherObjects()
		{
			return _otherObjects.Values;
		}

		/// <summary>
		/// Insert objects into object DB tables
		/// </summary>
		public void ImportObjects(string tablePrefix)
		{
			foreach (var obj in _objectsCache)
			{
				// temporary key
				var columns = "TK";
				var values = String.Format("'{0}'", obj.Key);

				// Type ID
				columns = String.Format("{0},\nTypeID", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.EdgeType.TypeID);

				// account
				columns = String.Format("{0},\nAccountID", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.Account.ID);

				// extra fields
				foreach (var field in obj.Value.ExtraFields)
				{
					columns = String.Format("{0},\n{1}_Field{2}", columns, field.Key.ColumnType, field.Key.ColumnIndex);
					// TODO - how to support all types???
					values = String.Format("{0},\n'{1}'", values, (field.Value is StringValue) ? (field.Value as StringValue).Value : String.Empty);
				}

				var insertSql = String.Format("INSERT INTO [DBO].[{0}_{1}] \n({2}) \nVALUES \n({3})", tablePrefix, obj.Value.EdgeType.TableName, columns, values);
				using (var command = new SqlCommand(insertSql, _deliverySqlConnection))
				{
					command.ExecuteNonQuery();
				}
			}
		}

		public void AddToCache(EdgeObject obj)
		{
			if (!_objectsCache.ContainsKey(obj.TK))
			{
				_objectsCache.Add(obj.TK, obj);
			}
		}
		#endregion
	}

	public static class Extensions
	{
		public static IEnumerable<TSource> DistinctBy<TSource, TValue>(
			this IEnumerable<TSource> source, Func<TSource, TValue> selector)
		{
			var hashset = new HashSet<TValue>();
			return from item in source let value = selector(item) where hashset.Add(value) select item;
		}
	}
}
