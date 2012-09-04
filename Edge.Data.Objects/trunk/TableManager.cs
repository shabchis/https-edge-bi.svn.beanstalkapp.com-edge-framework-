using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Edge.Data.Objects
{
	public class TableManager
	{

		Dictionary<string, Column> _cols = new Dictionary<string, Column>();
		Dictionary<int, List<EdgeObject>> _objects = new Dictionary<int, List<EdgeObject>>();
		public List<Column> GetColumnsList(MetricsUnit metricsUnit)
		{
			int pass = 0;
			if (metricsUnit is AdMetricsUnit)
			{
				AdMetricsUnit adMetricsUnit = (AdMetricsUnit)metricsUnit;
				AddObjects(adMetricsUnit.Ad, pass);
				AddObjects(adMetricsUnit.Ad.Creative, pass);
			}
			foreach (var target in metricsUnit.TargetDimensions)
			{
				AddObjects(target, pass);
			}


			while (_objects.ContainsKey(pass) && _objects[pass] != null && _objects[pass].Count > 0)
			{
				foreach (var obj in _objects[pass])
				{
					AddColumn(obj);
				}

				foreach (var obj in _objects[pass])
				{
					foreach (var field in obj.GetType().GetFields())
					{
						if (field.FieldType.IsSubclassOf(typeof(EdgeObject)))
						{
							AddObjects((EdgeObject)field.GetValue(obj), pass + 1);
						}
					}
				}
				pass++;
			}



			foreach (List<EdgeObject> edgeObjects in _objects.Values)
			{
				foreach (EdgeObject edgeObject in edgeObjects)
				{
					AddObjects(edgeObject);
				}
			}

			if (metricsUnit.MeasureValues != null)
			{
				foreach (KeyValuePair<Measure, double> measure in metricsUnit.MeasureValues)
				{
					AddColumn(measure);
				}
			}


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

				if (t==typeof(KeyValuePair<MetaProperty, object>))
				{
					KeyValuePair<MetaProperty, object> metaProperty = (KeyValuePair<MetaProperty, object>)obj;
					if (!_cols.ContainsKey(metaProperty.Key.PropertyName))
					{
						_cols.Add(metaProperty.Key.PropertyName, new Column() { Name = metaProperty.Key.PropertyName });
					}

				}
				else if (t==typeof( KeyValuePair<Measure, double>))
				{
					KeyValuePair<Measure, double> measure = (KeyValuePair<Measure, double>)obj;
					if (!_cols.ContainsKey(measure.Key.Name))
					{
						_cols.Add(measure.Key.Name, new Column() { Name = measure.Key.Name, Value = measure.Value });
					}

				}
			}
		}
		private void AddObjects(EdgeObject obj)
		{

			List<EdgeObject> edgeObjetcs = new List<EdgeObject>();
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
						edgeObjetcs.Add((EdgeObject)metaProperty.Value);
					}
				}
				foreach (EdgeObject edgeObject in edgeObjetcs)
				{
					AddColumn(edgeObject);
				}
			}

		}
		private void AddObjects(EdgeObject obj, int pass)
		{
			if (!_objects.ContainsKey(pass))
				_objects[pass] = new List<EdgeObject>();
			_objects[pass].Add(obj);


		}




































	}
}
