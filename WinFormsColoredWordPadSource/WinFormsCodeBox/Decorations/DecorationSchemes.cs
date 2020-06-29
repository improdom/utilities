using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
namespace WinFormsCodeBox.Decorations
{
    public static class DecorationSchemes
    {


        #region C#
        public static DecorationScheme CSharp3
        {
            get
            {
                DecorationScheme ds = new DecorationScheme();

                ds.Name = "C#";
                ds.Decorations.Add( new MultiRegexWordDecoration()
                {
                    Color = Color.Blue,
                    Words = CSharpReservedWords(),
                    Key = "Reserved Words"
                });
                
                ds.Decorations.Add( new MultiRegexWordDecoration()
                {
                    Color = Color.Blue,
                    Words = CSharpVariableReservations()
                });

                ds.Decorations.Add(new MultiStringDecoration()
                {
                    Color = Color.Blue,
                    Strings = CSharpRegions()
                });

                ds.Decorations.Add(new RegexDecoration()
                {   Key="Quoted",
                    Color = Color.Brown,
                    RegexString = "(?s:\".*?\")"
                });
             
                //Color single line comments green
                ds.Decorations.Add(new RegexDecoration()
                {
                    DecorationType = EDecorationType.TextColor,
                    Color = Color.Green,
                    RegexString = "//.*"
                });
                
                //Color multiline comments green
                ds.Decorations.Add(new RegexDecoration()
                {
                    DecorationType = EDecorationType.TextColor,
                    Color = Color.Green,
                    RegexString = @"(?s:/\*.*?\*/)"
                });

                return ds;
            }
        }

        private static List<string> CSharpReservedWords()
        {
            return new List<string>() { "using" ,"namespace" , "static", "class" ,"public" ,"get" , "private" , "return" ,"partial" , "new"
          ,"set" , "value" , "protected" , "override", "switch" , "case" , "break" , "foreach" ,"if"};
        }

        private static List<string> CSharpVariableReservations()
        {
            return new List<string>() { "string", "int", "double", "long", "void" , "true", "false", "null"};
        }

        private static List<string> CSharpRegions()
        {
            return new List<string>() { "#region", "#endregion" };
        }

        #endregion


        #region SQL Server

        public static DecorationScheme SQLServer2008
        {
            get
            {
                DecorationScheme ds = new DecorationScheme();
                ds.Name = "SQL Server";

                // Color Built in functions Magenta
                ds.Decorations.Add(new MultiRegexWordDecoration()
                {
                    Color = Color.Magenta,
                    Words = GetBuiltInFunctions()
                });
                 
                //Color global variables Magenta
                 ds.Decorations.Add(new MultiStringDecoration()
                 {
                    Color = Color.Magenta,
                    Strings=GetGlobalVariables() 
                 });

                //Color most reserved words blue
                 ds.Decorations.Add(new MultiRegexWordDecoration()
                 {
                     Color = Color.Blue,
                     Words = GetBlueKeyWords()
                 });

                 ds.Decorations.Add(new MultiRegexWordDecoration()
                 {
                     Color = Color.Gray,
                     Words = GetGrayKeyWords()
                 });

                 ds.Decorations.Add(new MultiRegexWordDecoration()
                 {
                     Color = Color.Blue,
                     Words = GetDataTypes()
                 });

                 ds.Decorations.Add(new MultiRegexWordDecoration()
                 {
                     Color = Color.Green,
                     Words = GetSystemViews()
                 });

                 ds.Decorations.Add(new MultiStringDecoration()
                 {
                     Color = Color.Gray,
                     Strings = GetOperators()
                 });

                 ds.Decorations.Add(new RegexDecoration()
                 {
                     Color = Color.Red,
                     RegexString = "'.*?'"
                 });

                 ds.Decorations.Add(new RegexDecoration()
                 {
                     DecorationType = EDecorationType.TextColor,
                     Color = Color.Red,
                     RegexString = "N''"
                 });
                
                //Color single line comments green
                 ds.Decorations.Add(new RegexDecoration()
                 {
                     DecorationType = EDecorationType.TextColor,
                     Color = Color.Green,
                     RegexString = "--.*"
                 });
                
                //Color multiline comments green
                 ds.Decorations.Add(new RegexDecoration()
                 {
                     DecorationType = EDecorationType.TextColor,
                     Color = Color.Green,
                     RegexString = @"(?s:/\*.*?\*/)"
                 });
               
                return ds;
            }
        }



        static List<string> GetBuiltInFunctions()
        {
            List<string> funct =new List<string>() { "parsename", "db_name", "object_id", "count", "ColumnProperty", "LEN",
                             "CHARINDEX" ,"isnull" , "SUBSTRING" };
            return funct;

        }

        static List<string> GetGlobalVariables()
        {

            List<string> globals = new List<string>() { "@@fetch_status" };
            return globals;

        }

        static List<string> GetDataTypes()
        {
            List<string> dt = new List<string>() { "int", "sysname", "nvarchar", "char" };
            return dt;

        }


        static List<string> GetBlueKeyWords() // List from 
        {
            List<string> res = new List<string>()  {"ADD","EXISTS","PRECISION","ALL","EXIT","PRIMARY","ALTER","EXTERNAL",
                            "PRINT","FETCH","PROC","ANY","FILE","PROCEDURE","AS","FILLFACTOR",
                            "PUBLIC","ASC","FOR","RAISERROR","AUTHORIZATION","FOREIGN","READ","BACKUP",
                            "FREETEXT","READTEXT","BEGIN","FREETEXTTABLE","RECONFIGURE","BETWEEN","FROM",
                            "REFERENCES","BREAK","FULL","REPLICATION","BROWSE","FUNCTION","RESTORE",
                            "BULK","GOTO","RESTRICT","BY","GRANT","RETURN","CASCADE","GROUP","REVERT",
                            "CASE","HAVING","REVOKE","CHECK","HOLDLOCK","RIGHT","CHECKPOINT","IDENTITY",
                            "ROLLBACK","CLOSE","IDENTITY_INSERT","ROWCOUNT","CLUSTERED","IDENTITYCOL",
                            "ROWGUIDCOL","COALESCE","IF","RULE","COLLATE","IN","SAVE","COLUMN","INDEX",
                            "SCHEMA","COMMIT","INNER","SECURITYAUDIT","COMPUTE","INSERT","SELECT",
                            "CONSTRAINT","INTERSECT","SESSION_USER","CONTAINS","INTO","SET","CONTAINSTABLE",
                            "SETUSER","CONTINUE","JOIN","SHUTDOWN","CONVERT","KEY","SOME","CREATE",
                            "KILL","STATISTICS","CROSS","LEFT","SYSTEM_USER","CURRENT","LIKE","TABLE",
                            "CURRENT_DATE","LINENO","TABLESAMPLE","CURRENT_TIME","LOAD","TEXTSIZE",
                            "CURRENT_TIMESTAMP","MERGE","THEN","CURRENT_USER","NATIONAL","TO","CURSOR",
                            "NOCHECK","TOP","DATABASE","NONCLUSTERED","TRAN","DBCC","NOT","TRANSACTION",
                            "DEALLOCATE","NULL","TRIGGER","DECLARE","NULLIF","TRUNCATE","DEFAULT","OF",
                            "TSEQUAL","DELETE","OFF","UNION","DENY","OFFSETS","UNIQUE","DESC", "ON", 
                            "UNPIVOT","DISK","OPEN","UPDATE","DISTINCT","OPENDATASOURCE","UPDATETEXT",
                            "DISTRIBUTED","OPENQUERY","USE","DOUBLE","OPENROWSET","USER","DROP","OPENXML",
                            "VALUES","DUMP","OPTION","VARYING","ELSE","OR","VIEW","END","ORDER","WAITFOR",
                            "ERRLVL","OUTER","WHEN","ESCAPE","OVER","WHERE","EXCEPT","PERCENT","WHILE",
                            "EXEC","PIVOT","WITH","EXECUTE","PLAN","WRITETEXT", "GO", "ANSI_NULLS",
                            "NOCOUNT", "QUOTED_IDENTIFIER", "master"};

            return res;
        }


        static List<string> GetGrayKeyWords()
        {
            List<string> res = new List<string>() { "AND", "Null", "IS" };

            return res;

        }

        static List<string> GetOperators()
        {
            List<string> ops = new List<string>() { "=", "+", ".", ",", "-", "(", ")", "*", "<", ">" };

            return ops;

        }

        static List<string> GetSystemViews()
        {
            List<string> views = new List<string>() { "syscomments", "sysobjects", "sys.syscomments" };
            return views;
        }

        #endregion


        #region DBML

        public static DecorationScheme Dbml
        {
            get
            {
                DecorationScheme ds = new DecorationScheme();

                ds.Name = "Dbml#";

                MultiRegexWordDecoration BrownWords = new MultiRegexWordDecoration();
                BrownWords.Color = Color.Brown;
                BrownWords.Words = new List<string>() { "xml", "Database", "Table", "Type", "Column", "Association" };
                ds.Decorations.Add(BrownWords);


                MultiRegexWordDecoration RedWords = new MultiRegexWordDecoration();
                RedWords.Color = Color.Red;
                RedWords.Words = new List<string>() { "version", "encoding", "Name", "Class", "xmlns", "Member", "Type", "DbType", "CanBeNull" , "DeleteRule", "IsPrimaryKey"
              ,"IsForeignKey", "ThisKey", "OtherKey", "IsDbGenerated" ,"UpdateCheck" };
                ds.Decorations.Add(RedWords);

                DoubleQuotedDecoration quoted = new DoubleQuotedDecoration();
                quoted.Color = Color.Blue;
                ds.Decorations.Add(quoted);


                MultiStringDecoration blueStrings = new MultiStringDecoration();
                blueStrings.Color = Color.Blue;
                blueStrings.Strings = new List<string>() { "<", "?", "=", "/", ">" };
                ds.Decorations.Add(blueStrings);


                StringDecoration quotationMarks = new StringDecoration();
                quotationMarks.String = "\"";
                quotationMarks.Color = Color.Black;
                ds.Decorations.Add(quotationMarks);



                //RegexDecoration quotedText = new RegexDecoration();
                //quotedText.Color = Color.Brown);
                //quotedText.RegexString = "(?s:\".*?\")";
                //ds.Decorations.Add(quotedText);


                ////Color single line comments green
                //RegexDecoration singleLineComment = new RegexDecoration();
                //singleLineComment.DecorationType = EDecorationType.TextColor;
                //singleLineComment.Color = Color.Green);
                //singleLineComment.RegexString = "//.*";
                //ds.Decorations.Add(singleLineComment);

                ////Color multiline comments green
                //RegexDecoration multiLineComment = new RegexDecoration();
                //multiLineComment.DecorationType = EDecorationType.TextColor;
                //multiLineComment.Color = Color.Green);
                //multiLineComment.RegexString = @"(?s:/\*.*?\*/)";
                //ds.Decorations.Add(multiLineComment);

                return ds;

            }
        }


        #endregion

        #region XML
        public static DecorationScheme Xml
        {
            get
            {
                DecorationScheme ds = new DecorationScheme();
                ds.Name = "XML";

                MultiStringDecoration specialCharacters = new MultiStringDecoration();
                specialCharacters.Color = Color.Blue;
                specialCharacters.Strings = new List<string>() { "<", "/", ">", "<?", "?>", "=" };
                ds.Decorations.Add(specialCharacters);

                RegexMatchDecoration xmlTagName = new RegexMatchDecoration();
                xmlTagName.Color = Color.Brown;
                xmlTagName.RegexString = @"</?(?<selected>\w.*?)(\s|>|/)";
                ds.Decorations.Add(xmlTagName);

                RegexMatchDecoration xmlAttributeName = new RegexMatchDecoration()
                {
                    Color = Color.Red,
                    RegexString = @"\s(?<selected>\w+|\w+:\w+|(\w|\.)+)="".*?"""
                };
                ds.Decorations.Add(xmlAttributeName);

                RegexMatchDecoration xmlAttributeValue = new RegexMatchDecoration()
                {
                    Color = Color.Blue,
                    RegexString = @"\s(\w+|\w+:\w+|(\w|\.)+)\s*=\s*""(?<selected>.*?)"""
                };
                ds.Decorations.Add(xmlAttributeValue);

                RegexMatchDecoration xml = new RegexMatchDecoration()
                {
                    Color = Color.Brown,
                    RegexString = @"<\?(?<selected>xml)"
                };
                ds.Decorations.Add(xml);


                return ds;
            }

        }

        #endregion

        #region XAML

        public static DecorationScheme Xaml
        {
            get
            {
                DecorationScheme ds = new DecorationScheme();
                ds.Name = "XAML";
                MultiStringDecoration specialCharacters = new MultiStringDecoration();
                specialCharacters.Color = Color.Blue;
                specialCharacters.Strings = new List<string>() { "<", "/", ">", "<?", "?>" };
                ds.Decorations.Add(specialCharacters);

                RegexMatchDecoration xmlTagName = new RegexMatchDecoration();
                xmlTagName.Color = Color.Brown;
                xmlTagName.RegexString = @"</?(?<selected>\w.*?)(\s|>|/)";
                ds.Decorations.Add(xmlTagName);



                RegexMatchDecoration xmlAttributeName = new RegexMatchDecoration();
                xmlAttributeName.Color = Color.Red;
                xmlAttributeName.RegexString = @"\s(?<selected>\w+|\w+:\w+|(\w|\.)+)="".*?""";
                ds.Decorations.Add(xmlAttributeName);

                RegexMatchDecoration xmlAttributeValue = new RegexMatchDecoration();
                xmlAttributeValue.Color = Color.Blue;
                xmlAttributeValue.RegexString = @"\s(\w+|\w+:\w+|(\w|\.)+)\s*(?<selected>=\s*"".*?"")";
                ds.Decorations.Add(xmlAttributeValue);

                RegexMatchDecoration xamlMarkupExtension = new RegexMatchDecoration();
                xamlMarkupExtension.RegexString = @"\s(\w+|\w+:\w+|(\w|\.)+)\s*=\s*""{(?<selected>.*?)\s+.*?}""";
                xamlMarkupExtension.Color = Color.Brown;
                ds.Decorations.Add(xamlMarkupExtension);

                RegexMatchDecoration xamlMarkupExtensionValue = new RegexMatchDecoration();
                xamlMarkupExtensionValue.RegexString = @"\s(\w+|\w+:\w+|(\w|\.)+)\s*=\s*""{.*?\s+(?<selected>.*?)}""";
                xamlMarkupExtensionValue.Color = Color.Red;
                ds.Decorations.Add(xamlMarkupExtensionValue);

                DoubleRegexDecoration MarkupPeriods = new DoubleRegexDecoration();
                MarkupPeriods.Color = Color.Blue;
                MarkupPeriods.OuterRegexString = @"\s(\w+|\w+:\w+|(\w|\.)+)\s*=\s*""{.*?\s+.*?}""";
                MarkupPeriods.InnerRegexString = @"\.";
                ds.Decorations.Add(MarkupPeriods);

                RegexMatchDecoration xml = new RegexMatchDecoration();
                xml.Color = Color.Brown;
                xml.RegexString = @"<\?(?<selected>xml)";
                ds.Decorations.Add(xml);

                RegexMatchDecoration elementValues = new RegexMatchDecoration();
                elementValues.Color = Color.Brown;
                elementValues.RegexString = @">(?<selected>.*?)</";
                ds.Decorations.Add(elementValues);

                RegexDecoration comment = new RegexDecoration();
                comment.Color = Color.Green;
                comment.RegexString = @"<!--.*?-->";
                ds.Decorations.Add(comment);

                return ds;
            }
        }

        #endregion


    }
}
