// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock.SqlParser
{
    using Furly.Azure.IoT.Models;
    using Furly.Extensions.Serializers;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Mock device registry query processor
    /// </summary>
    internal sealed class SqlQuery
    {
        /// <summary>
        /// Create Registry
        /// </summary>
        /// <param name="hub"></param>
        /// <param name="serializer"></param>
        public SqlQuery(IIoTHub hub, IJsonSerializer serializer)
        {
            _hub = hub;
            _serializer = serializer;
        }

        /// <summary>
        /// Parse and retrieve results
        /// </summary>
        /// <param name="sqlSelectString"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public IEnumerable<VariantValue> Query(string sqlSelectString)
        {
            // Parse
            var lexer = new SqlSelectLexer(new AntlrInputStream(sqlSelectString));
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new RaiseException<int>());
            var parser = new SqlSelectParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new RaiseException<IToken>());
            var context = parser.parse();

            // Select
            if (context.collection()?.DEVICES_MODULES() != null)
            {
                return Project(Select(_hub.Modules.Select(r => r with
                {
                    PrimaryKey = null,
                    SecondaryKey = null
                }), context)
                    .Select(_serializer.FromObject), context);
            }
            if (context.collection()?.DEVICES() != null)
            {
                return Project(Select(_hub.Devices.Select(r => r with
                {
                    PrimaryKey = null,
                    SecondaryKey = null
                }), context)
                    .Select(_serializer.FromObject), context);
            }
            throw new FormatException("Bad format");
        }

        /// <summary>
        /// Where
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="records"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private IEnumerable<T> Select<T>(IEnumerable<T> records,
            SqlSelectParser.ParseContext context)
        {
            var pe = Expression.Parameter(typeof(T));

            // Top
            if (context.selectList().topExpr() != null)
            {
                var maxCount = double.Parse(context.selectList().topExpr().maxCount().GetText(),
                    CultureInfo.InvariantCulture);
                records = records.Take((int)maxCount);
            }

            // Where
            if (context.expr() != null)
            {
                var where = (Expression<Func<T, bool>>)Expression.Lambda(
                    ParseWhereExpression(pe, context.expr()), pe);
                var compiled = where.Compile();
                records = records.Where(compiled);
            }

            return records;
        }

        /// <summary>
        /// Project
        /// </summary>
        /// <param name="records"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
        private static IEnumerable<VariantValue> Project(IEnumerable<VariantValue> records,
            SqlSelectParser.ParseContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // TODO: Implement projection

            return records;
        }

        /// <summary>
        /// Parse string function
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private Expression<Func<VariantValue?, VariantValue?>> ParseScalarLambda(
            SqlSelectParser.ScalarFunctionContext context)
        {
            if (context.STARTS_WITH() != null)
            {
                return s => s == null ? null : _serializer.FromObject(
                    ((string)s!).StartsWith(ParseStringValue(context.STRING_LITERAL()),
                        StringComparison.Ordinal));
            }

            if (context.ENDS_WITH() != null)
            {
                return s => s == null ? null : _serializer.FromObject(
                    ((string)s!).EndsWith(ParseStringValue(context.STRING_LITERAL()),
                        StringComparison.Ordinal));
            }
            throw new ArgumentException("Bad function");
        }

        /// <summary>
        /// Parse test function
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private Expression<Func<VariantValue?, VariantValue?>> ParseScalarLambda(
            SqlSelectParser.ScalarTypeFunctionContext context)
        {
            if (context.IS_DEFINED() != null)
            {
                return s => _serializer.FromObject(s != null);
            }
            if (context.IS_NULL() != null)
            {
                return s => _serializer.FromObject(s != null && s.IsNull());
            }
            if (context.IS_BOOL() != null)
            {
                return s => _serializer.FromObject(s != null && s.IsBoolean);
            }
            if (context.IS_NUMBER() != null)
            {
                return s => _serializer.FromObject(s != null && s.IsDecimal);
            }
            if (context.IS_STRING() != null)
            {
                return s => _serializer.FromObject(s != null && s.IsString);
            }
            if (context.IS_OBJECT() != null)
            {
                return s => _serializer.FromObject(s != null && s.IsObject);
            }
            return s => _serializer.FromObject(true);
        }

        /// <summary>
        /// Parse expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private Expression ParseWhereExpression(ParameterExpression parameter,
            SqlSelectParser.ExprContext context)
        {
            if (context.NOT() != null)
            {
                return Expression.Not(
                    ParseWhereExpression(parameter, context.expr(0)));
            }
            if (context.AND() != null)
            {
                return Expression.And(
                    ParseWhereExpression(parameter, context.expr(0)),
                    ParseWhereExpression(parameter, context.expr(1)));
            }
            if (context.OR() != null)
            {
                return Expression.Or(
                    ParseWhereExpression(parameter, context.expr(0)),
                    ParseWhereExpression(parameter, context.expr(1)));
            }

            if (context.scalarFunction() != null)
            {
                return ParseScalarFunction(parameter, context);
            }

            if (context.BR_OPEN() != null)
            {
                return ParseWhereExpression(parameter, context.expr(0));
            }
            return ParseComparisonExpression(parameter, context);
        }

        /// <summary>
        /// Parse scalar function
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private Expression ParseScalarFunction(ParameterExpression parameter,
            SqlSelectParser.ExprContext context)
        {
            var scalarFunctionContext = context.scalarFunction();

            var expr = scalarFunctionContext.scalarTypeFunction() != null ?
                ParseScalarLambda(scalarFunctionContext.scalarTypeFunction()) :
                ParseScalarLambda(scalarFunctionContext);

            var lhs = Expression.Invoke(expr,
                ParseParameterBinding(parameter, scalarFunctionContext.columnName()));
            var rhs = Expression.Constant(context.literal_value() != null ?
                ParseLiteralValue(context.literal_value()) : _serializer.FromObject(true));

            return CreateBinaryExpression(context.COMPARISON_OPERATOR()?.GetText() ?? "=",
                lhs, rhs);
        }

        /// <summary>
        /// Parse comparison expression
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private Expression ParseComparisonExpression(ParameterExpression parameter,
            SqlSelectParser.ExprContext context)
        {
            var lhs = ParseParameterBinding(parameter, context.columnName(0));
            Expression rhs;
            if (context.columnName().Length > 1)
            {
                rhs = ParseParameterBinding(parameter, context.columnName(1));
            }
            else if (context.array_literal() != null)
            {
                rhs = Expression.Constant(context.array_literal().literal_value()
                    .Select(ParseLiteralValue)
                    .ToArray());
            }
            else
            {
                rhs = Expression.Constant(ParseLiteralValue(context.literal_value()));
            }
            return CreateBinaryExpression(context.COMPARISON_OPERATOR().GetText(), lhs, rhs);
        }

        /// <summary>
        /// Parse target
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        private Expression<Func<DeviceTwinModel, string, VariantValue?>> CreateBindingLambda(
            string identifier)
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            switch (identifier.ToLowerInvariant())
            {
                case "tags":
                    return (t, s) => GetByPath(t.Tags, s);
                case "deviceid":
                    return (t, s) => _serializer.FromObject(t.Id);
                case "moduleid":
                    return (t, s) => _serializer.FromObject(t.ModuleId);
                case "reported":
                    return (t, s) => GetByPath(t.Reported, s);
                case "desired":
                    return (t, s) => GetByPath(t.Desired, s);
                case "properties":
                case "capabilities":
                    return (t, s) => _serializer.FromObject(t);
                case "configurations":
                    // TODO
                    return (t, s) => null;
                case "connectionstate":
                    return (t, s) => GetByPath(t.ConnectionState, s);
                default:
                    return (t, s) => null;
            }
#pragma warning restore CA1308 // Normalize strings to uppercase
        }

        /// <summary>
        /// Select targeted token value based on path
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private VariantValue? GetByPath<T>(T? target, string path) where T : class
        {
            if (target == null)
            {
                return null;
            }
            var root = _serializer.FromObject(target);
            return root.GetByPath(path);
        }

        /// <summary>
        /// Parse target
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <param name="aggregateResultColumnNames"></param>
        /// <returns></returns>
        private InvocationExpression ParseParameterBinding(ParameterExpression parameter,
            SqlSelectParser.ColumnNameContext context, List<string>? aggregateResultColumnNames = null)
        {
            if (aggregateResultColumnNames != null)
            {
                // ...
            }
            //    var property = new QueryProperty {
            //        PropertyName = context.propertyName().GetText()
            //    };
            //
            //    if (context.propertyName().IDENTIFIER().Length == 1) {
            //        if (aggregateResultColumnNames?.Contains(property.PropertyName) ?? false) {
            //            property.PropertyType = PropertyType.AggregatedProperty;
            //        }
            //        return property;
            //    }

            var identifiers = context.propertyName().IDENTIFIER().AsEnumerable();
            var root = ParseIdentifier(identifiers.First());
            if (root.Equals("properties", StringComparison.OrdinalIgnoreCase) && identifiers.Count() > 2)
            {
                identifiers = identifiers.Skip(1);
                root = ParseIdentifier(identifiers.First());
            }
            var path = string.Join(".", identifiers.Skip(1).Select(ParseIdentifier));
            return Expression.Invoke(CreateBindingLambda(root), parameter, Expression.Constant(path));
        }

        /// <summary>
        /// Build binary expression
        /// </summary>
        /// <param name="op"></param>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        private static Expression CreateBinaryExpression(string op, Expression lhs, Expression rhs)
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            switch (op.ToLowerInvariant())
            {
                case "=":
                    return Expression.Equal(lhs, rhs);
                case "!=":
                case "<>":
                    return Expression.NotEqual(lhs, rhs);
                case "<":
                    return Expression.LessThan(lhs, rhs);
                case "<=":
                    return Expression.LessThanOrEqual(lhs, rhs);
                case ">":
                    return Expression.GreaterThan(lhs, rhs);
                case ">=":
                    return Expression.GreaterThanOrEqual(lhs, rhs);
                // TODO   case "in":
                // TODO       return Expression.In;
                // TODO   case "nin":
                // TODO       return Expression.NotIn;
                default:
                    return lhs;
            }
#pragma warning restore CA1308 // Normalize strings to uppercase
        }

        /// <summary>
        /// Parse identifier including wrapped between [[ and ]]
        /// </summary>
        /// <param name="identifierNode"></param>
        /// <returns></returns>
        private string ParseIdentifier(ITerminalNode identifierNode)
        {
            var identifier = identifierNode.GetText();
            if (identifier.StartsWith("[[", StringComparison.OrdinalIgnoreCase) &&
                identifier.EndsWith("]]", StringComparison.OrdinalIgnoreCase))
            {
                return identifier[2..^2];
            }
            return identifier;
        }

        /// <summary>
        /// Parse literal
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private VariantValue? ParseLiteralValue(SqlSelectParser.Literal_valueContext context)
        {
            return context.object_literal() != null ?
                ParseObjectLiteralValue(context.object_literal()) :
                ParseScalarLiteralValue(context.scalar_literal());
        }

        /// <summary>
        /// Parse object literal
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private VariantValue ParseObjectLiteralValue(
            SqlSelectParser.Object_literalContext context)
        {
            var result = new Dictionary<string, VariantValue?>();
            foreach (var kvpContext in context.keyValuePair())
            {
                var key = ParseIdentifier(kvpContext.IDENTIFIER());
                var value = kvpContext.scalar_literal() != null
                    ? ParseScalarLiteralValue(kvpContext.scalar_literal())
                    : ParseObjectLiteralValue(kvpContext.object_literal());

                result.Add(key, value);
            }
            return _serializer.FromObject(result);
        }

        /// <summary>
        /// Parse scalar
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private VariantValue? ParseScalarLiteralValue(SqlSelectParser.Scalar_literalContext context)
        {
            if (context.BOOLEAN() != null)
            {
                return _serializer.FromObject(bool.Parse(context.BOOLEAN().GetText()));
            }
            if (context.NUMERIC_LITERAL() != null)
            {
                return _serializer.FromObject(double.Parse(context.NUMERIC_LITERAL().GetText(),
                    CultureInfo.InvariantCulture));
            }
            if (context.STRING_LITERAL() != null)
            {
                return _serializer.FromObject(ParseStringValue(context.STRING_LITERAL()));
            }
            return null;
        }

        /// <summary>
        /// Parse literal strings by trimming quotes
        /// </summary>
        /// <param name="stringLiteralContext"></param>
        /// <returns></returns>
        private static string ParseStringValue(ITerminalNode stringLiteralContext)
        {
            return stringLiteralContext.GetText().TrimQuotes();
        }

        /// <summary>
        /// Error callback
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class RaiseException<T> : IAntlrErrorListener<T>
        {
            public void SyntaxError(IRecognizer recognizer, T offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e)
            {
                throw new FormatException(
                    $"{offendingSymbol} at #{line}:{charPositionInLine} : {msg} ", e);
            }
        }

        private readonly IIoTHub _hub;
        private readonly IJsonSerializer _serializer;
    }
}
