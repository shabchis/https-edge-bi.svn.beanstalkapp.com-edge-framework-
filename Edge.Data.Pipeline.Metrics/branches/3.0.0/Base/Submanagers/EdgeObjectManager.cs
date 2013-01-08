using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Services;

namespace Edge.Data.Pipeline.Metrics.Base.Submanagers
{
	/// <summary>
	/// Contains collection of edge objects
	/// </summary>
	internal class EdgeObjectsManager
	{
		#region Data Members

		private readonly SqlConnection _sqlConnection;

		private readonly Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		private readonly Dictionary<int, List<object>> _objectsByPass = new Dictionary<int, List<object>>();
		private readonly Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>();

		#endregion

		#region Ctor

		public EdgeObjectsManager(SqlConnection connection)
		{
			_sqlConnection = connection;
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
				var cmd = SqlUtility.CreateCommand("MD_CreateObjectTables", CommandType.StoredProcedure);
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
				throw new System.ArgumentException(string.Format(
					"element {0} of type {1}  already exists in _allObjects dictionary", obj.Account.Name, obj.GetType().Name));

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

		#endregion

	}
}
