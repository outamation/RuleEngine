using DemoRuleEngine.Data;
using DemoRuleEngine.Models;
using DemoRuleEngine.Services;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace DemoRuleEngine.Endpoints;

// 1. GET /api/rules/workflows/{workflowId}/rules
public class GetRulesForWorkflow : EndpointWithoutRequest<List<RuleEntity>>
{
    private readonly IRuleManagerService _ruleManager;

    public GetRulesForWorkflow(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Get("/api/rules/workflows/{workflowId:int}/rules");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var rules = await _ruleManager.GetRulesAsync(workflowId);
        await Send.ResponseAsync(rules, cancellation: ct);
    }
}

// 2. GET /api/rules/workflows/{workflowId}/rules/{ruleId}
public class GetRule : EndpointWithoutRequest<RuleEntity>
{
    private readonly IRuleManagerService _ruleManager;

    public GetRule(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Get("/api/rules/workflows/{workflowId:int}/rules/{ruleId:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var ruleId = Route<int>("ruleId");

        var rule = await _ruleManager.GetRuleAsync(workflowId, ruleId);
        if (rule is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        await Send.ResponseAsync(rule, cancellation: ct);
    }
}

// 3. POST /api/rules/workflows/{workflowId}/rules/builder
public class AddRuleFromBuilder : Endpoint<RuleDefinitionDto, RuleEntity>
{
    private readonly IRuleManagerService _ruleManager;

    public AddRuleFromBuilder(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Post("/api/rules/workflows/{workflowId:int}/rules/builder");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RuleDefinitionDto req, CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");

        await _ruleManager.AddRuleFromDtoAsync(workflowId, req);
        var created = await _ruleManager.GetRuleByNameAsync(workflowId, req.RuleName);

        if (created is null)
        {
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        await Send.ResponseAsync(created, cancellation: ct);
    }
}

// 4. PUT /api/rules/workflows/{workflowId}/rules/{ruleId}
public class UpdateRule : Endpoint<RuleDefinitionDto, RuleEntity>
{
    private readonly IRuleManagerService _ruleManager;

    public UpdateRule(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Put("/api/rules/workflows/{workflowId:int}/rules/{ruleId:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RuleDefinitionDto req, CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var ruleId = Route<int>("ruleId");

        var existing = await _ruleManager.GetRuleAsync(workflowId, ruleId);
        if (existing is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        var updated = await _ruleManager.UpdateRuleAsync(workflowId, ruleId, req);
        await Send.ResponseAsync(updated, cancellation: ct);
    }
}

// 5. PATCH /api/rules/workflows/{workflowId}/rules/{ruleId}/toggle
public class ToggleRuleRequest
{
    public bool? Enabled { get; set; }
    public int? ModifiedBy { get; set; }
}

public class ToggleRule : Endpoint<ToggleRuleRequest, RuleEntity>
{
    private readonly IRuleManagerService _ruleManager;

    public ToggleRule(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Patch("/api/rules/workflows/{workflowId:int}/rules/{ruleId:int}/toggle");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ToggleRuleRequest req, CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var ruleId = Route<int>("ruleId");

        var enabled = req.Enabled ?? false;

        var existing = await _ruleManager.GetRuleAsync(workflowId, ruleId);
        if (existing is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        await _ruleManager.ToggleRuleAsync(workflowId, ruleId, enabled, req.ModifiedBy);
        var updated = await _ruleManager.GetRuleAsync(workflowId, ruleId);

        if (updated is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        await Send.ResponseAsync(updated, cancellation: ct);
    }
}

// 6. DELETE /api/rules/workflows/{workflowId}/rules/{ruleId}
public class DeleteRule : EndpointWithoutRequest
{
    private readonly IRuleManagerService _ruleManager;

    public DeleteRule(IRuleManagerService ruleManager)
    {
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Delete("/api/rules/workflows/{workflowId:int}/rules/{ruleId:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflowId = Route<int>("workflowId");
        var ruleId = Route<int>("ruleId");
        var changedById = Query<int?>("changedById", isRequired: false);

        await _ruleManager.DeleteRuleAsync(workflowId, ruleId, changedById);
        await Send.NoContentAsync(cancellation: ct);
    }
}

// 7. PUT /api/rules/workflows/{workflowId}/rules/{ruleId}/sample-json
public class SaveSampleJsonRequest
{
    public int WorkflowId { get; set; }
    public int RuleId { get; set; }
    public string? SampleJson { get; set; }
}

public class SaveSampleJson : Endpoint<SaveSampleJsonRequest>
{
    private readonly IDbContextFactory<RuleDbContext> _dbFactory;

    public SaveSampleJson(IDbContextFactory<RuleDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public override void Configure()
    {
        Put("/api/rules/workflows/{workflowId:int}/rules/{ruleId:int}/sample-json");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SaveSampleJsonRequest req, CancellationToken ct)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == req.WorkflowId && r.Id == req.RuleId, ct);
        if (existing is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        existing.SampleJson = req.SampleJson;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(cancellation: ct);
    }
}

// 6. GET /api/rules/audit
public class GetAuditHistoryRequest
{
    public string? RuleName { get; set; }
}

public class GetAuditHistory : Endpoint<GetAuditHistoryRequest, List<RuleAuditLog>>
{
    private readonly IRuleAuditService _auditService;
    private readonly DemoRuleEngine.Data.RuleDbContext _db;

    public GetAuditHistory(IRuleAuditService auditService, DemoRuleEngine.Data.RuleDbContext db)
    {
        _auditService = auditService;
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/rules/audit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetAuditHistoryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.RuleName))
        {
            var logs = await _db.RuleAuditLogs
                .OrderByDescending(x => x.ChangedDate)
                .ToListAsync(ct);
            await Send.ResponseAsync(logs, cancellation: ct);
            return;
        }

        var history = await _auditService.GetAuditLogsAsync(req.RuleName);
        await Send.ResponseAsync(history, cancellation: ct);
    }
}
