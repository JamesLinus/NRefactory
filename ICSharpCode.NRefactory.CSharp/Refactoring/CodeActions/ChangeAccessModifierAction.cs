//
// ChangeAccessModifierAction.cs
//
// Author:
//       Luís Reis <luiscubal@gmail.com>
//
// Copyright (c) 2012 Simon Lindgren
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
using ICSharpCode.NRefactory.CSharp;
using System;
using System.Collections.Generic;

namespace ICSharpCode.NRefactory.CSharp.Refactoring
{
	public class ChangeAccessModifierAction : ICodeActionProvider
	{
		Dictionary<string, Modifiers> accessibilityLevels = new Dictionary<string, Modifiers>() {
			{ "private", Modifiers.Private },
			{ "protected", Modifiers.Protected },
			{ "protected internal", Modifiers.Protected | Modifiers.Internal },
			{ "internal", Modifiers.Internal },
			{ "public", Modifiers.Public }
		};

		public IEnumerable<CodeAction> GetActions(RefactoringContext context)
		{
			var node = context.GetNode<EntityDeclaration>();
			if (node == null) {
				yield break;
			}

			if (node is EnumMemberDeclaration) {
				yield break;
			}

			var parentTypeDeclaration = node.GetParent<TypeDeclaration>();
			if (parentTypeDeclaration != null && parentTypeDeclaration.ClassType == ClassType.Interface) {
				//Interface members have no access modifiers
				yield break;
			}

			var methodDeclaration = node as MethodDeclaration;
			if (methodDeclaration != null && !methodDeclaration.PrivateImplementationType.IsNull) {
				//Explictly implemented methods have no access modifiers
				yield break;
			}

			var propertyDeclaration = node as PropertyDeclaration;
			if (propertyDeclaration != null && !propertyDeclaration.PrivateImplementationType.IsNull) {
				//Explictly implemented properties have no access modifiers
				yield break;
			}

			if (node.HasModifier(Modifiers.Override) ||
			    node.HasModifier(Modifiers.Virtual) ||
			    node.HasModifier(Modifiers.New) ||
				node.HasModifier(Modifiers.Abstract)) {
				//Do not change offer to change modifier, just to be safe
				yield break;
			}

			var nodeAccess = node.Modifiers & Modifiers.VisibilityMask;

			foreach (var accessName in accessibilityLevels.Keys) {
				var access = accessibilityLevels [accessName];

				if (parentTypeDeclaration == null && ((access & (Modifiers.Private | Modifiers.Protected)) != 0)) {
					//Top-level declarations can only be public or internal
					continue;
				}

				Accessor accessor = node as Accessor;
				if (accessor != null) {
					//Allow only converting to modifiers stricter than the parent entity

					if (!IsStricterThan (access, GetActualAccess(parentTypeDeclaration, accessor))) {
						continue;
					}
				}

				if (GetActualAccess(parentTypeDeclaration, node) != access) {
					yield return GetActionForLevel(context, accessName, access, node);
				}
			}
		}

		bool IsStricterThan(Modifiers access1, Modifiers access2)
		{
			//First cover the basic cases
			if (access1 == access2) {
				return false;
			}

			if (access1 == Modifiers.Private) {
				return true;
			}
			if (access2 == Modifiers.Private) {
				return false;
			}

			if (access1 == Modifiers.Public) {
				return false;
			}
			if (access2 == Modifiers.Public) {
				return true;
			}

			return access2 == (Modifiers.Protected | Modifiers.Internal);
		}

		Modifiers GetActualAccess(AstNode parentTypeDeclaration, EntityDeclaration node)
		{
			Modifiers nodeAccess = node.Modifiers & Modifiers.VisibilityMask;
			if (nodeAccess == Modifiers.None && node is Accessor) {
				EntityDeclaration parent = node.GetParent<EntityDeclaration>();

				nodeAccess = parent.Modifiers & Modifiers.VisibilityMask;
			}

			if (nodeAccess == Modifiers.None) {
				if (parentTypeDeclaration == null) {
					return Modifiers.Internal;
				}
				return Modifiers.Private;
			}

			return nodeAccess & Modifiers.VisibilityMask;
		}

		CodeAction GetActionForLevel(RefactoringContext context, string accessName, Modifiers access, AstNode node)
		{
			return new CodeAction(context.TranslateString("To " + accessName), script => {

				var newNode = (EntityDeclaration) node.Clone();
				newNode.Modifiers &= ~Modifiers.VisibilityMask;
				newNode.Modifiers |= access;

				script.Replace(node, newNode);

			}, node);
		}
	}
}

