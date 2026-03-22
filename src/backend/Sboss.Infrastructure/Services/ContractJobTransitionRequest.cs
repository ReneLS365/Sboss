using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed record ContractJobTransitionRequest(
    Guid ContractJobId,
    ContractJobState TargetState,
    string IdempotencyKey);
