using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AiMusicWorkstation.Infrastructure.Persistence;

public class DbLibraryRepository : ILibraryRepository
{
    private readonly AiMusicWorkstationDbContext _context;

    public DbLibraryRepository(AiMusicWorkstationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Group)
            .OrderByDescending(p => p.DateAdded)
            .ToListAsync(cancellationToken);
    }

    public async Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        await _context.Projects.AddAsync(project, cancellationToken);
    }

    public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var existing = await _context.Projects.FindAsync(new object[] { project.Id }, cancellationToken);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(project);
        }
        else
        {
            await _context.Projects.AddAsync(project, cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Projects.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
        {
            _context.Projects.Remove(existing);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
