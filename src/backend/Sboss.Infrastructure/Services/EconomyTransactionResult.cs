using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed record EconomyTransactionResult(
    EconomyTransaction Transaction,
    AccountBalance Balance,
    bool IsIdempotentReplay);
