using System.Collections.Generic;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Base.Submanagers
{
	/// <summary>
	/// TODO - add summary
	/// </summary>
	internal class EdgeObjectsManager
	{
		#region Data Members
		private readonly Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		private readonly Dictionary<int, List<EdgeObject>> _objectsByPass = new Dictionary<int, List<EdgeObject>>();
		private readonly Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>(); 
		#endregion

		#region Indexer
		public List<EdgeObject> this[int index]
		{
			get { return _objectsByPass[index]; }

		} 
		#endregion

		#region Public Methods
		public void Add(EdgeObject obj, int pass)
		{


			if (!_objectsByPass.ContainsKey(pass))
				_objectsByPass.Add(pass, new List<EdgeObject>());
			_objectsByPass[pass].Add(obj);

		}

		public void Add(EdgeObject obj)
		{
			if (_allObjects.ContainsKey(obj))
				throw new System.ArgumentException(string.Format("element {0} of type {1}  already exists in _allObjects dictionary", obj.Account.Name, obj.GetType().Name));

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

		public Dictionary<int, List<EdgeObject>>.ValueCollection ObjectsByPassValues()
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
