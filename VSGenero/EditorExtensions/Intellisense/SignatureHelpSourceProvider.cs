﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSGenero.EditorExtensions.Intellisense
{
    [Export(typeof(ISignatureHelpSourceProvider)), ContentType(VSGeneroConstants.ContentType4GL), Order, Name("Genero Signature Help Source")]
    class SignatureHelpSourceProvider : ISignatureHelpSourceProvider
    {
        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer)
        {
            return new SignatureHelpSource(this, textBuffer);
        }
    }
}