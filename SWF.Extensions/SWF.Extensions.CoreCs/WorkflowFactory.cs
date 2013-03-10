using SWF.Extensions.CoreCs.Builders;

namespace SWF.Extensions.CoreCs
{
    public class WorkflowFactory
    {
        public IWorkflowBuilder Create(string domain, string name, string version)
        {
            return new WorkflowBuilderImpl(domain, name, version);
        }
    }
}
