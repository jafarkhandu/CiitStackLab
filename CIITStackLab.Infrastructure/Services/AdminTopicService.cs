using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminTopicService : IAdminTopicService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminTopicService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminTopicDto>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminTopicDto
            {
                Id = x.Id,
                Title = x.Title,
                PublicFolderId = x.PublicFolderId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminArchivedTopicDto>> GetArchivedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedTopicDto
            {
                Id = x.Id,
                Title = x.Title,
                PublicFolderId = x.PublicFolderId,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> TopicNameExistsAsync(
        string topicName,
        int? excludedTopicId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = topicName.Trim().ToLower();

        var query = _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.Title.Trim().ToLower() == normalized);

        if (excludedTopicId.HasValue)
        {
            query = query.Where(x => x.Id != excludedTopicId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(
        string topicName,
        string? publicFolderId,
        CancellationToken cancellationToken = default)
    {
        var topic = new Domain.Entities.Topic
        {
            Title = topicName,
            PublicFolderId = string.IsNullOrWhiteSpace(publicFolderId)
                ? null
                : publicFolderId.Trim(),
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.Topics.Add(topic);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(topic).State = EntityState.Detached;
            return (false, "The topic could not be created. Please verify the topic name.");
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(
        int id,
        string topicName,
        string? publicFolderId,
        CancellationToken cancellationToken = default)
    {
        var topic = await _dbContext.Topics
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (topic is null)
        {
            return (false, "The selected topic was not found.");
        }

        topic.Title = topicName;
        topic.PublicFolderId = string.IsNullOrWhiteSpace(publicFolderId)
            ? null
            : publicFolderId.Trim();
        topic.UpdatedAt = DateTime.Now;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (false, "The topic could not be updated. Please verify the topic name.");
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var topic = await _dbContext.Topics
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (topic is null)
        {
            return (false, "The selected topic was not found.");
        }

        topic.Flag = 1;
        topic.DeletedAt = DateTime.Now;
        topic.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var topic = await _dbContext.Topics
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (topic is null)
        {
            return (false, "The selected archived topic was not found.");
        }

        topic.Flag = 0;
        topic.DeletedAt = null;
        topic.RestoredAt = DateTime.Now;
        topic.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
