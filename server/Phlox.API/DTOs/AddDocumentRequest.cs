using System.ComponentModel.DataAnnotations;

namespace Phlox.API.DTOs;

public class AddDocumentRequest
{
    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    [Required]
    public required string Content { get; set; }
}
