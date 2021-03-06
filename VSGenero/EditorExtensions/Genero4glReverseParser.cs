﻿/* ****************************************************************************
 * Copyright (c) 2015 Greg Fullman 
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSGenero.Analysis;
using VSGenero.Analysis.Parsing;

namespace VSGenero.EditorExtensions
{
    public class Genero4glReverseParser : IEnumerable<ClassificationSpan>, IReverseTokenizer
    {
        private readonly ITextSnapshot _snapshot;
        private readonly ITextBuffer _buffer;
        private readonly ITrackingSpan _span;
        private IList<ClassificationSpan> _tokens;
        private IList<TokenInfo> _tokenInfos;
        private ITextSnapshotLine _curLine;
        private Genero4glClassifier _classifier;
        private static readonly string[] _assignOperators = new[] {
            "=" ,  ":=" //,  "-=" ,  "/=" ,  "%=" ,  "^=" ,  "*=" ,  "//=" ,  "&=" ,  "|=" ,  ">>=" ,  "<<=" ,  "**="
        };


        public Genero4glReverseParser(ITextSnapshot snapshot, ITextBuffer buffer, ITrackingSpan span)
        {
            _snapshot = snapshot;
            _buffer = buffer;
            _span = span;

            var loc = span.GetSpan(snapshot);
            var line = _curLine = snapshot.GetLineFromPosition(loc.Start);

            var targetSpan = new Span(line.Start.Position, span.GetEndPoint(snapshot).Position - line.Start.Position);
            var snapSpan = new SnapshotSpan(snapshot, targetSpan);
            if (Classifier != null)
            {
                _tokens = Classifier.GetClassificationSpans(snapSpan);
                _tokenInfos = Classifier.GetTokens(snapSpan);
            }
        }

        public SnapshotSpan? GetExpressionRange(out bool isFunctionCallOrDefinition, bool forCompletion = true, int nesting = 0)
        {
            int dummy;
            SnapshotPoint? dummyPoint;
            string lastKeywordArg;
            bool isParameterName;
            try
            {
                return GetExpressionRange(nesting, out dummy, out dummyPoint, out lastKeywordArg, out isParameterName, out isFunctionCallOrDefinition, forCompletion);
            }
            catch(NullReferenceException)
            {
                isFunctionCallOrDefinition = false;
                return null;
            }
        }

        internal static IEnumerable<TokenInfo> ReverseTokenInfoEnumerable(Genero4glClassifier classifier, SnapshotPoint startPoint)
        {
            var startLine = startPoint.GetContainingLine();
            int curLine = startLine.LineNumber;
            var tokens = classifier.GetTokens(new SnapshotSpan(startLine.Start, startPoint), false);

            for (; ; )
            {
                for (int i = tokens.Count - 1; i >= 0; i--)
                {
                    yield return tokens[i];
                }

                // indicate the line break
                yield return default(TokenInfo);

                curLine--;
                if (curLine >= 0)
                {
                    var prevLine = startPoint.Snapshot.GetLineFromLineNumber(curLine);
                    tokens = classifier.GetTokens(prevLine.Extent, false);
                }
                else
                {
                    break;
                }
            }
        }

        internal static IEnumerator<ClassificationSpan> ReverseClassificationSpanEnumerator(Genero4glClassifier classifier, SnapshotPoint startPoint)
        {
            if (classifier != null && startPoint != null)
            {
                var startLine = startPoint.GetContainingLine();
                if (startLine != null)
                {
                    int curLine = startLine.LineNumber;
                    var tokens = classifier.GetClassificationSpans(new SnapshotSpan(startLine.Start, startPoint));

                    for (;;)
                    {
                        for (int i = tokens.Count - 1; i >= 0; i--)
                        {
                            yield return tokens[i];
                        }

                        // indicate the line break
                        yield return null;

                        curLine--;
                        if (curLine >= 0 && startPoint.Snapshot != null)
                        {
                            var prevLine = startPoint.Snapshot.GetLineFromLineNumber(curLine);
                            tokens = classifier.GetClassificationSpans(prevLine.Extent);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Walks backwards to figure out if we're a parameter name which comes after a (     
        /// </summary>
        private bool IsParameterNameOpenParen(IEnumerator<ClassificationSpan> enumerator)
        {
            if (MoveNextSkipExplicitNewLines(enumerator))
            {
                if (enumerator.Current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier))
                {
                    if (MoveNextSkipExplicitNewLines(enumerator) &&
                        enumerator.Current.ClassificationType == Genero4glClassifierProvider.Keyword &&
                        enumerator.Current.Span.GetText() == "def")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Walks backwards to figure out if we're a parameter name which comes after a comma.
        /// </summary>
        private bool IsParameterNameComma(IEnumerator<ClassificationSpan> enumerator)
        {
            int groupingLevel = 1;

            while (MoveNextSkipExplicitNewLines(enumerator))
            {
                if (enumerator.Current.ClassificationType == Genero4glClassifierProvider.Keyword)
                {
                    if (enumerator.Current.Span.GetText() == "def" && groupingLevel == 0)
                    {
                        return true;
                    }

                    //if (PythonKeywords.IsOnlyStatementKeyword(enumerator.Current.Span.GetText())) {
                    //    return false;
                    //}
                }
                if (enumerator.Current.IsOpenGrouping())
                {
                    groupingLevel--;
                    if (groupingLevel == 0)
                    {
                        return IsParameterNameOpenParen(enumerator);
                    }
                }
                else if (enumerator.Current.IsCloseGrouping())
                {
                    groupingLevel++;
                }
            }

            return false;
        }

        private bool MoveNextSkipExplicitNewLines(IEnumerator<ClassificationSpan> enumerator)
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current == null)
                {
                    while (enumerator.Current == null)
                    {
                        if (!enumerator.MoveNext())
                        {
                            return false;
                        }
                    }
                    if (!IsExplicitLineJoin(enumerator.Current))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the range of the expression to the left of our starting span.  
        /// </summary>
        /// <param name="nesting">1 if we have an opening parenthesis for sig completion</param>
        /// <param name="paramIndex">The current parameter index.</param>
        /// <returns></returns>
        public SnapshotSpan? GetExpressionRange(int nesting, out int paramIndex, out SnapshotPoint? sigStart, out string lastKeywordArg, out bool isParameterName, out bool isFunctionCallOrDefinition, bool forCompletion = true)
        {
            isFunctionCallOrDefinition = false;
            SnapshotSpan? start = null;
            paramIndex = 0;
            sigStart = null;
            bool nestingChanged = false, lastTokenWasCommaOrOperator = true, lastTokenWasKeywordArgAssignment = false, lastTokenWasDot = false, firstTokenWasIdentOrKeyword = false;
            int otherNesting = 0;
            bool isSigHelp = nesting != 0;
            isParameterName = false;
            lastKeywordArg = null;

            ClassificationSpan lastToken = null;
            bool firstToken = true;
            // Walks backwards over all the lines

            var enumerator = ReverseClassificationSpanEnumerator(_classifier, _span.GetSpan(_snapshot).End);
            if (enumerator.MoveNext())
            {
                if (enumerator.Current != null && enumerator.Current.ClassificationType == Genero4glClassifierProvider.StringLiteral)
                {
                    return enumerator.Current.Span;
                }

                lastToken = enumerator.Current;
                while (ShouldSkipAsLastToken(lastToken, forCompletion) && enumerator.MoveNext())
                {
                    // skip trailing new line if the user is hovering at the end of the line
                    if ((lastToken == null && enumerator.Current != null && enumerator.Current.ClassificationType != Genero4glClassifierProvider.DotClassification) &&
                        (nesting + otherNesting == 0))
                    {
                        // new line out of a grouping...
                        return _span.GetSpan(_snapshot);
                    }
                    lastToken = enumerator.Current;
                }

                int currentParamAtLastColon = -1;   // used to track the current param index at this last colon, before we hit a lambda.
                SnapshotSpan? startAtLastToken = null;
                // Walk backwards over the tokens in the current line
                do
                {
                    var token = enumerator.Current;

                    if (token == null)
                    {
                        // new line
                        if (nesting != 0 ||
                            otherNesting != 0 ||
                            lastTokenWasDot ||
                            (enumerator.MoveNext() &&
                             (IsExplicitLineJoin(enumerator.Current) || (enumerator.Current != null && enumerator.Current.ClassificationType == Genero4glClassifierProvider.DotClassification))
                            )
                           )
                        {
                            if (enumerator.Current != null)
                            {
                                // we're in a grouping, or the previous token is an explicit line join, we'll keep going.
                                if (enumerator.Current.ClassificationType != Genero4glClassifierProvider.DotClassification)
                                {
                                    continue;
                                }
                                else
                                {
                                    token = lastToken = enumerator.Current;
                                }
                            }
                            else
                            {
                                //if (lastTokenWasDot)
                                //{
                                    while (enumerator.MoveNext())
                                    {
                                        token = enumerator.Current;
                                        if (token != null)
                                            break;
                                    }
                                //}
                                //else
                                //    break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    var text = token.Span.GetText();
                    if (text == "(")
                    {
                        if (nesting != 0)
                        {
                            nesting--;
                            nestingChanged = true;
                            if (nesting == 0)
                            {
                                if (sigStart == null)
                                {
                                    sigStart = token.Span.Start;
                                }
                            }
                        }
                        else
                        {
                            if (start == null && !forCompletion)
                            {
                                // hovering directly over an open paren, don't provide a tooltip
                                return null;
                            }

                            // figure out if we're a parameter definition
                            isParameterName = IsParameterNameOpenParen(enumerator);
                            break;
                        }
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        lastTokenWasDot = false;
                    }
                    else if (token.IsOpenGrouping())
                    {
                        if (otherNesting != 0)
                        {
                            otherNesting--;
                        }
                        else
                        {
                            if (nesting == 0)
                            {
                                if (start == null)
                                {
                                    return null;
                                }
                                break;
                            }
                            paramIndex = 0;
                        }
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        lastTokenWasDot = false;
                    }
                    else if (text == ")")
                    {
                        nesting++;
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        lastTokenWasDot = false;
                    }
                    else if (token.IsCloseGrouping())
                    {
                        otherNesting++;
                        nestingChanged = true;
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        lastTokenWasDot = false;
                    }
                    else if (/*token.ClassificationType == Classifier.Provider.Keyword ||*/   // We're going to exclude keywords from this logic...messing too many things up
                             token.ClassificationType == Genero4glClassifierProvider.Operator)
                    {
                        lastTokenWasKeywordArgAssignment = false;

                        if (token.ClassificationType == Genero4glClassifierProvider.Keyword && text == "lambda")
                        {
                            if (currentParamAtLastColon != -1)
                            {
                                paramIndex = currentParamAtLastColon;
                                currentParamAtLastColon = -1;
                            }
                            else
                            {
                                // fabcd(lambda a, b, c[PARAMINFO]
                                // We have to be the 1st param.
                                paramIndex = 0;
                            }
                        }

                        if (text == ":")
                        {
                            startAtLastToken = start;
                            currentParamAtLastColon = paramIndex;
                        }

                        if (nesting == 0 && otherNesting == 0)
                        {
                            if (start == null)
                            {
                                // http://pytools.codeplex.com/workitem/560
                                // yield_value = 42
                                // def f():
                                //     yield<ctrl-space>
                                //     yield <ctrl-space>
                                // 
                                // If we're next to the keyword, just return the keyword.
                                // If we're after the keyword, return the span of the text proceeding
                                //  the keyword so we can complete after it.
                                // 
                                // Also repros with "return <ctrl-space>" or "print <ctrl-space>" both
                                // of which we weren't reporting completions for before
                                if (forCompletion)
                                {
                                    if (token.Span.IntersectsWith(_span.GetSpan(_snapshot)))
                                    {
                                        return token.Span;
                                    }
                                    else
                                    {
                                        return _span.GetSpan(_snapshot);
                                    }
                                }

                                // hovering directly over a keyword, don't provide a tooltip
                                return null;
                            }
                            else if ((nestingChanged || forCompletion) && token.ClassificationType == Genero4glClassifierProvider.Keyword && text == "def")
                            {
                                return null;
                            }
                            if (text == "*" || text == "**")
                            {
                                if (enumerator.MoveNext())
                                {
                                    if (enumerator.Current.ClassificationType == Genero4glClassifierProvider.CommaClassification)
                                    {
                                        isParameterName = IsParameterNameComma(enumerator);
                                    }
                                    else if (enumerator.Current.IsOpenGrouping() && enumerator.Current.Span.GetText() == "(")
                                    {
                                        isParameterName = IsParameterNameOpenParen(enumerator);
                                    }
                                }
                            }
                            break;
                        }
                        else if ((token.ClassificationType == Genero4glClassifierProvider.Keyword) &&
                            //PythonKeywords.IsOnlyStatementKeyword(text)) ||
                          (token.ClassificationType == Genero4glClassifierProvider.Operator && IsAssignmentOperator(text)))
                        {
                            if (isSigHelp && text == "=")
                            {
                                // keyword argument allowed in signatures
                                lastTokenWasKeywordArgAssignment = lastTokenWasCommaOrOperator = true;
                                lastTokenWasDot = false;
                            }
                            else if (start == null || (nestingChanged && nesting != 0))
                            {
                                return null;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else if (token.ClassificationType == Genero4glClassifierProvider.Keyword && (text == "if" || text == "else"))
                        {
                            // if and else can be used in an expression context or a statement context
                            if (currentParamAtLastColon != -1)
                            {
                                start = startAtLastToken;
                                if (start == null)
                                {
                                    return null;
                                }
                                break;
                            }
                        }
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasDot = false;
                    }
                    else if (token.ClassificationType == Genero4glClassifierProvider.DotClassification)
                    {
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasDot = true;
                        lastTokenWasKeywordArgAssignment = false;
                    }
                    else if (token.ClassificationType == Genero4glClassifierProvider.CommaClassification)
                    {
                        lastTokenWasCommaOrOperator = true;
                        lastTokenWasKeywordArgAssignment = false;
                        lastTokenWasDot = false;
                        if (nesting == 0 && otherNesting == 0)
                        {
                            if (start == null && !forCompletion)
                            {
                                return null;
                            }
                            isParameterName = IsParameterNameComma(enumerator);
                            break;
                        }
                        else if (nesting == 1 && otherNesting == 0 && sigStart == null)
                        {
                            paramIndex++;
                        }
                    }
                    else if (token.ClassificationType == Genero4glClassifierProvider.Comment)
                    {
                        return null;
                    }
                    else if(firstTokenWasIdentOrKeyword && text.ToLower() == "function")
                    {
                        isFunctionCallOrDefinition = true;
                        break;

                    }
                    else if (!lastTokenWasCommaOrOperator)
                    {
                        break;
                    }
                    else
                    {
                        if (lastTokenWasKeywordArgAssignment &&
                            token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) &&
                            lastKeywordArg == null)
                        {
                            if (paramIndex == 0)
                            {
                                lastKeywordArg = text;
                            }
                            else
                            {
                                lastKeywordArg = "";
                            }
                        }
                        if (firstToken)
                        {
                            // check to see if the next token after this one is an open paren
                            int endCheck = 50;
                            if (enumerator.Current.Span.End.Position + endCheck > _snapshot.Length)
                            {
                                endCheck = _snapshot.Length - enumerator.Current.Span.End.Position;
                            }
                            var nextTokens = _classifier.GetTokens(new SnapshotSpan(_snapshot, new Span(enumerator.Current.Span.End.Position - 1, endCheck)), false).ToList();
                            int parenIndex = -1;
                            if (nextTokens.Count > 0)
                            {
                                parenIndex = 0;
                                TokenInfo tokInfo;
                                while (parenIndex < nextTokens.Count)
                                {
                                    tokInfo = nextTokens[parenIndex];
                                    if (Math.Abs(tokInfo.SourceSpan.Start.Index - enumerator.Current.Span.Start.Position) <= 1 &&
                                       Math.Abs(tokInfo.SourceSpan.End.Index - enumerator.Current.Span.End.Position) <= 1)
                                    {
                                        parenIndex++;
                                        break;
                                    }
                                    parenIndex++;
                                }
                            }
                            if (parenIndex >= 0 && parenIndex < nextTokens.Count && nextTokens[parenIndex].Token.Kind == TokenKind.LeftParenthesis)
                            {
                                // we might have a function call or function name
                                isFunctionCallOrDefinition = true;
                            }

                            if(token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) ||
                               token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword))
                            {
                                firstTokenWasIdentOrKeyword = true;
                            }
                        }
                        lastTokenWasCommaOrOperator = false;
                        lastTokenWasDot = false;
                    }

                    start = token.Span;
                    if (firstToken)
                        firstToken = false;
                } while (enumerator.MoveNext());
            }

            if (start.HasValue && lastToken != null && (lastToken.Span.End.Position - start.Value.Start.Position) >= 0)
            {
                return new SnapshotSpan(
                    Snapshot,
                    new Span(
                        start.Value.Start.Position,
                        lastToken.Span.End.Position - start.Value.Start.Position
                    )
                );
            }

            return _span.GetSpan(_snapshot);
        }

        private static bool IsAssignmentOperator(string text)
        {
            return ((IList<string>)_assignOperators).Contains(text);
        }

        internal static bool IsExplicitLineJoin(ClassificationSpan cur)
        {
            if (cur != null && cur.ClassificationType.IsOfType(Genero4glPredefinedClassificationTypeNames.Operator))
            {
                var text = cur.Span.GetText();
                return text == "\\\r\n" || text == "\\\r" || text == "\n";
            }
            return false;
        }

        /// <summary>
        /// Returns true if we should skip this token when it's the last token that the user hovers over.  Currently true
        /// for new lines and dot classifications.  
        /// </summary>
        private bool ShouldSkipAsLastToken(ClassificationSpan lastToken, bool forCompletion)
        {
            return lastToken == null || (
                (lastToken.ClassificationType.Classification == PredefinedClassificationTypeNames.WhiteSpace &&
                    (lastToken.Span.GetText() == "\r\n" || lastToken.Span.GetText() == "\n" || lastToken.Span.GetText() == "\r")) ||
                    (lastToken.ClassificationType == Genero4glClassifierProvider.DotClassification && !forCompletion));
        }

        public Genero4glClassifier Classifier
        {
            get
            {
                if(_classifier == null)
                {
                    _buffer.Properties.TryGetProperty(typeof(Genero4glClassifier), out _classifier);
                }
                return _classifier;
            }
        }

        public ITextSnapshot Snapshot
        {
            get { return _snapshot; }
        }

        public ITextBuffer Buffer
        {
            get { return _buffer; }
        }

        public ITrackingSpan Span
        {
            get { return _span; }
        }

        /// <summary>
        /// Tokens for the current line
        /// </summary>
        public IList<ClassificationSpan> Tokens
        {
            get { return _tokens; }
            set { _tokens = value; }
        }

        public ITextSnapshotLine CurrentLine
        {
            get { return _curLine; }
            set { _curLine = value; }
        }

        public IEnumerator<ClassificationSpan> GetEnumerator()
        {
            return ReverseClassificationSpanEnumerator(_classifier, _span.GetSpan(_snapshot).End);
        }

        public IEnumerable<TokenInfo> GetReversedTokens()
        {
            return ReverseTokenInfoEnumerable(_classifier, _span.GetSpan(_snapshot).End);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ReverseClassificationSpanEnumerator(_classifier, _span.GetSpan(_snapshot).End);
        }

        internal bool IsInGrouping()
        {
            // We assume that groupings are correctly matched and keep a simple
            // nesting count.
            int nesting = 0;
            foreach (var token in this)
            {
                if (token == null)
                {
                    continue;
                }

                if (token.IsCloseGrouping())
                {
                    nesting++;
                }
                else if (token.IsOpenGrouping())
                {
                    if (nesting-- == 0)
                    {
                        return true;
                    }
                }
                else if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword))
                {// &&
                    //PythonKeywords.IsOnlyStatementKeyword(token.Span.GetText())) {
                    return false;
                }
            }
            return false;
        }


        public string GetExpressionText(out int startIndex, out int endIndex, out bool isFunctionCallOrDefinition)
        {
            startIndex = -1; endIndex = -1;
            var span = GetExpressionRange(out isFunctionCallOrDefinition);
            if (span.HasValue)
            {
                startIndex = span.Value.Start.Position;
                endIndex = span.Value.End.Position;
                var str = span.Value.GetText();
                var strLines = str.Split(new[] { '\n' });
                StringBuilder sb = new StringBuilder();
                foreach (var line in strLines)
                    sb.Append(line.Trim());
                return sb.ToString();
            }
            return null;
        }
    }
}
