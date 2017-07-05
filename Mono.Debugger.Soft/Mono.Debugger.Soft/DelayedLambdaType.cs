﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mono.Debugger.Soft
{
	// Represents a temporary type of a lambda expression.
	// In order to determine the type of a lambda based on
	// its context, type evaluation for it must be delayed.
	public class DelayedLambdaType : Mirror
	{
		class ParsedType
		{
			public string nameSpace;
			public string typeName;
			public string[] argTypeNames;
		}

		string expression;

		public DelayedLambdaType (VirtualMachine vm, string expression) : base (vm, 0)
		{
			this.expression = expression;
		}

		public string Expression {
			get { return expression; }
		}

		public bool IsAcceptableType (TypeMirror t)
		{
			return IsAcceptable (t.FullName);
		}

		private bool IsAcceptable (string fullName)
		{
			return IsAcceptable (ParseFullName (fullName));
		}

		private bool IsAcceptable (ParsedType t)
		{
			return t.nameSpace == "System" && t.typeName == "Func";
		}

		public string GetLiteralType (TypeMirror typ)
		{
			return GetLiteralType (typ.FullName);
		}

		public string GetLiteralType (string fullName)
		{
			ParsedType t = ParseFullName (fullName);
			if (!IsAcceptable (t))
				return null;
			string types = String.Join (",", t.argTypeNames);
			return t.nameSpace + "." + t.typeName + "<" + types + ">";
		}

		private static ParsedType ParseFullName (TypeMirror t)
		{
			return ParseFullName (t.FullName);
		}

		private static ParsedType ParseFullName (string fullName)
		{
			try {
				return OnParseFullName (fullName);
			} catch (Exception) {
				return null;
			}
		}

		private static ParsedType OnParseFullName (string fullName)
		{
			// Parse fullName of type to ParsedType that is defined above. Examples for the parsing
			// are following.
			// 1) System.Action`1[[System.Int32, mscorlib, Version=xxx, Culture=xxx,
			//    PublicKeyToken=xxx]]
			//  => { nameSpace: "System", typeName: "Action", argTypeNames: ["System.Int32"] }
			// 2) System.Func`2[[System.Int32, mscorlib, Version=xxx, Culture=xxx,
			//    PublicKeyToken=xxx],[System.Single, mscorlib, Version=xxx, Culture=xxx,
			//    PublicKeyToken=xxx]]
			//  => { nameSpace: "System", typeName: "Func",
			//       argTypeNames: ["System.Int32", "System.Single"] }

			string nameSpace = "";
			string typeName = "";
			string rest = fullName;
			if (rest.StartsWith ("System.", StringComparison.Ordinal)) {
				nameSpace = "System";
				rest = rest.Substring (7);
			} else {
				throw new ArgumentException ("Currently, we support only lambda whose namespace is `System`");
			}

			string pattern = @"(.*?)`\d+(.*)";
			Match m = Regex.Match (rest, pattern);
			if (!m.Success)
				throw new ArgumentException ("Failed to parse typeName");
			typeName = m.Groups[1].Value;
			rest = m.Groups[2].Value;

			int len = rest.Length;
			if (rest[0] == '[' && rest[len - 1] == ']')
				rest = rest.Substring (1, len - 2);
			else
				throw new ArgumentException ("Failed to skip braces");

			ParsedType t = new ParsedType ();
			t.nameSpace = nameSpace;
			t.typeName = typeName;
			t.argTypeNames = ReadArgTypeNames (rest);

			return t;
		}

		private static string[] ReadArgTypeNames (string s)
		{
			List<string> args = new List<string> ();
			int nest = 0;
			int start = 0;
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (c == '[') {
					if (nest == 0)
						start = i;
					nest++;
				} else if (c == ']') {
					if (nest == 1)
						args.Add (s.Substring (start, i - start + 1));
					nest--;
				}
			}
			string[] result = new string [args.Count];
			int n = 0;
			foreach (string arg in args) {
				string pattern = @"\[(.*?)[,\]]";
				Match m = Regex.Match (arg, pattern);
				if (!m.Success)
					throw new ArgumentException ("Failed to parse arg's names");
				result[n] = m.Groups[1].Value;
				n++;
			}
			return result;
		}
	}
}