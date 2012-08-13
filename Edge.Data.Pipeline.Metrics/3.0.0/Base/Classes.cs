using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Data;
using System.Reflection;
using Edge.Core.Configuration;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Common.Importing
{
	public class MetricsImportManagerOptions
	{
		public string StagingConnectionString { get; set; }
		public string SqlTransformCommand { get; set; }
		public string SqlStageCommand { get; set; }
		public string SqlRollbackCommand { get; set; }
		public double ChecksumThreshold { get; set; }
		public MeasureOptions MeasureOptions { get; set; }
		public OptionsOperator MeasureOptionsOperator { get; set; }
		public SegmentOptions SegmentOptions { get; set; }
		public OptionsOperator SegmentOptionsOperator { get; set; }
	}

	public static class TableDef
	{
		static Dictionary<Type, ColumnDef[]> _columns = new Dictionary<Type, ColumnDef[]>();
		public static ColumnDef[] GetColumns<T>(bool expandCopies = true)
		{
			return GetColumns(typeof(T), expandCopies);
		}

		public static ColumnDef[] GetColumns(Type type, bool expandCopies = true)
		{
			ColumnDef[] columns;
			lock (_columns)
			{
				if (_columns.TryGetValue(type, out columns))
					return columns;

				FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);
				columns = new ColumnDef[fields.Length];
				for (int i = 0; i < fields.Length; i++)
				{
					columns[i] = (ColumnDef)fields[i].GetValue(null);
				}
				_columns.Add(type, columns);
			}

			if (expandCopies)
			{
				var expanded = new List<ColumnDef>(columns.Length);
				foreach (ColumnDef col in columns)
				{
					if (col.Copies <= 1)
					{
						expanded.Add(col);
					}
					else
					{
						for (int i = 1; i <= col.Copies; i++)
							expanded.Add(new ColumnDef(col, i));
					}

				}
				columns = expanded.ToArray();
			}

			return columns;
		}
	}

	public struct ColumnDef
	{
		public string Name;
		public SqlDbType Type;
		public int Size;
		public bool Nullable;
		public int Copies;
		public string DefaultValue;


		public ColumnDef(string name, int size = 0, SqlDbType type = SqlDbType.NVarChar, bool nullable = true, int copies = 1, string defaultValue = "")
		{
			this.Name = name;
			this.Type = type;
			this.Size = size;
			this.Nullable = nullable;
			this.Copies = copies;
			this.DefaultValue = defaultValue;

			if (copies < 1)
				throw new ArgumentException("Column copies cannot be less than 1.", "copies");
			if (copies > 1 && this.Name.IndexOf("{0}") < 0)
				throw new ArgumentException("If copies is bigger than 1, name must include a formattable placholder.", "name");
		}

		public ColumnDef(ColumnDef copySource, int index)
			: this(
				name: String.Format(copySource.Name, index),
				size: copySource.Size,
				type: copySource.Type,
				nullable: copySource.Nullable,
				copies: 1
				)
		{
		}
	}

	public class BulkObjects : IDisposable
	{

		public SqlConnection Connection;
		public List<ColumnDef> Columns;
		public DataTable Table;
		public SqlBulkCopy BulkCopy;
		public readonly int BufferSize;

		public BulkObjects(string tablePrefix, Type tableDefinition, SqlConnection connection, int bufferSize)
		{
			this.BufferSize = bufferSize;

			string tbl = tablePrefix + "_" + tableDefinition.Name;
			this.Columns = new List<ColumnDef>(TableDef.GetColumns(tableDefinition, true));

			// Create the table used for bulk insert
			this.Table = new DataTable(tbl);
			foreach (ColumnDef col in this.Columns)
			{
				var tableCol = new DataColumn(col.Name);
				tableCol.AllowDBNull = col.Nullable;
				if (col.Size != 0)
					tableCol.MaxLength = col.Size;
				this.Table.Columns.Add(tableCol);
			}

			// Create the bulk insert operation
			this.BulkCopy = new SqlBulkCopy(connection);
			this.BulkCopy.DestinationTableName = tbl;
			foreach (ColumnDef col in this.Columns)
				this.BulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(col.Name, col.Name));
		}
		public void AddColumn(ColumnDef columnDef)
		{
			this.Columns.Add(columnDef);
			var tableCol = new DataColumn(columnDef.Name);
			tableCol.AllowDBNull = columnDef.Nullable;
			if (columnDef.Size != 0)
				tableCol.MaxLength = columnDef.Size;
			this.Table.Columns.Add(tableCol);
			this.BulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(columnDef.Name, columnDef.Name));
		}

		public void SubmitRow(Dictionary<ColumnDef, object> values)
		{
			DataRow row = this.Table.NewRow();
			foreach (KeyValuePair<ColumnDef, object> col in values)
			{
				row[col.Key.Name] = DataManager.Normalize(col.Value);
			}

			this.Table.Rows.Add(row);

			// Auto flush
			if (this.Table.Rows.Count >= BufferSize)
				this.Flush();
		}

		public string GetCreateTableSql()
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendFormat("create table [dbo].{0} (\n", this.Table.TableName);
			for (int i = 0; i < this.Columns.Count; i++)
			{
				ColumnDef col = this.Columns[i];
				builder.AppendFormat("\t[{0}] [{1}] {2} {3} {4}, \n",
					col.Name,
					col.Type,
					col.Size != 0 ? string.Format("({0})", col.Size) : null,
					col.Nullable ? "null" : "not null",
					col.DefaultValue != string.Empty ? string.Format("Default {0}", col.DefaultValue) : string.Empty
				);
			}
			builder.Remove(builder.Length - 1, 1);
			builder.Append(");");

			string cmdText = builder.ToString();
			return cmdText;
		}

		public string GetCreateIndexSql()
		{
			throw new NotImplementedException();
		}

		public void Flush()
		{
			this.BulkCopy.WriteToServer(this.Table);
			this.Table.Clear();
		}

		public void Dispose()
		{
			this.BulkCopy.Close();
		}
	}

}