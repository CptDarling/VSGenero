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

using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.ProductivityTools
{
    internal class QuoteFilter : BaseFilter
    {
        // Fields
        private bool escape;
        private char quote;

        // Methods
        public QuoteFilter(ITextSnapshot snapshot)
            : base(snapshot)
        {
            this.quote = ' ';
        }

        public bool Next()
        {
            if (++base.position >= base.snapshot.Length)
            {
                return false;
            }
            bool escape = this.escape;
            this.escape = false;
            char character = base.Character;
            if (this.quote == ' ')
            {
                switch (character)
                {
                    case '#':
                        {
                            // skip over a commented line
                            ITextSnapshotLine lineFromPosition = base.snapshot.GetLineFromPosition(base.position);
                            base.position = (int)lineFromPosition.End;
                            break;
                        }
                    case '\'':
                    case '"':
                        this.quote = character;
                        break;

                    case '-':
                        char ch3 = base.PeekNextChar();
                        if (ch3 == '-')
                        {
                            ITextSnapshotLine line2 = base.snapshot.GetLineFromPosition(base.position);
                            base.position = (int)line2.End;
                            break;
                        }
                        break;

                    case '{':
                        base.position++;
                        while (base.position < base.snapshot.Length)
                        {
                            if (base.snapshot[base.position] == '}')
                            {
                                break;
                            }
                            base.position++;
                        }
                        break;
                }
            }
            else if (character == '\\' && !escape)
            {
                this.escape = true;
            }
            else if (((character == this.quote) || ((character == '"'))) && !escape)
            {
                this.quote = ' ';
            }
            else if (((this.quote == '"') || (this.quote == '\'')) && (base.snapshot.GetLineFromPosition(base.position).End == base.position))
            {
                this.quote = ' ';
            }
            return (base.position < base.snapshot.Length);
        }

        // Properties
        public bool InQuote
        {
            get
            {
                return (this.quote != ' ');
            }
        }
    }
}
