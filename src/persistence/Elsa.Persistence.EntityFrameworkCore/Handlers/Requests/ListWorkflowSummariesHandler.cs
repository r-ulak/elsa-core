using Elsa.Mediator.Services;
using Elsa.Persistence.Entities;
using Elsa.Persistence.EntityFrameworkCore.Services;
using Elsa.Persistence.Extensions;
using Elsa.Persistence.Models;
using Elsa.Persistence.Requests;
using Microsoft.EntityFrameworkCore;

namespace Elsa.Persistence.EntityFrameworkCore.Handlers.Requests;

public class ListWorkflowSummariesHandler : IRequestHandler<ListWorkflowDefinitionSummaries, Page<WorkflowDefinitionSummary>>
{
    private readonly IStore<WorkflowDefinition> _store;
    public ListWorkflowSummariesHandler(IStore<WorkflowDefinition> store) => _store = store;

    public async Task<Page<WorkflowDefinitionSummary>> HandleAsync(ListWorkflowDefinitionSummaries request, CancellationToken cancellationToken)
    {
        await using var dbContext = await _store.CreateDbContextAsync(cancellationToken);
        var set = dbContext.WorkflowDefinitions;
        var query = set.AsQueryable();
        
        if (request.VersionOptions != null)
            query = query.WithVersion(request.VersionOptions.Value);

        query = query.OrderBy(x => x.Name);

        return await query.PaginateAsync(x => WorkflowDefinitionSummary.FromDefinition(x), request.PageArgs);
    }
}