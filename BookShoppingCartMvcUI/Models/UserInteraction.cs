using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

public class UserInteraction
{
    [Key]
    public int InteractionId { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public int BookId { get; set; }

    public double? Rating { get; set; }
    public double? Score { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime InteractionDate { get; set; } = DateTime.Now;

    // ✅ Navigation Properties
    [ForeignKey("UserId")]
    public IdentityUser User { get; set; }

    [ForeignKey("BookId")]
    public Book Book { get; set; }
}
