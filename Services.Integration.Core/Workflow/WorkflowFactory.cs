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
using System.Threading.Tasks;

namespace Services.Integration.Core.Workflow
{
    public sealed class WorkflowFactory : IWorkflowFactory
    {
        public string Id => "WorkflowFactory";

        public WorkflowFactory ()
        {
            if (_workflowBuilder == null)
            {
                _workflowBuilder = new WorkflowBuilder();
            }
        }

        readonly IWorkflowBuilder _workflowBuilder;
        IWorkflow _workflow;
        IWorkflowBuilder IWorkflowFactory.DefaultBuilder => _workflowBuilder;

        async Task<IWorkflowResult> IWorkflowFactory.Execute<T, TConfig> (T input, TConfig configuration)
        {
            _workflow = _workflowBuilder?.Build();

            if (_workflow == null || _workflow.IsDone)
            {
                return default;
            }

            IWorkflowResult result = default;

            while (_workflow.Next() != null)
            {
                result = await _workflow.Current.ExecuteAsync(result == default ? input : result.Result, configuration);
                if (result == null || !result.Success || result.Exit)
                {
                    return result;
                }
            }
            return result;
        }
    }
}