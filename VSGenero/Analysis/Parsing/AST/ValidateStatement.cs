﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    /// <summary>
    /// VALIDATE target [,...] LIKE
    /// {
    ///    table.*
    /// |
    ///    table.column
    /// }
    /// 
    /// For more info, see: http://www.4js.com/online_documentation/fjs-fgl-manual-html/index.html#c_fgl_variables_VALIDATE.html
    /// </summary>
    public class ValidateStatement : FglStatement
    {
        public List<NameExpression> TargetVariables { get; private set; }
        public string TableName { get; private set; }
        public string ColumnName { get; private set; }

        public static bool TryParseNode(Parser parser, out ValidateStatement defNode)
        {
            defNode = null;
            bool result = false;

            if (parser.PeekToken(TokenKind.ValidateKeyword))
            {
                result = true;
                defNode = new ValidateStatement();
                parser.NextToken();
                defNode.StartIndex = parser.Token.Span.Start;

                NameExpression name;
                while (NameExpression.TryParseNode(parser, out name))
                {
                    defNode.TargetVariables.Add(name);
                    if (!parser.PeekToken(TokenKind.Comma))
                        break;
                    parser.NextToken();
                }

                if (parser.PeekToken(TokenKind.LikeKeyword))
                {
                    parser.NextToken();
                    defNode.TableName = parser.Token.Token.Value.ToString();
                    parser.NextToken(); // advance to the dot
                    if (parser.Token.Token.Kind == TokenKind.Dot)
                    {
                        if (parser.PeekToken(TokenKind.Multiply) ||
                            parser.PeekToken(TokenCategory.Identifier) ||
                            parser.PeekToken(TokenCategory.Keyword))
                        {
                            parser.NextToken(); // advance to the column name
                            defNode.ColumnName = parser.Token.Token.Value.ToString();
                            defNode.IsComplete = true;
                            defNode.EndIndex = parser.Token.Span.End;
                        }
                        else
                        {
                            parser.ReportSyntaxError("Invalid validation form detected.");
                        }
                    }
                    else
                    {
                        parser.ReportSyntaxError("Invalid validation form detected.");
                    }
                }
                else
                {
                    parser.ReportSyntaxError("Variables can only be validated against a database table spec.");
                }
                defNode.EndIndex = parser.Token.Span.End;
            }

            return result;
        }
    }
}
