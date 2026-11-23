using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Users;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Users;

public sealed record SetOrganizerLikeCommand(Guid OrganizerId, Guid UserId, bool Liked) : ICommand<OrganizerProfileDto>;

public sealed class SetOrganizerLikeHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<SetOrganizerLikeCommand, OrganizerProfileDto> {
    public async ValueTask<OrganizerProfileDto> Handle(SetOrganizerLikeCommand command, CancellationToken cancellationToken) {
        if (command.UserId == command.OrganizerId) {
            throw new OperationConflictException("CANNOT_LIKE_SELF");
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken)) {
            throw new InvalidCredentialsException();
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == command.OrganizerId, cancellationToken)) {
            throw new EntityNotFoundException("USER_NOT_FOUND");
        }

        await dbContext.SetOrganizerLikeAsync(command.OrganizerId, command.UserId, command.Liked, cancellationToken);
        return await GetOrganizerProfileHandler.ReadAsync(dbContext, clock.GetUtcNow(),
            new GetOrganizerProfileQuery(command.OrganizerId) { ViewerId = command.UserId }, cancellationToken)
            ?? throw new EntityNotFoundException("USER_NOT_FOUND");
    }
}
