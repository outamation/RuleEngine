using DemoRuleEngine.Models;
using DemoRuleEngine.Services;
using FastEndpoints;

namespace DemoRuleEngine.Endpoints;

public class GetWorkflows : EndpointWithoutRequest<List<WorkflowDto>>
{
    private readonly IRuleManagerService _ruleManager;

    public GetWorkflows(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Get("/api/rules/workflows");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflows = await _ruleManager.GetWorkflowsAsync();
        await Send.ResponseAsync(workflows, cancellation: ct);
    }
}

public class CreateWorkflowRequest
{
    public string WorkflowName { get; set; } = string.Empty;
    public int? CreatedBy { get; set; }
}

public class CreateWorkflow : Endpoint<CreateWorkflowRequest, WorkflowDto>
{
    private readonly IRuleManagerService _ruleManager;

    public CreateWorkflow(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Post("/api/rules/workflows");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateWorkflowRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.WorkflowName))
        {
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var wf = await _ruleManager.CreateWorkflowAsync(req.WorkflowName, req.CreatedBy);
        await Send.ResponseAsync(wf, cancellation: ct);
    }
}

public class DeleteWorkflow : EndpointWithoutRequest
{
    private readonly IRuleManagerService _ruleManager;

    public DeleteWorkflow(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Delete("/api/rules/workflows/{workflowId:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var changedById = Query<int?>("changedById", isRequired: false);
        await _ruleManager.DeleteWorkflowAsync(workflowId, changedById);
        await Send.NoContentAsync(cancellation: ct);
    }
}


