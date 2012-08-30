using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Objects;
using System.Reflection;

namespace Edge.Data.Objects
{
	public class TableManager
	{

		Dictionary<string, Column> cols = new Dictionary<string, Column>();
		Dictionary<int, List<EdgeObject>> objects = new Dictionary<int, List<EdgeObject>>();
		public List<Column> GetColumnsList(MetricsUnit metricsUnit)
		{
			int pass = 0;
			if (metricsUnit is AdMetricsUnit)
			{
				AdMetricsUnit adMetricsUnit=(AdMetricsUnit)metricsUnit;
				AddObjects(adMetricsUnit.Ad, pass);
				AddObjects(adMetricsUnit.Ad.Creative,pass);
			}
			foreach (var target in metricsUnit.TargetDimensions)
			{				
				AddObjects(target, pass);
			}
			
			
			while (objects.ContainsKey(pass) && objects[pass]!=null && objects[pass].Count>0)
			{
				foreach (var obj in objects[pass])
				{
					AddColumn(obj);
					
				}
				
				foreach (var obj in objects[pass])
				{
					
					foreach (var field in obj.GetType().GetFields())
					{
						if (field.FieldType.IsSubclassOf(typeof(EdgeObject)))
						{
							AddObjects((EdgeObject)field.GetValue(obj),pass+1);
						}
						
					}
					
				}
				pass++;
				

			}

			


			return cols.Values.ToList();


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
							if (!cols.ContainsKey(typeName))
								cols.Add(typeName, new Column() { Name = typeName });
							break;
						}
					case "CompositeCreative":
						{
							CompositeCreative composite = (CompositeCreative)obj;
							if (!cols.ContainsKey(typeName))
								cols.Add(typeName, new Column() { Name = typeName });
							var childCreatives = composite.ChildCreatives.OrderBy(p => p.Key);


							foreach (var childCreative in childCreatives)
							{
								if (!cols.ContainsKey(childCreative.Key))
									cols.Add(childCreative.Key, new Column() { Name = childCreative.Key });
							}

							break;
						}
					case "SingleCreative":
						{
							if (!cols.ContainsKey(typeName))
							{
								cols.Add("Creative", null);
							}
							break;
						}
					default:
						{
							if (obj is Target)
							{
								int i = 2;
								string targetName = typeName;

								while (cols.ContainsKey(targetName))
								{
									targetName = string.Format("{0}{1}", typeName, i);
									i++;
									
								}
								cols.Add(targetName, new Column() { Name = targetName });
							}
							break;
						}
				} 
			}
		}
		private void AddObjects(EdgeObject obj, int pass)
		{
			if (!objects.ContainsKey(pass))
				objects[pass] = new List<EdgeObject>();
			objects[pass].Add(obj);
			//foreach (FieldInfo field in obj.GetType().DeclaringType.GetFields())
			//{
			//    if (field.FieldType ==typeof(EdgeObject))
			//        AddObjects((EdgeObject)field.GetValue(obj), pass);
			//}

		}


































	
		
	}
}
