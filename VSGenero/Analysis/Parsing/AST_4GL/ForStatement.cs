﻿/* ****************************************************************************
 * Copyright (c) 2015 Greg Fullman 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/ 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST_4GL
{
    public class ForStatement : FglStatement
    {
        public ExpressionNode CounterVariable { get; private set; }
        public ExpressionNode StartValueExpresison { get; private set; }
        public ExpressionNode EndValueExpression { get; private set; }
        public ExpressionNode StepValue { get; private set; }

        public static bool TryParserNode(Genero4glParser parser, out ForStatement node, 
                                        IModuleResult containingModule,
                                        List<Func<PrepareStatement, bool>> prepStatementBinders,
                                        Func<ReturnStatement, ParserResult> returnStatementBinder = null,
                                        Action<IAnalysisResult, int, int> limitedScopeVariableAdder = null,
                                        List<TokenKind> validExitKeywords = null,
                                        IEnumerable<ContextStatementFactory> contextStatementFactories = null,
                                        ExpressionParsingOptions expressionOptions = null,
                                        HashSet<TokenKind> endKeywords = null)
        {
            node = null;
            bool result = false;

            if(parser.PeekToken(TokenKind.ForKeyword))
            {
                result = true;
                node = new ForStatement();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;

                ExpressionNode counterVar;
                if(FglExpressionNode.TryGetExpressionNode(parser, out counterVar, new List<TokenKind> { TokenKind.Equals }))
                    node.CounterVariable = counterVar;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if (parser.PeekToken(TokenKind.Equals))
                    parser.NextToken();
                else
                    parser.ReportSyntaxError("For statement missing counter variable assignment.");

                ExpressionNode startValue;
                if(FglExpressionNode.TryGetExpressionNode(parser, out startValue, new List<TokenKind> { TokenKind.ToKeyword }))
                    node.StartValueExpresison = startValue;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if (parser.PeekToken(TokenKind.ToKeyword))
                    parser.NextToken();
                else
                    parser.ReportSyntaxError("For statement missing \"to\" keyword.");

                List<TokenKind> keywords = new List<TokenKind>(Genero4glAst.ValidStatementKeywords);
                keywords.Add(TokenKind.StepKeyword);
                ExpressionNode endValue;
                if (FglExpressionNode.TryGetExpressionNode(parser, out endValue, keywords))
                    node.EndValueExpression = endValue;
                else
                    parser.ReportSyntaxError("Invalid expression found in for statement");

                if(parser.PeekToken(TokenKind.StepKeyword))
                {
                    parser.NextToken();
                    ExpressionNode stepExpr;
                    if (FglExpressionNode.TryGetExpressionNode(parser, out stepExpr))
                        node.StepValue = stepExpr;
                    else
                        parser.ReportSyntaxError("Invalid step expression found.");
                    //bool negative = false;
                    //if (parser.PeekToken(TokenKind.Subtract))
                    //{
                    //    negative = true;
                    //    parser.NextToken();
                    //}
                    //if(parser.PeekToken(TokenCategory.NumericLiteral))
                    //{
                    //    parser.NextToken();
                    //    int temp;
                    //    if(int.TryParse(parser.Token.Token.Value.ToString(), out temp))
                    //    {
                    //        node.StepValue = temp;
                    //        if (negative)
                    //            node.StepValue = -(node.StepValue);
                    //    }
                    //    else
                    //    {
                    //        parser.ReportSyntaxError("Invalid step value found.");
                    //    }
                    //}
                    //else
                    //{
                    //    parser.ReportSyntaxError("Invalid step value found.");
                    //}
                }

                node.DecoratorEnd = parser.Token.Span.End;

                List<TokenKind> validExits = new List<TokenKind>();
                if (validExitKeywords != null)
                    validExits.AddRange(validExitKeywords);
                validExits.Add(TokenKind.ForKeyword);

                HashSet<TokenKind> newEndKeywords = new HashSet<TokenKind>();
                if (endKeywords != null)
                    newEndKeywords.AddRange(endKeywords);
                newEndKeywords.Add(TokenKind.ForKeyword);

                prepStatementBinders.Insert(0, node.BindPrepareCursorFromIdentifier);
                while (!parser.PeekToken(TokenKind.EndOfFile) &&
                      !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.ForKeyword, 2)))
                {
                    FglStatement statement;
                    if (parser.StatementFactory.TryParseNode(parser, out statement, containingModule, prepStatementBinders, 
                                                             returnStatementBinder, limitedScopeVariableAdder, false, validExits, 
                                                             contextStatementFactories, expressionOptions, newEndKeywords) && statement != null)
                    {
                        AstNode4gl stmtNode = statement as AstNode4gl;
                        node.Children.Add(stmtNode.StartIndex, stmtNode);

                        if (statement is ExitStatement &&
                           (statement as ExitStatement).ExitType != TokenKind.ForKeyword)
                        {
                            if (validExitKeywords == null || !validExitKeywords.Contains((statement as ExitStatement).ExitType))
                                parser.ReportSyntaxError("Invalid exit statement for for loop detected.");
                        }

                        if (statement is ContinueStatement &&
                           (statement as ContinueStatement).ContinueType != TokenKind.ForKeyword)
                        {
                            if (validExitKeywords == null || !validExitKeywords.Contains((statement as ContinueStatement).ContinueType))
                                parser.ReportSyntaxError("Invalid continue statement for for loop detected.");
                        }
                    }
                    else if (parser.PeekToken(TokenKind.EndKeyword) && endKeywords != null && endKeywords.Contains(parser.PeekToken(2).Kind))
                    {
                        break;
                    }
                    else
                    {
                        parser.NextToken();
                    }
                }
                prepStatementBinders.RemoveAt(0);

                if (!(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.ForKeyword, 2)))
                {
                    parser.ReportSyntaxError("A for statement must be terminated with \"end for\".");
                }
                else
                {
                    parser.NextToken(); // advance to the 'end' token
                    parser.NextToken(); // advance to the 'for' token
                    node.EndIndex = parser.Token.Span.End;
                }
            }

            return result;
        }

        public override bool CanOutline
        {
            get { return true; }
        }

        public override int DecoratorEnd { get; set; }
    }
}
