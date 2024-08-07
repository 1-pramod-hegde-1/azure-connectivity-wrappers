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
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Services.Integration.Sql
{
    [SqlOperationMetadata(SqlCommandTypes.StoredProcedure)]
    sealed class StoredProcedureOperation : ISqlOperation
    {
        readonly IConnectionManager _connectionManager = default;
        readonly ISqlConfiguration _executionMetadata = default;

        public StoredProcedureOperation(IConnectionManager connectionManager, ISqlConfiguration executionMetadata)
        {
            _connectionManager = connectionManager;
            _executionMetadata = executionMetadata;
        }

        object ISqlOperation.Execute<TIn>(params TIn[] parameters)
        {
            using SqlCommand command = new SqlCommand(_executionMetadata.CommandText, _connectionManager.Connection)
            {
                CommandTimeout = 0,
                CommandType = CommandType.StoredProcedure
            };

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var parameter in parameters)
                {
                    if (parameter is default(SqlParameter))
                    {
                        continue;
                    }
                    if (parameter is KeyValuePair<string, object>)
                    {
                        var p = (KeyValuePair<string, object>)Convert.ChangeType(parameter, typeof(KeyValuePair<string, object>));
                        command.Parameters.Add(new SqlParameter(p.Key, p.Value));
                    }
                    else if (parameter is KeyValuePair<string, SqlDbType>)
                    {
                        var p = (KeyValuePair<string, SqlDbType>)Convert.ChangeType(parameter, typeof(KeyValuePair<string, SqlDbType>));
                        command.Parameters.Add(new SqlParameter(p.Key, p.Value));
                    }
                    else
                    {
                        command.Parameters.Add(parameter);
                    }
                }
            }

            return _executionMetadata.ExecutionMode switch
            {
                SqlExecutionModes.Scalar => command.ExecuteScalar(),
                SqlExecutionModes.TSql => ReadProcData(command),
                SqlExecutionModes.TSqlSave => command.ExecuteNonQuery(),
                SqlExecutionModes.DataReader => command.ExecuteReader(),
                _ => default,
            };
        }

        private object ReadProcData(SqlCommand command)
        {
            using SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataSet set = new DataSet();
            adapter.Fill(set);

            return set;
        }
    }
}
