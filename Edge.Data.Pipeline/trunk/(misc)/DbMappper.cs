using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Collections;
using System.Data;

namespace Edge.Data.Pipeline
{
    class DbMappper
    {
        public static int SaveOrRemoveSimpleObject<T>(string Command, object objectToInsert) where T : class, new()
        {
            Type t = typeof(T);
            using (DataManager.Current.OpenConnection())
            {

                try
                {
                    using (SqlCommand sqlCommand = DataManager.CreateCommand(Command, CommandType.StoredProcedure))
                    {
                        if (Command.Contains("@RETVAL"))
                            sqlCommand.Parameters["@RETVAL"].Direction = ParameterDirection.Output;
                        foreach (FieldInfo fieldInfo in objectToInsert.GetType().GetFields())
                        {
                            if (Attribute.IsDefined(fieldInfo, typeof(FieldMapAttribute)))
                            {
                                FieldMapAttribute fieldMapAttribute = (FieldMapAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(FieldMapAttribute));
                                if (sqlCommand.Parameters.Contains(string.Format("@{0}", fieldMapAttribute.FieldName)))
                                {
                                    if (fieldInfo.GetValue(objectToInsert) != null)
                                        sqlCommand.Parameters[string.Format("@{0}", fieldMapAttribute.FieldName)].Value = fieldInfo.GetValue(objectToInsert);
                                    else
                                        sqlCommand.Parameters[string.Format("@{0}", fieldMapAttribute.FieldName)].Value = DBNull.Value;
                                }
                            }
                        }
                        sqlCommand.ExecuteNonQuery();
                        if (sqlCommand.Parameters.Contains("@RETVAL"))
                            return (int)sqlCommand.Parameters["@RETVAL"].Value;
                        else
                            return 0;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        public static T ExpandObject<T>(object o, Func<FieldInfo, IDataRecord, object> customApply) where T : class, new()
        {
            Type t = typeof(T);
            T returnObject = (T)o;
            string dictionaryKey = string.Empty;
            SqlCommand sqlCommand;

            foreach (FieldInfo fieldInfo in returnObject.GetType().GetFields())
            {
                if (Attribute.IsDefined(fieldInfo, typeof(DictionaryMapAttribute)))
                {
                    DictionaryMapAttribute dictionaryMapAttribute = (DictionaryMapAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DictionaryMapAttribute));
                    using (DataManager.Current.OpenConnection())
                    {
                        if (!dictionaryMapAttribute.IsStoredProcedure)
                            sqlCommand = DataManager.CreateCommand(dictionaryMapAttribute.Command);
                        else
                            sqlCommand = DataManager.CreateCommand(dictionaryMapAttribute.Command, CommandType.StoredProcedure);
                        foreach (SqlParameter param in sqlCommand.Parameters)
                        {
                            string fieldName = param.ParameterName.Substring(1); //without the "@"
                            param.Value = returnObject.GetType().GetField(fieldName).GetValue(returnObject);

                        }

                        fieldInfo.SetValue(returnObject, GetDictionryObject(fieldInfo, sqlCommand.ExecuteReader()));
                    }
                }
            }
            return (T)returnObject;
        }
        public static IList GetListObject(FieldInfo fieldInfo, IDataReader sqlDataReader)
        {
            if (!fieldInfo.FieldType.IsGenericType || fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(IList))
                throw new Exception("This is not generic list");
            Type typeElement = fieldInfo.FieldType.GetGenericArguments()[0];
            IList returnObject = (IList)Activator.CreateInstance(fieldInfo.GetType());
            //Get the inner type

            while (sqlDataReader.Read())
            {
                object currentItem = Activator.CreateInstance(typeElement);
                foreach (FieldInfo f in typeElement.GetFields())
                {
                    if (Attribute.IsDefined(f, typeof(FieldMapAttribute)))
                    {
                        FieldMapAttribute fieldMapAttribute = (FieldMapAttribute)Attribute.GetCustomAttribute(f, typeof(FieldMapAttribute));
                        object val = sqlDataReader[fieldMapAttribute.FieldName];
                        if (val is DBNull)
                        {
                            f.SetValue(currentItem, null);
                        }
                        else
                            f.SetValue(currentItem, val);
                    }
                }
                returnObject.Add(currentItem);
            }
            return returnObject;
        }

        public static IDictionary GetDictionryObject(FieldInfo fieldInfo, IDataReader sqlDataReader)
        {
            string dictionaryKey = string.Empty;
            if (!fieldInfo.FieldType.IsGenericType || fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(IDictionary))
                throw new Exception("This is not generic Dictionary");

            Type keyElement = fieldInfo.FieldType.GetGenericArguments()[0];
            Type typeElement = fieldInfo.FieldType.GetGenericArguments()[1];

            if (Attribute.IsDefined(fieldInfo, typeof(DictionaryMapAttribute)))
            {
                DictionaryMapAttribute dictionaryMapAttribute = (DictionaryMapAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DictionaryMapAttribute));
                dictionaryKey = dictionaryMapAttribute.DictionaryKey;
            }
            else
                throw new Exception("DictionaryMapatrribute not defined");


            IDictionary returnObject = (IDictionary)Activator.CreateInstance(fieldInfo.FieldType);
            if (typeElement.IsGenericType)
            {
                IList list = (IList)Activator.CreateInstance(typeElement);
                int? lastAccountId = null;
                while (sqlDataReader.Read())
                {
                    object currentItem = Activator.CreateInstance(typeElement.GetGenericArguments()[0]);
                    if (lastAccountId != null)
                    {
                        if (lastAccountId != (int)sqlDataReader[dictionaryKey])
                        {
                            returnObject.Add(lastAccountId, list);
                            list = (IList)Activator.CreateInstance(typeElement);
                        }

                    }
                    foreach (FieldInfo f in typeElement.GetGenericArguments()[0].GetFields())
                    {
                        if (Attribute.IsDefined(f, typeof(FieldMapAttribute)))
                        {
                            FieldMapAttribute fieldMapAttribute = (FieldMapAttribute)Attribute.GetCustomAttribute(f, typeof(FieldMapAttribute));
                            object val = sqlDataReader[fieldMapAttribute.FieldName];
                            if (val is DBNull)
                            {
                                f.SetValue(currentItem, null);
                            }
                            else
                                f.SetValue(currentItem, val);
                        }
                    }
                    list.Add(currentItem);
                    lastAccountId = (int)sqlDataReader[dictionaryKey];
                }
                if (lastAccountId != null)
                {
                    returnObject.Add(lastAccountId, list);
                }
            }
            return returnObject;
        }

    }
    

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    sealed class TableMapAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        public readonly string TableName;

        // This is a positional argument
        public TableMapAttribute(string tableName)
        {
            this.TableName = tableName;
        }
    }

    #region AtrributesClass
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    sealed class FieldMapAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        public readonly string FieldName;
        public string Cast { get; set; }
        public bool IsKey { get; set; }
        public bool Show = true;
        public bool UseApplyFunction { get; set; }
        public bool RecursiveLookupField { get; set; }
        public bool IsDistinct { get; set; }

        // This is a positional argument
        public FieldMapAttribute(string fieldName)
        {
            this.FieldName = fieldName;

        }
    }

    
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    sealed class DictionaryMapAttribute : Attribute
    {
        public string Command { get; set; }
        public string DictionaryKey { get; set; }
        public bool IsStoredProcedure { get; set; }

    }
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    sealed class ListMapAttribute : Attribute
    {
        public string Command { get; set; }
        public string DictionaryKey { get; set; }
        public bool IsStoredProcedure { get; set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    sealed class HasChildOfTheSameAttribute : Attribute
    {

    }

    #endregion


}
