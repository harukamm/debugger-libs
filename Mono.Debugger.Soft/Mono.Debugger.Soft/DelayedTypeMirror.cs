//
// DelayedTypeMirror.cs
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
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mono.Debugger.Soft
{
	public class DelayedTypeMirror : Mirror
	{
		string expression;

		public DelayedTypeMirror (VirtualMachine vm, string expression) : base (vm, 0) {
			this.expression = expression;
		}

		public string Expression {
			get { return expression; }
		}

		public bool IsAssignableFrom (TypeMirror c)
		{
			return false;
		}

		public bool IsDelayed {
			get {
				return true;
			}
		}

		public bool AbleToCastTo (TypeMirror t)
		{
			return AbleToCastTo (t.FullName);
		}

		public bool AbleToCastTo (string fullName)
		{
			try {
				ParseFullName (fullName);
				return true;
			} catch (NotImplementedException ex) {
				return false;
			}
		}

		public string FullName {
			get {
				throw new Exception("Cast is needed");
			}
		}

		private static string EmitBraces(string s)
		{
			int len = s.Length;
			if (s [0] == '[' && s [len - 1] == ']')
			return s.Substring (1, len - 2);
			throw new ArgumentException ("EmitBraces");
		}

		public static string ParseFullName (TypeMirror t)
		{
			return ParseFullName (t.FullName);
		}

		public static string ParseFullName (string fullName)
		{
			try {
				return OnParseFullName (fullName);
			} catch (Exception ex) {
				return null;
			}
		}

		private static string OnParseFullName (string parsedName)
		{
			string nameSpace = "";
			string name = "";
			string types = "";

			// Resolve namespace
			if (parsedName.StartsWith ("System.", StringComparison.Ordinal)) {
				nameSpace = "System";
				parsedName = parsedName.Substring (7);
			} else {
				throw new NotImplementedException ();
			}

			// Resolve name
			string pattern = @"(.*)`\d+(.*)";
			Match m = Regex.Match (parsedName, pattern);
			if (!m.Success || (m.Success && m.Groups [1].Value != "Func"))
				throw new ArgumentException ("name");
			name = m.Groups [1].Value;
			parsedName = m.Groups [2].Value;

			// Resolve argment types
			parsedName = EmitBraces (parsedName);
			List<string> argTypes = ReadBraces (parsedName);
			List<string> argTypeNames = new List<string> ();
			foreach (string s in argTypes) {
				pattern = @"\[(.*?),";
				m = Regex.Match (s, pattern);
				if (!m.Success)
					throw new ArgumentException ("argtypes");
				argTypeNames.Add (m.Groups [1].Value);
			}
			types = String.Join (",", argTypeNames);

			return nameSpace + "." + name + "<" + types + ">";
		}

		private static List<string> ReadBraces (string s)
		{
			List<string> t = new List<string> ();
			int nest = 0;
			int start = 0;
			for (int i = 0; i < s.Length; i++) {
				char c = s [i];
				if (c == '[') {
					if (nest == 0)
						start = i;
					nest++;
				} else if (c == ']') {
					if (nest == 1)
						t.Add (s.Substring (start, i - start + 1));
					nest--;
				}
			}
			return t;
		}
	}
}
