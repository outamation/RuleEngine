using DemoRuleEngine.Models;
using System.Threading.Tasks;

namespace DemoRuleEngine.Services;

public interface IEligibilityService
{
    Task<EligibilityResult> EvaluateAsync(string workflowName, object inputData);
    Task<AdhocEvaluateResult> EvaluateAdhocAsync(AdhocEvaluateRequest request);
}
