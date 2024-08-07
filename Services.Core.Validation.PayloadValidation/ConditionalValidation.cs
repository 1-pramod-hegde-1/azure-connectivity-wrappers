// Copyright (c) [2022] Pramod Hegde
//
// This library is designed to simplify connectivity to Azure resources.
// You may copy and distribute this code as long as this copyright notice is retained.
// For business purposes, the copyright notice must remain intact.
//
// This license is based on the MIT License, with additional conditions as specified below.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Services.Core.Validation.PayloadValidation
{
    [NodeType(ValidationActions.Conditional)]
    class ConditionalValidation : ActionBase
    {
        public ConditionalValidation(ValidationNode currentNode) : base(currentNode)
        {
        }

        public override IDataValidationResult Validate<T>(T payload, ValidationNode node, ValidationNode currentNodePath, params object[] references)
        {
            return ConditionalEvaluator(payload, node.Expression, node.ReferenceObject, references);
        }

        IDataValidationResult ConditionalEvaluator<T>(T payload, string expression, string referObject, object[] reference = default)
        {
            var response = new DataValidationResult();
            try
            {
                var inputObject = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                if (!string.IsNullOrEmpty(referObject))
                {
                    var referenceObject = reference.First(x => x.GetType().Name == referObject);
                    var refObject = Expression.Parameter(referenceObject.GetType(), referenceObject.GetType().Name);
                    var result = Convert.ToBoolean(DynamicExpressionParser.ParseLambda(new[] { inputObject, refObject }, null, expression).Compile().DynamicInvoke(payload, referenceObject));
                    response = new DataValidationResult
                    {
                        InternalResult = result ? InternalValidationStepResult.True : InternalValidationStepResult.False,
                        Payload = payload
                    };
                    return response;
                }
                else
                {
                    var result = Convert.ToBoolean(DynamicExpressionParser.ParseLambda(new[] { inputObject }, null, expression).Compile().DynamicInvoke(payload));
                    response = new DataValidationResult
                    {
                        InternalResult = result ? InternalValidationStepResult.True : InternalValidationStepResult.False,
                        Payload = payload
                    };
                    return response;
                }
            }
            catch (Exception ex)
            {
                response = new DataValidationResult
                {
                    ValidationResult = ValidationResults.DataValidationFailure,
                    ValidationException = new ValidationException(ex.Message, ex),
                    Payload = payload
                };
                response.ValidationFlow.AddRange(ValidationNode.ValidationFlow);
                throw new ValidationException("Conditional Evaluation Failed", new object[] { expression, payload, response });
            }

        }
    }
}
