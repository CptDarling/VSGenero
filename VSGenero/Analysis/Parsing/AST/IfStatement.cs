﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.Analysis.Parsing.AST
{
    public class IfStatement : FglStatement, IOutlinableResult
    {
        public ExpressionNode ConditionExpression { get; private set; }

        public static bool TryParseNode(Parser parser, out IfStatement node, 
                                 IModuleResult containingModule,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            node = null;
            bool result = false;

            if(parser.PeekToken(TokenKind.IfKeyword))
            {
                result = true;
                node = new IfStatement();
                parser.NextToken();
                node.StartIndex = parser.Token.Span.Start;

                ExpressionNode conditionExpr;
                if (!ExpressionNode.TryGetExpressionNode(parser, out conditionExpr, new List<TokenKind> { TokenKind.ThenKeyword }))
                {
                    parser.ReportSyntaxError("An if statement must have a condition expression.");
                }
                else
                {
                    node.ConditionExpression = conditionExpr;
                }

                if (!parser.PeekToken(TokenKind.ThenKeyword))
                    parser.ReportSyntaxError("An if statement must have a \"then\" keyword prior to containing code.");
                else
                    parser.NextToken();

                node.DecoratorEnd = parser.Token.Span.End;

                IfBlockContentsNode ifBlock;
                if(IfBlockContentsNode.TryParseNode(parser, out ifBlock, containingModule, prepStatementBinder, validExitKeywords))
                {
                    node.Children.Add(ifBlock.StartIndex, ifBlock);
                }

                if(parser.PeekToken(TokenKind.ElseKeyword))
                {
                    parser.NextToken();
                    ElseBlockContentsNode elseBlock;
                    if (ElseBlockContentsNode.TryParseNode(parser, out elseBlock, containingModule, prepStatementBinder, validExitKeywords))
                    {
                        node.Children.Add(elseBlock.StartIndex, elseBlock);
                    }
                }

                if (!(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.IfKeyword, 2)))
                {
                    parser.ReportSyntaxError("An if statement must be terminated with \"end if\".");
                }
                else
                {
                    parser.NextToken(); // advance to the 'end' token
                    parser.NextToken(); // advance to the 'if' token
                    node.EndIndex = parser.Token.Span.End;
                }

            }

            return result;
        }

        public bool CanOutline
        {
            get { return true; }
        }

        public int DecoratorStart
        {
            get
            {
                return StartIndex;
            }
            set
            {
            }
        }

        public int DecoratorEnd { get; set; }
    }

    public class IfBlockContentsNode : AstNode
    {
        public static bool TryParseNode(Parser parser, out IfBlockContentsNode node,
                                 IModuleResult containingModule,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            node = new IfBlockContentsNode();
            node.StartIndex = parser.Token.Span.Start;
            while(!parser.PeekToken(TokenKind.EndOfFile) &&
                  !parser.PeekToken(TokenKind.ElseKeyword) &&
                  !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.IfKeyword, 2)))
            {
                FglStatement statement;
                if (parser.StatementFactory.TryParseNode(parser, out statement, containingModule, prepStatementBinder, false, validExitKeywords))
                {
                    AstNode stmtNode = statement as AstNode;
                    node.Children.Add(stmtNode.StartIndex, stmtNode);
                }
                else
                {
                    parser.NextToken();
                }
            }
            node.EndIndex = parser.Token.Span.End;

            return true;
        }
    }

    public class ElseBlockContentsNode : AstNode
    {
        public static bool TryParseNode(Parser parser, out ElseBlockContentsNode node,
                                 IModuleResult containingModule,
                                 Action<PrepareStatement> prepStatementBinder = null,
                                 List<TokenKind> validExitKeywords = null)
        {
            node = new ElseBlockContentsNode();
            node.StartIndex = parser.Token.Span.Start;
            while (!parser.PeekToken(TokenKind.EndOfFile) && 
                   !(parser.PeekToken(TokenKind.EndKeyword) && parser.PeekToken(TokenKind.IfKeyword, 2)))
            {
                FglStatement statement;
                if (parser.StatementFactory.TryParseNode(parser, out statement, containingModule, prepStatementBinder, false, validExitKeywords))
                {
                    AstNode stmtNode = statement as AstNode;
                    node.Children.Add(stmtNode.StartIndex, stmtNode);
                }
                else
                {
                    parser.NextToken();
                }
            }
            node.EndIndex = parser.Token.Span.End;
            
            return true;
        }
    }
}