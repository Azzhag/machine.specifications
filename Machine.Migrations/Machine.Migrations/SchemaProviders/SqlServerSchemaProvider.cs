﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Text;

using Machine.Migrations.DatabaseProviders;

namespace Machine.Migrations.SchemaProviders
{
  public class SqlServerSchemaProvider : ISchemaProvider
  {
    #region Member Data
    private readonly IDatabaseProvider _databaseProvider;
    #endregion

    #region SqlServerSchemaProvider()
    public SqlServerSchemaProvider(IDatabaseProvider databaseProvider)
    {
      _databaseProvider = databaseProvider;
    }
    #endregion

    #region ISchemaProvider Members
    public void AddTable(string table, ICollection<Column> columns)
    {
      if (columns.Count == 0)
      {
        throw new ArgumentException("columns");
      }
      using (Machine.Core.LoggingUtilities.Log4NetNdc.Push("AddTable"))
      {
        StringBuilder sb = new StringBuilder();
        sb.Append("CREATE TABLE \"").Append(table).Append("\" (");
        bool first = true;
        foreach (Column column in columns)
        {
          if (!first) sb.Append(",");
          sb.AppendLine().Append(ColumnToCreateTableSql(column));
          first = false;
        }
        foreach (Column column in columns)
        {
          string sql = ColumnToConstraintsSql(table, column);
          if (sql != null)
          {
            sb.Append(",").AppendLine().Append(sql);
          }
        }
        sb.AppendLine().Append(")");
        _databaseProvider.ExecuteNonQuery(sb.ToString());
      }
    }

    public void DropTable(string table)
    {
      _databaseProvider.ExecuteNonQuery("DROP TABLE \"{0}\"", table);
    }

    public bool HasTable(string table)
    {
      using (Machine.Core.LoggingUtilities.Log4NetNdc.Push("HasTable({0})", table))
      {
        return _databaseProvider.ExecuteScalar<Int32>("SELECT COUNT(*) FROM syscolumns WHERE id = object_id('{0}')", table) > 0;
      }
    }

    public void AddColumn(string table, string column, Type type)
    {
      AddColumn(table, column, type, -1, false, false);
    }

    public void AddColumn(string table, string column, Type type, short size, bool isPrimaryKey, bool allowNull)
    {
      _databaseProvider.ExecuteNonQuery("ALTER TABLE \"{0}\" ADD {1}", table, ColumnToCreateTableSql(new Column(column, type, size, isPrimaryKey, allowNull)));
    }

    public void AddColumn(string table, string column, Type type, bool allowNull)
    {
      AddColumn(table, column, type, 0, false, allowNull);
    }

    public void AddColumn(string table, string column, Type type, short size, bool allowNull)
    {
      AddColumn(table, column, type, size, false, allowNull);
    }

    public void RemoveColumn(string table, string column)
    {
      _databaseProvider.ExecuteNonQuery("ALTER TABLE \"{0}\" DROP COLUMN \"{1}\"", table, column);
    }

    public void RenameTable(string table, string newName)
    {
      _databaseProvider.ExecuteNonQuery("EXEC sp_rename '{0}', '{1}'", table, newName);
    }

    public void RenameColumn(string table, string column, string newName)
    {
      _databaseProvider.ExecuteNonQuery("EXEC sp_rename '{0}.{1}', '{2}', 'COLUMN'", table, column, newName);
    }

    public bool HasColumn(string table, string column)
    {
      using (Machine.Core.LoggingUtilities.Log4NetNdc.Push("HasColumn({0}.{1})", table, column))
      {
        if (!HasTable(table))
        {
          return false;
        }
        return _databaseProvider.ExecuteScalar<Int32>("SELECT COUNT(*) FROM syscolumns WHERE id = object_id('{0}') AND name = '{1}'", table, column) > 0;
      }
    }

    public void ChangeColumn(string table, string column, Type type, short size, bool allowNull)
    {
      _databaseProvider.ExecuteNonQuery("ALTER TABLE \"{0}\" ALTER COLUMN {1}", table, ColumnToCreateTableSql(new Column(column, type, size, false, allowNull)));
    }

    public string[] Columns(string table)
    {
      using (IDataReader reader = _databaseProvider.ExecuteReader("SELECT name FROM syscolumns WHERE id = object_id('{0}')", table))
      {
        List<string> values = new List<string>();
        while (reader.Read())
        {
          values.Add(reader.GetString(0));
        }
        return values.ToArray();
      }
    }

    public string[] Tables()
    {
      using (IDataReader reader = _databaseProvider.ExecuteReader("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"))
      {
        List<string> values = new List<string>();
        while (reader.Read())
        {
          values.Add(reader.GetString(0));
        }
        return values.ToArray();
      }
    }

    public void AddForiegnKeyConstraint(string table, string name, string column, string foreignTable, string foreignColumn)
    {
      _databaseProvider.ExecuteNonQuery("ALTER TABLE \"{0}\" ADD CONSTRAINT \"{1}\" FOREIGN KEY (\"{2}\") REFERENCES \"{3}\" (\"{4}\")", table, name, column, foreignTable, foreignColumn);
    }

    public void DropConstraint(string table, string name)
    {
      _databaseProvider.ExecuteNonQuery("ALTER TABLE \"{0}\" DROP CONSTRAINT \"{1}\"", table, name);
    }
    #endregion

    #region Member Data
    public static string ColumnToCreateTableSql(Column column)
    {
      return String.Format("\"{0}\" {1} {2} {3}", column.Name, DotNetToSqlType(column.Type, column.Size), column.AllowNull ? "" : "NOT NULL", column.IsPrimaryKey ? "IDENTITY(1, 1)" : "").Trim();
    }

    public static string ColumnToConstraintsSql(string tableName, Column column)
    {
      if (column.IsPrimaryKey)
      {
        return String.Format("CONSTRAINT PK_{0} PRIMARY KEY CLUSTERED (\"{1}\")", tableName, column.Name);
      }
      return null;
    }

    public static string DotNetToSqlType(Type type, int size)
    {
      if (type == typeof(Int16))
      {
        return "INT";
      }
      if (type == typeof(Int32))
      {
        return "INT";
      }
      if (type == typeof(Int64))
      {
        return "BIGINT";
      }
      if (type == typeof(byte[]))
      {
        return "VARBINARY(MAX)";
      }
      if (type == typeof(String))
      {
        if (size == 0)
        {
          return "NVARCHAR(MAX)";
        }
        return String.Format("NVARCHAR({0})", size);
      }
			if (type == typeof(DateTime))
			{
				return "DATETIME";
			}
			if (type == typeof(bool))
			{
				return "BIT";
			}
			if (type == typeof(float) || type == typeof(double))
			{
				return "REAL";
			}
      if (type == typeof(decimal))
      {
        return "MONEY";
      }
      if (type == typeof(SqlMoney))
      {
        return "MONEY";
      }
      throw new ArgumentException("type");
    }
    #endregion
  }
}