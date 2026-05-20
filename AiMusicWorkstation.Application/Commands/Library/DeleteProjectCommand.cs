namespace AiMusicWorkstation.Application.Commands.Library;

public class DeleteProjectCommand : ICommand
{
    public string ProjectId { get; set; } = string.Empty;
    public bool DeleteFiles { get; set; }
}
