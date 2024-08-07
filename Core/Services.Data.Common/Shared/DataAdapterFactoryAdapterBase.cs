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
using Services.Core.Common;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Data.Common
{
    public abstract class DataAdapterFactoryAdapterBase<TFactory, TDataAdapter> : IDataAdapterFactoryAdapter<TDataAdapter>
        where TFactory : class, IDataAdapterFactory
    {
        private static readonly Type OpenGenericFactoryType;

        public string DisplayName { get; private set; }
        public string Description { get { return factory.Description; } }
        public Type ConfigurationType { get; private set; }

        private TFactory factory;
        private MethodInfo createMethod;

        static DataAdapterFactoryAdapterBase ()
        {
            if (!typeof(TFactory).IsGenericType)
                throw Errors.NonGenericDataAdapterFactoryType(typeof(TFactory));
            OpenGenericFactoryType = typeof(TFactory).GetGenericTypeDefinition();
        }

        public DataAdapterFactoryAdapterBase (TFactory factory, string displayName)
        {
            Ensure.NotNull("factory", factory);

            this.factory = factory;
            DisplayName = displayName;

            ConfigurationType = GetConfigurationType(factory.GetType());
            createMethod = factory.GetType().GetMethod("CreateAsync", new[] { ConfigurationType, typeof(IDataAccessContext), typeof(CancellationToken) });
        }

        public static Type GetConfigurationType (Type adapterFactoryType)
        {
            Ensure.NotNull("adapterFactoryType", adapterFactoryType);

            var factoryInterface =
                adapterFactoryType
                    .FindInterfaces(TypesHelper.IsOpenGenericType, OpenGenericFactoryType)
                    .FirstOrDefault();

            if (factoryInterface == null)
                throw Errors.InvalidDataAdapterFactoryType(OpenGenericFactoryType, adapterFactoryType);

            return factoryInterface.GetGenericArguments()[0];
        }

        public Task<TDataAdapter> CreateAsync (object configuration, IDataAccessContext context, CancellationToken cancellation)
        {
            if (configuration != null && !ConfigurationType.IsAssignableFrom(configuration.GetType()))
                throw Errors.InvalidDataAdapterConfigrationType(ConfigurationType, configuration.GetType());

            try
            {
                return (Task<TDataAdapter>)createMethod.Invoke(factory, new[] { configuration, context, cancellation });
            }
            catch (TargetInvocationException invocationException)
            {
                if (invocationException.InnerException != null)
                    throw invocationException.InnerException;

                throw;
            }
        }
    }
}
