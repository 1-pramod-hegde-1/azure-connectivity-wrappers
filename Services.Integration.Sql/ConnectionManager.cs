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
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Services.Integration.Sql
{
    sealed class ConnectionManager : IConnectionManager
    {
        SqlConnection _connection = default;
        ISqlConfiguration _metadata = default;
       
        internal ConnectionManager(ISqlConfiguration metadata)
        {
            _metadata = metadata;
        }

        SqlConnection IConnectionManager.Connection => _connection;

        async Task IConnectionManager.Close()
        {
            if (_connection != default && _connection.State != System.Data.ConnectionState.Closed)
            {
                _connection.Close();
                GC.Collect();
            }
        }

        async Task IConnectionManager.Establish()
        {
            _connection = new SqlConnection();

            var sqlAuthConfig = (string[])_metadata.AuthenticationConfig.AuthenticationCallback();

            if (sqlAuthConfig[0]== "DirectConnection" || sqlAuthConfig[0] == "SqlVaultSecretConnection")
            {
                _connection.ConnectionString = sqlAuthConfig[1];               
            }
            else
            {
                _connection.ConnectionString = sqlAuthConfig[1];
                _connection.AccessToken = sqlAuthConfig[2];
            }

            await _connection.OpenAsync();
        }
    }
}
