using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Objects;
using System.Reflection;

namespace Edge.Data.Objects
{
	class TableManager
	{
		Dictionary<string, object> objects;
		Dictionary<string, Column> cols;
		public List<string> GetColumnsList(MetricsUnit metricsUnit)
		{
			List<string> columns = new List<string>();
			GetAllColumns(metricsUnit);




			return columns;

		}
		public void GetAllColumns(object obj)
		{
			if (obj.GetType().BaseType == typeof(MetricsUnit))
			{

				if (obj.GetType() == typeof(AdMetricsUnit))
				{
					//TODO:GET ALL OTHER PROPERTIS
					GetAllColumns(((AdMetricsUnit)obj).Ad);
				}
				else
				{		
					//TODO:GET ALL OTHER PROPERTIS
					foreach (KeyValuePair<MetaProperty, object> propertyDimension in ((GenericMetricsUnit)obj).PropertyDimensions)
					{
						GetAllColumns(propertyDimension);
					}
				}
			}
			else if (obj.GetType().BaseType == typeof(EdgeObject))
			{
				//TODO: ADD TYPE NAME AS COLUMN
				foreach (FieldInfo field in obj.GetType().BaseType.GetFields()) //only edgeobject properties
				{
					GetAllColumns(field.GetValue(obj));					
				}
				foreach (FieldInfo field in obj.GetType().GetFields(BindingFlags.DeclaredOnly))
				{										
						GetAllColumns((EdgeObject)field.GetValue(obj));					
				}
			}
			else
			{
				//ADD FIELD

			}

		}
		private void AddColumn(FieldInfo field)
		{
			throw new NotImplementedException();
		}
	}
}
