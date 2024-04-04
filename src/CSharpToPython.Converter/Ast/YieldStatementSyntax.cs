// /* ****************************************************************************
//  *
//  * Copyright (c) Microsoft Corporation. 
//  *
//  * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
//  * copy of the license can be found in the License.html file at the root of this distribution. If 
//  * you cannot locate the  Apache License, Version 2.0, please send an email to 
//  * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
//  * by the terms of the Apache License, Version 2.0.
//  *
//  * You must not remove this notice, or any other, from this software.
//  *
//  *
//  * ***************************************************************************/

// #if FEATURE_CORE_DLR
// using MSAst = System.Linq.Expressions;
// #else

// #endif

// using System;
// using System.Diagnostics;
// using Microsoft.CodeAnalysis.CSharp.Syntax;



// namespace IronPython.Compiler.Ast {
    

//     // New in Pep342 for Python 2.5. Yield is an expression with a return value.
//     //    x = yield z
//     // The return value (x) is provided by calling Generator.Send()
//     public class YieldStatementSyntax : StatementSyntax { 
//         private readonly Expression _expression;

//         public YieldStatementSyntax(Expression expression) {
//             _expression = expression;
//         }

//         public void GetCachedSlot (int index)
//         {
//             throw new NotImplementedException();
//         }
        
//         public void Accept (Microsoft.CodeAnalysis.CSharp.CSharpSyntaxVisitor visitor)
//         {
//         }

//         public Expression Expression {
//             get { return _expression; }
//         }
//     }
// }
