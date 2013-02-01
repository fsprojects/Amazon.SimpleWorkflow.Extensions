namespace SWF.Extensions.Core

module Constants =
    // see http://docs.aws.amazon.com/amazonswf/latest/apireference/API_RegisterDomain.html
    let minWorkflowExecRetentionPeriodInDays = 1
    let maxWorkflowExecRetentionPeriodInDays = 8