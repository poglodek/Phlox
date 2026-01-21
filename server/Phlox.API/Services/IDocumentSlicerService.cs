namespace Phlox.API.Services;

public interface IDocumentSlicerService
{
    List<string> SliceIntoParagraphs(string content);
}
