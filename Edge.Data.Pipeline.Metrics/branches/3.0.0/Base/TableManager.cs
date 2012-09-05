using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;

namespace Edge.Data.Objects
{
	internal class TableManager
	{
		public class Column
		{
			public string Name { get; set; }
			public SqlDbType DbType { get; set; }
			public int Length { get; set; }
			public object Value { get; set; }

		}

		Dictionary<string, Column> _cols = new Dictionary<string, Column>();
		EdgeObjectsManager _objectsManger = new EdgeObjectsManager();
		public List<Column> GetColumnsList(MetricsUnit metricsUnit)
		{
			#region looping object in predetermined order-
			int pass = 0;
			if (metricsUnit is AdMetricsUnit)
			{
				AdMetricsUnit adMetricsUnit = (AdMetricsUnit)metricsUnit;
				_objectsManger.Add(adMetricsUnit.Ad, pass);
				_objectsManger.Add(adMetricsUnit.Ad.Creative, pass);
			}
			foreach (var target in metricsUnit.TargetDimensions)
			{
				_objectsManger.Add(target, pass);
			}


			while (_objectsManger.ContainsKey(pass) && _objectsManger[pass] != null && _objectsManger[pass].Count > 0)
			{
				foreach (var obj in _objectsManger[pass])
				{
					AddColumn(obj);
				}

				foreach (var obj in _objectsManger[pass])
				{
					foreach (var field in obj.GetType().GetFields())
					{
						if (field.FieldType.IsSubclassOf(typeof(EdgeObject)))
						{
							_objectsManger.Add((EdgeObject)field.GetValue(obj), pass + 1);
						}
					}
				}
				pass++;
			}
			#endregion

			#region runing deeper on all edgeobjects and relevant properties
			foreach (List<EdgeObject> edgeObjects in _objectsManger.ObjectsByPassValues())
			{
				foreach (EdgeObject edgeObject in edgeObjects)
				{
					AddObjects(edgeObject);
				}
			}
			foreach (EdgeObject edgeObject in _objectsManger.GetOtherObjects())
			{
				AddColumn(edgeObject);
			}
			#endregion

			#region runing on measures
			if (metricsUnit.MeasureValues != null)
			{
				foreach (KeyValuePair<Measure, double> measure in metricsUnit.MeasureValues)
				{
					AddColumn(measure);
				}
			}
			#endregion


			return _cols.Values.ToList();


		}
		private void AddColumn(object obj)
		{
			string typeName = obj.GetType().Name;

			if (obj.GetType().IsSubclassOf(typeof(EdgeObject)))
			{
				switch (typeName)
				{
					case "Ad":
						{
							Ad ad = (Ad)obj;
							if (!_cols.ContainsKey(typeName))
							{

								_cols.Add(typeName, new Column() { Name = typeName, Value = ad.GK });
							}
							break;
						}
					case "CompositeCreative":
						{
							CompositeCreative composite = (CompositeCreative)obj;
							if (!_cols.ContainsKey(typeName))
								_cols.Add(typeName, new Column() { Name = typeName });
							var childCreatives = composite.ChildCreatives.OrderBy(p => p.Key);


							foreach (var childCreative in childCreatives)
							{
								if (!_cols.ContainsKey(childCreative.Key))
									_cols.Add(childCreative.Key, new Column() { Name = childCreative.Key, Value = childCreative.Value.GK });

							}

							break;
						}
					case "SingleCreative":
						{
							if (!_cols.ContainsKey(typeName))
							{
								Creative creative = (Creative)obj;
								_cols.Add("Creative", new Column() { Name = "Creative", Value = creative.GK });
							}
							break;
						}
					default:
						{
							if (obj is Target)
							{
								int i = 2;
								string targetName = typeName;
								while (_cols.ContainsKey(targetName))
								{
									targetName = string.Format("{0}{1}", typeName, i);
									i++;
								}
								Target target = (Target)obj;
								_cols.Add(targetName, new Column() { Name = targetName, Value = target.GK });
							}
							break;
						}
				}
			}
			else
			{
				Type t = obj.GetType();

				if (t == typeof(KeyValuePair<MetaProperty, object>))
				{
					KeyValuePair<MetaProperty, object> metaProperty = (KeyValuePair<MetaProperty, object>)obj;
					if (!_cols.ContainsKey(metaProperty.Key.PropertyName))
					{
						_cols.Add(metaProperty.Key.PropertyName, new Column() { Name = metaProperty.Key.PropertyName });
					}

				}
				else if (t == typeof(KeyValuePair<Measure, double>))
				{
					KeyValuePair<Measure, double> measure = (KeyValuePair<Measure, double>)obj;
					string name = measure.Key.IsCurrency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
					if (!_cols.ContainsKey(name))
					{
						_cols.Add(name, new Column() { Name = name, Value = measure.Value });
					}

				}
			}
		}
		private void AddObjects(EdgeObject obj)
		{


			if (obj.MetaProperties != null)
			{
				foreach (KeyValuePair<MetaProperty, object> metaProperty in obj.MetaProperties)
				{
					if (metaProperty.Value.GetType() != typeof(EdgeObject))
					{
						AddColumn(metaProperty);
					}
					else
					{
						if (!_objectsManger.ContainsKey((EdgeObject)metaProperty.Value))
							_objectsManger.Add((EdgeObject)metaProperty.Value);
					}
				}

			}

		}

	}
	public class EdgeObjectsManager
	{
		Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		Dictionary<int, List<EdgeObject>> _objectsByPass = new Dictionary<int, List<EdgeObject>>();
		Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>();
		public List<EdgeObject> this[int index]
		{
			get
			{
				return _objectsByPass[index];
			}

		}
		public void Add(EdgeObject obj, int pass)
		{


			if (!_objectsByPass.ContainsKey(pass))
				_objectsByPass.Add(pass, new List<EdgeObject>());
			_objectsByPass[pass].Add(obj);

		}
		public void Add(EdgeObject obj)
		{
			if (_allObjects.ContainsKey(obj))
				throw new System.ArgumentException(string.Format("element {0} of type {1}  already exists in _allObjects dictionary", obj.Name, obj.GetType().Name));
			_otherObjects.Add(obj, obj);
			_allObjects.Add(obj, obj);
		}
		public bool ContainsKey(EdgeObject obj)
		{
			return _allObjects.ContainsKey(obj) ? true : false;
		}
		public bool ContainsKey(int pass)
		{
			return _objectsByPass.ContainsKey(pass) ? true : false;
		}
		public Dictionary<int, List<EdgeObject>>.ValueCollection ObjectsByPassValues()
		{
			return _objectsByPass.Values;
		}

		public Dictionary<EdgeObject, EdgeObject>.ValueCollection GetOtherObjects()
		{
			return _otherObjects.Values;
		}
	}
}
