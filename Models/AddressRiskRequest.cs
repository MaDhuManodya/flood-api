namespace FloodApp.Models;

/// <summary>Request body for POST /api/flood-risk/address</summary>
public class AddressRiskRequest
{
    /// <summary>
    /// A free-text Sri Lankan address string.
    /// Example: "Colombo 07, Sri Lanka"
    /// </summary>
    public string? Address { get; set; }
}
