﻿/* ****************************************************************************
*
* Copyright (c) Microsoft Corporation. 
*
* This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
* copy of the license can be found in the License.html file at the root of this distribution. If 
* you cannot locate the Apache License, Version 2.0, please send an email to 
* vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
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

namespace VSGenero.Analysis
{
    public class ParameterResult : IEquatable<ParameterResult>
    {
        public string Name { get; private set; }
        public string Documentation { get; private set; }
        public string Type { get; private set; }

        public ParameterResult(string name)
            : this(name, String.Empty, "object")
        {
        }
        public ParameterResult(string name, string doc)
            : this(name, doc, "object")
        {
        }
        public ParameterResult(string name, string doc, string type)
        {
            Name = name;
            Documentation = doc;
            Type = type;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ParameterResult);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^
                (Type ?? "").GetHashCode();
        }

        public bool Equals(ParameterResult other)
        {
            return other != null &&
                Name == other.Name &&
                Documentation == other.Documentation &&
                Type == other.Type;
        }
    }
}
