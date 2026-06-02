using FastEndpoints;
using DemoRuleEngine.Services;
using DemoRuleEngine.Models;
using DemoRuleEngine.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace DemoRuleEngine.Endpoints;

public class EvaluateEligibility : EndpointWithoutRequest<EligibilityResult>
{
    private readonly IEligibilityService _eligibilityService;
    private readonly IRuleManagerService _ruleManager;

    public EvaluateEligibility(IEligibilityService eligibilityService, IRuleManagerService ruleManager)
    {
        _eligibilityService = eligibilityService;
        _ruleManager = ruleManager;
    }

    public override void Configure()
    {
        Post("/api/eligibility/evaluate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var wfId = Query<int?>("workflowId");

        if (!wfId.HasValue)
        {
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var wfName = await _ruleManager.GetWorkflowNameAsync(wfId.Value);

        if (string.IsNullOrEmpty(wfName))
        {
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        // Read the raw JSON body since the payload is schema-free
        using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
        var inputData = doc.RootElement.Clone();

        var result = await _eligibilityService.EvaluateAsync(wfName, inputData);
        await Send.ResponseAsync(result, cancellation: ct);
    }
}

public class EvaluateAdhocRequest
{
    public string Expression { get; set; } = string.Empty;
    public string SampleJson { get; set; } = string.Empty;
    public int? WorkflowId { get; set; }
    public int? RuleId { get; set; }
}

public class EvaluateAdhoc : Endpoint<EvaluateAdhocRequest, AdhocEvaluateResult>
{
    private readonly IEligibilityService _eligibilityService;
    private readonly IDbContextFactory<RuleDbContext> _dbFactory;

    public EvaluateAdhoc(IEligibilityService eligibilityService, IDbContextFactory<RuleDbContext> dbFactory)
    {
        _eligibilityService = eligibilityService;
        _dbFactory = dbFactory;
    }

    public override void Configure()
    {
        Post("/api/eligibility/evaluate-adhoc");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EvaluateAdhocRequest req, CancellationToken ct)
    {
        if (req.WorkflowId.HasValue && req.RuleId.HasValue)
        {
            try
            {
                using var db = await _dbFactory.CreateDbContextAsync(ct);
                var existing = await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == req.WorkflowId.Value && r.Id == req.RuleId.Value, ct);
                if (existing is not null)
                {
                    existing.SampleJson = req.SampleJson;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (System.Exception)
            {
                // Soft fail on auto-save error during adhoc evaluation
            }
        }

        var adhocReq = new AdhocEvaluateRequest
        {
            Expression = req.Expression,
            SampleJson = req.SampleJson,
            WorkflowId = req.WorkflowId,
            RuleId = req.RuleId
        };

        var result = await _eligibilityService.EvaluateAdhocAsync(adhocReq);
        await Send.ResponseAsync(result, cancellation: ct);
    }
}
