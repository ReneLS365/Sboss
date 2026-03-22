using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed record ContractJobTransitionResult(
    ContractJob Job,
    bool IsIdempotentReplay);
