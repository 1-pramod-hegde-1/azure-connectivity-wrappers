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
using System.Reflection;
using System.Text.RegularExpressions;

namespace Services.Core.Validation.PayloadValidation
{
    [NodeType(ValidationActions.Transform)]
    class Transformation : ActionBase
    {
        public Transformation(ValidationNode currentNode) : base(currentNode)
        {
        }

        public override IDataValidationResult Validate<T>(T payload, ValidationNode node, ValidationNode currentNodePath, params object[] references)
        {
            return TransformPayload(payload, node.Expression, node.ReferenceObject, references);
        }

        IDataValidationResult TransformPayload<T>(T payload,string expression, string referenceObject, object[] reference = default)
        {
            var response = new DataValidationResult();
            try
            {
                string[] operands = Regex.Split(expression, @"[.,=\s+]");
                if (operands[2].Equals("value", StringComparison.InvariantCultureIgnoreCase))
                {
                    PropertyInfo field = payload.GetType().GetProperty(operands[1]);
                    ParameterExpression targetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                    ParameterExpression valueExp = Expression.Parameter(typeof(int), operands[2]);
                    MemberExpression fieldExp = Expression.Property(targetExp, field);
                    BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);

                    var setter = Expression.Lambda<Action<T, int>>
                        (assignExp, targetExp, valueExp).Compile();

                    setter(payload, Convert.ToInt32(operands[3]));
                }
                else
                if (operands[2].Equals("DateTime", StringComparison.InvariantCultureIgnoreCase))
                {
                    PropertyInfo field = payload.GetType().GetProperty(operands[1]);
                    ParameterExpression targetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                    ParameterExpression valueExp = Expression.Parameter(typeof(DateTime), operands[2]);
                    MemberExpression fieldExp = Expression.Property(targetExp, field);
                    BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);

                    var setter = Expression.Lambda<Action<T, DateTime>>
                        (assignExp, targetExp, valueExp).Compile();

                    setter(payload, DateTime.UtcNow);
                }
                else
                if (operands[2].Equals("string", StringComparison.InvariantCultureIgnoreCase))
                {
                    PropertyInfo field = payload.GetType().GetProperty(operands[1]);
                    ParameterExpression targetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                    ParameterExpression valueExp = Expression.Parameter(typeof(string), operands[2]);
                    MemberExpression fieldExp = Expression.Property(targetExp, field);
                    BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);

                    var setter = Expression.Lambda<Action<T, string>>
                        (assignExp, targetExp, valueExp).Compile();

                    setter(payload, operands[3]);
                }
                else 
                if (operands[2].Equals("bool", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(referenceObject))
                    {
                        var refObject = reference.First(x => x.GetType().Name == referenceObject);
                        PropertyInfo refField = refObject.GetType().GetProperty(operands[1]);
                        ParameterExpression refTargetExp = Expression.Parameter(refObject.GetType(), refObject.GetType().Name);
                        ParameterExpression refValueExp = Expression.Parameter(typeof(bool), operands[2]);
                        MemberExpression refFieldExp = Expression.Property(refTargetExp, refField);
                        BinaryExpression refAssignExp = Expression.Assign(refFieldExp, refValueExp);

                        refObject.GetType().GetProperty(operands[1]).SetValue(refObject, Convert.ToBoolean(operands[3]));
                    }
                    else
                    {
                        PropertyInfo field = payload.GetType().GetProperty(operands[1]);
                        ParameterExpression targetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                        ParameterExpression valueExp = Expression.Parameter(typeof(bool), operands[2]);
                        MemberExpression fieldExp = Expression.Property(targetExp, field);
                        BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);

                        var setter = Expression.Lambda<Action<T, bool>>
                            (assignExp, targetExp, valueExp).Compile();

                        setter(payload, Convert.ToBoolean(operands[3]));
                    }                    
                }
                else
                {
                    if (string.IsNullOrEmpty(referenceObject))
                    {
                        if (payload.GetType().GetProperty(operands[3]) != null)
                        {
                            PropertyInfo inpField = payload.GetType().GetProperty(operands[1]);
                            PropertyInfo reffField = payload.GetType().GetProperty(operands[3]);
                            ParameterExpression targetExpName = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                            ParameterExpression valueExpName = Expression.Parameter(payload.GetType(), payload.GetType().Name);
                            MemberExpression fieldExpression = Expression.Property(targetExpName, inpField);
                            MemberExpression refFieldExpression = Expression.Property(valueExpName, reffField);
                            BinaryExpression assignExpression = Expression.Assign(fieldExpression, refFieldExpression);
                            payload.GetType().GetProperty(operands[1]).SetValue(payload, payload.GetType().GetProperty(operands[3]).GetValue(payload));
                        }
                        else
                        {
                            throw new ArgumentNullException("Reference object not known");
                        }
                    }
                    else
                    {
                        var refObject = reference.First(x => x.GetType().Name == referenceObject);
                        if (operands.Length > 4)
                        {
                            PropertyInfo mulInField = payload.GetType().GetProperty(operands[1]);
                            PropertyInfo mulRefField = refObject.GetType().GetProperty(operands[3]).PropertyType.GetProperty(operands[4]);
                            ParameterExpression mulTargetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);

                            if (mulRefField.PropertyType == typeof(DateTime))
                            {
                                DateTime? date = (DateTime?)refObject.GetType().GetProperty(operands[3]).PropertyType.GetProperty(operands[4]).GetValue(refObject.GetType().GetProperty(operands[3]).GetValue(refObject));
                                ParameterExpression valueExper = Expression.Parameter(typeof(DateTime?), operands[3]);
                                MemberExpression fieldExper = Expression.Property(mulTargetExp, mulInField);
                                BinaryExpression assignExper = Expression.Assign(fieldExper, valueExper);

                                var setter = Expression.Lambda<Action<T, DateTime?>>
                                    (assignExper, mulTargetExp, valueExper).Compile();

                                setter(payload, date);
                            }
                            else
                            {
                                ParameterExpression mulValueExp = Expression.Parameter(refObject.GetType().GetProperty(operands[3]).PropertyType, refObject.GetType().GetProperty(operands[3]).PropertyType.Name);
                                MemberExpression fieldExp = Expression.Property(mulTargetExp, mulInField);
                                MemberExpression refFieldExp = Expression.Property(mulValueExp, mulRefField);
                                BinaryExpression assignExp = Expression.Assign(fieldExp, refFieldExp);

                                payload.GetType().GetProperty(operands[1])
                                    .SetValue(payload, refObject.GetType().GetProperty(operands[3]).PropertyType.GetProperty(operands[4])
                                    .GetValue(refObject.GetType().GetProperty(operands[3]).GetValue(refObject)));
                            }
                        }

                        else
                        {
                            PropertyInfo inField = payload.GetType().GetProperty(operands[1]);
                            PropertyInfo refField = refObject.GetType().GetProperty(operands[3]);
                            ParameterExpression targetExp = Expression.Parameter(payload.GetType(), payload.GetType().Name);

                            /// TODO: To be deleted once AgreementService gets updated to send dates Null
                            if (refField.PropertyType == typeof(DateTime?))
                            {
                                DateTime date = (DateTime)refObject.GetType().GetProperty(operands[3]).GetValue(refObject);
                                ParameterExpression valueExper = Expression.Parameter(typeof(DateTime), operands[2]);
                                MemberExpression fieldExper = Expression.Property(targetExp, inField);
                                BinaryExpression assignExper = Expression.Assign(fieldExper, valueExper);

                                var setter = Expression.Lambda<Action<T, DateTime>>
                                    (assignExper, targetExp, valueExper).Compile();

                                setter(payload, date);
                            }
                            /// endTODO
                            else
                            {

                                ParameterExpression valueExp = Expression.Parameter(refObject.GetType(), refObject.GetType().Name);
                                MemberExpression fieldExp = Expression.Property(targetExp, inField);
                                MemberExpression refFieldExp = Expression.Property(valueExp, refField);
                                BinaryExpression assignExp = Expression.Assign(fieldExp, refFieldExp);

                                payload.GetType().GetProperty(operands[1]).SetValue(payload, refObject.GetType().GetProperty(operands[3]).GetValue(refObject));
                            }
                        }                                                
                    }
                }
                response = new DataValidationResult
                {
                    InternalResult = InternalValidationStepResult.Transform,
                    ValidationResult = ValidationResults.ValidationCompleted,
                    Payload = payload
                };
                //response.ValidationFlow.AddRange(ValidationNode.ValidationFlow);
                return response;
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
                throw new ValidationException("Tranforamtion failed", new object[] {expression, referenceObject, response });
            }
        }       
    }
}
