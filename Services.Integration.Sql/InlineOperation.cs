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
using System.Data;
using System.Data.SqlClient;

namespace Services.Integration.Sql
{
    [SqlOperationMetadata(SqlCommandTypes.Text)]
    sealed class InlineOperation : ISqlOperation
    {
        readonly IConnectionManager _connectionManager = default;
        readonly ISqlConfiguration _executionMetadata = default;

        public InlineOperation(IConnectionManager connectionManager, ISqlConfiguration executionMetadata)
        {
            _connectionManager = connectionManager;
            _executionMetadata = executionMetadata;
        }

        object ISqlOperation.Execute<TIn>(params TIn[] parameters)
        {
            using SqlCommand command = new SqlCommand()
            {
                CommandTimeout = 0,
                CommandType = CommandType.Text,
                Connection = _connectionManager.Connection
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.CommandText = string.Format(_executionMetadata.CommandText, parameters);
            }

            return _executionMetadata.ExecutionMode switch
            {
                SqlExecutionModes.Scalar => command.ExecuteScalar(),
                SqlExecutionModes.TSql => command.ExecuteNonQuery(),
                SqlExecutionModes.TSqlSave => command.ExecuteNonQuery(),
                SqlExecutionModes.DataReader => command.ExecuteReader(),
                _ => default,
            };
        }
    }
}
