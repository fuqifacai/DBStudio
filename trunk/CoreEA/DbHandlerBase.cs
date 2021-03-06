using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;
using System.Data;
using System.Data.OleDb;
using CoreEA.Args;
using CoreEA.Exceptions;
using System.Diagnostics;
using CoreEA.LoginInfo;
using CoreEA.SchemaInfo;

namespace CoreEA
{
    internal abstract class BaseRobot : ICoreEAHander
    {


        internal DbConnection baseConn = null;
        internal BaseLoginInfo baseLoginInfo = null;

        internal Invalidation.InvalidationBase invalidator = null;
        public string CurPwd { get; set; }
        public string CurDatabase { get; set; }

        /// <summary>
        /// For Specifed Sql Command
        /// </summary>
        public abstract CommandTextBase CurrentCommandTextHandler {get;}

        /// <summary>
        /// Abstract class protected constructor
        /// 
        /// </summary>
        protected BaseRobot()
        {
        }

        #region 虛擬的LimitSize
        public virtual int MaxTableNameLength
        {
            get
            {
                return 128;
            }
        }

        public virtual int MaxDatabaseNameLength
        {
            get
            {
                return 128;
            }
        }
        public virtual int MaxColumnNameLength
        {
            get
            {
                return 128;
            }
        }
        public virtual int MaxCursorNameLength
        {
            get
            {
                return 128;
            }
        }
        public virtual int MaxSchemaNameLength
        {
            get
            {
                return 128;
            }
        }
        public virtual int MaxIdentifierNameLength
        {
            get
            {
                return 128;
            }
        }
        public virtual int MaxUsernameLength
        {
            get
            {
                return 128;
            }
        }
        #endregion

        #region 基类没有实现的Virtual方法

        public virtual bool ChangePassword(UserTokenInfo info)
        {
            throw new SubClassMustImplementException();
        }

        public virtual void SubmitChanges()
        {
            throw new SubClassMustImplementException();
        }

        public virtual List<string> GetSystemViewColumnsNameByViewName(string viewName)
        {
            throw new SubClassMustImplementException();
        }

        public virtual bool ExecuteTransactionList(List<DbCommand> sqlCmdList)
        {
            throw new SubClassMustImplementException();
        }

        public virtual List<BaseStoredProcedureInfo> GetStoredProceduresList()
        {
            List<BaseStoredProcedureInfo> info = new List<BaseStoredProcedureInfo>();
            DataTable dt = this.GetConnection().GetSchema("PROCEDURES");
            foreach (DataRow item in dt.Rows)
            {
                info.Add(new BaseStoredProcedureInfo()
                {
                    ProcedureName=item["name"].ToString()
                });
            }

            return info;
        }

        public virtual List<BaseViewInfo> GetViews()
        {
            List<BaseViewInfo> info = new List<BaseViewInfo>();

            DataTable dt= this.GetConnection().GetSchema("VIEWS");
            foreach (DataRow row in dt.Rows)
            {
                info.Add(new BaseViewInfo()
                {
                    ViewName=row["TABLE_NAME"].ToString()
                });
            }
            Debug.WriteLine("View count :" + info.Count);

            return info;
        }

        public virtual List<BaseTriggerInfo> GetTriggersInfo()
        {
            List<BaseTriggerInfo> info = new List<BaseTriggerInfo>();

            DataTable dt = this.GetConnection().GetSchema("TRIGGERS", new string[] { });
            foreach (DataRow item in dt.Rows)
            {
                info.Add(new BaseTriggerInfo()
                {
                    TriggerName = item["name"].ToString()
                });
            }
            return info;
        }

        public virtual DataTable GetKeyColumnInfoFromTable(string tableName)
        {
            throw new SubClassMustImplementException();
        }


        public virtual List<string> GetIndexNameListFromTable(string tableName)
        {
            throw new SubClassMustImplementException();
        }

        /// <summary>
        /// Recommend do not implement this method in base class
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="columnValue"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public virtual bool InsertData(string tableName, List<string> columnName, List<object> columnValue, List<DbType> types)
        {
            throw new SubClassMustImplementException();
        }


        [Obsolete("This method has obsoleted , please use CreateTable instead", true)]
        public virtual bool CreateTableWithSchemaInfo(List<CreateTableArgs> args, string tableName)
        {
            throw new SubClassMustImplementException();
        }

        public virtual bool CreateTableWithSql(string sqlCmd)
        {
            if (DoExecuteNonQuery(sqlCmd) != -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        #endregion

        #region 基类已经实现了的，但子类可以有变化的Virtual方法
        public virtual DataTable GetSchemaTable()
        {
            return baseConn.GetSchema();
        }


        /// <summary>
        /// Default Result is for SqlServer(SSCE as well)
        /// 
        /// The max number 35 is according to the value (enum SqlDbType) defined in system.data.dll (current version 4.0)
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual List<int> GetDbType()
        {
            List<int> typeList = new List<int>();
            for (int i = 0; i < 35; i++)
            {
                //If the enum do not contain this value ,then next
                if(string.IsNullOrEmpty(Enum.GetName(typeof(System.Data.SqlDbType),i)))
                {
                    continue;
                }

                typeList.Add((int)i);
            }

            return typeList;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnSchema"></param>
        /// <returns></returns>
        public virtual bool AddColumnToTable(string tableName, BaseColumnSchema columnSchema)
        {
            bool ret = false;
            try
            {
                string sqlCmd = string.Format("Alter table {0} add column {1} {2} ",
                    tableName, columnSchema.ColumnName, columnSchema.ColumnType);
                if (columnSchema.CharacterMaxLength != 0)
                {
                    sqlCmd += string.Format("({0})", columnSchema.CharacterMaxLength);
                }
                DoExecuteNonQuery(sqlCmd);

                ret = true;
            }
            catch (Exception ee)
            {
                throw ee;
            }

            return ret;
        }



        /// <summary>
        /// SqlServer and SSCE has beed tested ok . with this base method
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public virtual List<BasePrimaryKeyInfo> GetPrimaryKeysFromTable(string tableName)
        {
            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }
            List<BasePrimaryKeyInfo> pkeyInfoList = new List<BasePrimaryKeyInfo>();


            DataTable dt = ExecuteDataList(
        "SELECT u.COLUMN_NAME,u.CONSTRAINT_NAME,u.ORDINAL_POSITION " +
        "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS c INNER JOIN " +
            "INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS u ON c.CONSTRAINT_NAME = u.CONSTRAINT_NAME " +
        "where u.TABLE_NAME = '" + tableName + "' and c.CONSTRAINT_TYPE = 'PRIMARY KEY'").Tables[0];

#if DEBUG
            dt.TableName = "dfsfdscxcx";
            dt.WriteXml("pkeyInfo.xml");
#endif

            if ((dt != null) && (dt.Rows.Count > 0))
            {
                foreach (DataRow item in dt.Rows)
                {
                    pkeyInfoList.Add(new BasePrimaryKeyInfo()
                    {
                        ColumnName = item["COLUMN_NAME"].ToString(),
                        PKName = item["CONSTRAINT_NAME"].ToString(),
                        KeySequence=int.Parse(item["ORDINAL_POSITION"].ToString()),
                    });
                }
            }

            return pkeyInfoList;

        }

        public virtual bool ExecuteTransactionList(List<string> sqlList)
        {
            foreach (string t in sqlList)
            {
                if (invalidator.IsInvalidSql(t))
                {
                    return false;
                }
            }

            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }

            DbTransaction myTrans = baseConn.BeginTransaction();
            try
            {

                DbCommand myCmd = GetNewCommand();
                foreach (string t in sqlList)
                {
                    myCmd.Transaction = myTrans;
                    myCmd.CommandText = t;
                    myCmd.ExecuteNonQuery();
                }

                myTrans.Commit();
                return true;
            }
            catch
            {
                try
                {
                    myTrans.Rollback();
                }
                catch (DbException ex)
                {
                    if (myTrans.Connection != null)
                    {
                        GlobalDefine.SP.LastErrorMsg = "An exception of type " + ex.GetType() +
                            " was encountered while attempting to roll back the transaction.";
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Keep Virtual ,maybe some db is different
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public virtual int GetLastId(DbConnection connection)
        {
            using (DbCommand command = new OleDbCommand())
            {
                command.CommandType = CommandType.Text;

                command.CommandText = "SELECT @@IDENTITY";

                command.Connection = connection;

                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return -1;
                    else if (reader[0] is DBNull)
                        return -1;
                    else return Convert.ToInt32(reader[0]);
                }
            }
        }

        /// <summary>
        /// 直接生成带有参数的Command
        /// 这个方法整合了AddParameters方法,GetNewCommand方法,CreateParameter方法
        /// 建议使用此方法
        /// </summary>
        /// <param name="cmdText"></param>
        /// <param name="paraName"></param>
        /// <param name="paraType"></param>
        /// <param name="paraLength"></param>
        /// <param name="paraValue"></param>
        /// <returns></returns>
        public virtual DbCommand GetNewCommandWithGivenParameters(string cmdText, List<string> paraName,
            List<DbType> paraType, List<int> paraLength, List<object> paraValue)
        {
            DbCommand cmd = GetNewCommand();
            try
            {
                cmd.CommandText = cmdText;
                int paraCount = paraName.Count;
                List<DbParameter> paraList = new List<DbParameter>();

                for (int i = 0; i < paraCount; i++)
                {
                    paraList.Add(CreateParameter(paraName[i], paraType[i], paraLength[i], paraValue[i]));
                }

                AddParameters(cmd, paraList);

                return cmd;
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        /// <summary>
        ///		Adds any parameters to the command
        /// </summary>
        /// <param name="command">The command object you want prepared.</param>
        /// <param name="parameters">Zero or more parameters to add to the command.</param>
        public virtual void AddParameters(DbCommand command, List<DbParameter> parameters)
        {
            if (parameters != null)
            {
                foreach (DbParameter item in parameters)
                {
                    command.Parameters.Add(item);
                }
            }
        }

        /// <summary>
        ///		Adds any parameters to the command
        /// </summary>
        /// <param name="command">The command object you want prepared.</param>
        /// <param name="parameters">Zero or more parameters to add to the command.</param>
        public virtual void AddParameters(DbCommand command, params DbParameter[] parameters)
        {
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                    command.Parameters.Add(parameters[i]);
            }
        }

        /// <summary>
        ///		Creates a new parameter and sets the name of the parameter.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The type of the parameter.</param>
        /// <param name="size">The size of this parameter.</param>
        /// <param name="value">
        ///		The value you want assigned to this parameter. A null value will be converted to
        ///		a <see cref="DBNull"/> value in the parameter.
        /// </param>
        /// <returns>
        ///		A new <see cref="DbParameter"/> instance of the correct type for this database.</returns>
        /// <remarks>
        ///		The database will automatically add the correct prefix, like "@" for SQL Server, to the
        ///		parameter name. In other words, you can just supply the name without a prefix.
        /// </remarks>
        public virtual DbParameter CreateParameter(string name, DbType type, int size, object value)
        {
            DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.OleDb");

            DbParameter param = factory.CreateParameter();
            param.ParameterName = name;
            param.DbType = type;
            param.Size = size;
            param.Value = (value == null) ? DBNull.Value : value;
            return param;
        }

        public virtual IDataReader DoExecuteReader(DbCommand command, CommandBehavior cmdBehavior)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                IDataReader reader = command.ExecuteReader(cmdBehavior);
                return reader;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public virtual DataTable GetIndexInfoFromTable(string tableName)
        {
            DataTable dt = baseConn.GetSchema("Indexes", new string[] { null, null, tableName, null });

            return dt;
        }

        public virtual List<string> GetColumnNameListFromTable(string tableName)
        {
            tableName = GetMaskedTableName(tableName);

            List<string> rt = new List<string>();

            DataTable dt = baseConn.GetSchema("Columns", new string[] { null, null, tableName });
            foreach (DataRow item in dt.Rows)
            {
                rt.Add(item["column_name"].ToString());
            }

            return rt;
        }

        /// <summary>
        /// Keep Virtual ,maybe some db is different
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public virtual DataTable GetColumnInfoFromTable(string tableName)
        {

            DataTable dt = baseConn.GetSchema("Columns", new string[] { null, null, tableName, null });
            return dt;
        }

        /// <summary>
        /// Get a default value for a given data type
        /// </summary>
        /// <param name="dataType">Data type for which to get the default value</param>
        /// <returns>An object of the default value.</returns>
        public virtual Object GetDefaultByType(DbType dataType)
        {
            switch (dataType)
            {
                case DbType.AnsiString: return string.Empty;
                case DbType.AnsiStringFixedLength: return string.Empty;
                case DbType.Binary: return new byte[] { };
                case DbType.Boolean: return false;
                case DbType.Byte: return (byte)0;
                case DbType.Currency: return 0m;
                case DbType.Date: return DateTime.MinValue;
                case DbType.DateTime: return DateTime.MinValue;
                case DbType.Decimal: return 0m;
                case DbType.Double: return 0f;
                case DbType.Guid: return Guid.Empty;
                case DbType.Int16: return (short)0;
                case DbType.Int32: return 0;
                case DbType.Int64: return (long)0;
                case DbType.Object: return null;
                case DbType.Single: return 0F;
                case DbType.String: return String.Empty;
                case DbType.StringFixedLength: return string.Empty;
                case DbType.Time: return DateTime.MinValue;
                case DbType.VarNumeric: return 0;
                default: return null;

            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public virtual DbType GetDbTypeFromTypeName(string typeName)
        {
            DbType curType;

            switch (typeName)
            {
                case "Byte":
                    curType = DbType.Byte;
                    break;

                case "Boolean":
                    curType = DbType.Boolean;
                    break;
                case "Char":
                    curType = DbType.AnsiString;
                    break;
                case "DateTime":
                    curType = DbType.DateTime;
                    break;
                case "Decimal":
                    curType = DbType.Decimal;
                    break;
                case "Double":
                    curType = DbType.Double;
                    break;
                case "Int16":
                    curType = DbType.Int16;
                    break;
                case "Int32":
                    curType = DbType.Int32;
                    break;
                case "Int64":
                    curType = DbType.Int64;
                    break;
                case "SByte":
                    curType = DbType.SByte;
                    break;
                case "Single":
                    curType = DbType.Single;
                    break;
                case "String":
                    curType = DbType.String;
                    break;
                case "TimeSpan":
                    curType = DbType.Time;
                    break;
                case "UInt16":
                    curType = DbType.UInt16;
                    break;
                case "UInt32":
                    curType = DbType.UInt32;
                    break;
                case "UInt64":
                    curType = DbType.UInt64;
                    break;
                default:
                    curType = DbType.String;
                    break;
            }
            return curType;
        }

        public virtual DataTable GetSupportedDbType()
        {
            throw new SubClassMustImplementException();
        }

        /// <summary>
        /// No need to override
        /// </summary>
        public bool IsOpened
        {
            get
            {
                return CheckBaseConnectStatus();
            }
        }

        #endregion

        #region 一般情况下不需要重写的方法

        public BaseLoginInfo GetLoginInfo()
        {
            return baseLoginInfo;
        }
        /// <summary>
        /// Get The Create Primary Key String
        /// </summary>
        /// <param name="pKeyList"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public string GetCreatePrimaryKeySqlString(List<string> pKeyList, string tableName)
        {
            StringBuilder _sbScript = new StringBuilder();

            List<string> primaryKeys = pKeyList;

            if (primaryKeys.Count > 0)
            {
                _sbScript.AppendFormat("ALTER TABLE [{0}] ADD PRIMARY KEY (", tableName);

                primaryKeys.ForEach(delegate(string columnName)
                {
                    _sbScript.AppendFormat("[{0}]", columnName);
                    _sbScript.Append(",");
                });

                // Remove the last comma
                _sbScript.Remove(_sbScript.Length - 1, 1);
                _sbScript.AppendFormat(");{0}", System.Environment.NewLine);
            }

            return _sbScript.ToString();
        }

        /// <summary>
        /// Looks there no other robot override this method
        /// </summary>
        /// <param name="tableSchema"></param>
        /// <returns></returns>
        public virtual bool CreateTable(BaseTableSchema tableSchema)
        {
            bool ret = false;

            string cmdText = GetCreateTableString(tableSchema);
            DbCommand cmd = GetNewStringCommand(cmdText);
            Debug.WriteLine(cmdText);
            try
            {
                cmd.ExecuteNonQuery();
                ret = true;
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.Message);
#if DEBUG
                throw ee;
#else
                GlobalDefine.SP.LastErrorMsg = ee.Message;
                 return false;
#endif

            }

            //Here is another code whcih in SSCE mode 
            //If any question occurred,please diff them.

            //string[] createTableSchameCommands = GetCreateTableString(ts).Split(';');

            //foreach (var item in createTableSchameCommands)
            //{
            //    //Here the last item will be a '\n' 
            //    //so add judegement of length 
            //    //it not a good idea ,but workaround
            //    if (item.Length > 5)
            //    {
            //        DoExecuteNonQuery(item);
            //    }
            //}
            return ret;
        }

        public virtual bool DeleteTable(string tableName)
        {
            bool ret = false;

            string cmdText = "Drop Table "+GetMaskedTableName(tableName);
            DbCommand cmd = GetNewStringCommand(cmdText);

            try
            {
                cmd.ExecuteNonQuery();
                ret = true;
            }
            catch (Exception ee)
            {
#if DEBUG
                throw ee;
#else
                GlobalDefine.SP.LastErrorMsg = ee.Message;
#endif
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public DataColumn GetDataColumnObjectByName(string tableName, string columnName)
        {
            DataColumn column = null;
            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }

            DataTable dt = GetAllDataFromTable(tableName);
            foreach (DataColumn item in dt.Columns)
            {
                if (item.ColumnName == columnName)
                {
                    column = item;
                    break;
                }
            }
            return column;
        }

        /// <summary>
        /// Retrive the record by given column id and it's value
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="columnValue"></param>
        /// <returns></returns>
        public DataTable GetDataByColumnNameAndValue(string tableName, string columnName, string columnValue)
        {
            DataTable ds = null;
            if (invalidator.IsInvalidArguments(tableName))
            {
                throw new ArgumentException();
            }
            if (invalidator.IsInvalidArguments(columnName))
            {
                throw new ArgumentException();
            }

            tableName = GetMaskedTableName(tableName);
            columnName = GetMaskedColumnName(columnName);

            if (!IsOpened) { return null; }

            string strCmd = string.Empty;
            strCmd = "Select * from " + tableName + " where " + columnName + " = " + columnValue;


            using (DbCommand cmd = GetNewStringCommand(strCmd))
            using (DbDataAdapter da = GetDataAdapter(cmd))
            {
                //cmd.CommandTimeout = 30;
                da.SelectCommand = cmd;
                //If you wanna Update DataAdapter You must specfie the UpdataCommand/InsertCommand/DeleteCommand etc.. 
                try
                {
                    ds = new DataTable();
                    da.Fill(ds);//DataTable
                }
                catch (DbException ee)
                {
                    GlobalDefine.SP.LastErrorMsg = ee.Message;

                    throw ee;
                }
            }
            return ds;
        }

        public bool DeleteOneRecordInTable(string tableName, string recordName, string recordValue)
        {
            bool jieguo = false;
            if (invalidator.IsInvalidArguments(tableName))
            {
                return false;
            }
            if (invalidator.IsInvalidArguments(recordName))
            {
                return false;
            }
            if (invalidator.IsInvalidArguments(recordValue))
            {
                return false;
            }

            tableName = GetMaskedTableName(tableName);
            recordName = GetMaskedColumnName(recordName);

            if (!IsOpened) { return false; }
            try
            {
                string cmdStr = string.Format("delete from {0} where {1} = {2} ", tableName, recordName, recordValue);

                using (DbCommand myCmd = GetNewStringCommand(cmdStr))
                {
                    myCmd.ExecuteNonQuery();
                }
                jieguo = true;
            }
            catch (Exception ee)
            {
                GlobalDefine.SP.LastErrorMsg = ee.Message;
                throw ee;
            }
            return jieguo;


        }


        public DataSet ExecuteDataList(DbCommand dbCmd)
        {

            DataSet tempDs = new DataSet();

            DbDataAdapter ad = GetDataAdapter(dbCmd);

            ad.Fill(tempDs, "RandomTableName");

            return tempDs;
        }

        public DataSet ExecuteDataList(string cmdStr)
        {
            if (invalidator.IsInvalidSql(cmdStr))
            {
                throw new ArgumentException();
            }
            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }

            DbCommand dbCmd = GetNewStringCommand(cmdStr);

            DataSet tempDs = new DataSet();

            DbDataAdapter ad = GetDataAdapter(dbCmd);

            ad.Fill(tempDs, "RandomTableName");
            return tempDs;
        }


        public int GetMaxIDFromTable(string tableName, string IdName)
        {
            if (invalidator.IsInvalidArguments(tableName))
            {
                throw new ArgumentException();
            }

            if (invalidator.IsInvalidArguments(IdName))
            {
                throw new ArgumentException();
            }
            tableName = GetMaskedTableName(tableName);

            string sqlCmd = "select " + IdName + " from " + tableName + " order by " + IdName + " desc";
            try
            {
                if (!IsOpened) { return GlobalDefine.SP.ErrorNumber; }
                object temp = DoExecuteScalar(sqlCmd);
                if (temp == null)
                {
                    return 0;
                }

                int maxID = (int)temp;
                maxID++;
                return maxID;
            }
            catch (DbException ee)
            {
#if DEBUG
                throw ee;
#else
                GlobalDefine.SP.LastErrorMsg = ee.Message;
                return GlobalDefine.SP.ErrorNumber;
#endif

            }
        }

        public int DoExecuteNonQuery(string sqlCmd)
        {
            if (invalidator.IsInvalidSql(sqlCmd))
            {
                throw new ArgumentException();
            }

            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }

            using (DbCommand cmd = GetNewStringCommand(sqlCmd))
            {
                try
                {
                    int count= cmd.ExecuteNonQuery();
                    return count;
                }
                catch (Exception ee)
                {
                    GlobalDefine.SP.LastErrorMsg = ee.Message;

                    throw ee;
                }
            }
           
        }

        public object DoExecuteScalar(string sqlCmd)
        {
            if (!IsOpened)
            {
                throw new ConnectErrorException();
            }
            try
            {
                object result;
                using (DbCommand myCmd = GetNewStringCommand(sqlCmd))
                {
                    result = myCmd.ExecuteScalar();
                }

                return result;
            }
            catch (OleDbException ee)
            {
                GlobalDefine.SP.LastErrorMsg = ee.Message;
                throw ee;
            }
        }

        public DbConnection GetConnection()
        {
            return baseConn;
        }

        /// <summary>
        /// Need Pay attention to this method . 
        /// May by will interrupt current wrapper
        /// </summary>
        /// <param name="dbConn"></param>
        public void SetConnection(DbConnection dbConn)
        {
            if (dbConn != null)
            {
                baseConn = dbConn;
            }
        }

        /// <summary>
        /// This method is always override by Inherited Class
        /// </summary>
        /// <returns></returns>
        public DbCommand GetNewCommand()
        {
            return GetNewStringCommand(string.Empty);
        }



        /// <summary>
        /// No need to override
        /// </summary>
        public void Close()
        {
            if (IsOpened)
            {
                baseConn.Close();
            }
        }

        /// <summary>
        /// Get all record from a table by given table name
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="orderbyOwner"></param>
        /// <param name="IsDesc"></param>
        /// <param name="recordCount"></param>
        /// <returns></returns>
        public DataTable GetAllDataFromTable(string tableName, string orderbyOwner, bool IsDesc, int recordCount)
        {
            tableName = GetMaskedTableName(tableName);

            DataTable ds = null;


            if (!IsOpened) { return null; }

            string strCmd = "select";

            if (recordCount != 0)
            {
                strCmd += " top " + recordCount.ToString();
            }

            if ((orderbyOwner == null) || (orderbyOwner.Length == 0))
            {
                strCmd += " * from " + tableName;
            }
            else
            {
                strCmd += " * from " + tableName + " order by " + orderbyOwner;

                if (IsDesc)
                {
                    strCmd += " desc";
                }

            }


            using (DbCommand cmd = GetNewStringCommand(strCmd))
            using (DbDataAdapter da = GetDataAdapter(cmd))
            {
                da.SelectCommand = cmd;
                //If you wanna Update DataAdapter You must specfie the UpdataCommand/InsertCommand/DeleteCommand etc.. 
                try
                {
                    ds = new DataTable();
                    da.Fill(ds);
                }
                catch (Exception ee)
                {
                    GlobalDefine.SP.LastErrorMsg = ee.Message;
                    throw ee;
                }
            }
            return ds;
        }


        /// <summary>
        /// Some database no need specified the database
        /// 
        /// </summary>
        /// <returns></returns>
        public List<string> GetTableListInDatabase()
        {
            if (string.IsNullOrEmpty(CurDatabase))
            {
                return GetTableListInDatabase("");
            }
            else
            {
                return GetTableListInDatabase(CurDatabase);
            }
        }

        /// <summary>
        /// Delete Duplicate Data
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="primaryKeyName"></param>
        /// <param name="matchColumnName"></param>
        public void DeleteDuplicateData(string tableName, string primaryKeyName, string matchColumnName)
        {
            tableName = GetMaskedTableName(tableName);

            DbCommand cmd = GetNewCommand();
            try
            {
                cmd.CommandText = String.Format("Delete from {0} where {1} not in (select max({1}) from {0} group by {2})",
                       GetMaskedTableName(tableName), primaryKeyName, matchColumnName);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 查找某条记录N到M的
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="primaryKeyName"></param>
        /// <param name="FromN"></param>
        /// <param name="ToN"></param>
        /// <returns></returns>
        public DataTable SelectDataFromNtoNByPrimaryKey(string tableName, string primaryKeyName, int FromN, int ToN)
        {
            tableName = GetMaskedTableName(tableName);

            if (ToN <= FromN)
            {
                throw new ArgumentException();
            }

            DbCommand cmd = GetNewCommand();
            try
            {
                cmd.CommandText = String.Format(
                    "select * from (select top {0} * from (select top {1} * from {2} order by {3})t1 order by {3})t2 order by {3} desc",
                        ToN - FromN + 1, ToN, GetMaskedTableName(tableName), primaryKeyName);
                DataTable ds = this.ExecuteDataList(cmd).Tables[0];
                return ds;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Remove all data according to the tablename
        /// </summary>
        /// <param name="tableName"></param>
        public void RemoveAllData(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new Exception("Not valid Argument");
            }
            tableName = GetMaskedTableName(tableName);

            DbCommand cmd = GetNewCommand();
            try
            {
                cmd.CommandText = string.Format("delete from {0}", GetMaskedTableName(tableName));
                this.DoExecuteNonQuery(cmd);
            }
            catch (Exception e)
            {
                throw e;
            }
        }



        public string LastErrorMsg
        {
            get
            {
                return GlobalDefine.SP.LastErrorMsg;
            }
        }

        /// <summary>
        /// Executes the query for <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The <see cref="DbCommand"/> representing the query to execute.</param>
        /// <returns>The quantity of rows affected.</returns>
        public int DoExecuteNonQuery(DbCommand command)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public object DoExecuteScalar(DbCommand command)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                object returnValue = command.ExecuteScalar();
                return returnValue;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public System.Data.DataTable GetAllDataFromTable(string tableName)
        {
            return GetAllDataFromTable(tableName, "", false, 0);
        }


        private bool CheckBaseConnectStatus()
        {
            if (baseConn == null)
            {
                return false;
            }
            if (baseConn.State != ConnectionState.Open)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// No need to override
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (IsOpened)
                {
                    Close();
                }
            }
            catch (DataException de)
            {
                throw de;
            }
            catch (Exception ee)
            {
                throw ee;
            }

        }

        #endregion

        #region 子类必须实现的方法


        public abstract string GetCreateTableString(BaseTableSchema ts);


        public abstract BaseTableSchema GetTableSchemaInfoObject(string tableName);

        public abstract bool HasIdentityColumnInTable(string tableName);

        public abstract List<string> GetSystemViewList();


        public abstract DataTable GetProviderInfoFromTable(string tableName);

        public abstract CoreE.UsedDatabaseType HostedType { get; }

        public abstract DbDataAdapter GetDataAdapter(DbCommand dbCmd);

        public abstract DbCommand GetNewStringCommand(string sql);

        /// <summary>
        /// This method can not abstract
        /// </summary>
        /// <returns></returns>
        public abstract List<string> GetDatabaseList();

       
        public abstract bool CreateDatabase(BaseLoginInfo loginInfo);

        /// <summary>
        /// 如果当前的HANDLE 已经打开或者拥有CONNECTION则不会再次创建联接
        /// </summary>
        /// <param name="info"></param>
        public abstract void Open(BaseLoginInfo info);

        /// <summary>
        /// 如果当前的HANDLE 已经打开或者拥有CONNECTION则不会再次创建联接
        /// </summary>
        /// <param name="connectionString"></param>
        public abstract void Open(string connectionString);


        /// <summary>
        /// Please pay attention to this method 
        /// We have another same name method which no need parameters
        /// When using ServerSide hosted db ,please use this method
        /// otherwise use no parameter method 
        /// </summary>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public abstract List<string> GetTableListInDatabase(string databaseName);


        public abstract bool ExecuteProcedureWithNoQuery(string procedureName, object[] varList, OleDbType[] dbTypeList, int[] objectLengthList, object[] objectList, object[] objectValueList);

        /// <summary>
        /// Get the mask char when query with table name 
        /// </summary>
        /// <returns></returns>
        public abstract string GetMaskedTableName(string tableName);

        /// <summary>
        /// Get the mask char when query with column name 
        /// </summary>
        /// <returns></returns>
        public abstract string GetMaskedColumnName(string columnName);

        /// <summary>
        /// Get Column Length From Various Db Type
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columName"></param>
        /// <returns></returns>
        public abstract Decimal GetColumnLength(string tableName, string columName);
        #endregion Endof Abstract methods
    }
}
