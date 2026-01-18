using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Colo3D.Analyzers
{
    /// <summary>
    /// 阻止引用 System.Random 的分析器。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RandomSystemAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// 诊断规则 ID。
        /// </summary>
        public const string DiagnosticId = "COLO3D001";

        private static readonly LocalizableString Title = "禁止使用 System.Random";
        private static readonly LocalizableString MessageFormat = "检测到对 System.Random 的引用，请改用 Lonize.Random。";
        private static readonly LocalizableString Description = "为保证随机数行为一致，项目禁止使用 System.Random。";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            "Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        /// <summary>
        /// 获取当前分析器支持的诊断规则。
        /// </summary>
        /// <returns>诊断规则集合。</returns>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <summary>
        /// 初始化分析器。
        /// </summary>
        /// <param name="context">分析器上下文。</param>
        /// <returns>无。</returns>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(RegisterActions);
        }

        /// <summary>
        /// 注册编译期分析动作。
        /// </summary>
        /// <param name="context">编译启动上下文。</param>
        /// <returns>无。</returns>
        private static void RegisterActions(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? randomType = context.Compilation.GetTypeByMetadataName("System.Random");
            if (randomType == null)
            {
                return;
            }

            context.RegisterSymbolAction(
                symbolContext => AnalyzeSymbol(symbolContext, randomType),
                SymbolKind.Field,
                SymbolKind.Local,
                SymbolKind.Parameter,
                SymbolKind.Property,
                SymbolKind.Method);

            context.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, randomType),
                SyntaxKind.ObjectCreationExpression);

            context.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeTypeOf(nodeContext, randomType),
                SyntaxKind.TypeOfExpression);
        }

        /// <summary>
        /// 分析符号类型是否为 System.Random。
        /// </summary>
        /// <param name="context">符号分析上下文。</param>
        /// <param name="randomType">System.Random 类型符号。</param>
        /// <returns>无。</returns>
        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol randomType)
        {
            ITypeSymbol? referencedType = context.Symbol switch
            {
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                ILocalSymbol localSymbol => localSymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                IMethodSymbol methodSymbol => methodSymbol.ReturnType,
                _ => null
            };

            if (referencedType == null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(referencedType, randomType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Symbol.Locations[0]));
            }
        }

        /// <summary>
        /// 分析对象构造表达式是否为 System.Random。
        /// </summary>
        /// <param name="context">语法分析上下文。</param>
        /// <param name="randomType">System.Random 类型符号。</param>
        /// <returns>无。</returns>
        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol randomType)
        {
            var creationExpression = (ObjectCreationExpressionSyntax)context.Node;
            ITypeSymbol? createdType = context.SemanticModel.GetTypeInfo(creationExpression, context.CancellationToken).Type;
            if (createdType == null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(createdType, randomType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, creationExpression.GetLocation()));
            }
        }

        /// <summary>
        /// 分析 typeof 表达式是否引用 System.Random。
        /// </summary>
        /// <param name="context">语法分析上下文。</param>
        /// <param name="randomType">System.Random 类型符号。</param>
        /// <returns>无。</returns>
        private static void AnalyzeTypeOf(SyntaxNodeAnalysisContext context, INamedTypeSymbol randomType)
        {
            var typeOfExpression = (TypeOfExpressionSyntax)context.Node;
            ITypeSymbol? referencedType = context.SemanticModel.GetTypeInfo(typeOfExpression.Type, context.CancellationToken).Type;
            if (referencedType == null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(referencedType, randomType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, typeOfExpression.GetLocation()));
            }
        }
    }
}
