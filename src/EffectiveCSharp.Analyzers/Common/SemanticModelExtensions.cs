namespace EffectiveCSharp.Analyzers.Common;

internal static class SemanticModelExtensions
{
    internal static bool AreExpressionsEquivalent(this SemanticModel semanticModel, ExpressionSyntax left, ExpressionSyntax right)
    {
        // This method checks if the two expressions represent the same value/initialization
        IOperation? leftOperation = semanticModel.GetOperation(left);
        IOperation? rightOperation = semanticModel.GetOperation(right);

        // Compare the operations for semantic equivalence
        return leftOperation != null && rightOperation != null && leftOperation.Kind == rightOperation.Kind && leftOperation.ConstantValue.Equals(rightOperation.ConstantValue);
    }

    internal static bool IsDefaultInitialization(
            this SemanticModel semanticModel,
            ITypeSymbol fieldType,
            ExpressionSyntax expressionSyntaxNode,
            CancellationToken cancellationToken = default)
    {
        // Handle default keyword
        if (expressionSyntaxNode.IsKind(SyntaxKind.DefaultExpression))
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(expressionSyntaxNode, cancellationToken);
            return typeInfo.Type?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true;
        }

        // Handle cases where the 'default' literal is used directly (e.g., `default` or `default(int)`)
        if (expressionSyntaxNode.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true; // 'default' literal always indicates default initialization
        }

        // Handle numeric types (int, double, etc.)
        if (fieldType.IsValueType)
        {
            Optional<object?> defaultValue = semanticModel.GetConstantValue(expressionSyntaxNode, cancellationToken);
            if (defaultValue.HasValue && IsDefaultValue(defaultValue.Value, fieldType))
            {
                return true;
            }

            // Handle user-defined structs initialized with 'new'
            if (expressionSyntaxNode is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } objectCreation
                && semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true)
            {
                return true;
            }
        }

        // Handle string types
        if (fieldType.SpecialType == SpecialType.System_String && expressionSyntaxNode.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return ((LiteralExpressionSyntax)expressionSyntaxNode).Token.ValueText == string.Empty;
        }

        // Handle default expressions
        if (expressionSyntaxNode.IsKind(SyntaxKind.DefaultExpression))
        {
            ITypeSymbol? expressionType = semanticModel.GetTypeInfo(expressionSyntaxNode, cancellationToken).Type;
            return expressionType?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true;
        }

        return false;
    }

    internal static bool IsInitializedFromConstructorParameter(this SemanticModel semanticModel, ExpressionSyntax expressionSyntaxNode)
    {
        IOperation? operation = semanticModel.GetOperation(expressionSyntaxNode);

        // Check if the assignment directly involves a constructor parameter
        if (operation is IParameterReferenceOperation)
        {
            return true;
        }

        // Check for local variables initialized from constructor parameters (like dependency injection)
        if (operation is ILocalReferenceOperation localReferenceOperation)
        {
            ILocalSymbol localSymbol = localReferenceOperation.Local;
            VariableDeclaratorSyntax? localDeclaration = localSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;

            if (localDeclaration?.Initializer?.Value != null)
            {
                IOperation? initializerOperation = semanticModel.GetOperation(localDeclaration.Initializer.Value);
                return initializerOperation is IParameterReferenceOperation;
            }
        }

        // Check if the assignment involves an object creation where the constructor uses a parameter
        if (operation is IObjectCreationOperation objectCreation)
        {
            for (int i = 0; i < objectCreation.Arguments.Length; i++)
            {
                IArgumentOperation argument = objectCreation.Arguments[i];
                if (argument.Value is IParameterReferenceOperation)
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsInitializedFromInstanceMember(this SemanticModel semanticModel, ExpressionSyntax right)
    {
        IOperation? operation = semanticModel.GetOperation(right);

        if (operation is IObjectCreationOperation objectCreation)
        {
            // Check if the object initializer references any instance members
            foreach (IOperation? initializer in objectCreation.Initializer?.Initializers ?? Enumerable.Empty<IOperation>())
            {
                if (initializer is ISimpleAssignmentOperation { Value: IMemberReferenceOperation mro } && IsInstanceMemberOfContainingType(semanticModel, mro, right))
                {
                    return true;
                }
            }
        }

        // Check if the operation is a member reference and whether that member is an instance (non-static) member of the containing type.
        if (operation is IMemberReferenceOperation memberReference && IsInstanceMemberOfContainingType(semanticModel, memberReference, right))
        {
            return true;
        }

        // Also check for nested member access, such as accessing a field inside another field (e.g., `this.otherField.Field`)
        if (operation is IFieldReferenceOperation { Instance: not null, Field.IsStatic: false })
        {
            return true;
        }

        return false;
    }

    internal static bool IsInitializedFromMethodCall(this SemanticModel semanticModel, ExpressionSyntax right)
    {
        // Check if the right side of the assignment is a method call
        return semanticModel.GetOperation(right) is IInvocationOperation;
    }

    private static bool IsDefaultValue(object? value, ITypeSymbol fieldType)
    {
        if (value == null)
        {
            return false;
        }

        try
        {
            switch (fieldType.SpecialType)
            {
                // Handle numeric conversions
                case SpecialType.System_Double when Convert.ToDouble(value) == 0.0:
                case SpecialType.System_Single when Convert.ToSingle(value) == 0.0f:
                case SpecialType.System_Int32 when Convert.ToInt32(value) == 0:
                case SpecialType.System_Int64 when Convert.ToInt64(value) == 0L:
                case SpecialType.System_Int16 when Convert.ToInt16(value) == 0:
                case SpecialType.System_Byte when Convert.ToByte(value) == 0:
                // Handle other types like boolean, char, etc.
                case SpecialType.System_Boolean when value is bool and false:
                case SpecialType.System_Char when value is char and '\0':
                    return true;

                default:
                    return false;
            }
        }
        catch (InvalidCastException)
        {
            return false; // If conversion fails, it's not the default value.
        }
        catch (FormatException)
        {
            return false; // If conversion fails, it's not the default value.
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="IMemberReferenceOperation"/> refers to a non-static member
    /// of the containing type where the member reference is located.
    /// </summary>
    /// <param name="semanticModel">An instance of <see cref="SemanticModel"/>.</param>
    /// <param name="memberReferenceOperation">The member reference operation to evaluate.</param>
    /// <param name="right">The <see cref="ExpressionSyntax"/> to get the containing type.</param>
    /// <returns>
    /// <c>true</c> if the member referenced by <paramref name="memberReferenceOperation"/> is a non-static member
    /// of the containing type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the member being referenced belongs to the same type that contains the operation
    /// and ensures that the member is not static.
    /// </remarks>
    private static bool IsInstanceMemberOfContainingType(
        this SemanticModel semanticModel,
        IMemberReferenceOperation memberReferenceOperation,
        ExpressionSyntax right)
    {
        INamedTypeSymbol? containingType = semanticModel.GetEnclosingSymbol(right.SpanStart)?.ContainingType;

        if (containingType != null
            && memberReferenceOperation.Member.ContainingType.Equals(containingType, SymbolEqualityComparer.IncludeNullability)
            && !memberReferenceOperation.Member.IsStatic)
        {
            return true;
        }

        return false;
    }
}
