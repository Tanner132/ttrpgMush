using MediatR;

namespace SeattleByNight.Application.RoleAdmin;

public sealed record SearchAdminUsersQuery(string Search) : IRequest<IReadOnlyList<AdminUserSummary>>;

public sealed class SearchAdminUsersQueryHandler : IRequestHandler<SearchAdminUsersQuery, IReadOnlyList<AdminUserSummary>>
{
    private readonly IUserAdminStore _store;

    public SearchAdminUsersQueryHandler(IUserAdminStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<AdminUserSummary>> Handle(SearchAdminUsersQuery request, CancellationToken cancellationToken)
        => _store.SearchUsersAsync(request.Search ?? string.Empty, cancellationToken);
}
