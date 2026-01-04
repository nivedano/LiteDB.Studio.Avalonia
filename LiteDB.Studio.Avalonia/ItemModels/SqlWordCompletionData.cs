using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;
using System.Collections.Generic;

namespace LiteDB.Studio.Avalonia.ItemModels
{
    public class SqlWordCompletionData : ICompletionData
    {
        public SqlWordCompletionData(string text)
        {
            this.Text = text;
            this.Description = text;
        }

        public SqlWordCompletionData(string text, string description)
        {
            this.Text = text;
            this.Description = description;
        }

        public IImage? Image
        {
            get 
            { 
                return null; 
            }
        }

        public string Text 
        {
            get;
            private set; 
        }
        public object Content
        {
            get 
            { 
                return this.Text;
            }
        }

        public object Description
        {
            get;
            set;
        }

        public double Priority 
        { 
            get 
            {
                return 0;
            }
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            if (insertionRequestEventArgs is KeyEventArgs eventArgs)
            {
                if (eventArgs.Source is CompletionWindow cw)
                {
                    cw.Tag = string.Empty;
                }
            }
            if (completionSegment.Length == 0)
            {
                return;
            }
            textArea.Document.Replace(completionSegment, this.Text);
        }

        public static List<SqlWordCompletionData> Instances
        {
            get
            {
                return new()
                {
                    // Comparison operators
                    new SqlWordCompletionData("AND"),
                    new SqlWordCompletionData("OR"),
                    new SqlWordCompletionData("LIKE"),
                    new SqlWordCompletionData("BETWEEN"),
                    new SqlWordCompletionData("IN"),
                    // Value keywords
                    new SqlWordCompletionData("TRUE"),
                    new SqlWordCompletionData("FALSE"),
                    new SqlWordCompletionData("NULL"),
                    // SQL main keywords
                    new SqlWordCompletionData("BEGIN"),
                    new SqlWordCompletionData("COMMIT"),
                    new SqlWordCompletionData("ROLLBACK"),
                    new SqlWordCompletionData("TRANS"),
                    new SqlWordCompletionData("TRANSACTION"),
                    new SqlWordCompletionData("ANALYZE"),
                    new SqlWordCompletionData("CHECKPOINT"),
                    new SqlWordCompletionData("REBUILD"),
                    new SqlWordCompletionData("PRAGMA"),
                    new SqlWordCompletionData("CREATE"),
                    new SqlWordCompletionData("INDEX"),
                    new SqlWordCompletionData("ON"),
                    new SqlWordCompletionData("UNIQUE"),
                    new SqlWordCompletionData("DELETE"),
                    new SqlWordCompletionData("DROP"),
                    new SqlWordCompletionData("COLLECTION"),
                    new SqlWordCompletionData("RENAME"),
                    new SqlWordCompletionData("TO"),
                    new SqlWordCompletionData("INSERT"),
                    new SqlWordCompletionData("INTO"),
                    new SqlWordCompletionData("VALUES"),
                    new SqlWordCompletionData("SELECT"),
                    new SqlWordCompletionData("ALL"),
                    new SqlWordCompletionData("FULL"),
                    new SqlWordCompletionData("FROM"),
                    new SqlWordCompletionData("WHERE"),
                    new SqlWordCompletionData("INCLUDE"),
                    new SqlWordCompletionData("GROUP"),
                    new SqlWordCompletionData("ORDER"),
                    new SqlWordCompletionData("BY"),
                    new SqlWordCompletionData("AS"),
                    new SqlWordCompletionData("ASC"),
                    new SqlWordCompletionData("DESC"),
                    new SqlWordCompletionData("HAVING"),
                    new SqlWordCompletionData("LIMIT"),
                    new SqlWordCompletionData("OFFSET"),
                    new SqlWordCompletionData("FOR"),
                    new SqlWordCompletionData("SET"),
                    new SqlWordCompletionData("EXPLAIN"),
                    new SqlWordCompletionData("UPDATE"),
                    new SqlWordCompletionData("REPLACE"),
                    new SqlWordCompletionData("VACUUM"),
                    new SqlWordCompletionData("CHECK"),
                    new SqlWordCompletionData("MAP"),
                    new SqlWordCompletionData("FILTER"),
                    new SqlWordCompletionData("SORT"),
                    // SQL functions/methods
                    new SqlWordCompletionData("USER_VERSION"),
                    new SqlWordCompletionData("COLLATION"),
                    new SqlWordCompletionData("TIMEOUT"),
                    new SqlWordCompletionData("LIMIT_SIZE"),
                    new SqlWordCompletionData("UTC_DATE"),
                    new SqlWordCompletionData("CHECKPOINT"),
                    new SqlWordCompletionData("COUNT"),
                    new SqlWordCompletionData("MIN"),
                    new SqlWordCompletionData("MAX"),
                    new SqlWordCompletionData("FIRST"),
                    new SqlWordCompletionData("LAST"),
                    new SqlWordCompletionData("AVG"),
                    new SqlWordCompletionData("SUM"),
                    new SqlWordCompletionData("ANY"),
                    new SqlWordCompletionData("JOIN"),
                    new SqlWordCompletionData("MINVALUE"),
                    new SqlWordCompletionData("OBJECTID"),
                    new SqlWordCompletionData("GUID"),
                    new SqlWordCompletionData("NOW"),
                    new SqlWordCompletionData("NOW_UTC"),
                    new SqlWordCompletionData("TODAY"),
                    new SqlWordCompletionData("MAXVALUE"),
                    new SqlWordCompletionData("INT32"),
                    new SqlWordCompletionData("INT"),
                    new SqlWordCompletionData("INT64"),
                    new SqlWordCompletionData("LONG"),
                    new SqlWordCompletionData("DOUBLE"),
                    new SqlWordCompletionData("DECIMAL"),
                    new SqlWordCompletionData("STRING"),
                    new SqlWordCompletionData("ARRAY"),
                    new SqlWordCompletionData("BINARY"),
                    new SqlWordCompletionData("BOOLEAN"),
                    new SqlWordCompletionData("BOOL"),
                    new SqlWordCompletionData("DATETIME"),
                    new SqlWordCompletionData("DATETIME_UTC"),
                    new SqlWordCompletionData("DATE"),
                    new SqlWordCompletionData("DATE_UTC"),
                    new SqlWordCompletionData("IS_MINVALUE"),
                    new SqlWordCompletionData("IS_NULL"),
                    new SqlWordCompletionData("IS_INT32"),
                    new SqlWordCompletionData("IS_INT"),
                    new SqlWordCompletionData("IS_INT64"),
                    new SqlWordCompletionData("IS_LONG"),
                    new SqlWordCompletionData("IS_DOUBLE"),
                    new SqlWordCompletionData("IS_DECIMAL"),
                    new SqlWordCompletionData("IS_NUMBER"),
                    new SqlWordCompletionData("IS_STRING"),
                    new SqlWordCompletionData("IS_DOCUMENT"),
                    new SqlWordCompletionData("IS_ARRAY"),
                    new SqlWordCompletionData("IS_BINARY"),
                    new SqlWordCompletionData("IS_OBJECTID"),
                    new SqlWordCompletionData("IS_GUID"),
                    new SqlWordCompletionData("IS_BOOLEAN"),
                    new SqlWordCompletionData("IS_BOOL"),
                    new SqlWordCompletionData("IS_DATETIME"),
                    new SqlWordCompletionData("IS_DATE"),
                    new SqlWordCompletionData("IS_MAXVALUE"),
                    new SqlWordCompletionData("YEAR"),
                    new SqlWordCompletionData("MONTH"),
                    new SqlWordCompletionData("DAY"),
                    new SqlWordCompletionData("HOUR"),
                    new SqlWordCompletionData("MINUTE"),
                    new SqlWordCompletionData("SECOND"),
                    new SqlWordCompletionData("DATEADD"),
                    new SqlWordCompletionData("DATEDIFF"),
                    new SqlWordCompletionData("TO_LOCAL"),
                    new SqlWordCompletionData("TO_UTC"),
                    new SqlWordCompletionData("ABS"),
                    new SqlWordCompletionData("ROUND"),
                    new SqlWordCompletionData("POW"),
                    new SqlWordCompletionData("JSON"),
                    new SqlWordCompletionData("EXTEND"),
                    new SqlWordCompletionData("ITEMS"),
                    new SqlWordCompletionData("CONCAT"),
                    new SqlWordCompletionData("RAW_ID"),
                    new SqlWordCompletionData("KEYS"),
                    new SqlWordCompletionData("OID_CREATIONTIME"),
                    new SqlWordCompletionData("IIF"),
                    new SqlWordCompletionData("COALESCE"),
                    new SqlWordCompletionData("LENGTH"),
                    new SqlWordCompletionData("TOP"),
                    new SqlWordCompletionData("UNION"),
                    new SqlWordCompletionData("EXCEPT"),
                    new SqlWordCompletionData("DISTINCT"),
                    new SqlWordCompletionData("LOWER"),
                    new SqlWordCompletionData("UPPER"),
                    new SqlWordCompletionData("LTRIM"),
                    new SqlWordCompletionData("RTRIM"),
                    new SqlWordCompletionData("TRIM"),
                    new SqlWordCompletionData("INDEXOF"),
                    new SqlWordCompletionData("SUBSTRING"),
                    new SqlWordCompletionData("REPLACE"),
                    new SqlWordCompletionData("LPAD"),
                    new SqlWordCompletionData("RPAD"),
                    new SqlWordCompletionData("SPLIT"),
                    new SqlWordCompletionData("FORMAT"),
                    new SqlWordCompletionData("JOIN"),
                    new SqlWordCompletionData("IS_MATCH"),
                    new SqlWordCompletionData("MATCH"),
                };
            }
        }
    }
}
