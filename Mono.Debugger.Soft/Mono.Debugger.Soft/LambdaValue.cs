//
// LambdaValue.cs
//
// Author:
//       harukamatsumoto <harukam0416@gmail.com>
//
// Copyright (c) 2017
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

namespace Mono.Debugger.Soft
{
	public class LambdaValue : Value {
		DelayedTypeMirror delayedType;

		public LambdaValue(VirtualMachine vm, DelayedTypeMirror delayedType) : base (vm, 0)
		{
			this.delayedType = delayedType;
		}

		public string Expression {
			get { return DelayedType.Expression; }
		}

		public DelayedTypeMirror DelayedType {
			get { return delayedType; }
		}

		public static string ParseFullName (TypeMirror t)
		{
			return DelayedTypeMirror.ParseFullName (t);
		}

		public bool AbleToCastTo(TypeMirror toType)
		{
			return DelayedType.AbleToCastTo (toType);
		}

		public override string ToString ()
		{
			return string.Format ("LambdaValue for ({0})", Expression);
		}
	}
}
