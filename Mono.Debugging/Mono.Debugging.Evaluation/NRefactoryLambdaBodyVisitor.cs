using System;
using System.Collections.Generic;
using System.Reflection;

using Mono.Debugging.Client;

using ICSharpCode.NRefactory.CSharp;

namespace Mono.Debugging.Evaluation
{
	public class NRefactoryLambdaBodyVisitor : IAstVisitor<string>
	{
		readonly Dictionary<string, ValueReference>  userVariables = new Dictionary<string, ValueReference> ();
		readonly EvaluationContext ctx;
		int count = 0;
		Dictionary<string, Tuple<string, object>> localValues = new Dictionary<string, Tuple<string, object>> ();

		public NRefactoryLambdaBodyVisitor (EvaluationContext ctx)
		{
			this.ctx = ctx;
		}

		public Tuple<string, object>[] GetLocalValues ()
		{
			var locals = new Tuple<string, object>[localValues.Count];
			int n = 0;
			foreach(var d in localValues) {
				var pair = d.Value;
				locals[n] = pair;
				n++;
			}
			return locals;
		}

		static Exception NotSupportedToConsistency ()
		{
			return new NotSupportedExpressionException ();
		}

		static Exception NotSupported ()
		{
			return new NotSupportedExpressionException ();
		}

		static Exception EvaluationError (string message, params object [] args)
		{
			return new EvaluatorException (message, args);
		}

		string GenerateSym (string s)
		{
			string sym;
			do {
				sym = s + "_" + count++;
			} while (ExistsLocalName(sym));

			return sym;
		}

		ValueReference Evaluate (AstNode t, string expression)
		{
			var visitor = new NRefactoryExpressionEvaluatorVisitor (ctx, expression, null, userVariables);
			return t.AcceptVisitor<ValueReference> (visitor);
		}

		bool HasPublicValue (ValueReference vr)
		{
			if (vr is NamespaceValueReference)
				return true;

			var isField = (vr.Flags & ObjectValueFlags.Field) != 0;
			var isProperty = (vr.Flags & ObjectValueFlags.Property) != 0;
			var isPublic = (vr.Flags & ObjectValueFlags.Public) != 0;

			return !(isField || isProperty) || isPublic;
		}

		bool HasPublicType (ValueReference vr)
		{
			if (vr is NamespaceValueReference)
				return true;

			return ctx.Adapter.IsPublic(ctx, vr.Type);
		}

		string AddToLocals (string name, object val, bool shouldRename)
		{
			if (localValues.ContainsKey(name))
				return GetLocalName(name);

			string localName;
			if (shouldRename) {
				localName = GenerateSym(name);
			} else if (!ExistsLocalName(name)) {
				localName = name;
			} else {
				throw EvaluationError("");
			}

			var pair = Tuple.Create(localName, val);
			localValues.Add(name, pair);
			return localName;
		}

		string GetLocalName (string name)
		{
			Tuple<string, object> pair;
			if (localValues.TryGetValue(name, out pair))
				return pair.Item1;
			return null;
		}

		bool ExistsLocalName (string localName)
		{
			foreach(var pair in localValues.Values) {
				if (pair.Item1 == localName)
					return true;
			}
			return false;
		}

		#region IAstVisitor implementation

		public string VisitAnonymousMethodExpression (AnonymousMethodExpression anonymousMethodExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUndocumentedExpression (UndocumentedExpression undocumentedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitArrayCreateExpression (ArrayCreateExpression arrayCreateExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitArrayInitializerExpression (ArrayInitializerExpression arrayInitializerExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitAsExpression (AsExpression asExpression)
		{
			var exp = asExpression.Expression.AcceptVisitor (this);
			var as_ = asExpression.AsToken;
			var typ = asExpression.Type.AcceptVisitor (this);

			return exp + " " + as_ + " " + typ;
		}

		public string VisitAssignmentExpression (AssignmentExpression assignmentExpression)
		{
			throw NotSupported ();
		}

		public string VisitBaseReferenceExpression (BaseReferenceExpression baseReferenceExpression)
		{
			var baser = "base";
			var localbase = GetLocalName(baser);
			if (localbase != null)
				return localbase;

			var vr = Evaluate (baseReferenceExpression, baser);
			localbase = AddToLocals(baser, vr.Value, true);
			return localbase;
		}

		public string VisitBinaryOperatorExpression (BinaryOperatorExpression binaryOperatorExpression)
		{
			var left = binaryOperatorExpression.Left.AcceptVisitor (this);
			var right = binaryOperatorExpression.Right.AcceptVisitor (this);

			return left + binaryOperatorExpression.OperatorToken + right;
		}

		public string VisitCastExpression (CastExpression castExpression)
		{
			var lpar = castExpression.LParToken;
			var typ = castExpression.Type.AcceptVisitor (this);
			var rpar = castExpression.RParToken;
			var exp = castExpression.Expression.AcceptVisitor (this);

			return lpar + typ + rpar + exp;
		}

		public string VisitCheckedExpression (CheckedExpression checkedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitConditionalExpression (ConditionalExpression conditionalExpression)
		{
			var cond = conditionalExpression.Condition.AcceptVisitor (this);
			var mark = conditionalExpression.QuestionMarkToken;
			var texp = conditionalExpression.TrueExpression.AcceptVisitor (this);
			var colon = conditionalExpression.ColonToken;
			var fexp = conditionalExpression.FalseExpression.AcceptVisitor (this);

			return cond + mark + texp + colon + fexp;
		}

		public string VisitDefaultValueExpression (DefaultValueExpression defaultValueExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitDirectionExpression (DirectionExpression directionExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitIdentifierExpression(IdentifierExpression identifierExpression)
		{
			var identifier = identifierExpression.Identifier;

			var localid = GetLocalName(identifier);
			if (localid != null)
				return localid;

			var vr = Evaluate (identifierExpression, identifier);
			localid = AddToLocals(identifier, vr.Value, false);
			return localid;
		}

		public string VisitIndexerExpression (IndexerExpression indexerExpression)
		{
			throw new NotImplementedException ();
		}

		public string VisitInvocationExpression (InvocationExpression invocationExpression)
		{
			var invocationTarget = invocationExpression.Target;
			var argc = invocationExpression.Arguments.Count;
			string methodName;
			var target = "";

			if (invocationTarget is MemberReferenceExpression) {
				var method = (MemberReferenceExpression)invocationTarget;
				if (method.TypeArguments.Count != 0)
					throw EvaluationError("Not supported... yet :D");

				methodName = method.MemberName;
				target = method.Target.AcceptVisitor(this);
				target = target + "." + methodName;

			} else if (invocationTarget is IdentifierExpression) {
				var method = (IdentifierExpression)invocationTarget;
				if (method.TypeArguments.Count != 0)
					throw EvaluationError("Not supported... yet :D");

				methodName = method.Identifier;
				var vref = ctx.Adapter.GetThisReference(ctx);
				var vtype = ctx.Adapter.GetEnclosingType(ctx);
				var hasInstanceMethod = ctx.Adapter.HasMethod(ctx, vtype, methodName, BindingFlags.Instance);
				var hasStaticMethod = ctx.Adapter.HasMethod(ctx, vtype, methodName, BindingFlags.Static);
				var opt = "";

				if (vref == null && hasStaticMethod) {
					var typeName = ctx.Adapter.GetTypeName(ctx, vtype);
					opt = ctx.Adapter.GetDisplayTypeName(typeName);
				} else if (vref != null && hasInstanceMethod){
					if (hasStaticMethod) {
						throw EvaluationError("Not supported.");
					}
					opt = GetLocalName("this");
					if (opt == null)
						opt = AddToLocals("this", vref, true);
				}
				target = opt + "." + methodName;
			} else {
				throw NotSupportedToConsistency();
			}

			var lpar = invocationExpression.LParToken;
			var args = new string[invocationExpression.Arguments.Count];
			int i = 0;
			foreach (var arg in invocationExpression.Arguments) {
				args[i] = arg.AcceptVisitor(this);
				i++;
			}
			var rpar = invocationExpression.RParToken;

			return target + lpar + string.Join(", ", args) + rpar;
		}

		public string VisitIsExpression (IsExpression isExpression)
		{
			var typ = isExpression.Type.AcceptVisitor (this);
			var is_ = isExpression.IsToken;
			var exp = isExpression.Expression.AcceptVisitor (this);

			return typ + " " + is_ + " " + exp;
		}

		public string VisitLambdaExpression (LambdaExpression lambdaExpression)
		{
			var args = new string[lambdaExpression.Parameters.Count];
			int i = 0;
			foreach (var p in lambdaExpression.Parameters) {
				args[i] = p.ToString();
				i++;
			}
			var lpar = "(";
			var arg_ = string.Join(", ", args);
			var rpar = ")";
			var arrow = lambdaExpression.ArrowToken;
			var body = lambdaExpression.Body.AcceptVisitor(this);

			return lpar + arg_ + rpar + " " + arrow + " " + body;
		}

		public string VisitMemberReferenceExpression (MemberReferenceExpression memberReferenceExpression)
		{
			var str = memberReferenceExpression.ToString();
			var e = Evaluate(memberReferenceExpression, str);
			if (!HasPublicValue(e))
				throw EvaluationError("inaccessible member: {0}", memberReferenceExpression.MemberName);
			else if (!HasPublicType(e))
				throw EvaluationError("inaccessible type: {0}", ctx.Adapter.GetDisplayTypeName(ctx, e.Type));

			var target = memberReferenceExpression.Target.AcceptVisitor(this);
			var memberName = memberReferenceExpression.MemberName;

			return target + "." + memberName;
			// type argument.. 
		}

		public string VisitNamedArgumentExpression (NamedArgumentExpression namedArgumentExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitNamedExpression (NamedExpression namedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitNullReferenceExpression (NullReferenceExpression nullReferenceExpression)
		{
			return nullReferenceExpression.ToString ();
		}

		public string VisitObjectCreateExpression (ObjectCreateExpression objectCreateExpression)
		{
			throw new NotImplementedException();
			//OverloadResolve (cx, tm, ".ctor", null, types, true, false, false);
			/*

				return objectCreateExpression.ToString ();
				*/
		}

		public string VisitAnonymousTypeCreateExpression (AnonymousTypeCreateExpression anonymousTypeCreateExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitParenthesizedExpression (ParenthesizedExpression parenthesizedExpression)
		{
			var lpar = parenthesizedExpression.LParToken;
			var exp = parenthesizedExpression.Expression.AcceptVisitor (this);
			var rpar = parenthesizedExpression.RParToken;

			return lpar + exp + rpar;
		}

		public string VisitPointerReferenceExpression (PointerReferenceExpression pointerReferenceExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitPrimitiveExpression (PrimitiveExpression primitiveExpression)
		{
			return primitiveExpression.ToString ();
		}

		public string VisitSizeOfExpression (SizeOfExpression sizeOfExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitStackAllocExpression (StackAllocExpression stackAllocExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitThisReferenceExpression (ThisReferenceExpression thisReferenceExpression)
		{
			var thisr = "this";
			var localthis = GetLocalName(thisr);
			if (localthis != null)
				return localthis;

			var vr = Evaluate(thisReferenceExpression, thisr);
			localthis = AddToLocals(thisr, vr.Value, true);
			return localthis;
		}

		public string VisitTypeOfExpression (TypeOfExpression typeOfExpression)
		{
			var typof = typeOfExpression.TypeOfToken.ToString();
			var lpar = typeOfExpression.LParToken;
			var typ = typeOfExpression.Type.AcceptVisitor(this);
			var rpar = typeOfExpression.RParToken;

			return typof + lpar + typ + rpar;
		}

		public string VisitTypeReferenceExpression (TypeReferenceExpression typeReferenceExpression)
		{
			return typeReferenceExpression.ToString ();
		}

		public string VisitUnaryOperatorExpression (UnaryOperatorExpression unaryOperatorExpression)
		{
			var opType = unaryOperatorExpression.Operator;
			var opSymbol = UnaryOperatorExpression.GetOperatorRole(opType);
			var exp = unaryOperatorExpression.Expression.AcceptVisitor(this);
			if (opType == UnaryOperatorType.PostIncrement || opType == UnaryOperatorType.PostDecrement)
				return exp + opSymbol;

			return opSymbol + exp;
		}

		public string VisitUncheckedExpression (UncheckedExpression uncheckedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitEmptyExpression (EmptyExpression emptyExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryExpression (QueryExpression queryExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryContinuationClause (QueryContinuationClause queryContinuationClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryFromClause (QueryFromClause queryFromClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryLetClause (QueryLetClause queryLetClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryWhereClause (QueryWhereClause queryWhereClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryJoinClause (QueryJoinClause queryJoinClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryOrderClause (QueryOrderClause queryOrderClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryOrdering (QueryOrdering queryOrdering)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQuerySelectClause (QuerySelectClause querySelectClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitQueryGroupClause (QueryGroupClause queryGroupClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitAttribute (ICSharpCode.NRefactory.CSharp.Attribute attribute)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitAttributeSection (AttributeSection attributeSection)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitDelegateDeclaration (DelegateDeclaration delegateDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitNamespaceDeclaration (NamespaceDeclaration namespaceDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitTypeDeclaration (TypeDeclaration typeDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUsingAliasDeclaration (UsingAliasDeclaration usingAliasDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUsingDeclaration (UsingDeclaration usingDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitExternAliasDeclaration (ExternAliasDeclaration externAliasDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitBlockStatement (BlockStatement blockStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitBreakStatement (BreakStatement breakStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitCheckedStatement (CheckedStatement checkedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitContinueStatement (ContinueStatement continueStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitDoWhileStatement (DoWhileStatement doWhileStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitEmptyStatement (EmptyStatement emptyStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitExpressionStatement (ExpressionStatement expressionStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitFixedStatement (FixedStatement fixedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitForeachStatement (ForeachStatement foreachStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitForStatement (ForStatement forStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitGotoCaseStatement (GotoCaseStatement gotoCaseStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitGotoDefaultStatement (GotoDefaultStatement gotoDefaultStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitGotoStatement (GotoStatement gotoStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitIfElseStatement (IfElseStatement ifElseStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitLabelStatement (LabelStatement labelStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitLockStatement (LockStatement lockStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitReturnStatement (ReturnStatement returnStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitSwitchStatement (SwitchStatement switchStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitSwitchSection (SwitchSection switchSection)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitCaseLabel (CaseLabel caseLabel)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitThrowStatement (ThrowStatement throwStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitTryCatchStatement (TryCatchStatement tryCatchStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitCatchClause (CatchClause catchClause)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUncheckedStatement (UncheckedStatement uncheckedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUnsafeStatement (UnsafeStatement unsafeStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitUsingStatement (UsingStatement usingStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitVariableDeclarationStatement (VariableDeclarationStatement variableDeclarationStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitWhileStatement (WhileStatement whileStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitYieldBreakStatement (YieldBreakStatement yieldBreakStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitYieldReturnStatement (YieldReturnStatement yieldReturnStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitAccessor (Accessor accessor)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitConstructorDeclaration (ConstructorDeclaration constructorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitConstructorInitializer (ConstructorInitializer constructorInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitDestructorDeclaration (DestructorDeclaration destructorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitEnumMemberDeclaration (EnumMemberDeclaration enumMemberDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitEventDeclaration (EventDeclaration eventDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitCustomEventDeclaration (CustomEventDeclaration customEventDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitFieldDeclaration (FieldDeclaration fieldDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitIndexerDeclaration (IndexerDeclaration indexerDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitMethodDeclaration (MethodDeclaration methodDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitOperatorDeclaration (OperatorDeclaration operatorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitParameterDeclaration (ParameterDeclaration parameterDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitPropertyDeclaration (PropertyDeclaration propertyDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitVariableInitializer (VariableInitializer variableInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitFixedFieldDeclaration (FixedFieldDeclaration fixedFieldDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitFixedVariableInitializer (FixedVariableInitializer fixedVariableInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitSyntaxTree (SyntaxTree syntaxTree)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitSimpleType (SimpleType simpleType)
		{
			throw new NotImplementedException ();
		}

		public string VisitMemberType (MemberType memberType)
		{
			throw new NotImplementedException ();
		}

		public string VisitComposedType (ComposedType composedType)
		{
			throw new NotImplementedException ();
		}

		public string VisitArraySpecifier (ArraySpecifier arraySpecifier)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitPrimitiveType (PrimitiveType primitiveType)
		{
			return primitiveType.ToString ();
		}

		public string VisitComment (Comment comment)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitWhitespace (WhitespaceNode whitespaceNode)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitText (TextNode textNode)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitNewLine (NewLineNode newLineNode)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitPreProcessorDirective (PreProcessorDirective preProcessorDirective)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitDocumentationReference (DocumentationReference documentationReference)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitTypeParameterDeclaration (TypeParameterDeclaration typeParameterDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitConstraint (Constraint constraint)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitCSharpTokenNode (CSharpTokenNode cSharpTokenNode)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitIdentifier (Identifier identifier)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitPatternPlaceholder (AstNode placeholder, ICSharpCode.NRefactory.PatternMatching.Pattern pattern)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitNullNode (AstNode nullNode)
		{
			throw NotSupportedToConsistency ();
		}

		public string VisitErrorNode (AstNode errorNode)
		{
			throw NotSupportedToConsistency ();
		}

		#endregion
	}
}
