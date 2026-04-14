using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries.Library;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Queries;

public class FilterProjectsQueryHandler : IQueryHandler<FilterProjectsQuery, List<SongProjectDto>>
{
    private readonly ILibraryRepository _repository;
    private readonly ILogger<FilterProjectsQueryHandler> _logger;
    
    public FilterProjectsQueryHandler(ILibraryRepository repository, ILogger<FilterProjectsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<List<SongProjectDto>> Handle(FilterProjectsQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _repository.GetAllAsync(cancellationToken);
            
            var dtos = projects
                .ConvertAll(p => new SongProjectDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Artist = p.Artist,
                    Bpm = (int)p.Bpm,
                    Key = p.Key,
                    Genre = p.Genre
                });
            
            // Search filter
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchLower = query.SearchTerm.ToLower();
                dtos = dtos.Where(p =>
                    p.Title.ToLower().Contains(searchLower) ||
                    p.Artist.ToLower().Contains(searchLower) ||
                    p.Genre.ToLower().Contains(searchLower))
                    .ToList();
            }
            
            // Genre filter
            if (!string.IsNullOrWhiteSpace(query.SelectedGenre) && query.SelectedGenre != "All")
            {
                dtos = dtos.Where(p => p.Genre == query.SelectedGenre).ToList();
            }
            
            // Sort
            dtos = query.SortBy switch
            {
                "A-Z" => dtos.OrderBy(p => p.Title).ToList(),
                "BPM" => dtos.OrderBy(p => p.Bpm).ToList(),
                _ => dtos // Default: Latest (assumes insertion order)
            };
            
            _logger.LogInformation("Filtered projects: {Count} results", dtos.Count);
            return await Task.FromResult(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering projects");
            throw;
        }
    }
}
