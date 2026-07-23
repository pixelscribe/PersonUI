using System.ComponentModel.DataAnnotations;

namespace PersonUI.Models;

// Mirrors PersonApi's CreatePersonRequest/UpdatePersonRequest shape (both are identical),
// so this one model can back both the create and edit forms.
public class PersonFormModel
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name can't exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100, ErrorMessage = "Last name can't exceed 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255, ErrorMessage = "Email can't exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;
}
