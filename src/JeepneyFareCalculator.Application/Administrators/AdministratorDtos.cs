namespace JeepneyFareCalculator.Application.Administrators;

/// <summary>Body of <c>POST /api/admin/login</c>.</summary>
public sealed record AdministratorLoginRequest(string Username, string Password);

/// <summary>Body of <c>GET /api/admin/me</c>: who is signed in.</summary>
public sealed record SignedInAdministrator(string Username);
