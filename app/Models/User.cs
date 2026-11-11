using System.ComponentModel.DataAnnotations;

namespace Unbreakable.Models;

// Opdracht 4: een gebruiker zoals Keycloak hem kent. De sub is de sleutel (nooit de naam).
public class User
{
    [Key]
    [StringLength(100)]
    public string Sub { get; set; }

    // Weergavenaam (claim preferred_username). Uniek, zodat een ontvanger eenduidig gevonden wordt.
    [Required]
    [StringLength(40)]
    public string Username { get; set; }

    public DateTime CreatedAt { get; set; }
}
